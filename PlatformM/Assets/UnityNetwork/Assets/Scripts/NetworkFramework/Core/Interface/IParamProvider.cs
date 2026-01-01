using Scommon;

namespace NetworkFramework.Core.Interface
{
    /// <summary>
    /// 参数提供者接口 - 对应Android的ParamProvider.kt
    /// 提供应用运行时的各种参数信息
    /// </summary>
    public interface IParamProvider
    {
        /// <summary>
        /// App语言设置
        /// </summary>
        /// <returns>语言代码，如"zh-CN"、"en-US"等</returns>
        string GetAppLocale();

        /// <summary>
        /// 用户token
        /// </summary>
        /// <returns>用户认证令牌</returns>
        string GetToken();

        /// <summary>
        /// 用户uid
        /// </summary>
        /// <returns>用户唯一标识符</returns>
        string GetUid();

        /// <summary>
        /// 用户性别
        /// </summary>
        /// <returns>性别枚举值</returns>
        Gender? GetGender();

        /// <summary>
        /// 包渠道,pkg_channel,名称历史原因
        /// </summary>
        /// <returns>包渠道标识</returns>
        string GetRel();

        /// <summary>
        /// 广告渠道,ad_channel,名称历史原因
        /// </summary>
        /// <returns>广告渠道标识</returns>
        string getCha();

        /// <summary>
        /// 子渠道
        /// </summary>
        /// <returns>子渠道标识</returns>
        string GetSubCha();

        /// <summary>
        /// 国家/地区代码，可选
        /// </summary>
        /// <returns>国家代码，如"CN"、"US"等</returns>
        string GetCou() => null;

        /// <summary>
        /// 版本号
        /// </summary>
        /// <returns>应用版本号字符串</returns>
        string GetVersionName() => null;

        /// <summary>
        /// 版本号代码
        /// </summary>
        /// <returns>版本代码数字</returns>
        long? GetVersionCode() => null;

        /// <summary>
        /// 设备ID
        /// </summary>
        /// <returns>设备唯一标识符</returns>
        string GetDid() => null;

        /// <summary>
        /// 包名
        /// </summary>
        /// <returns>应用包名</returns>
        string GetPackageName() => null;

        /// <summary>
        /// 业务版本号
        /// </summary>
        /// <returns>业务版本号字符串</returns>
        string GetBizVer() => null;
    }
}