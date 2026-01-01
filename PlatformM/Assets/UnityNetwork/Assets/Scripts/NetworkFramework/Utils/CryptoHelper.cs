
using System;
using System.Collections.Generic;
using System.Text;
using Random = System.Random;

namespace NetworkFramework.Utils
{
    
    /// <summary>
    /// 加密助手类
    /// </summary>
    public static class CryptoHelper
    {
        /// <summary>
        /// 生成随机密钥（与Android版本完全一致）
        /// </summary>
        /// <returns>随机生成的16字节密钥</returns>
        public static byte[] GenerateRandomKey()
        {
            char[] candidate = "1234567890".ToCharArray(); // 与Android版本一致的候选字符
            Random random = new Random();
            char[] charArray = new char[16];

            for (int i = 0; i < 16; i++)
            {
                charArray[i] = candidate[Math.Abs(random.Next()) % candidate.Length];
            }

            return Encoding.UTF8.GetBytes(new string(charArray));
        }

        /// <summary>
        /// AES加密方法 - 使用CBC模式和PKCS7填充（与Android版本PKCS5完全一致）
        /// </summary>
        /// <param name="data">待加密数据</param>
        /// <param name="key">加密密钥</param>
        /// <returns>加密后的数据</returns>
        /// <exception cref="Exception">加密过程中的异常</exception>
        public static byte[] NormalAESEncrypt(byte[] data, byte[] key)
        {
            if (data == null || data.Length == 0)
            {
                return data;
            }

            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                // 设置加密模式和填充方式 - 与Android版本一致
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7; // .NET中PKCS7等同于Java的PKCS5

                // 直接使用原始密钥作为Key和IV - 与Android版本完全一致
                aes.Key = key;
                aes.IV = key;

                // 创建加密器
                using (System.Security.Cryptography.ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    using (System.IO.MemoryStream msEncrypt = new System.IO.MemoryStream())
                    {
                        using (System.Security.Cryptography.CryptoStream csEncrypt =
                               new System.Security.Cryptography.CryptoStream(msEncrypt, encryptor,
                                   System.Security.Cryptography.CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(data, 0, data.Length);
                            csEncrypt.FlushFinalBlock();
                        }

                        return msEncrypt.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// AES解密方法 - 使用CBC模式和PKCS5填充（与Android版本完全一致）
        /// </summary>
        /// <param name="data">待解密的数据</param>
        /// <param name="key">解密密钥</param>
        /// <returns>解密后的数据</returns>
        public static byte[] NormalAESDecrypt(byte[] data, byte[] key)
        {
            if (data == null || data.Length == 0)
            {
                return data;
            }

            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                // 设置加密模式和填充方式 - 与Android版本一致
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7; // .NET中PKCS7等同于Java的PKCS5

                // 直接使用原始密钥作为Key和IV - 与Android版本完全一致
                aes.Key = key;
                aes.IV = key;

                // 创建解密器
                using (System.Security.Cryptography.ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    using (System.IO.MemoryStream msDecrypt = new System.IO.MemoryStream(data))
                    {
                        using (System.Security.Cryptography.CryptoStream csDecrypt =
                               new System.Security.Cryptography.CryptoStream(msDecrypt, decryptor,
                                   System.Security.Cryptography.CryptoStreamMode.Read))
                        {
                            using (System.IO.MemoryStream resultStream = new System.IO.MemoryStream())
                            {
                                csDecrypt.CopyTo(resultStream);
                                return resultStream.ToArray();
                            }
                        }
                    }
                }
            }
        }



        /// <summary>
        /// RSA加密方法
        /// </summary>
        /// <param name="pubKey">Base64编码的公钥</param>
        /// <param name="data">待加密数据</param>
        /// <returns>加密后的数据</returns>
        public static byte[] KeyRSAEncrypt(string pubKey, byte[] data)
        {
            if (string.IsNullOrEmpty(pubKey) || data == null || data.Length == 0)
            {
                throw new ArgumentException("Public key or data cannot be empty");
            }

            // 解析公钥
            var publicKey = PublicKeyParser(pubKey);

            // 创建RSA加密器
            using (var rsa = System.Security.Cryptography.RSA.Create())
            {
                rsa.ImportParameters(publicKey);
                // 使用RSA/PKCS1Padding模式（与Android的RSA/NONE/PKCS1Padding等效）
                return rsa.Encrypt(data, System.Security.Cryptography.RSAEncryptionPadding.Pkcs1);
            }
        }

        /// <summary>
        /// 解析Base64编码的RSA公钥
        /// </summary>
        /// <param name="pubKey">Base64编码的公钥</param>
        /// <returns>RSA参数</returns>
        private static System.Security.Cryptography.RSAParameters PublicKeyParser(string pubKey)
        {
            if (string.IsNullOrEmpty(pubKey))
            {
                throw new ArgumentException("Public key cannot be empty");
            }

            try
            {
                // 解码Base64字符串
                byte[] data = Convert.FromBase64String(pubKey);

                // 创建ASN.1解析器并解析数据
                Asn1Parser parser = new Asn1Parser();
                Asn1Node node = parser.Parse(data);

                // 检查是否为SEQUENCE类型
                if (node.Type != Asn1NodeType.Sequence)
                {
                    throw new InvalidOperationException("Invalid public key format: root node is not SEQUENCE type");
                }

                Asn1Sequence sequence = (Asn1Sequence)node;

                // 检查SEQUENCE是否包含恰好2个INTEGER节点
                if (sequence.Nodes.Count != 2)
                {
                    throw new InvalidOperationException("Invalid public key format: incorrect number of SEQUENCE nodes");
                }

                if (sequence.Nodes[0].Type != Asn1NodeType.Integer || sequence.Nodes[1].Type != Asn1NodeType.Integer)
                {
                    throw new InvalidOperationException("Invalid public key format: incorrect child node types");
                }

                // 获取模数和公钥指数
                byte[] modulusBytes = ((Asn1Integer)sequence.Nodes[0]).Value;
                byte[] exponentBytes = ((Asn1Integer)sequence.Nodes[1]).Value;

                // 创建RSA参数
                return new System.Security.Cryptography.RSAParameters
                {
                    Modulus = modulusBytes,
                    Exponent = exponentBytes
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse public key: " + ex.Message, ex);
            }
        }

        #region ASN.1解析相关类

        /// <summary>
        /// ASN.1节点类型
        /// </summary>
        private enum Asn1NodeType
        {
            Sequence = 0x30,
            Integer = 0x02,
            Null = 0x05,
            ObjectIdentifier = 0x06,
            BitString = 0x03,
            OctetString = 0x04
        }

        /// <summary>
        /// ASN.1节点基类
        /// </summary>
        private abstract class Asn1Node
        {
            public abstract Asn1NodeType Type { get; }
            public virtual byte[] Data { get; }
        }

        /// <summary>
        /// ASN.1 SEQUENCE节点
        /// </summary>
        private class Asn1Sequence : Asn1Node
        {
            public override Asn1NodeType Type => Asn1NodeType.Sequence;
            public List<Asn1Node> Nodes { get; }

            public Asn1Sequence(List<Asn1Node> nodes)
            {
                Nodes = nodes;
            }
        }

        /// <summary>
        /// ASN.1 INTEGER节点
        /// </summary>
        private class Asn1Integer : Asn1Node
        {
            public override Asn1NodeType Type => Asn1NodeType.Integer;
            public override byte[] Data => Value;
            public byte[] Value { get; }

            public Asn1Integer(byte[] value)
            {
                // 移除前导零字节（如果有的话，除了值为0的情况）
                if (value.Length > 1 && value[0] == 0 && value[1] != 0)
                {
                    int nonZeroIndex = 0;
                    while (nonZeroIndex < value.Length && value[nonZeroIndex] == 0)
                    {
                        nonZeroIndex++;
                    }

                    if (nonZeroIndex > 0)
                    {
                        byte[] newValue = new byte[value.Length - nonZeroIndex];
                        Buffer.BlockCopy(value, nonZeroIndex, newValue, 0, newValue.Length);
                        Value = newValue;
                    }
                    else
                    {
                        Value = value;
                    }
                }
                else
                {
                    Value = value;
                }
            }
        }

        /// <summary>
        /// ASN.1解析器
        /// </summary>
        private class Asn1Parser
        {
            public Asn1Node Parse(byte[] data)
            {
                Asn1Scanner scanner = new Asn1Scanner(data);
                return ParseNode(scanner);
            }

            private Asn1Node ParseNode(Asn1Scanner scanner)
            {
                byte typeByte = scanner.Consume(1)[0];
                Asn1NodeType type = (Asn1NodeType)typeByte;

                switch (type)
                {
                    case Asn1NodeType.Sequence:
                        int sequenceLength = scanner.ConsumeLength();
                        byte[] sequenceData = scanner.Consume(sequenceLength);
                        Asn1Scanner sequenceScanner = new Asn1Scanner(sequenceData);
                        List<Asn1Node> nodes = new List<Asn1Node>();

                        while (!sequenceScanner.IsComplete())
                        {
                            nodes.Add(ParseNode(sequenceScanner));
                        }

                        return new Asn1Sequence(nodes);

                    case Asn1NodeType.Integer:
                        int integerLength = scanner.ConsumeLength();
                        byte[] integerData = scanner.Consume(integerLength);
                        return new Asn1Integer(integerData);

                    default:
                        throw new NotSupportedException($"Unsupported ASN.1 type: {typeByte}");
                }
            }
        }

        /// <summary>
        /// ASN.1数据扫描器
        /// </summary>
        private class Asn1Scanner
        {
            private readonly byte[] _data;
            private int _index;

            public Asn1Scanner(byte[] data)
            {
                _data = data;
                _index = 0;
            }

            public bool IsComplete()
            {
                return _index >= _data.Length;
            }

            public byte[] Consume(int length)
            {
                if (_index + length > _data.Length)
                {
                    throw new ArgumentException($"Insufficient data length: need {length} bytes, but {_data.Length - _index} bytes remaining");
                }

                byte[] result = new byte[length];
                Buffer.BlockCopy(_data, _index, result, 0, length);
                _index += length;
                return result;
            }

            public int ConsumeLength()
            {
                byte lengthByte = Consume(1)[0];

                // 短形式长度
                if ((lengthByte & 0x80) == 0)
                {
                    return lengthByte;
                }

                // 长形式长度
                int lengthFieldLength = lengthByte & 0x7F;
                byte[] lengthBytes = Consume(lengthFieldLength);

                // 确保长度字段不超过4字节
                byte[] paddedLength = new byte[4];
                int copyStartIndex = Math.Max(0, 4 - lengthBytes.Length);
                Buffer.BlockCopy(lengthBytes, 0, paddedLength, copyStartIndex, Math.Min(lengthBytes.Length, 4));

                // 计算长度值
                int length = 0;
                for (int i = 0; i < 4; i++)
                {
                    length = (length << 8) | (paddedLength[i] & 0xFF);
                }

                return length;
            }
        }

        #endregion
    }

}