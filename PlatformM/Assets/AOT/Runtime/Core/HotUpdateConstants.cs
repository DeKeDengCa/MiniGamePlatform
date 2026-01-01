using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using YooAsset;

namespace Astorise.Framework.AOT
{
    /// <summary>
    /// HotUpdate 模块的常量、枚举、类型定义文件。
    /// 包含该模块的所有常量、委托、结构体和内部类定义。
    /// </summary>
    public static class HotUpdateConstants
    {
        // =========================
        // 常量定义
        // =========================

        /// <summary>
        /// 默认最大并发下载数
        /// </summary>
        public const int DefaultMaxConcurrentDownloads = 10;

        /// <summary>
        /// 默认重试次数
        /// </summary>
        public const int DefaultRetryCount = 3;

        /// <summary>
        /// 小游戏资源根路径前缀
        /// </summary>
        public const string MiniGamesRootPath = "Assets/MiniGames/";
    }

    // =========================
    // 委托定义
    // =========================

    /// <summary>
    /// 结果回调委托。
    /// </summary>
    /// <param name="success">是否成功</param>
    /// <param name="error">错误信息，成功时为空字符串</param>
    public delegate void ResultCallback(bool success, string error);

    /// <summary>
    /// 进度回调委托。
    /// </summary>
    /// <param name="totalCount">总文件数</param>
    /// <param name="currentCount">当前已下载文件数</param>
    /// <param name="totalBytes">总字节数</param>
    /// <param name="currentBytes">当前已下载字节数</param>
    public delegate void ProgressCallback(int totalCount, int currentCount, long totalBytes, long currentBytes);

    // =========================
    // 事件定义
    // =========================

    /// <summary>
    /// 游戏事件系统，用于跨模块通信。
    /// </summary>
    public static class GameEvents
    {
        /// <summary>
        /// Update 事件，每帧触发一次。
        /// 用于需要每帧更新的系统（如资源延时释放）。
        /// </summary>
        public static event Action OnUpdate;
        
        /// <summary>
        /// 触发 Update 事件。
        /// </summary>
        internal static void InvokeUpdate()
        {
            OnUpdate?.Invoke();
        }
    }

    // =========================
    // 内部类定义
    // =========================

    /// <summary>
    /// 游戏上下文，用于管理游戏相关的包信息。
    /// </summary>
    internal sealed class GameContext
    {
        /// <summary>
        /// 游戏名称
        /// </summary>
        public string GameName;

        /// <summary>
        /// 已注册的包名列表
        /// </summary>
        public readonly List<string> RegisteredPackageNames = new List<string>(4);

        /// <summary>
        /// 包名到 ResourcePackage 的映射
        /// </summary>
        public readonly Dictionary<string, ResourcePackage> Packages = new Dictionary<string, ResourcePackage>(4);
    }

    /// <summary>
    /// 默认的远程服务实现，使用内置的 URL 构建规则。
    /// URL 格式：{rootURL}/{packageName}/{fileName}
    /// </summary>
    internal sealed class DefaultRemoteServices : IRemoteServices
    {
        /// <summary>
        /// 服务器根 URL
        /// </summary>
        private readonly string _rootURL;

        /// <summary>
        /// 包名
        /// </summary>
        private readonly string _packageName;

        /// <summary>
        /// 创建默认远程服务实例。
        /// </summary>
        /// <param name="rootURL">服务器根 URL（应包含平台名，例如：http://127.0.0.1:8000/AstroRise）</param>
        /// <param name="packageName">包名</param>
        public DefaultRemoteServices(string rootURL, string packageName)
        {
            _rootURL = rootURL.TrimEnd('/');
            _packageName = packageName;
        }

        /// <summary>
        /// 获取远程主 URL。
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>完整的远程 URL</returns>
        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            // URL 格式：{rootURL}/{packageName}/{fileName}
            // 例如：http://127.0.0.1:8000/AstroRise/BBQAsset/PackageManifest.json
            return $"{_rootURL}/{_packageName}/{fileName}";
        }

        /// <summary>
        /// 获取远程备用 URL。
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>完整的远程 URL</returns>
        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            // 备用地址也用同一个路径
            return ((IRemoteServices)this).GetRemoteMainURL(fileName);
        }
    }
}

