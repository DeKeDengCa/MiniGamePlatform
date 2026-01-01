using System;
using System.Reflection;
using UnityEngine;
using YooAsset;
using Cysharp.Threading.Tasks;

namespace Astorise.Framework.AOT
{
    /// <summary>
    /// 游戏引导类，用于统一管理游戏启动流程。
    /// 负责 YooAsset 初始化、配置管理以及热更新测试的执行。
    /// 资源加载相关操作通过 HotUpdateLauncher 处理。
    /// </summary>
    public static class GameBootstrap
    {
        #region 常量定义

        /// <summary>
        /// Framework 程序集名称
        /// </summary>
        private const string FrameworkAssemblyName = "Framework";

        /// <summary>
        /// Framework.Main 类型全名
        /// </summary>
        private const string FrameworkMainTypeName = "Astorise.Framework.Core.Main";

        /// <summary>
        /// Framework.Main.Start 方法名
        /// </summary>
        private const string FrameworkMainStartMethodName = "Start";

        /// <summary>
        /// 小游戏 Main 类方法名
        /// </summary>
        private const string MiniGameMainStartMethodName = "Start";

        #endregion

        /// <summary>
        /// 获取 rootURL。
        /// </summary>
        /// <returns>服务器根 URL，包含平台名（当前为测试数据）</returns>
        public static string GetRootURL()
        {
            return "http://127.0.0.1:8000/AstroRise";
        }

        /// <summary>
        /// 获取 version。
        /// </summary>
        /// <returns>包版本（当前为测试数据）</returns>
        public static string GetProjectName()
        {
            return "AstroRise";
        }

        /// <summary>
        /// 获取 buildinRoot。
        /// 从 YooAsset 配置中获取内置资源根目录，如果无法读取则默认使用 "yoo"。
        /// </summary>
        /// <returns>内置资源根目录</returns>
        public static string GetBuildinRoot()
        {
            // 从 YooAsset 配置中读取 DefaultYooFolderName
            // 如果无法读取或为空，则默认使用 "yoo"
            // 返回 StreamingAssets/{DefaultYooFolderName}
            // 注意：YooAssetSettings 是 internal 类，使用反射访问
            string defaultYooFolderName = "yoo"; // 默认值

            UnityEngine.Object settingsAsset = Resources.Load("YooAssetSettings");
            if (settingsAsset != null)
            {
                System.Reflection.FieldInfo defaultYooFolderNameField = settingsAsset.GetType().GetField("DefaultYooFolderName");
                if (defaultYooFolderNameField != null)
                {
                    string configValue = defaultYooFolderNameField.GetValue(settingsAsset) as string;
                    if (!string.IsNullOrEmpty(configValue))
                    {
                        defaultYooFolderName = configValue;
                    }
                }
            }

            return System.IO.Path.Combine(Application.streamingAssetsPath, defaultYooFolderName).Replace('\\', '/');
        }



        /// <summary>
        /// 启动 Framework 层。
        /// 通过 AssemblyReferenceManager.InvokeMethodAsync 调用 Framework.Main.Start() 初始化 Framework 层各个模块并执行登录流程。
        /// </summary>
        /// <returns>UniTask</returns>
        public static async UniTask StartFramework()
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[GameBootstrap] 开始启动 Framework 层");
#endif

            // 使用 AssemblyReferenceManager 调用 Framework.Main.Start()
            await AssemblyReferenceManager.InvokeMethodAsync(FrameworkAssemblyName, FrameworkMainTypeName, FrameworkMainStartMethodName);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[GameBootstrap] Framework.Main.Start 执行成功");
#endif
        }

        /// <summary>
        /// 启动指定的小游戏。
        /// 下载游戏资源（DLL 加载已在 DownloadGame 内部完成），然后通过 AssemblyReferenceManager 调用游戏的 Main.Start() 方法。
        /// 按照项目约定，小游戏的入口类型为 `Astorise.MiniGames.{gameName}.Main`，方法为 `Start`。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <returns>UniTask</returns>
        public static async UniTask StartGame(string gameName)
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[GameBootstrap] 开始启动游戏: {gameName}");
#endif

            // 获取游戏 DLL 位置
            string dllLocation = HotUpdateLauncher.GetGameDllLocation(gameName);
            if (string.IsNullOrEmpty(dllLocation))
            {
#if UNITY_DEBUG
                Debug.LogError($"[GameBootstrap] 游戏 {gameName} 未配置 DLL，无法启动");
#endif
                return;
            }

            // 下载游戏资源（DLL 已在 DownloadGame 内部加载并注册到 AssemblyReferenceManager）
            Assembly assembly = await HotUpdateLauncher.DownloadGame(gameName);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            if (assembly != null)
            {
                Debug.Log($"[GameBootstrap] ✓ 游戏 {gameName} 资源下载完成，DLL 已加载");
            }
            else
            {
                Debug.Log($"[GameBootstrap] ✓ 游戏 {gameName} 资源下载完成，未配置 DLL");
            }
#endif

            if (assembly == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[GameBootstrap] 游戏 {gameName} DLL 加载失败");
#endif
                return;
            }

            // 按照项目约定，小游戏入口类型为 Astorise.MiniGames.{gameName}.Main，方法为 Start
            string typeName = $"Astorise.MiniGames.{gameName}.Main";

            // 使用 AssemblyReferenceManager 调用游戏的 Main.Start()
            await AssemblyReferenceManager.InvokeMethodAsync(dllLocation, typeName, MiniGameMainStartMethodName);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[GameBootstrap] 游戏 {gameName}.Main.Start 执行成功");
#endif
        }
    }
}

