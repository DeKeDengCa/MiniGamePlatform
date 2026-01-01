using System.Collections.Generic;

namespace Astorise.Framework.SDK
{
    /// <summary>
    /// 统一结果结构
    /// </summary>
    public sealed class BridgeResult
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int status;

        /// <summary>
        /// 消息
        /// </summary>
        public string message;

        /// <summary>
        /// 具体业务数据
        /// </summary>
        public Dictionary<string, object> data;

        public BridgeResult()
        {
            data = new Dictionary<string, object>();
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static BridgeResult Success(string message = "success")
        {
            return new BridgeResult
            {
                status = StatusCode.Success,
                message = message
            };
        }

        /// <summary>
        /// 创建带数据的成功结果
        /// </summary>
        public static BridgeResult SuccessWithData(string message, Dictionary<string, object> data)
        {
            return new BridgeResult
            {
                status = StatusCode.Success,
                message = message,
                data = data
            };
        }

        /// <summary>
        /// 创建方法不存在错误
        /// </summary>
        public static BridgeResult MethodNotFound(string methodName)
        {
            return new BridgeResult
            {
                status = StatusCode.MethodNotFound,
                message = $"Method '{methodName}' not found"
            };
        }

        /// <summary>
        /// 创建方法未实现错误
        /// </summary>
        public static BridgeResult MethodNotImplemented(string methodName)
        {
            return new BridgeResult
            {
                status = StatusCode.MethodNotImplemented,
                message = $"Method '{methodName}' not implemented"
            };
        }

        /// <summary>
        /// 创建参数错误
        /// </summary>
        public static BridgeResult InvalidParameters(string message)
        {
            return new BridgeResult
            {
                status = StatusCode.InvalidParameters,
                message = message
            };
        }
    }
}

