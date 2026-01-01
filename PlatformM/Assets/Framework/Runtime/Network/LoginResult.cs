namespace Astorise.Framework.Network
{
    /// <summary>
    /// 登录结果数据结构（字段口径对齐平台下发与业务侧使用）。
    /// </summary>
    public sealed class LoginResult
    {
        /// <summary>登录类型（例如 google/apple/did 等）</summary>
        public string Type { get; set; }

        /// <summary>用户 ID</summary>
        public long Uid { get; set; }

        /// <summary>是否首次注册</summary>
        public bool IsFirstRegister { get; set; }

        /// <summary>访问令牌</summary>
        public string AccessToken { get; set; }

        /// <summary>刷新令牌</summary>
        public string RefreshToken { get; set; }

        /// <summary>头像 URL（可能为空）</summary>
        public string AvatarUrl { get; set; }

        /// <summary>用户名</summary>
        public string UserName { get; set; }
    }
}


