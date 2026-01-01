using System;

namespace Astorise.Framework.SDK
{
    /// <summary>
    /// 平台桥接实现接口：Unity ↔ Native 的最小收发入口。
    /// </summary>
    public interface IUnityBridgePlatform
    {
        /// <summary>
        /// 初始化桥接。
        /// </summary>
        void Initialize();

        /// <summary>
        /// Unity→Native 发送消息。
        /// </summary>
        /// <param name="message">要发送的桥接消息</param>
        void SendRequestMessage(BridgeMessage message);
    }
}


