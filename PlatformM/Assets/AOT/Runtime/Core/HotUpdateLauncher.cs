using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;
using Cysharp.Threading.Tasks;

namespace Astorise.Framework.AOT
{
    /// <summary>
    /// 热更新资源加载器，负责 Framework 和游戏的资源下载、DLL 加载等操作。
    /// 所有资源加载相关操作都通过 HotUpdateManager。
    /// 使用 UniTask 提供清晰的步骤化执行流程。
    /// </summary>
    public static class HotUpdateLauncher
    {
        /// <summary>
        /// 游戏包映射配置管理类缓存
        /// </summary>
        private static GamePackageMappingXML _gamePackageMapping;

        /// <summary>
        /// PatchAOTAssemblies 配置管理类缓存
        /// </summary>
        private static PatchAOTAssembliesXML _patchAOTAssembliesXML;

        #region 常量定义

        /// <summary>
        /// Framework 游戏名称
        /// </summary>
        private const string FrameworkGameName = "Framework";

        /// <summary>
        /// Framework 包名
        /// </summary>
        private const string FrameworkPackageName = "FrameworkRaw";

        /// <summary>
        /// Framework DLL 位置（使用 Assets 开头的全路径）
        /// </summary>
        private const string FrameworkDllLocation = "Assets/Framework/Dll/HotUpdate/Framework.dll.bytes";

        /// <summary>
        /// 游戏包映射配置文件位置
        /// </summary>
        private const string GamePackageMappingLocation = "Assets/Framework/Config/GamePackageMapping.xml";

        /// <summary>
        /// AOT 程序集补丁配置文件位置
        /// </summary>
        private const string PatchAOTAssembliesLocation = "Assets/Framework/Config/PatchAOTAssemblies.xml";

        #endregion

        #region 公共 API

        /// <summary>
        /// 主引导方法，按顺序执行所有步骤。
        /// </summary>
        /// <param name="rootURL">服务器根 URL，如果为 null 则使用 GameBootstrap.GetRootURL()</param>
        /// <param name="buildinRoot">内置资源根目录，如果为 null 则使用 GameBootstrap.GetBuildinRoot()</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>UniTask</returns>
        public static async UniTask Start(
            string rootURL = null,
            string buildinRoot = null,
            CancellationToken cancellationToken = default)
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] 开始游戏引导流程");
#endif

            // 获取配置参数（如果未提供则使用 GameBootstrap 的默认值）
            string finalRootURL = rootURL ?? GameBootstrap.GetRootURL();
            string finalBuildinRoot = buildinRoot ?? GameBootstrap.GetBuildinRoot();

            // 初始化
            await InitializeYooAsset(cancellationToken);

            // 初始化配置
            await InitializeConfig(finalRootURL, finalBuildinRoot, cancellationToken);

            // 注册 Framework
            RegisterFramework();

            // 下载 Framework（内部会初始化 XML 配置）
            await DownloadAndLoadFramework(cancellationToken);
        }

        /// <summary>
        /// 下载指定游戏的所有资源包，并在下载完成后自动加载配置中的 DLL。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已加载的 DLL 程序集，如果未配置或加载失败则返回 null</returns>
        public static async UniTask<Assembly> DownloadGame(string gameName, CancellationToken cancellationToken = default)
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateLauncher] 开始下载游戏资源: {gameName}");
#endif

            // 检查配置是否已初始化
            if (_gamePackageMapping == null || !_gamePackageMapping.IsInitialized)
            {
                throw new Exception("GamePackageMappingXML 未初始化，请先调用 HotUpdateLauncher.Start()");
            }

            // 使用缓存的游戏配置（已在 DownloadFrameworkPackage 中初始化）
            // 注册所有包
            RegisterGamePackages(gameName);

            // 初始化游戏
            await InitializeGame(gameName, cancellationToken);

            // 下载所有包
            await DownloadAllGamePackages(gameName, cancellationToken);

            // 加载 DLL（如果配置了）
            Assembly assembly = await LoadGameDll(gameName, cancellationToken);

            return assembly;
        }

        /// <summary>
        /// 获取游戏的 DLL 位置。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <returns>DLL 位置，如果未配置则返回 null</returns>
        public static string GetGameDllLocation(string gameName)
        {
            if (_gamePackageMapping == null || !_gamePackageMapping.IsInitialized)
            {
                return null;
            }

            return _gamePackageMapping.GetGameDll(gameName);
        }

        #endregion

        #region Framework 相关

        /// <summary>
        /// 注册 Framework 游戏（使用硬编码常量）。
        /// </summary>
        private static void RegisterFramework()
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] 注册 Framework 游戏");
#endif
            HotUpdateManager.RegisterPackage(FrameworkGameName, FrameworkPackageName);
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] ✓ Framework 游戏注册完成");
#endif
        }

        /// <summary>
        /// 下载 Framework 并加载 DLL。
        /// </summary>
        private static async UniTask DownloadAndLoadFramework(CancellationToken cancellationToken = default)
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] 开始下载 Framework 并加载 DLL");
#endif

            // 初始化 Framework 游戏
            await InitializeGame(FrameworkGameName, cancellationToken);

            // 下载 Framework 包
            await DownloadFrameworkPackage(FrameworkGameName, cancellationToken);

            // 加载 AOT Metadata DLL（失败不影响主流程）
            await LoadAOTMetadataDlls(cancellationToken);

            // 加载 Framework DLL
            await LoadFrameworkDll(FrameworkGameName, cancellationToken);
        }

        /// <summary>
        /// 下载 Framework 包并初始化 XML 配置。
        /// </summary>
        private static async UniTask DownloadFrameworkPackage(string frameworkGameName, CancellationToken cancellationToken)
        {
            await HotUpdateManager.DownloadPackage(
                frameworkGameName,
                FrameworkPackageName,
                0, // 使用默认最大并发下载数
                0, // 使用默认重试次数
                cancellationToken);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] ✓ Framework 包下载完成");
#endif

            // 初始化 GamePackageMappingXML
            await InitializeGamePackageMapping(cancellationToken);

            // 初始化 PatchAOTAssembliesXML
            await InitializePatchAOTAssembliesXML(cancellationToken);
        }

        /// <summary>
        /// 加载 Framework DLL。
        /// </summary>
        private static async UniTask LoadFrameworkDll(string frameworkGameName, CancellationToken cancellationToken)
        {
            Assembly assembly = await HotUpdateManager.LoadHotUpdateDll(frameworkGameName, FrameworkPackageName, FrameworkDllLocation, cancellationToken);

            if (assembly == null)
            {
                throw new Exception("Failed to load Framework DLL");
            }

            // 注册 Framework 程序集到 AssemblyReferenceManager
            AssemblyReferenceManager.RegisterAssembly(FrameworkGameName, assembly);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] ✓ Framework DLL 加载成功并已注册到 AssemblyReferenceManager");
#endif
        }

        #endregion

        #region 游戏下载相关

        /// <summary>
        /// 初始化 GamePackageMappingXML（从 FrameworkRaw 包中加载并初始化）。
        /// </summary>
        private static async UniTask InitializeGamePackageMapping(CancellationToken cancellationToken)
        {
            RawFileHandle mappingHandle = await HotUpdateManager.LoadRawFile(
                FrameworkGameName,
                FrameworkPackageName,
                GamePackageMappingLocation,
                0,
                cancellationToken);

            if (mappingHandle == null)
            {
                throw new Exception($"从 FrameworkRaw 包加载 GamePackageMapping.xml 失败: {GamePackageMappingLocation}");
            }

            string xmlContent = mappingHandle.GetRawFileText();
            if (string.IsNullOrEmpty(xmlContent))
            {
                throw new Exception("GamePackageMapping.xml 内容为空");
            }

            // 使用 System.Xml.Linq 进行纯 XML 解析
            XDocument doc = XDocument.Parse(xmlContent);
            if (doc == null || doc.Root == null)
            {
                throw new Exception("解析 GamePackageMapping.xml 失败");
            }

            // 如果已存在，直接创建新的（不需要 release，因为只是简单的类实例）
            _gamePackageMapping = new GamePackageMappingXML();
            _gamePackageMapping.Initialize(doc);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] ✓ GamePackageMapping.xml 初始化完成");
#endif
        }

        /// <summary>
        /// 初始化 PatchAOTAssembliesXML（从 FrameworkRaw 包中加载并初始化）。
        /// </summary>
        private static async UniTask InitializePatchAOTAssembliesXML(CancellationToken cancellationToken)
        {
            RawFileHandle handle = await HotUpdateManager.LoadRawFile(
                FrameworkGameName,
                FrameworkPackageName,
                PatchAOTAssembliesLocation,
                0,
                cancellationToken);

            if (handle == null)
            {
                throw new Exception($"从 FrameworkRaw 包加载 PatchAOTAssemblies.xml 失败: {PatchAOTAssembliesLocation}");
            }

            string xmlContent = handle.GetRawFileText();
            if (string.IsNullOrEmpty(xmlContent))
            {
                throw new Exception("PatchAOTAssemblies.xml 内容为空");
            }

            // 使用 System.Xml.Linq 进行纯 XML 解析
            XDocument doc = XDocument.Parse(xmlContent);
            if (doc == null || doc.Root == null)
            {
                throw new Exception("解析 PatchAOTAssemblies.xml 失败");
            }

            // 如果已存在，直接创建新的（不需要 release，因为只是简单的类实例）
            _patchAOTAssembliesXML = new PatchAOTAssembliesXML();
            _patchAOTAssembliesXML.Initialize(doc);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] ✓ PatchAOTAssemblies.xml 初始化完成");
#endif
        }


        /// <summary>
        /// 注册游戏的所有包。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        private static void RegisterGamePackages(string gameName)
        {
            if (_gamePackageMapping == null || !_gamePackageMapping.IsInitialized)
            {
                throw new Exception("GamePackageMappingXML 未初始化，无法注册游戏包");
            }

            XElement packageElement = _gamePackageMapping.GetGamePackageElement(gameName);

            foreach (XElement subPackageElement in packageElement.Elements("SubPackage"))
            {
                XAttribute nameAttr = subPackageElement.Attribute("Name");
                if (nameAttr == null || string.IsNullOrEmpty(nameAttr.Value))
                {
                    continue;
                }

                string packageName = nameAttr.Value;

                // 注册包
                HotUpdateManager.RegisterPackage(gameName, packageName);

                // 检查并设置 AppendExtension 配置
                XAttribute appendExtensionAttr = subPackageElement.Attribute("AppendExtension");
                if (appendExtensionAttr != null && !string.IsNullOrEmpty(appendExtensionAttr.Value))
                {
                    string appendExtensionValue = appendExtensionAttr.Value;
                    if (string.Equals(appendExtensionValue, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        HotUpdateManager.SetAppendFileExtensionPackage(packageName);
#if UNITY_DEBUG && YOO_ASSET
                        Debug.Log($"[HotUpdateLauncher] 从配置中设置包需要启用 APPEND_FILE_EXTENSION: game='{gameName}', package='{packageName}'");
#endif
                    }
                }
            }
        }

        /// <summary>
        /// 初始化游戏。
        /// </summary>
        private static async UniTask InitializeGame(string gameName, CancellationToken cancellationToken)
        {
            await HotUpdateManager.InitializeGame(gameName, cancellationToken);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateLauncher] ✓ 游戏 {gameName} 初始化完成");
#endif
        }

        /// <summary>
        /// 下载游戏的所有包。
        /// </summary>
        private static async UniTask DownloadAllGamePackages(string gameName, CancellationToken cancellationToken)
        {
            // 获取包数量用于日志
            string[] packages = HotUpdateManager.GetRegisteredPackages(gameName);
            int packageCount = packages != null ? packages.Length : 0;

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateLauncher] 开始下载游戏 {gameName} 的资源包，共 {packageCount} 个包");
            if (packageCount > 0)
            {
                Debug.Log($"[HotUpdateLauncher] 包列表: {string.Join(", ", packages)}");
            }
#endif

            await HotUpdateManager.UpdateAndDownloadAllPackages(
                gameName,
                0, // 使用默认最大并发下载数
                0, // 使用默认重试次数
                cancellationToken);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateLauncher] ✓ 游戏 {gameName} 所有包下载完成");
#endif
        }

        /// <summary>
        /// 如果配置了 DLL，则加载游戏的 DLL。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <param name="gameMapping">游戏包映射配置管理类</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>已加载的程序集，如果未配置 DLL 或加载失败则返回 null</returns>
        private static async UniTask<Assembly> LoadGameDll(string gameName, CancellationToken cancellationToken)
        {
            if (_gamePackageMapping == null || !_gamePackageMapping.IsInitialized)
            {
                throw new Exception("GamePackageMappingXML 未初始化，无法加载游戏 DLL");
            }

            string dllLocation = _gamePackageMapping.GetGameDll(gameName);
            if (string.IsNullOrEmpty(dllLocation))
            {
#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.Log($"[HotUpdateLauncher] 游戏 {gameName} 没有配置 DLL，跳过加载");
#endif
                return null;
            }

            // 查找包含 "Raw" 的包名（DLL 必须在 Raw 包里）
            string[] packages = _gamePackageMapping.GetGamePackages(gameName);
            string dllPackageName = packages.FirstOrDefault(p => !string.IsNullOrEmpty(p) && p.Contains("Raw"));

            if (string.IsNullOrEmpty(dllPackageName))
            {
                throw new Exception($"Game '{gameName}' has no 'Raw' package for DLL loading");
            }

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateLauncher] 开始加载游戏 {gameName} 的 DLL: {dllLocation}");
#endif

            Assembly assembly = await HotUpdateManager.LoadHotUpdateDll(gameName, dllPackageName, dllLocation, cancellationToken);

            if (assembly == null)
            {
                throw new Exception($"Failed to load DLL for game '{gameName}': {dllLocation}");
            }

            // 使用 dllLocation 作为 key 注册程序集到 AssemblyReferenceManager
            AssemblyReferenceManager.RegisterAssembly(dllLocation, assembly);

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log($"[HotUpdateLauncher] ✓ 游戏 {gameName} DLL 加载成功并已注册到 AssemblyReferenceManager，key: {dllLocation}");
#endif

            return assembly;
        }

        #endregion

        #region 初始化相关

        /// <summary>
        /// 初始化 YooAsset 平台。
        /// </summary>
        private static async UniTask InitializeYooAsset(CancellationToken cancellationToken = default)
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] 初始化 YooAsset");
#endif
            await HotUpdateManager.InitializePlatform(cancellationToken);
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] ✓ YooAsset 初始化完成");
#endif
        }

        /// <summary>
        /// 初始化配置（rootURL、buildinRoot）。
        /// </summary>
        private static async UniTask InitializeConfig(
            string rootURL,
            string buildinRoot,
            CancellationToken cancellationToken = default)
        {
            // 检查是否使用 Editor 模式
            bool useEditorMode = HotUpdateManager.IsUsingEditorMode();

            if (useEditorMode)
            {
#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.Log("[HotUpdateLauncher] Editor 模式，跳过配置初始化");
#endif
            }
            else
            {
                // Bundle 模式下需要验证配置
                if (string.IsNullOrEmpty(rootURL))
                {
                    throw new Exception("rootURL is required in Bundle mode. Please provide rootURL parameter.");
                }

                // 直接调用 HotUpdateManager.SetConfig，避免过度封装
                HotUpdateManager.SetConfig(rootURL, buildinRoot);
#if UNITY_DEBUG && GAME_BOOTSTRAP
                Debug.Log("[HotUpdateLauncher] ✓ 配置初始化完成");
#endif
            }
        }

        #endregion

        #region 配置加载相关


        #endregion

        #region AOT Metadata 相关

        /// <summary>
        /// 加载所有 AOT Metadata DLL。
        /// </summary>
        private static async UniTask LoadAOTMetadataDlls(CancellationToken cancellationToken = default)
        {
#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] 开始加载 AOT Metadata DLL");
#endif

            // 直接使用类变量（已在 DownloadFrameworkPackage 中初始化）
            string[] assemblies = _patchAOTAssembliesXML.GetAssemblyList();

            if (assemblies == null || assemblies.Length == 0)
            {
#if UNITY_DEBUG
                Debug.LogWarning("[HotUpdateLauncher] PatchAOTAssemblies.xml 为空或读取失败");
#endif
                return;
            }

            // 顺序加载所有 DLL
            for (int i = 0; i < assemblies.Length; i++)
            {
                string dllName = assemblies[i];
                try
                {
                    // 移除 .dll 扩展名（如果存在）
                    string dllNameWithoutExt = dllName;
                    if (dllNameWithoutExt.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        dllNameWithoutExt = dllNameWithoutExt.Substring(0, dllNameWithoutExt.Length - 4);
                    }

                    // 构建路径：Assets/Framework/Dll/AOTMetadata/{dllName}.dll.bytes
                    string dllLocation = $"Assets/Framework/Dll/AOTMetadata/{dllNameWithoutExt}.dll.bytes";

                    // 加载 DLL 文件（只处理 .dll.bytes）
                    RawFileHandle handle = await HotUpdateManager.LoadRawFile(FrameworkGameName, FrameworkPackageName, dllLocation, 0, cancellationToken);

                    if (handle == null || handle.Status != EOperationStatus.Succeed)
                    {
#if UNITY_DEBUG
                        Debug.LogWarning($"[HotUpdateLauncher] 未找到 AOT Metadata DLL: {dllLocation}");
#endif
                        continue;
                    }

                    // 获取 DLL 字节数据
                    byte[] dllBytes = handle.GetRawFileData();
                    if (dllBytes == null || dllBytes.Length == 0)
                    {
#if UNITY_DEBUG
                        Debug.LogWarning($"[HotUpdateLauncher] AOT Metadata DLL 数据为空: {dllName}");
#endif
                        continue;
                    }

                    // 执行补元操作
                    await HotUpdateManager.LoadAOTMetadataDll(dllName, dllBytes, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 继续加载其他 DLL，不中断流程
#if UNITY_DEBUG
                    Debug.LogWarning($"[HotUpdateLauncher] AOT Metadata DLL 加载失败: {dllName}, error: {ex.Message}");
#endif
                }
            }

#if UNITY_DEBUG && GAME_BOOTSTRAP
            Debug.Log("[HotUpdateLauncher] ✓ AOT Metadata DLL 加载完成");
#endif
        }

        #endregion
    }
}
