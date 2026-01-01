using System;

namespace Astorise.Framework.SDK
{
    /// <summary>
    /// UnityBridge 的基础消息结构（JSON 协议承载对象）。
    /// 说明：当前仅提供字段定义用于参数传递；序列化/反序列化由上层或平台实现负责。
    /// </summary>
    [Serializable]
    public sealed class BridgeMessage
    {
        /// <summary>
        /// 消息类型：使用 BridgeMessageType.Request 或 BridgeMessageType.Response
        /// </summary>
        public string type;

        /// <summary>
        /// 请求/响应关联 ID
        /// </summary>
        public string id;

        /// <summary>
        /// 方法名 / methodName
        /// </summary>
        public string name;

        /// <summary>
        /// 微秒时间戳
        /// </summary>
        public long ts;

        /// <summary>
        /// 扩展数据（通常为 Dictionary/JObject 等）
        /// </summary>
        public object data;

        /// <summary>
        /// 获取当前时间戳（微秒）。
        /// </summary>
        public static long GetCurrentTimestampMicroseconds()
        {
            return (long)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 1000000) +
                   (DateTimeOffset.UtcNow.Ticks % 10000000) / 10;
        }
    }
}


