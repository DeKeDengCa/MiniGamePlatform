using System;

namespace Astorise.Framework.SDK
{
    /// <summary>
    /// iOS 平台 UnityBridge（空实现）。
    /// </summary>
    public sealed class UnityBridgeIOS : IUnityBridgePlatform
    {
        public void Initialize()
        {
            // TODO: 框架阶段不实现
            // 通知原生端 Unity 已经准备好
            // TODO: iOS 实现
        }

        public void SendRequestMessage(BridgeMessage message)
        {
            // TODO: 框架阶段不实现
        }
    }
}


