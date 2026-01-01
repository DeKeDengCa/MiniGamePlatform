using System.IO;
using System.IO.Compression;

namespace NetworkFramework.Utils
{
    public static class GzipUtil
    {
        
        /// <summary>
        /// 使用GZIP压缩数据
        /// </summary>
        public static byte[] CompressGzip(byte[] data)
        {
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (GZipStream compressionStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    compressionStream.Write(data, 0, data.Length);
                }
                return outputStream.ToArray();
            }
        }

        /// <summary>
        /// 使用GZIP解压数据
        /// </summary>
        public static  byte[] DecompressGzip(byte[] data)
        {
            using (MemoryStream inputStream = new MemoryStream(data))
            {
                using (GZipStream decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress))
                {
                    using (MemoryStream outputStream = new MemoryStream())
                    {
                        decompressionStream.CopyTo(outputStream);
                        return outputStream.ToArray();
                    }
                }
            }
        }
    }
}