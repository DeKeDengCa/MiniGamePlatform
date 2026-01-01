using NetworkFramework.Core.Interface;

namespace NetworkFramework.Core.Model
{
    /// <summary>
    /// 应用网络配置类 - 对应Android的AppNetConfig.kt
    /// 包含网络组件的各种配置参数
    /// </summary>
    public class AppNetConfig
    {
        /// <summary>
        /// 公参提供者
        /// </summary>
        public IParamProvider ParamProvider { get; set; } = null;

        /// <summary>
        /// 是否开启debug模式
        /// </summary>
        public bool EnableDebug { get; set; } = false;

        /// <summary>
        /// 公钥编号
        /// </summary>
        public string PublicKeyNo { get; set; }

        /// <summary>
        /// 公钥
        /// </summary>
        public string PublicKey { get; set; }
        


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="publicKeyNo">公钥编号（必需）</param>
        /// <param name="publicKey">公钥（必需）</param>
        public AppNetConfig(string publicKeyNo, string publicKey)
        {
            PublicKeyNo = publicKeyNo ?? throw new System.ArgumentNullException(nameof(publicKeyNo));
            PublicKey = publicKey ?? throw new System.ArgumentNullException(nameof(publicKey));
        }
    }
}