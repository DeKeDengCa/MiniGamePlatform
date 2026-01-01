namespace Astorise.Framework.SDK
{
    /// <summary>
    /// BridgeMessage 消息类型常量
    /// </summary>
    public static class BridgeMessageType
    {
        /// <summary>
        /// 请求消息
        /// </summary>
        public const string Request = "req";

        /// <summary>
        /// 响应消息
        /// </summary>
        public const string Response = "resp";
    }

    /// <summary>
    /// 业务场景状态码
    /// </summary>
    public static class StatusCode
    {
        /// <summary>
        /// 表示成功
        /// </summary>
        public const int Success = 200;

        /// <summary>
        /// 表示此方法不存在，前端可根据业务情况是否需要升级 App 提示
        /// </summary>
        public const int MethodNotFound = 10000;

        /// <summary>
        /// 表示此方法未实现，用于纯业务接口，如果业务方未实现则返回此码
        /// </summary>
        public const int MethodNotImplemented = 20000;

        /// <summary>
        /// 参数缺失或错误
        /// </summary>
        public const int InvalidParameters = 20001;
    }

    /// <summary>
    /// UnityBridge 平台类型枚举
    /// </summary>
    public enum UnityBridgePlatform
    {
        /// <summary>
        /// 编辑器平台
        /// </summary>
        Editor = 0,

        /// <summary>
        /// 安卓平台
        /// </summary>
        Android = 1,

        /// <summary>
        /// IOS平台
        /// </summary>
        IOS = 2,
    }

    /// <summary>
    /// UnityBridge 方法名常量
    /// </summary>
    public static class BridgeMethodName
    {
        /// <summary>
        /// 通用调用原生能力
        /// </summary>
        public const string CallNative = "callNative";

        /// <summary>
        /// 通用调用 Unity 能力
        /// </summary>
        public const string CallUnity = "callUnity";

        /// <summary>
        /// 获取配置信息
        /// </summary>
        public const string GetConfigInfo = "getConfigInfo";

        /// <summary>
        /// 获取设备信息
        /// </summary>
        public const string GetDeviceInfo = "getDeviceInfo";

        /// <summary>
        /// 获取公共参数
        /// </summary>
        public const string GetPublicParam = "getPublicParam";

        /// <summary>
        /// 获取 Unity 信息
        /// </summary>
        public const string GetUnityInfo = "getUnityInfo";

        /// <summary>
        /// 上报事件
        /// </summary>
        public const string Report = "report";

        /// <summary>
        /// 输出日志
        /// </summary>
        public const string Log = "log";

        /// <summary>
        /// 检查方法是否支持
        /// </summary>
        public const string IsSupportMethod = "isSupportMethod";
    }

    /// <summary>
    /// UnityBridge 命令名常量（用于 callNative）
    /// </summary>
    public static class BridgeCommandName
    {
        /// <summary>
        /// 登录
        /// </summary>
        public const string Login = "login";

        /// <summary>
        /// 登出
        /// </summary>
        public const string Logout = "logout";

        /// <summary>
        /// 删除账号
        /// </summary>
        public const string DeleteAccount = "deleteAccount";

        /// <summary>
        /// 刷新 Token
        /// </summary>
        public const string RefreshToken = "refreshToken";
    }

    /// <summary>
    /// UnityBridge 命令名常量（用于 callUnity：Native → Unity 回调事件）
    /// </summary>
    public static class BridgeCallUnityCommandName
    {
        /// <summary>
        /// 登录完成回调
        /// </summary>
        public const string LoginCompletion = "loginCompletion";

        /// <summary>
        /// 登出完成回调
        /// </summary>
        public const string LogoutCompletion = "logoutCompletion";

        /// <summary>
        /// 删除账号完成回调
        /// </summary>
        public const string DeleteAccountCompletion = "deleteAccountCompletion";

        /// <summary>
        /// 刷新 Token 完成回调
        /// </summary>
        public const string RefreshTokenCompletion = "refreshTokenCompletion";
    }
}

