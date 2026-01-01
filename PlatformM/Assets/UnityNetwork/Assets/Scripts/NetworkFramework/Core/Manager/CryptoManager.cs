using System;
using NetworkFramework.Core.Model;
using NetworkFramework.Interceptor;
using NetworkFramework.Utils;

namespace NetworkFramework.Core.Manager
{
    
    /// <summary>
    /// 加密管理器
    /// </summary>
    public static class CryptoManager
    {
        private const string TAG = "CryptoManager";

        // 存储持久连接的加密信息，键为URL
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CryptoInfo>
            _persistentCryptoInfos =
                new System.Collections.Concurrent.ConcurrentDictionary<string, CryptoInfo>();

        // 非持久连接的加密信息
        private static CryptoInfo _inconstantCryptoInfo;

        // 用于线程安全访问_inconstantCryptoInfo的锁
        private static readonly object _inconstantLock = new object();

        // 默认密钥有效期（毫秒）
        private const long DefaultKeyExpireTime = 7 * 24 * 60 * 60 * 1000; // 7天有效期 

        // 最后更新时间
        private static long _lastUpdateTime;

        /// <summary>
        /// 获取持久连接的加密信息
        /// </summary>
        /// <param name="url">连接URL</param>
        /// <returns>加密信息</returns>
        public static CryptoInfo GetPersistentConnectionCryptoInfo(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            // 尝试从缓存中获取
            if (_persistentCryptoInfos.TryGetValue(url, out CryptoInfo cachedInfo))
            {
                return cachedInfo;
            }

            // 如果缓存中没有，创建新的加密信息
            CryptoInfo newInfo = CreateNewCryptoInfo(ConnectionType.PERSISTENT);
            _persistentCryptoInfos[url] = newInfo;

            return newInfo;
        }

        /// <summary>
        /// 获取非持久连接的加密信息
        /// </summary>
        /// <returns>加密信息</returns>
        public static CryptoInfo GetInconstantConnectionCryptoInfo()
        {
            lock (_inconstantLock)
            {
                // 检查是否需要更新密钥（如果超过有效期或尚未初始化）
                if (_inconstantCryptoInfo == null || IsKeyExpired())
                {
                    _inconstantCryptoInfo = CreateNewCryptoInfo(ConnectionType.INCONSTANT);
                    _lastUpdateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                }

                return _inconstantCryptoInfo;
            }
        }

        /// <summary>
        /// 创建新的加密信息
        /// </summary>
        /// <param name="url">URL（持久连接使用）</param>
        /// <param name="type">连接类型</param>
        /// <returns>新的加密信息</returns>
        private static CryptoInfo CreateNewCryptoInfo(ConnectionType type)
        {
            // 生成随机密钥
            byte[] key = CryptoHelper.GenerateRandomKey();

            // 注意：这里需要从配置中获取公钥，与Android代码中的appNetConfig.publicKey对应
            // 在实际项目中，应该从适当的配置类中获取
            string publicKey = GetPublicKeyFromConfig();
            string publicKeyNo = GetPublicKeyNoFromConfig();

            byte[] encryptKey;
            string encryptKeyBase64;

            try
            {
                // 使用RSA加密随机密钥
                encryptKey = CryptoHelper.KeyRSAEncrypt(publicKey, key);
                // 将加密后的密钥转换为Base64字符串，移除换行符
                encryptKeyBase64 = Convert.ToBase64String(encryptKey).Replace("\n", "");
            }
            catch (Exception ex)
            {
                LoggerUtil.LogWarning($"{TAG}: RSA encryption failed: {ex.Message}");
                // 如果加密失败，使用模拟的cache值
                string timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                encryptKeyBase64 = $"{type.ToString().ToLower()}_cache_{timestamp}";
            }

            return new CryptoInfo
            {
                Key = key,
                PublicKeyNo = publicKeyNo,
                Cache = encryptKeyBase64
            };
        }


        /// <summary>
        /// 从配置中获取公钥
        /// </summary>
        /// <returns>公钥字符串</returns>
        private static string GetPublicKeyFromConfig()
        {
            return NetworkManager.Instance.AppNetConfig?.PublicKey;
        }

        /// <summary>
        /// 从配置中获取公钥号
        /// </summary>
        /// <returns>公钥号字符串</returns>
        private static string GetPublicKeyNoFromConfig()
        {
            return NetworkManager.Instance.AppNetConfig?.PublicKeyNo;
        }


        /// <summary>
        /// 检查密钥是否过期
        /// </summary>
        /// <returns>如果密钥过期则返回true</returns>
        private static bool IsKeyExpired()
        {
            long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return (currentTime - _lastUpdateTime) > DefaultKeyExpireTime;
        }

        /// <summary>
        /// 清除所有加密信息缓存
        /// </summary>
        public static void ClearAllCryptoInfo()
        {
            _persistentCryptoInfos.Clear();

            lock (_inconstantLock)
            {
                _inconstantCryptoInfo = null;
            }

            _lastUpdateTime = 0;
        }

    }

}