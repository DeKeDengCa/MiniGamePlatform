using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using YooAsset;
using UnityEngine;
using HybridCLR;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Astorise.Framework.AOT
{
    /// <summary>
    /// 小游戏平台 YooAsset 资源 API（运行时版本）。
    /// 负责热更新相关的平台初始化、包管理、下载和 RawFile 加载。
    /// Asset 加载、释放、实例化等功能请使用 ResourceManager。
    /// 约束：
    /// - HotUpdate 层：可以使用 UniTask 处理异步
    /// - 以 gameName 维度工作：支持注册任意数量的 package
    /// </summary>
    public static class HotUpdateManager
    {

        /// <summary>
        /// 平台是否已初始化
        /// </summary>
        private static bool _platformInitialized;

        /// <summary>
        /// 服务器根 URL
        /// </summary>
        private static string _rootURL;


        /// <summary>
        /// 内置资源根目录，统一设置为 yoo 目录，内部会用 packageName 拼接
        /// </summary>
        private static string _buildinRoot;

        /// <summary>
        /// 游戏名称到上下文的映射
        /// </summary>
        private static readonly Dictionary<string, GameContext> Games = new Dictionary<string, GameContext>(16);

        /// <summary>
        /// 需要启用 APPEND_FILE_EXTENSION 的包名集合（运行时手动配置）
        /// </summary>
        private static readonly HashSet<string> AppendFileExtensionPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 是否在 Editor 环境（由外部设置，通常在 Main.cs 中设置）
        /// </summary>
        private static bool _isEditorEnvironment = false;

        /// <summary>
        /// Editor 模式开关
        /// </summary>
        private static bool _useEditorMode = false;

        #region Editor 环境设置和 Editor 模式开关

        /// <summary>
        /// 设置是否在 Editor 环境。
        /// 此方法应在游戏启动时调用（通常在 Main.cs 中）。
        /// </summary>
        /// <param name="isEditor">是否在 Editor 环境</param>
        public static void SetIsEditorEnvironment(bool isEditor)
        {
            _isEditorEnvironment = isEditor;
            // 如果在 Editor 环境，默认启用 Editor 模式
            if (isEditor)
            {
                _useEditorMode = true;
            }
        }

        /// <summary>
        /// 设置是否使用 Editor 模式（直接访问本地资源，无需打包 bundle）。
        /// Editor 环境下默认使用 Editor 模式，可以通过此方法切换到 Bundle 模式。
        /// 非 Editor 环境强制使用 Bundle 模式，此设置无效。
        /// </summary>
        /// <param name="useEditorMode">是否使用 Editor 模式</param>
        public static void SetUseEditorMode(bool useEditorMode)
        {
            if (_isEditorEnvironment)
            {
                _useEditorMode = useEditorMode;
            }
            else
            {
                // 非 Editor 环境强制使用 Bundle 模式，忽略设置
                _useEditorMode = false;
            }
        }

        /// <summary>
        /// 查询当前是否使用 Editor 模式。
        /// </summary>
        /// <returns>如果使用 Editor 模式返回 true，否则返回 false</returns>
        public static bool IsUsingEditorMode()
        {
            if (!_isEditorEnvironment)
            {
                // 非 Editor 环境强制使用 Bundle 模式
                return false;
            }
            return _useEditorMode;
        }

        #endregion

        #region 包注册

        /// <summary>
        /// 注册单个包到指定游戏。
        /// </summary>
        public static void RegisterPackage(string gameName, string packageName)
        {
            if (string.IsNullOrEmpty(gameName) || string.IsNullOrEmpty(packageName))
                return;

            GameContext ctx = GetOrCreate(gameName);
            if (!ctx.RegisteredPackageNames.Contains(packageName))
            {
                ctx.RegisteredPackageNames.Add(packageName);
            }
        }

        /// <summary>
        /// 批量注册多个包到指定游戏。
        /// </summary>
        public static void RegisterPackages(string gameName, string[] packageNames)
        {
            if (string.IsNullOrEmpty(gameName) || packageNames == null)
                return;

            GameContext ctx = GetOrCreate(gameName);
            for (int i = 0; i < packageNames.Length; i++)
            {
                string pkg = packageNames[i];
                if (!string.IsNullOrEmpty(pkg) && !ctx.RegisteredPackageNames.Contains(pkg))
                {
                    ctx.RegisteredPackageNames.Add(pkg);
                }
            }
        }

        /// <summary>
        /// 获取指定游戏已注册的所有包名。
        /// </summary>
        public static string[] GetRegisteredPackages(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
                return Array.Empty<string>();

            GameContext ctx = GetOrNull(gameName);
            if (ctx == null)
                return Array.Empty<string>();

            return ctx.RegisteredPackageNames.ToArray();
        }

        #endregion

        #region 平台初始化

        /// <summary>
        /// 初始化平台级 YooAsset（建议只调用一次）。
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask InitializePlatform(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (_platformInitialized)
            {
                return;
            }

            try
            {
                YooAssets.Initialize();
                _platformInitialized = true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize platform: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 设置全局配置（rootURL、buildinRoot）。
        /// 这些配置会被所有后续的 InitializeGame 调用使用。
        /// </summary>
        /// <param name="rootURL">服务器根 URL，应包含平台名，例如 "http://127.0.0.1:8000/AstroRise"</param>
        /// <param name="buildinRoot">内置资源根目录，统一设置为 yoo 目录，例如 "StreamingAssets/yoo"，内部会用 packageName 拼接：{buildinRoot}/{packageName}</param>
        public static void SetConfig(string rootURL, string buildinRoot)
        {
            _rootURL = rootURL;
            _buildinRoot = buildinRoot;
        }

        /// <summary>
        /// 初始化某个小游戏的所有已注册包（路由方法）。
        /// 根据 IsUsingEditorMode() 调用对应的 Editor 或 Runtime 实现。
        /// URL 构建规则（Bundle 模式）：{rootURL}/{packageName}/{fileName}（rootURL 应包含平台名）
        /// gameName 仅用于查询已注册的包，不参与 URL 构建。
        /// buildinRoot 统一设置为 yoo 目录，内部会自动用 packageName 拼接每个包的完整路径：{buildinRoot}/{packageName}
        /// 
        /// 模式选择：
        /// - Editor 模式：Editor 下默认使用，直接访问本地资源，无需 SetConfig
        /// - Bundle 模式：需要先调用 SetConfig 设置 rootURL 和 buildinRoot
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask InitializeGame(string gameName, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!_platformInitialized)
            {
                throw new Exception("Platform not initialized. Call InitializePlatform first.");
            }

            if (string.IsNullOrEmpty(gameName))
            {
                throw new ArgumentException("gameName is empty.", nameof(gameName));
            }

            if (IsUsingEditorMode())
            {
                await InitializeGameEditor(gameName, cancellationToken);
            }
            else
            {
                await InitializeGameRuntime(gameName, cancellationToken);
            }
        }

        /// <summary>
        /// Editor 模式初始化游戏的所有已注册包。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async UniTask InitializeGameEditor(string gameName, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!_isEditorEnvironment)
            {
                throw new Exception("Editor mode is only available in Unity Editor");
            }

            GameContext ctx = GetOrNull(gameName);
            if (ctx == null || ctx.RegisteredPackageNames.Count == 0)
            {
                throw new Exception("No packages registered for this game. Call RegisterPackage first.");
            }

            // 使用 for 循环顺序初始化所有包
            for (int i = 0; i < ctx.RegisteredPackageNames.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                string packageName = ctx.RegisteredPackageNames[i];
                
                // 创建/获取包并存储到上下文中
                ResourcePackage package = YooAssets.TryGetPackage(packageName) ?? YooAssets.CreatePackage(packageName);
                ctx.Packages[packageName] = package;

                // Editor 模式：检查包是否已初始化，如果已初始化则跳过
                if (package.InitializeStatus == EOperationStatus.Succeed)
                {
#if UNITY_DEBUG && YOO_ASSET
                    Debug.Log($"[MiniGameYooAssetApi] 包 '{packageName}' 已初始化，跳过初始化步骤");
#endif
                    continue;
                }

                // Editor 模式：使用 EditorSimulateMode
                await InitializePackageEditor(packageName, cancellationToken);
            }
        }

        /// <summary>
        /// Runtime 模式初始化游戏的所有已注册包。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async UniTask InitializeGameRuntime(string gameName, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            // Bundle 模式下需要检查配置
            if (string.IsNullOrEmpty(_rootURL))
            {
                throw new Exception("rootURL is not set. Call SetConfig first.");
            }

            GameContext ctx = GetOrNull(gameName);
            if (ctx == null || ctx.RegisteredPackageNames.Count == 0)
            {
                throw new Exception("No packages registered for this game. Call RegisterPackage first.");
            }

            // 使用 for 循环顺序初始化所有包
            for (int i = 0; i < ctx.RegisteredPackageNames.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                string packageName = ctx.RegisteredPackageNames[i];
                
                // 创建/获取包并存储到上下文中
                ResourcePackage package = YooAssets.TryGetPackage(packageName) ?? YooAssets.CreatePackage(packageName);
                ctx.Packages[packageName] = package;

                // Runtime 模式：使用 HostPlayMode/OfflinePlayMode
                await InitializePackageRuntime(packageName, cancellationToken);
            }
        }

        /// <summary>
        /// 检测包是否需要启用 APPEND_FILE_EXTENSION 参数。
        /// 检查运行时手动配置的包名集合。
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <returns>如果需要启用 APPEND_FILE_EXTENSION 返回 true，否则返回 false</returns>
        private static bool ShouldUseAppendFileExtension(string packageName)
        {
            // 检查运行时手动配置的包名集合
            if (AppendFileExtensionPackages.Contains(packageName))
            {
#if UNITY_DEBUG && YOO_ASSET
                Debug.Log($"[MiniGameYooAssetApi] 从运行时配置检测到需要启用 APPEND_FILE_EXTENSION: package='{packageName}'");
#endif
                return true;
            }

            return false;
        }

        /// <summary>
        /// 设置指定包需要启用 APPEND_FILE_EXTENSION 参数（运行时配置）。
        /// 当无法从 YooAsset 设置中读取打包配置时，可以使用此方法手动配置。
        /// </summary>
        /// <param name="packageName">包名</param>
        public static void SetAppendFileExtensionPackage(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return;

            AppendFileExtensionPackages.Add(packageName);
#if UNITY_DEBUG && YOO_ASSET
            Debug.Log($"[MiniGameYooAssetApi] 配置包需要启用 APPEND_FILE_EXTENSION: package='{packageName}'");
#endif
        }

        /// <summary>
        /// 移除指定包的 APPEND_FILE_EXTENSION 配置。
        /// </summary>
        /// <param name="packageName">包名</param>
        public static void RemoveAppendFileExtensionPackage(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return;

            AppendFileExtensionPackages.Remove(packageName);
#if UNITY_DEBUG && YOO_ASSET
            Debug.Log($"[MiniGameYooAssetApi] 移除包的 APPEND_FILE_EXTENSION 配置: package='{packageName}'");
#endif
        }

        /// <summary>
        /// Editor 模式初始化包（使用 EditorSimulateMode）
        /// </summary>
        /// <param name="packageName">包名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async UniTask InitializePackageEditor(string packageName, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!_isEditorEnvironment)
            {
                // 非 Editor 环境下不应该调用此方法
                throw new Exception("InitializePackageEditor can only be called in Editor environment");
            }

            // 创建/获取 ResourcePackage
            ResourcePackage package = YooAssets.TryGetPackage(packageName) ?? YooAssets.CreatePackage(packageName);

            // 检测是否需要启用 APPEND_FILE_EXTENSION
            bool appendFileExtension = ShouldUseAppendFileExtension(packageName);

            try
            {
                dynamic buildResult = null;
                try
                {
                    buildResult = EditorSimulateModeHelper.SimulateBuild(packageName);
                }
                catch (Exception ex)
                {
                    string errorMsg;
                    // 检查是否是资源收集配置为空的错误
                    if (ex.Message != null && ex.Message.Contains("pack asset info is empty"))
                    {
                        errorMsg = $"包 '{packageName}' 的资源收集配置为空。请在 YooAsset 设置中为该包配置资源收集器（AssetBundleCollector），并确保至少有一个收集器配置了资源路径。";
                    }
                    else
                    {
                        errorMsg = $"EditorSimulateModeHelper.SimulateBuild 失败，包名: '{packageName}'，错误: {ex.Message}。内部异常: {ex.InnerException?.Message ?? "无"}。请确保包已在 YooAsset 设置中配置并至少构建过一次。";
                    }
#if UNITY_DEBUG
                    Debug.LogError($"[MiniGameYooAssetApi] {errorMsg}");
                    Debug.LogError($"[MiniGameYooAssetApi] 异常类型: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        Debug.LogError($"[MiniGameYooAssetApi] 内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                    }
                    Debug.LogError($"[MiniGameYooAssetApi] 堆栈跟踪: {ex.StackTrace}");
#endif
                    throw new Exception(errorMsg, ex);
                }
                
                if (buildResult == null)
                {
                    throw new Exception($"EditorSimulateModeHelper.SimulateBuild returned null for package '{packageName}'. Please ensure the package is configured in YooAsset settings and has been built at least once.");
                }
                
                string packageRoot = buildResult.PackageRootDirectory;
                
                if (string.IsNullOrEmpty(packageRoot))
                {
                    throw new Exception($"Package root directory is empty for package '{packageName}'. Please ensure the package is configured in YooAsset settings and has been built at least once.");
                }
                
#if UNITY_DEBUG && YOO_ASSET
                Debug.Log($"[MiniGameYooAssetApi] Editor 模式初始化包: package='{packageName}', root='{packageRoot}'");
                Debug.Log($"[MiniGameYooAssetApi] Editor 模式实际资源路径: {packageRoot} (这是 YooAsset 构建输出目录，不是 yoo 缓存目录)");
                Debug.Log($"[MiniGameYooAssetApi] Editor 模式下资源从构建输出目录加载，而不是从 yoo 缓存目录加载");
#endif
                
                EditorSimulateModeParameters createParameters = new EditorSimulateModeParameters
                {
                    EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot)
                };

                // 如果需要启用 APPEND_FILE_EXTENSION（用于 PackRawFileWithExtension 规则）
                if (appendFileExtension)
                {
                    createParameters.EditorFileSystemParameters.AddParameter(FileSystemParametersDefine.APPEND_FILE_EXTENSION, true);
#if UNITY_DEBUG && YOO_ASSET
                    Debug.Log($"[MiniGameYooAssetApi] Editor 模式启用 APPEND_FILE_EXTENSION 参数: package='{packageName}'");
#endif
                }

                InitializationOperation initOp = package.InitializeAsync(createParameters);
                await ToUniTask(initOp, cancellationToken);

                if (initOp.Status != EOperationStatus.Succeed)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[MiniGameYooAssetApi] Editor 模式包初始化失败: package='{packageName}', error='{initOp.Error}'");
#endif
                    throw new Exception(initOp.Error ?? "Package initialization failed");
                }

#if UNITY_DEBUG && YOO_ASSET
                Debug.Log($"[MiniGameYooAssetApi] Editor 模式包初始化成功: package='{packageName}'");
#endif
                // Editor 模式下也需要请求版本和更新清单（与 Bundle 模式保持一致）
                RequestPackageVersionOperation versionOp = package.RequestPackageVersionAsync();
                await ToUniTask(versionOp, cancellationToken);

                if (versionOp.Status != EOperationStatus.Succeed)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[MiniGameYooAssetApi] Editor 模式版本请求失败: package='{packageName}', error='{versionOp.Error}'");
#endif
                    throw new Exception(versionOp.Error ?? "Version request failed");
                }

#if UNITY_DEBUG && YOO_ASSET
                Debug.Log($"[MiniGameYooAssetApi] Editor 模式版本请求成功: package='{packageName}', version='{versionOp.PackageVersion}'");
#endif
                UpdatePackageManifestOperation updateOp = package.UpdatePackageManifestAsync(versionOp.PackageVersion);
                await ToUniTask(updateOp, cancellationToken);

                if (updateOp.Status != EOperationStatus.Succeed)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[MiniGameYooAssetApi] Editor 模式清单更新失败: package='{packageName}', error='{updateOp.Error}'");
#endif
                    throw new Exception(updateOp.Error ?? "Manifest update failed");
                }

#if UNITY_DEBUG && YOO_ASSET
                Debug.Log($"[MiniGameYooAssetApi] Editor 模式清单更新成功: package='{packageName}'");
#endif
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
#if UNITY_DEBUG
                Debug.LogError($"[MiniGameYooAssetApi] Editor 模式包初始化异常: package='{packageName}', exception='{ex.Message}'");
#endif
                throw;
            }
        }

        /// <summary>
        /// Runtime 模式初始化包（使用 HostPlayMode/OfflinePlayMode）
        /// </summary>
        /// <param name="packageName">包名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async UniTask InitializePackageRuntime(string packageName, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            // Bundle 模式下需要检查配置
            if (string.IsNullOrEmpty(_rootURL))
            {
                throw new Exception("rootURL is not set. Call SetConfig first.");
            }

            // 创建/获取 ResourcePackage
            ResourcePackage package = YooAssets.TryGetPackage(packageName) ?? YooAssets.CreatePackage(packageName);

            // 检测是否需要启用 APPEND_FILE_EXTENSION
            bool appendFileExtension = ShouldUseAppendFileExtension(packageName);

            // Bundle 模式：使用 HostPlayMode/OfflinePlayMode
            // 使用内置的 URL 构建规则为当前包创建 IRemoteServices 实例
            // URL 格式：{rootURL}/{packageName}/{fileName}
            // 注意：rootURL 应该在调用 SetConfig 时已经包含平台名（例如：http://127.0.0.1:8000/AstroRise）
            IRemoteServices packageRemoteServices = new DefaultRemoteServices(_rootURL, packageName);

            // buildinRoot 统一设置为 yoo 目录，内部用 packageName 拼接每个包的完整路径
            // 例如：buildinRoot = "StreamingAssets/yoo"，则 BBQAsset 包的完整路径为 "StreamingAssets/yoo/BBQAsset"
            string packageBuildinRoot = string.IsNullOrEmpty(_buildinRoot) 
                ? null 
                : System.IO.Path.Combine(_buildinRoot, packageName);

            try
            {
                InitializeParameters initParams;
                if (packageRemoteServices != null)
                {
                    HostPlayModeParameters hostPlayModeParams = new HostPlayModeParameters
                    {
                        BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(packageRoot: packageBuildinRoot),
                        CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(packageRemoteServices)
                    };
                    // 如果没有 buildinRoot，禁用 catalog 文件检查
                    if (string.IsNullOrEmpty(packageBuildinRoot))
                    {
                        hostPlayModeParams.BuildinFileSystemParameters.AddParameter(FileSystemParametersDefine.DISABLE_CATALOG_FILE, true);
                    }
                    // 如果需要启用 APPEND_FILE_EXTENSION（用于 PackRawFileWithExtension 规则）
                    if (appendFileExtension)
                    {
                        hostPlayModeParams.BuildinFileSystemParameters.AddParameter(FileSystemParametersDefine.APPEND_FILE_EXTENSION, true);
                        hostPlayModeParams.CacheFileSystemParameters.AddParameter(FileSystemParametersDefine.APPEND_FILE_EXTENSION, true);
#if UNITY_DEBUG && YOO_ASSET
                        Debug.Log($"[MiniGameYooAssetApi] 启用 APPEND_FILE_EXTENSION 参数: package='{packageName}'");
#endif
                    }
                    initParams = hostPlayModeParams;
                }
                else
                {
                    OfflinePlayModeParameters offlinePlayModeParams = new OfflinePlayModeParameters
                    {
                        BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(packageRoot: packageBuildinRoot)
                    };
                    // 如果没有 buildinRoot，禁用 catalog 文件检查
                    if (string.IsNullOrEmpty(packageBuildinRoot))
                    {
                        offlinePlayModeParams.BuildinFileSystemParameters.AddParameter(FileSystemParametersDefine.DISABLE_CATALOG_FILE, true);
                    }
                    // 如果需要启用 APPEND_FILE_EXTENSION（用于 PackRawFileWithExtension 规则）
                    if (appendFileExtension)
                    {
                        offlinePlayModeParams.BuildinFileSystemParameters.AddParameter(FileSystemParametersDefine.APPEND_FILE_EXTENSION, true);
#if UNITY_DEBUG && YOO_ASSET
                        Debug.Log($"[MiniGameYooAssetApi] 启用 APPEND_FILE_EXTENSION 参数: package='{packageName}'");
#endif
                    }
                    initParams = offlinePlayModeParams;
                }

                InitializationOperation initOp = package.InitializeAsync(initParams);
                await ToUniTask(initOp, cancellationToken);

                if (initOp.Status != EOperationStatus.Succeed)
                {
                    throw new Exception(initOp.Error ?? "Package initialization failed");
                }

                RequestPackageVersionOperation versionOp = package.RequestPackageVersionAsync();
                await ToUniTask(versionOp, cancellationToken);

                if (versionOp.Status != EOperationStatus.Succeed)
                {
                    throw new Exception(versionOp.Error ?? "Version request failed");
                }

                UpdatePackageManifestOperation updateOp = package.UpdatePackageManifestAsync(versionOp.PackageVersion);
                await ToUniTask(updateOp, cancellationToken);

                if (updateOp.Status != EOperationStatus.Succeed)
                {
                    throw new Exception(updateOp.Error ?? "Manifest update failed");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize package '{packageName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取某个小游戏的某个 package（用于调试/高级用法）。
        /// </summary>
        public static ResourcePackage GetPackage(string gameName, string packageName)
        {
            if (string.IsNullOrEmpty(gameName) || string.IsNullOrEmpty(packageName))
                return null;
            if (!Games.TryGetValue(gameName, out GameContext ctx))
                return null;
            ctx.Packages.TryGetValue(packageName, out ResourcePackage package);
            return package;
        }

        #endregion

        #region 游戏初始化

        /// <summary>
        /// 更新并下载指定包到最新（路由方法）。
        /// 根据 IsUsingEditorMode() 调用对应的 Editor 或 Runtime 实现。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="packageName">包名称</param>
        /// <param name="maxConcurrentDownloads">最大并发下载数，小于等于 0 时使用默认值 10</param>
        /// <param name="retryCount">重试次数，小于 0 时使用默认值 3</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask DownloadPackage(
            string gameName,
            string packageName,
            int maxConcurrentDownloads,
            int retryCount,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!_platformInitialized)
            {
                throw new Exception("Platform not initialized.");
            }

            if (IsUsingEditorMode())
            {
                await DownloadPackageEditor(gameName, packageName, cancellationToken);
            }
            else
            {
                await DownloadPackageRuntime(gameName, packageName, maxConcurrentDownloads, retryCount, cancellationToken);
            }
        }

        /// <summary>
        /// Editor 模式下下载包（直接跳过，不执行下载）。
        /// Editor 模式下资源从构建输出目录直接加载，不需要下载到 yoo 缓存目录。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="packageName">包名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async UniTask DownloadPackageEditor(
            string gameName,
            string packageName,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!_isEditorEnvironment)
            {
                throw new Exception("DownloadPackageEditor can only be called in Editor environment");
            }

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateManager] Editor 模式下跳过包下载: package='{packageName}', game='{gameName}' (资源从构建输出目录直接加载)");
#endif

            await UniTask.Yield();
        }

        /// <summary>
        /// Runtime 模式下下载包到最新。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="packageName">包名称</param>
        /// <param name="maxConcurrentDownloads">最大并发下载数，小于等于 0 时使用默认值 10</param>
        /// <param name="retryCount">重试次数，小于 0 时使用默认值 3</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async UniTask DownloadPackageRuntime(
            string gameName,
            string packageName,
            int maxConcurrentDownloads,
            int retryCount,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            ResourcePackage package = GetPackage(gameName, packageName);
            if (package == null)
            {
                throw new Exception($"Package not found: {packageName}");
            }

            int max = maxConcurrentDownloads <= 0 ? HotUpdateConstants.DefaultMaxConcurrentDownloads : maxConcurrentDownloads;
            int retry = retryCount < 0 ? HotUpdateConstants.DefaultRetryCount : retryCount;

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateManager] 开始请求包版本: package='{packageName}', game='{gameName}'");
#endif

            RequestPackageVersionOperation versionOp = package.RequestPackageVersionAsync();
            await ToUniTask(versionOp, cancellationToken);

            if (versionOp.Status != EOperationStatus.Succeed)
            {
#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.LogError($"[HotUpdateManager] 包版本请求失败: package='{packageName}', game='{gameName}', error='{versionOp.Error ?? "未知错误"}'");
#endif
                throw new Exception(versionOp.Error ?? "Version request failed");
            }

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateManager] ✓ 包版本请求成功: package='{packageName}', game='{gameName}', version='{versionOp.PackageVersion}'");
            Debug.Log($"[HotUpdateManager] 开始更新包清单: package='{packageName}', game='{gameName}', version='{versionOp.PackageVersion}'");
#endif

            UpdatePackageManifestOperation updateOp = package.UpdatePackageManifestAsync(versionOp.PackageVersion);
            await ToUniTask(updateOp, cancellationToken);

            if (updateOp.Status != EOperationStatus.Succeed)
            {
#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.LogError($"[HotUpdateManager] 包清单更新失败: package='{packageName}', game='{gameName}', error='{updateOp.Error ?? "未知错误"}'");
#endif
                throw new Exception(updateOp.Error ?? "Manifest update failed");
            }

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateManager] ✓ 包清单更新成功: package='{packageName}', game='{gameName}'");
            Debug.Log($"[HotUpdateManager] 创建资源下载器: package='{packageName}', game='{gameName}', maxConcurrent={max}, retryCount={retry}");
#endif

            ResourceDownloaderOperation downloader = package.CreateResourceDownloader(max, retry);

            if (downloader.TotalDownloadCount <= 0)
            {
#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.Log($"[HotUpdateManager] 包无需下载: package='{packageName}', game='{gameName}' (TotalDownloadCount=0)");
#endif
                return;
            }

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateManager] 开始下载资源: package='{packageName}', game='{gameName}', 需要下载 {downloader.TotalDownloadCount} 个文件，总大小 {downloader.TotalDownloadBytes} 字节");
#endif

            // 设置下载进度回调
            downloader.DownloadUpdateCallback = (data) =>
            {
#if UNITY_DEBUG && GAME_BOOTSTRAP
                float progressPercent = data.Progress * 100f;
                Debug.Log($"[HotUpdateManager] 下载进度: package='{packageName}', game='{gameName}', " +
                    $"进度={progressPercent:F2}%, " +
                    $"文件={data.CurrentDownloadCount}/{data.TotalDownloadCount}, " +
                    $"大小={data.CurrentDownloadBytes}/{data.TotalDownloadBytes} 字节");
#endif
            };
            
            // 设置下载错误回调
            downloader.DownloadErrorCallback = (data) =>
            {
#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.LogError($"[HotUpdateManager] 下载文件失败: package='{packageName}', game='{gameName}', " +
                    $"文件名='{data.FileName}', 错误='{data.ErrorInfo}'");
#endif
            };
            
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateManager] 调用 BeginDownload 启动下载: package='{packageName}', game='{gameName}'");
#endif

            // 必须调用 BeginDownload() 才能启动下载
            downloader.BeginDownload();
            
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateManager] BeginDownload 调用完成，等待下载完成: package='{packageName}', game='{gameName}'");
#endif

            await ToUniTask(downloader, cancellationToken);

            if (downloader.Status != EOperationStatus.Succeed)
            {
#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.LogError($"[HotUpdateManager] 资源下载失败: package='{packageName}', game='{gameName}', error='{downloader.Error ?? "未知错误"}'");
#endif
                throw new Exception(downloader.Error ?? "Download failed");
            }

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateManager] ✓ 资源下载成功: package='{packageName}', game='{gameName}', 已下载 {downloader.TotalDownloadCount} 个文件");
#endif
        }

        /// <summary>
        /// 更新并下载指定游戏的所有已注册包到最新。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="maxConcurrentDownloads">最大并发下载数，小于等于 0 时使用默认值 10</param>
        /// <param name="retryCount">重试次数，小于 0 时使用默认值 3</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask UpdateAndDownloadAllPackages(
            string gameName,
            int maxConcurrentDownloads,
            int retryCount,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!_platformInitialized)
            {
                throw new Exception("Platform not initialized.");
            }

            GameContext ctx = GetOrNull(gameName);
            if (ctx == null || ctx.RegisteredPackageNames.Count == 0)
            {
                throw new Exception("No packages registered for this game.");
            }

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateManager] 开始下载游戏 {gameName} 的所有包，共 {ctx.RegisteredPackageNames.Count} 个包");
            Debug.Log($"[HotUpdateManager] 包列表: {string.Join(", ", ctx.RegisteredPackageNames)}");
#endif

            // 使用 for 循环顺序下载所有包
            for (int i = 0; i < ctx.RegisteredPackageNames.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                string packageName = ctx.RegisteredPackageNames[i];

#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.Log($"[HotUpdateManager] 开始下载包 [{i + 1}/{ctx.RegisteredPackageNames.Count}]: {packageName}");
#endif

                await DownloadPackage(gameName, packageName, maxConcurrentDownloads, retryCount, cancellationToken);

#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.Log($"[HotUpdateManager] ✓ 包下载完成 [{i + 1}/{ctx.RegisteredPackageNames.Count}]: {packageName}");
#endif
            }
        }

        #endregion

        #region 资源加载（RawFile）

        /// <summary>
        /// 异步加载 RawFile（UniTask 版本）。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="packageName">包名称</param>
        /// <param name="location">资源位置标识，用于定位资源文件</param>
        /// <param name="priority">加载优先级，数值越大优先级越高</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>RawFileHandle，失败时返回 null</returns>
        public static async UniTask<RawFileHandle> LoadRawFile(
            string gameName,
            string packageName,
            string location,
            uint priority,
            CancellationToken cancellationToken = default)
        {
            if (!_platformInitialized)
            {
#if UNITY_DEBUG
                Debug.LogError($"[MiniGameYooAssetApi] LoadRawFile 失败: 平台未初始化, location='{location}'");
#endif
                return null;
            }

            ResourcePackage package = GetPackage(gameName, packageName);
            if (package == null)
            {
#if UNITY_DEBUG
                Debug.LogError($"[MiniGameYooAssetApi] LoadRawFile 失败: 包不存在, location='{location}', package='{packageName}', game='{gameName}'");
#endif
                return null;
            }

            if (string.IsNullOrEmpty(location))
            {
#if UNITY_DEBUG
                Debug.LogError($"[MiniGameYooAssetApi] LoadRawFile 失败: location 为空, package='{packageName}', game='{gameName}'");
#endif
                return null;
            }

#if UNITY_DEBUG && YOO_ASSET
            Debug.Log($"[MiniGameYooAssetApi] 开始加载 RawFile: location='{location}', package='{packageName}', game='{gameName}'");
#endif

            UniTaskCompletionSource<RawFileHandle> completionSource = new UniTaskCompletionSource<RawFileHandle>();

            RawFileHandle handle = package.LoadRawFileAsync(location, priority);
            handle.Completed += h =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled();
                    return;
                }

#if UNITY_DEBUG && YOO_ASSET
                if (h != null && h.Status == EOperationStatus.Succeed)
                {
                    Debug.Log($"[MiniGameYooAssetApi] RawFile 加载成功: location='{location}', package='{packageName}'");
                }
                else
                {
#if UNITY_DEBUG
                    Debug.LogError($"[MiniGameYooAssetApi] RawFile 加载失败: location='{location}', package='{packageName}', error='{h?.LastError ?? "未知错误"}'");
#endif
                }
#endif

                if (h != null && h.Status == EOperationStatus.Succeed)
                {
                    completionSource.TrySetResult(h);
                }
                else
                {
                    completionSource.TrySetResult(null);
                }
            };

            return await completionSource.Task;
        }

        /// <summary>
        /// 路径映射：将输入 path 映射到该小游戏目录下的统一路径。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="path">相对路径，例如 "Audio/xxx.mp3" 或 "Assets/MiniGames/BBQ/Audio/xxx.mp3"</param>
        /// <returns>映射后的路径</returns>
        public static string MapMiniGameAssetPath(string gameName, string path)
        {
            if (string.IsNullOrEmpty(gameName))
                return path ?? string.Empty;
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string normalized = path.Replace('\\', '/').Trim();
            string basePrefix = $"Assets/MiniGames/{gameName}/";

            if (normalized.StartsWith(HotUpdateConstants.MiniGamesRootPath, StringComparison.Ordinal))
            {
                string rest = normalized.Substring(HotUpdateConstants.MiniGamesRootPath.Length);
                int slash = rest.IndexOf('/');
                if (slash >= 0)
                {
                    string tail = rest.Substring(slash + 1);
                    return basePrefix + tail;
                }
                return basePrefix;
            }

            if (normalized.StartsWith("Assets/", StringComparison.Ordinal))
                return normalized;

            normalized = normalized.TrimStart('/');
            return basePrefix + normalized;
        }

        #endregion

        #region HotUpdate DLL 加载

        /// <summary>
        /// 加载 HotUpdate DLL 并加载到运行时（路由方法）。
        /// 根据 IsUsingEditorMode() 的结果调用对应的 Editor 或 Runtime 实现。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="packageName">包名称</param>
        /// <param name="dllLocation">DLL 位置（location），例如 "Assets/MiniGames/BBQ/HotUpdate/BBQHotUpdate.dll.bytes"</param>
        /// <param name="onCompleted">完成回调，返回已加载的程序集对象，失败时返回 null</param>
        /// <summary>
        /// 加载热更新 DLL。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="packageName">包名称</param>
        /// <param name="dllLocation">DLL 位置（从 Assets/ 开始的路径）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已加载的程序集，失败时返回 null</returns>
        public static async UniTask<Assembly> LoadHotUpdateDll(
            string gameName,
            string packageName,
            string dllLocation,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (IsUsingEditorMode())
            {
                return await LoadHotUpdateDllEditor(dllLocation, cancellationToken);
            }
            else
            {
                return await LoadHotUpdateDllRuntime(gameName, packageName, dllLocation, cancellationToken);
            }
        }

        /// <summary>
        /// Editor 模式下加载 HotUpdate DLL。
        /// 在 Editor 模式下，程序集已经被 Unity 编译并加载到 AppDomain 中，所以直接从 AppDomain 中查找。
        /// </summary>
        /// <param name="dllLocation">DLL 位置（从 Assets/ 开始的路径），例如 "Assets/MiniGames/BBQ/HotUpdate/BBQHotUpdate.dll.bytes"</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已加载的程序集对象，失败时返回 null</returns>
        private static async UniTask<Assembly> LoadHotUpdateDllEditor(string dllLocation, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (string.IsNullOrEmpty(dllLocation))
            {
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] LoadHotUpdateDllEditor 失败: dllLocation 为空");
#endif
                return null;
            }

            // 从路径中提取程序集名称
            // 例如: Assets/MiniGames/BBQ/HotUpdate/BBQHotUpdate.dll.bytes -> BBQHotUpdate
            // 例如: Assets/Framework/Dll/HotUpdate/Framework.dll.bytes -> Framework
            string assemblyName = ExtractAssemblyNameFromPath(dllLocation);
            if (string.IsNullOrEmpty(assemblyName))
            {
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] LoadHotUpdateDllEditor 失败: 无法从路径提取程序集名称, location='{dllLocation}'");
#endif
                return null;
            }

            try
            {
                await UniTask.Yield();

                // 从 AppDomain 中查找已加载的程序集
                Assembly assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);

                if (assembly == null)
                {
#if UNITY_DEBUG
                    Debug.LogError($"[HotUpdateManager] LoadHotUpdateDllEditor 失败: 程序集未找到, assemblyName='{assemblyName}', location='{dllLocation}'");
#endif
                    return null;
                }

#if UNITY_DEBUG && YOO_ASSET
                Debug.Log($"[HotUpdateManager] LoadHotUpdateDllEditor 成功: location='{dllLocation}', assemblyName='{assemblyName}', assembly='{assembly.GetName().Name}'");
#endif
                return assembly;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] LoadHotUpdateDllEditor 查找程序集失败: location='{dllLocation}', assemblyName='{assemblyName}', error='{ex.Message}'");
#endif
                return null;
            }
        }

        /// <summary>
        /// 从 DLL 路径中提取程序集名称。
        /// 例如: Assets/MiniGames/BBQ/HotUpdate/BBQHotUpdate.dll.bytes -> BBQHotUpdate
        /// 例如: Assets/Framework/Dll/HotUpdate/Framework.dll.bytes -> Framework
        /// </summary>
        /// <param name="dllLocation">DLL 位置路径</param>
        /// <returns>程序集名称，如果无法提取则返回 null</returns>
        private static string ExtractAssemblyNameFromPath(string dllLocation)
        {
            if (string.IsNullOrEmpty(dllLocation))
                return null;

            // 移除路径分隔符，统一使用 /
            string normalized = dllLocation.Replace('\\', '/');

            // 获取文件名（不包含路径）
            int lastSlash = normalized.LastIndexOf('/');
            string fileName = lastSlash >= 0 ? normalized.Substring(lastSlash + 1) : normalized;

            // 移除扩展名（.dll.bytes 或 .dll）
            string assemblyName = fileName;
            if (assemblyName.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName = assemblyName.Substring(0, assemblyName.Length - 10); // 移除 ".dll.bytes" (10个字符)
            }
            else if (assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName = assemblyName.Substring(0, assemblyName.Length - 4); // 移除 ".dll" (4个字符)
            }

            // 确保去除尾部空格和点
            if (!string.IsNullOrEmpty(assemblyName))
            {
                assemblyName = assemblyName.TrimEnd('.', ' ');
            }

            return string.IsNullOrEmpty(assemblyName) ? null : assemblyName;
        }

        /// <summary>
        /// Runtime 模式下加载 HotUpdate DLL。
        /// 通过 YooAsset 包加载 DLL 文件，然后使用 Assembly.Load 加载程序集。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="packageName">包名称</param>
        /// <param name="dllLocation">DLL 位置（location），例如 "Assets/MiniGames/BBQ/HotUpdate/BBQHotUpdate.dll.bytes"</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已加载的程序集对象，失败时返回 null</returns>
        private static async UniTask<Assembly> LoadHotUpdateDllRuntime(
            string gameName,
            string packageName,
            string dllLocation,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            RawFileHandle handle = await LoadRawFile(gameName, packageName, dllLocation, 0, cancellationToken);
            
            if (handle == null || handle.Status != EOperationStatus.Succeed)
            {
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] LoadHotUpdateDllRuntime 失败: location='{dllLocation}', package='{packageName}', game='{gameName}'");
#endif
                return null;
            }

            byte[] dllBytes = handle.GetRawFileData();
            if (dllBytes == null || dllBytes.Length == 0)
            {
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] LoadHotUpdateDllRuntime 失败: DLL 字节数据为空, location='{dllLocation}', package='{packageName}', game='{gameName}'");
#endif
                return null;
            }

            // 验证 DLL 文件格式（检查 PE 头）
            if (dllBytes.Length < 64 || !IsValidPEFile(dllBytes))
            {
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] LoadHotUpdateDllRuntime 失败: DLL 文件格式无效或损坏, location='{dllLocation}', package='{packageName}', game='{gameName}', size={dllBytes.Length}");
                if (dllBytes.Length >= 2)
                {
                    Debug.LogError($"[HotUpdateManager] DLL 文件前两个字节: 0x{dllBytes[0]:X2} 0x{dllBytes[1]:X2} (应该是 0x4D 0x5A 表示 'MZ' PE 文件头)");
                }
#endif
                return null;
            }

            try
            {
                Assembly assembly = Assembly.Load(dllBytes);
#if UNITY_DEBUG && YOO_ASSET
                Debug.Log($"[HotUpdateManager] LoadHotUpdateDllRuntime 成功: location='{dllLocation}', package='{packageName}', game='{gameName}', assembly='{assembly.GetName().Name}'");
#endif
                return assembly;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (BadImageFormatException ex)
            {
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] LoadHotUpdateDllRuntime 加载程序集失败: DLL 文件格式错误 (BadImageFormatException), location='{dllLocation}', package='{packageName}', game='{gameName}', error='{ex.Message}'");
                Debug.LogError($"[HotUpdateManager] 可能的原因: 1) DLL 文件损坏 2) DLL 文件不完整 3) DLL 文件格式不正确 4) 架构不匹配 (x86/x64)");
                Debug.LogError($"[HotUpdateManager] DLL 文件大小: {dllBytes.Length} 字节");
#endif
                return null;
            }
            catch (Exception ex)
            {
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] LoadHotUpdateDllRuntime 加载程序集失败: location='{dllLocation}', package='{packageName}', game='{gameName}', error='{ex.Message}'");
                Debug.LogError($"[HotUpdateManager] 异常类型: {ex.GetType().Name}, DLL 文件大小: {dllBytes.Length} 字节");
#endif
                return null;
            }
        }

        /// <summary>
        /// 验证 DLL 文件是否为有效的 PE 格式。
        /// </summary>
        /// <param name="dllBytes">DLL 字节数据</param>
        /// <returns>如果是有效的 PE 文件则返回 true，否则返回 false</returns>
        private static bool IsValidPEFile(byte[] dllBytes)
        {
            if (dllBytes == null || dllBytes.Length < 64)
            {
                return false;
            }

            // 检查 DOS 头：前两个字节应该是 "MZ" (0x4D 0x5A)
            if (dllBytes[0] != 0x4D || dllBytes[1] != 0x5A)
            {
                return false;
            }

            // 检查 PE 头偏移（DOS 头的偏移 0x3C 处存储了 PE 头的偏移）
            if (dllBytes.Length < 64)
            {
                return false;
            }

            int peHeaderOffset = BitConverter.ToInt32(dllBytes, 0x3C);
            if (peHeaderOffset < 0 || peHeaderOffset >= dllBytes.Length - 4)
            {
                return false;
            }

            // 检查 PE 签名：应该是 "PE\0\0" (0x50 0x45 0x00 0x00)
            if (dllBytes[peHeaderOffset] != 0x50 || dllBytes[peHeaderOffset + 1] != 0x45 ||
                dllBytes[peHeaderOffset + 2] != 0x00 || dllBytes[peHeaderOffset + 3] != 0x00)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region AOT Metadata DLL 加载

        /// <summary>
        /// 从 FrameworkRaw 包中加载 AOT Metadata DLL（路由方法）。
        /// 根据 IsUsingEditorMode() 的结果调用对应的 Editor 或 Runtime 实现。
        /// </summary>
        /// <param name="dllName">DLL 名称（不包含路径，例如 "mscorlib.dll"）</param>
        /// <param name="onCompleted">完成回调，success 表示是否成功，error 为错误信息</param>
        /// <summary>
        /// 加载 AOT Metadata DLL（路由方法）。
        /// Editor 模式下直接返回成功，Runtime 模式下需要先加载 DLL 字节数据，然后调用此方法进行补元。
        /// </summary>
        /// <param name="dllName">DLL 名称（不包含路径，例如 "mscorlib.dll"）</param>
        /// <param name="dllBytes">DLL 字节数据（Runtime 模式必需，Editor 模式可忽略）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask LoadAOTMetadataDll(string dllName, byte[] dllBytes, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (string.IsNullOrEmpty(dllName))
            {
                throw new ArgumentException("DLL name is empty.", nameof(dllName));
            }

            if (IsUsingEditorMode())
            {
                await LoadAOTMetadataDllEditor(dllName, cancellationToken);
            }
            else
            {
                await LoadAOTMetadataDllRuntime(dllName, dllBytes, cancellationToken);
            }
        }

        /// <summary>
        /// Editor 模式下加载 AOT Metadata DLL。
        /// Editor 模式下不需要补元，Unity 已经处理了所有类型，直接返回成功。
        /// </summary>
        /// <param name="dllName">DLL 名称（用于日志）</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async UniTask LoadAOTMetadataDllEditor(string dllName, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

#if UNITY_DEBUG && YOO_ASSET
            Debug.Log($"[HotUpdateManager] Editor 模式下跳过 AOT Metadata DLL 加载: {dllName}");
#endif
            await UniTask.Yield();
        }

        /// <summary>
        /// Runtime 模式下加载 AOT Metadata DLL（仅执行补元操作）。
        /// 接收已加载的 DLL 字节数据，使用 HybridCLR 加载元数据。
        /// </summary>
        /// <param name="dllName">DLL 名称（用于日志）</param>
        /// <param name="dllBytes">DLL 字节数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async UniTask LoadAOTMetadataDllRuntime(string dllName, byte[] dllBytes, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (dllBytes == null || dllBytes.Length == 0)
            {
                string error = $"AOT Metadata DLL data is empty: {dllName}";
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] {error}");
#endif
                throw new Exception(error);
            }

            try
            {
                LoadImageErrorCode errorCode = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.SuperSet);
                if (errorCode != LoadImageErrorCode.OK)
                {
                    string error = $"Failed to load AOT Metadata DLL: {dllName}, error code: {errorCode}";
#if UNITY_DEBUG
                    Debug.LogError($"[HotUpdateManager] {error}");
#endif
                    throw new Exception(error);
                }

#if UNITY_DEBUG && YOO_ASSET
                Debug.Log($"[HotUpdateManager] AOT Metadata DLL loaded successfully: {dllName}");
#endif
                await UniTask.Yield();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                string error = $"Exception while loading AOT Metadata DLL: {dllName}, exception: {ex.Message}";
#if UNITY_DEBUG
                Debug.LogError($"[HotUpdateManager] {error}");
#endif
                throw new Exception(error, ex);
            }
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 回收未使用资源。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="packageName">包名称</param>
        /// <param name="loopCount">回收循环次数，小于等于 0 时使用默认值 10</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async UniTask UnloadUnusedAssets(string gameName, string packageName, int loopCount, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            ResourcePackage package = GetPackage(gameName, packageName);
            if (package == null)
            {
                throw new Exception("Package not found.");
            }

            AsyncOperationBase op = package.UnloadUnusedAssetsAsync(loopCount <= 0 ? 10 : loopCount);
            await ToUniTask(op, cancellationToken);

            if (op.Status != EOperationStatus.Succeed)
            {
                throw new Exception(op.Error ?? "Unload unused assets failed");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将 AsyncOperationBase 转换为 UniTask。
        /// </summary>
        private static async UniTask ToUniTask(AsyncOperationBase operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (operation.IsDone)
            {
                if (operation.Status == EOperationStatus.Succeed)
                {
                    return;
                }
                else
                {
                    throw new Exception(operation.Error ?? "Operation failed");
                }
            }

            UniTaskCompletionSource completionSource = new UniTaskCompletionSource();
            
            Action<AsyncOperationBase> handler = null;
            handler = op =>
            {
                operation.Completed -= handler;
                
                if (cancellationToken.IsCancellationRequested)
                {
                    completionSource.TrySetCanceled();
                    return;
                }

                if (op.Status == EOperationStatus.Succeed)
                {
                    completionSource.TrySetResult();
                }
                else
                {
                    completionSource.TrySetException(new Exception(op.Error ?? "Operation failed"));
                }
            };

            operation.Completed += handler;
            
            // 如果操作在注册回调后立即完成，需要检查
            if (operation.IsDone)
            {
                handler(operation);
            }

            await completionSource.Task;
        }

        #endregion

        #region 工具函数

        /// <summary>
        /// 销毁某个小游戏的所有 package。
        /// </summary>
        public static void DestroyGame(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
                return;
            if (!Games.TryGetValue(gameName, out GameContext ctx))
                return;

            foreach (KeyValuePair<string, ResourcePackage> kv in ctx.Packages)
            {
                DestroyPackageSilently(kv.Value);
            }

            ctx.Packages.Clear();
            ctx.RegisteredPackageNames.Clear();
            Games.Remove(gameName);
        }

        private static void DestroyPackageSilently(ResourcePackage package)
        {
            if (package == null)
                return;
            AsyncOperationBase op = package.DestroyAsync();
            op.Completed += _ => { YooAssets.RemovePackage(package); };
        }

        private static GameContext GetOrNull(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
                return null;
            Games.TryGetValue(gameName, out GameContext ctx);
            return ctx;
        }

        private static GameContext GetOrCreate(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
                return null;
            if (Games.TryGetValue(gameName, out GameContext ctx))
                return ctx;
            ctx = new GameContext { GameName = gameName };
            Games.Add(gameName, ctx);
            return ctx;
        }

        #endregion
    }
}
