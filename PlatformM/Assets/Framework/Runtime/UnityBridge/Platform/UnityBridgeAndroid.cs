using System;

namespace Astorise.Framework.SDK
{
    /// <summary>
    /// Android 平台 UnityBridge（空实现）。
    /// </summary>
    public sealed class UnityBridgeAndroid : IUnityBridgePlatform
    {
        public void Initialize()
        {
            // TODO: 框架阶段不实现
            // 通知原生端 Unity 已经准备好
            // AndroidJavaClass unityBridgeJNI = new AndroidJavaClass("com.entertain.sns.unity_bridge.UnityBridgeJNI");
            // unityBridgeJNI.CallStatic("unityBridgeReady");
        }

        public void SendRequestMessage(BridgeMessage message)
        {
            // TODO: 框架阶段不实现
        }
    }
}


