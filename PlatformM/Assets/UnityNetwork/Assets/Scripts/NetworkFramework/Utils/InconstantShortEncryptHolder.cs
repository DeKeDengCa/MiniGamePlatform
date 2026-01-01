using System;

namespace NetworkFramework.Utils
{
    
    /// <summary>
    /// 非持久连接短期加密密钥持有者
    /// </summary>
    public static class InconstantShortEncryptHolder
    {
        // 存储短期加密密钥的字典，键为原始密钥的Base64表示
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _shortEncryptKeys =
            new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

        /// <summary>
        /// 获取短期加密密钥
        /// </summary>
        /// <param name="key">原始密钥</param>
        /// <returns>短期加密密钥，如果不存在则返回null</returns>
        public static string GetShortEncryptKey(byte[] key)
        {
            if (key == null || key.Length == 0)
            {
                return null;
            }

            string keyBase64 = Convert.ToBase64String(key);
            _shortEncryptKeys.TryGetValue(keyBase64, out string shortKey);
            return shortKey;
        }

        /// <summary>
        /// 更新短期加密密钥
        /// </summary>
        /// <param name="key">原始密钥</param>
        /// <param name="encrypt">新的加密信息</param>
        public static void UpdateShortEncryptKey(byte[] key, string encrypt)
        {
            if (key == null || key.Length == 0 || string.IsNullOrEmpty(encrypt))
            {
                return;
            }

            string keyBase64 = Convert.ToBase64String(key);
            _shortEncryptKeys[keyBase64] = encrypt;
        }

        /// <summary>
        /// 清除所有短期加密密钥
        /// </summary>
        public static void Clear()
        {
            _shortEncryptKeys.Clear();
        }
    }
}