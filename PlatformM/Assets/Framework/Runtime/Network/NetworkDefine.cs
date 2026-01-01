using Google.Protobuf;

namespace Astorise.Framework.Network
{
    /// <summary>
    /// 网络请求回调结果对象（仅承载结果数据，不包含具体实现逻辑）。
    /// </summary>
    public sealed class NetworkCallback
    {
        /// <summary>请求结果状态：成功/失败/取消</summary>
        public NetworkCallbackState Status;

        /// <summary>返回数据</summary>
        public IMessage Data;
    }

    /// <summary>
    /// NetworkCallback 状态。
    /// </summary>
    public enum NetworkCallbackState
    {
        /// <summary>成功</summary>
        Success = 0,

        /// <summary>失败</summary>
        Failed = 1,

        /// <summary>取消</summary>
        Canceled = 2
    }
}


