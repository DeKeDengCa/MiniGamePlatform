#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using Astorise.Editor.HybridCLR;

namespace Astorise.Editor.Package
{
    /// <summary>
    /// 打包流程执行器。
    /// </summary>
    public static class BuildToolExecutor
    {
        /// <summary>
        /// 执行预构建步骤。
        /// </summary>
        public static void ExecutePreBuildSteps()
        {
            Debug.Log("[BuildToolExecutor] === 开始执行预构建步骤 ===");

            try
            {
                // 1. Auto Configure HotUpdate Assemblies
                Debug.Log("[BuildToolExecutor] 步骤 1: Auto Configure HotUpdate Assemblies");
                AutoConfigureHotUpdateAssemblies.Configure();

                // 2. Generate HybridCLR files
                Debug.Log("[BuildToolExecutor] 步骤 2: Generate HybridCLR All");
                HybridCLRDllCopyUtils.GenerateAll();

                // 3. 注意：根据用户需求，可能还需要调用其他 generate all 步骤
                // 这里假设 Tools/HybridCLR/generate all 就是 HybridCLRDllCopyUtils.GenerateAll()

                // 4. Generate GamePackageMapping.xml
                Debug.Log("[BuildToolExecutor] 步骤 3: Generate GamePackageMapping.xml");
                // 注意：这里需要从 EditorWindow 获取配置，暂时跳过
                // 实际应该在 EditorWindow 中调用 GenerateConfigToXml()

                Debug.Log("[BuildToolExecutor] === 预构建步骤完成 ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildToolExecutor] 预构建步骤失败: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 构建单个包。
        /// </summary>
        /// <param name="packageName">包名称</param>
        /// <param name="version">版本号</param>
        /// <param name="isMainPackage">是否为母包</param>
        /// <returns>是否成功</returns>
        public static bool BuildPackage(string packageName, string version, bool isMainPackage = false)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                Debug.LogError("[BuildToolExecutor] 包名称不能为空");
                return false;
            }

            if (string.IsNullOrEmpty(version))
            {
                Debug.LogError("[BuildToolExecutor] 版本号不能为空");
                return false;
            }

            try
            {
                Debug.Log($"[BuildToolExecutor] 开始构建包: {packageName}, 版本: {version}, 类型: {(isMainPackage ? "母包" : "更新包")}");

                // 获取 AssetBundleCollectorSetting
                var collectorSetting = AssetBundleCollectorSettingData.Setting;
                if (collectorSetting == null)
                {
                    Debug.LogError("[BuildToolExecutor] AssetBundleCollectorSetting 未找到");
                    return false;
                }

                // 验证包配置是否存在
                try
                {
                    collectorSetting.GetPackage(packageName);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BuildToolExecutor] 未找到包配置: {packageName}, 错误: {ex.Message}");
                    return false;
                }

                // 从配置窗口获取构建管线名称
                string buildPipelineName = AssetBundleBuilderSetting.GetPackageBuildPipeline(packageName);
                Debug.Log($"[BuildToolExecutor] 使用构建管线: {buildPipelineName}");

                // 在构建前检查并删除已存在的输出目录
                string buildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
                BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
                string packageOutputDirectory = $"{buildOutputRoot}/{buildTarget}/{packageName}/{version}";
                
                if (Directory.Exists(packageOutputDirectory))
                {
                    Debug.Log($"[BuildToolExecutor] 检测到已存在的输出目录，正在删除: {packageOutputDirectory}");
                    try
                    {
                        Directory.Delete(packageOutputDirectory, true);
                        Debug.Log($"[BuildToolExecutor] 已成功删除输出目录: {packageOutputDirectory}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[BuildToolExecutor] 删除输出目录失败: {packageOutputDirectory}, 错误: {ex.Message}");
                        // 继续构建，让 YooAsset 自己处理
                    }
                }

                // 根据构建管线名称创建对应的构建参数和执行构建
                BuildResult buildResult;
                if (buildPipelineName == EBuildPipeline.BuiltinBuildPipeline.ToString())
                {
                    buildResult = BuildWithBuiltinPipeline(packageName, version, buildPipelineName);
                }
                else if (buildPipelineName == EBuildPipeline.ScriptableBuildPipeline.ToString())
                {
                    buildResult = BuildWithScriptablePipeline(packageName, version, buildPipelineName);
                }
                else if (buildPipelineName == EBuildPipeline.RawFileBuildPipeline.ToString())
                {
                    buildResult = BuildWithRawFilePipeline(packageName, version, buildPipelineName);
                }
                else
                {
                    Debug.LogError($"[BuildToolExecutor] 不支持的构建管线: {buildPipelineName}");
                    return false;
                }

                if (buildResult.Success)
                {
                    Debug.Log($"[BuildToolExecutor] ✓ 包构建成功: {packageName}, 版本: {version}");
                    return true;
                }
                else
                {
                    Debug.LogError($"[BuildToolExecutor] ✗ 包构建失败: {packageName}, 错误: {buildResult.ErrorInfo}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildToolExecutor] 构建包时发生异常: {packageName}, 错误: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 使用 BuiltinBuildPipeline 构建。
        /// </summary>
        private static BuildResult BuildWithBuiltinPipeline(string packageName, string version, string pipelineName)
        {
            // 从配置窗口获取所有设置
            ECompressOption compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
            EFileNameStyle fileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName);
            EBuildinFileCopyOption buildinFileCopyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName);
            string buildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName);
            bool clearBuildCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
            bool useAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);

            // 创建构建参数
            BuiltinBuildParameters buildParameters = new BuiltinBuildParameters();
            buildParameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = pipelineName;
            buildParameters.BuildBundleType = (int)EBuildBundleType.AssetBundle;
            buildParameters.BuildTarget = EditorUserBuildSettings.activeBuildTarget;
            buildParameters.PackageName = packageName;
            buildParameters.PackageVersion = version; // 使用传入的版本号
            buildParameters.EnableSharePackRule = true;
            buildParameters.VerifyBuildingResult = true;
            buildParameters.FileNameStyle = fileNameStyle;
            buildParameters.BuildinFileCopyOption = buildinFileCopyOption;
            buildParameters.BuildinFileCopyParams = buildinFileCopyParams;
            buildParameters.CompressOption = compressOption;
            buildParameters.ClearBuildCacheFiles = clearBuildCache;
            buildParameters.UseAssetDependencyDB = useAssetDependencyDB;
            buildParameters.EncryptionServices = CreateEncryptionServicesInstance(packageName, pipelineName);
            buildParameters.ManifestProcessServices = CreateManifestProcessServicesInstance(packageName, pipelineName);
            buildParameters.ManifestRestoreServices = CreateManifestRestoreServicesInstance(packageName, pipelineName);

            // 执行构建
            BuiltinBuildPipeline pipeline = new BuiltinBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }

        /// <summary>
        /// 使用 ScriptableBuildPipeline 构建。
        /// </summary>
        private static BuildResult BuildWithScriptablePipeline(string packageName, string version, string pipelineName)
        {
            // 从配置窗口获取所有设置
            ECompressOption compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
            EFileNameStyle fileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName);
            EBuildinFileCopyOption buildinFileCopyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName);
            string buildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName);
            bool clearBuildCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
            bool useAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);

            // 创建构建参数
            ScriptableBuildParameters buildParameters = new ScriptableBuildParameters();
            buildParameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = pipelineName;
            buildParameters.BuildBundleType = (int)EBuildBundleType.AssetBundle;
            buildParameters.BuildTarget = EditorUserBuildSettings.activeBuildTarget;
            buildParameters.PackageName = packageName;
            buildParameters.PackageVersion = version; // 使用传入的版本号
            buildParameters.EnableSharePackRule = true;
            buildParameters.VerifyBuildingResult = true;
            buildParameters.FileNameStyle = fileNameStyle;
            buildParameters.BuildinFileCopyOption = buildinFileCopyOption;
            buildParameters.BuildinFileCopyParams = buildinFileCopyParams;
            buildParameters.CompressOption = compressOption;
            buildParameters.ClearBuildCacheFiles = clearBuildCache;
            buildParameters.UseAssetDependencyDB = useAssetDependencyDB;
            buildParameters.EncryptionServices = CreateEncryptionServicesInstance(packageName, pipelineName);
            buildParameters.ManifestProcessServices = CreateManifestProcessServicesInstance(packageName, pipelineName);
            buildParameters.ManifestRestoreServices = CreateManifestRestoreServicesInstance(packageName, pipelineName);

            // 执行构建
            ScriptableBuildPipeline pipeline = new ScriptableBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }

        /// <summary>
        /// 使用 RawFileBuildPipeline 构建。
        /// </summary>
        private static BuildResult BuildWithRawFilePipeline(string packageName, string version, string pipelineName)
        {
            // 从配置窗口获取所有设置
            EFileNameStyle fileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName);
            EBuildinFileCopyOption buildinFileCopyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName);
            string buildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName);
            bool clearBuildCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
            bool useAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);

            // 创建构建参数
            RawFileBuildParameters buildParameters = new RawFileBuildParameters();
            buildParameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = pipelineName;
            buildParameters.BuildBundleType = (int)EBuildBundleType.RawBundle;
            buildParameters.BuildTarget = EditorUserBuildSettings.activeBuildTarget;
            buildParameters.PackageName = packageName;
            buildParameters.PackageVersion = version; // 使用传入的版本号
            buildParameters.VerifyBuildingResult = true;
            buildParameters.FileNameStyle = fileNameStyle;
            buildParameters.BuildinFileCopyOption = buildinFileCopyOption;
            buildParameters.BuildinFileCopyParams = buildinFileCopyParams;
            buildParameters.ClearBuildCacheFiles = clearBuildCache;
            buildParameters.UseAssetDependencyDB = useAssetDependencyDB;
            buildParameters.EncryptionServices = CreateEncryptionServicesInstance(packageName, pipelineName);
            buildParameters.ManifestProcessServices = CreateManifestProcessServicesInstance(packageName, pipelineName);
            buildParameters.ManifestRestoreServices = CreateManifestRestoreServicesInstance(packageName, pipelineName);

            // 执行构建
            RawFileBuildPipeline pipeline = new RawFileBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }

        /// <summary>
        /// 创建资源包加密服务类实例。
        /// </summary>
        private static IEncryptionServices CreateEncryptionServicesInstance(string packageName, string pipelineName)
        {
            string className = AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(packageName, pipelineName);
            List<Type> classTypes = EditorTools.GetAssignableTypes(typeof(IEncryptionServices));
            Type classType = classTypes.Find(x => x.FullName.Equals(className));
            if (classType != null)
            {
                return (IEncryptionServices)Activator.CreateInstance(classType);
            }
            return null;
        }

        /// <summary>
        /// 创建资源清单加密服务类实例。
        /// </summary>
        private static IManifestProcessServices CreateManifestProcessServicesInstance(string packageName, string pipelineName)
        {
            string className = AssetBundleBuilderSetting.GetPackageManifestProcessServicesClassName(packageName, pipelineName);
            List<Type> classTypes = EditorTools.GetAssignableTypes(typeof(IManifestProcessServices));
            Type classType = classTypes.Find(x => x.FullName.Equals(className));
            if (classType != null)
            {
                return (IManifestProcessServices)Activator.CreateInstance(classType);
            }
            return null;
        }

        /// <summary>
        /// 创建资源清单解密服务类实例。
        /// </summary>
        private static IManifestRestoreServices CreateManifestRestoreServicesInstance(string packageName, string pipelineName)
        {
            string className = AssetBundleBuilderSetting.GetPackageManifestRestoreServicesClassName(packageName, pipelineName);
            List<Type> classTypes = EditorTools.GetAssignableTypes(typeof(IManifestRestoreServices));
            Type classType = classTypes.Find(x => x.FullName.Equals(className));
            if (classType != null)
            {
                return (IManifestRestoreServices)Activator.CreateInstance(classType);
            }
            return null;
        }

        /// <summary>
        /// 构建多个包（按顺序）。
        /// </summary>
        /// <param name="packageNames">包名称列表</param>
        /// <param name="version">版本号</param>
        /// <param name="isMainPackage">是否为母包</param>
        /// <returns>成功构建的包数量</returns>
        public static int BuildPackages(List<string> packageNames, string version, bool isMainPackage = false)
        {
            if (packageNames == null || packageNames.Count == 0)
            {
                Debug.LogWarning("[BuildToolExecutor] 包名称列表为空");
                return 0;
            }

            int successCount = 0;
            int totalCount = packageNames.Count;

            Debug.Log($"[BuildToolExecutor] 开始构建 {totalCount} 个包，版本: {version}");

            for (int i = 0; i < packageNames.Count; i++)
            {
                string packageName = packageNames[i];
                EditorUtility.DisplayProgressBar(
                    "构建包",
                    $"正在构建包 [{i + 1}/{totalCount}]: {packageName}",
                    (float)(i + 1) / totalCount
                );

                if (BuildPackage(packageName, version, isMainPackage))
                {
                    successCount++;
                }
                else
                {
                    Debug.LogError($"[BuildToolExecutor] 包构建失败，但继续构建下一个: {packageName}");
                    // 可以选择继续或停止
                }
            }

            EditorUtility.ClearProgressBar();
            Debug.Log($"[BuildToolExecutor] 构建完成: 成功 {successCount}/{totalCount}");

            return successCount;
        }
    }
}
#endif

