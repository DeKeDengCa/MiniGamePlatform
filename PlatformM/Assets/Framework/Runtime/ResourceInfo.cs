namespace Astorise.Framework.Core
{
    /// <summary>
    /// 资源信息类，包含资源的完整信息。
    /// </summary>
    public sealed class ResourceInfo
    {
        /// <summary>
        /// 资源位置标识
        /// </summary>
        public readonly string Location;

        /// <summary>
        /// 资源释放策略
        /// </summary>
        public readonly AssetLifePolicy ReleasePolicy;

        /// <summary>
        /// 延时释放时间（秒），仅在 ReleasePolicy 为 DelayedRelease 时有效
        /// </summary>
        public readonly float DelayedReleaseTime;

        /// <summary>
        /// 资源包名称（可选，如果为空则使用默认包名）
        /// </summary>
        public readonly string PackageName;

        /// <summary>
        /// 创建资源信息。
        /// </summary>
        /// <param name="location">资源位置标识</param>
        /// <param name="releasePolicy">资源释放策略</param>
        /// <param name="delayedReleaseTime">延时释放时间（秒），仅在 ReleasePolicy 为 DelayedRelease 时有效</param>
        /// <param name="packageName">资源包名称（可选，如果为空则使用默认包名）</param>
        public ResourceInfo(string location, AssetLifePolicy releasePolicy, float delayedReleaseTime = 120f, string packageName = null)
        {
            Location = location;
            ReleasePolicy = releasePolicy;
            DelayedReleaseTime = delayedReleaseTime > 0f ? delayedReleaseTime : 120f;
            PackageName = packageName;
        }
    }

    /// <summary>
    /// Asset 生命周期策略。
    /// </summary>
    public enum AssetLifePolicy
    {
        /// <summary>
        /// 常驻资源，永不自动释放
        /// </summary>
        Permanent = 0,

        /// <summary>
        /// 延时释放资源，在指定时间后自动释放
        /// </summary>
        DelayedRelease = 1
    }
}

