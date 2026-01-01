using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEditor;
using YooAsset;
using Astorise.Framework.AOT;

namespace YooAsset.Editor
{
    /// <summary>
    /// 增量补丁包生成工具。
    /// </summary>
    public static class IncrementalPatchGenerator
    {
        /// <summary>
        /// 生成增量补丁包。
        /// </summary>
        /// <param name="gameName">游戏名称（如 "BBQ"）</param>
        /// <param name="rootDirectory">根目录（如 "C:\Work\platform\Bundles\StandaloneWindows64"）</param>
        /// <param name="baseVersion">母包版本（如 "2025-12-17-1167"）</param>
        /// <param name="currentVersion">当前版本（如 "2025-12-24-1139"）</param>
        public static void GenerateIncrementalPatch(string gameName, string rootDirectory, string baseVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(gameName))
            {
                Debug.LogError("[IncrementalPatchGenerator] gameName 不能为空");
                return;
            }

            if (string.IsNullOrEmpty(rootDirectory))
            {
                Debug.LogError("[IncrementalPatchGenerator] rootDirectory 不能为空");
                return;
            }

            if (string.IsNullOrEmpty(baseVersion))
            {
                Debug.LogError("[IncrementalPatchGenerator] baseVersion 不能为空");
                return;
            }

            if (string.IsNullOrEmpty(currentVersion))
            {
                Debug.LogError("[IncrementalPatchGenerator] currentVersion 不能为空");
                return;
            }

            if (!Directory.Exists(rootDirectory))
            {
                Debug.LogError($"[IncrementalPatchGenerator] 根目录不存在: {rootDirectory}");
                return;
            }

            Debug.Log($"[IncrementalPatchGenerator] 开始生成增量补丁: gameName={gameName}, rootDirectory={rootDirectory}, baseVersion={baseVersion}, currentVersion={currentVersion}");

            // 读取 GamePackageMapping.xml 配置
            GamePackageMappingXML packageMapping = LoadGamePackageMapping();
            if (packageMapping == null)
            {
                Debug.LogError("[IncrementalPatchGenerator] 加载 GamePackageMapping.xml 失败");
                return;
            }

            // 获取游戏的 SubPackage 列表
            string[] subPackages;
            try
            {
                subPackages = packageMapping.GetGamePackages(gameName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IncrementalPatchGenerator] 获取游戏 '{gameName}' 的包列表失败: {ex.Message}");
                return;
            }

            if (subPackages == null || subPackages.Length == 0)
            {
                Debug.LogWarning($"[IncrementalPatchGenerator] 游戏 '{gameName}' 没有配置任何 SubPackage");
                return;
            }

            Debug.Log($"[IncrementalPatchGenerator] 找到 {subPackages.Length} 个 SubPackage: {string.Join(", ", subPackages)}");

            // 创建 patch 目录
            string patchDirectory = Path.Combine(rootDirectory, "patch", currentVersion);
            try
            {
                Directory.CreateDirectory(patchDirectory);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IncrementalPatchGenerator] 创建 patch 目录失败: {patchDirectory}, 错误: {ex.Message}");
                return;
            }

            // 处理每个 SubPackage
            int totalChangedCount = 0;
            int totalNewCount = 0;

            foreach (string packageName in subPackages)
            {
                try
                {
                    ProcessPackage(packageName, rootDirectory, baseVersion, currentVersion, patchDirectory, out int changedCount, out int newCount);
                    totalChangedCount += changedCount;
                    totalNewCount += newCount;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IncrementalPatchGenerator] 处理包 '{packageName}' 时出错: {ex.Message}");
                }
            }

            Debug.Log($"[IncrementalPatchGenerator] 增量补丁生成完成！总变更数: {totalChangedCount}, 总新增数: {totalNewCount}, 输出目录: {patchDirectory}");
        }

        /// <summary>
        /// 加载 GamePackageMapping.xml 配置。
        /// </summary>
        private static GamePackageMappingXML LoadGamePackageMapping()
        {
            string configPath = "Assets/Framework/Config/GamePackageMapping.xml";
            string fullPath = Path.Combine(Application.dataPath, "..", configPath).Replace('\\', '/');
            fullPath = Path.GetFullPath(fullPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[IncrementalPatchGenerator] GamePackageMapping.xml 文件不存在: {fullPath}");
                return null;
            }

            try
            {
                XDocument doc = XDocument.Load(fullPath);
                GamePackageMappingXML packageMapping = new GamePackageMappingXML();
                packageMapping.Initialize(doc);
                return packageMapping;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IncrementalPatchGenerator] 加载 GamePackageMapping.xml 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 处理单个包。
        /// </summary>
        private static void ProcessPackage(string packageName, string rootDirectory, string baseVersion, string currentVersion, string patchDirectory, out int changedCount, out int newCount)
        {
            changedCount = 0;
            newCount = 0;

            Debug.Log($"[IncrementalPatchGenerator] 处理包: {packageName}");

            // 构建 manifest 文件路径
            string baseManifestPath = GetManifestPath(rootDirectory, packageName, baseVersion);
            string currentManifestPath = GetManifestPath(rootDirectory, packageName, currentVersion);

            // 检查文件是否存在
            if (!File.Exists(baseManifestPath))
            {
                Debug.LogWarning($"[IncrementalPatchGenerator] 母包 manifest 文件不存在: {baseManifestPath}");
                return;
            }

            if (!File.Exists(currentManifestPath))
            {
                Debug.LogWarning($"[IncrementalPatchGenerator] 当前版本 manifest 文件不存在: {currentManifestPath}");
                return;
            }

            // 加载 manifest 文件
            PackageManifest baseManifest = LoadManifest(baseManifestPath);
            PackageManifest currentManifest = LoadManifest(currentManifestPath);

            if (baseManifest == null || currentManifest == null)
            {
                Debug.LogError($"[IncrementalPatchGenerator] 加载 manifest 文件失败");
                return;
            }

            // 比较差异
            List<PackageBundle> changeList = new List<PackageBundle>();
            List<PackageBundle> newList = new List<PackageBundle>();
            CompareManifests(baseManifest, currentManifest, changeList, newList);

            changedCount = changeList.Count;
            newCount = newList.Count;

            Debug.Log($"[IncrementalPatchGenerator] 包 '{packageName}' 差异: 变更 {changedCount} 个, 新增 {newCount} 个");

            // 复制增量资源
            CopyIncrementalBundles(packageName, rootDirectory, currentVersion, patchDirectory, changeList, newList);
        }

        /// <summary>
        /// 获取 manifest 文件路径。
        /// </summary>
        private static string GetManifestPath(string rootDirectory, string packageName, string version)
        {
            // manifest 文件格式: {packageName}_{version}.bytes
            string fileName = $"{packageName}_{version}.bytes";
            // 假设 manifest 文件在根目录下的包名目录中
            string packageDirectory = Path.Combine(rootDirectory, packageName, version);
            return Path.Combine(packageDirectory, fileName);
        }

        /// <summary>
        /// 加载 manifest 文件。
        /// </summary>
        private static PackageManifest LoadManifest(string manifestPath)
        {
            try
            {
                byte[] bytesData = FileUtility.ReadAllBytes(manifestPath);
                if (bytesData == null)
                {
                    Debug.LogError($"[IncrementalPatchGenerator] 读取 manifest 文件失败: {manifestPath}");
                    return null;
                }

                PackageManifest manifest = ManifestTools.DeserializeFromBinary(bytesData, null);
                return manifest;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IncrementalPatchGenerator] 加载 manifest 文件失败: {manifestPath}, 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 比较两个 manifest，找出差异。
        /// </summary>
        private static void CompareManifests(PackageManifest baseManifest, PackageManifest currentManifest, List<PackageBundle> changeList, List<PackageBundle> newList)
        {
            changeList.Clear();
            newList.Clear();

            // 遍历当前版本的资源包
            foreach (PackageBundle bundle2 in currentManifest.BundleList)
            {
                if (baseManifest.TryGetPackageBundleByBundleName(bundle2.BundleName, out PackageBundle bundle1))
                {
                    // 如果文件哈希不同，说明资源包已变更
                    if (bundle2.FileHash != bundle1.FileHash)
                    {
                        changeList.Add(bundle2);
                    }
                }
                else
                {
                    // 如果母包中不存在，说明是新增的资源包
                    newList.Add(bundle2);
                }
            }

            // 按字母重新排序
            changeList.Sort((x, y) => string.Compare(x.BundleName, y.BundleName));
            newList.Sort((x, y) => string.Compare(x.BundleName, y.BundleName));
        }

        /// <summary>
        /// 复制增量资源包文件。
        /// </summary>
        private static void CopyIncrementalBundles(string packageName, string rootDirectory, string currentVersion, string patchDirectory, List<PackageBundle> changeList, List<PackageBundle> newList)
        {
            // 合并变更和新增列表
            List<PackageBundle> allBundles = new List<PackageBundle>();
            allBundles.AddRange(changeList);
            allBundles.AddRange(newList);

            if (allBundles.Count == 0)
            {
                Debug.Log($"[IncrementalPatchGenerator] 包 '{packageName}' 没有增量资源");
                return;
            }

            // 在 patch 目录下创建包目录
            string packagePatchDirectory = Path.Combine(patchDirectory, packageName);
            try
            {
                Directory.CreateDirectory(packagePatchDirectory);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IncrementalPatchGenerator] 创建包 patch 目录失败: {packagePatchDirectory}, 错误: {ex.Message}");
                return;
            }

            // 当前版本的包目录
            string currentPackageDirectory = Path.Combine(rootDirectory, packageName, currentVersion);

            // 复制每个资源包文件
            int copiedCount = 0;
            foreach (PackageBundle bundle in allBundles)
            {
                try
                {
                    // 资源包文件路径（使用 FileName 属性）
                    string sourceFilePath = Path.Combine(currentPackageDirectory, bundle.FileName);
                    string destFilePath = Path.Combine(packagePatchDirectory, bundle.FileName);

                    if (!File.Exists(sourceFilePath))
                    {
                        Debug.LogWarning($"[IncrementalPatchGenerator] 资源包文件不存在: {sourceFilePath}");
                        continue;
                    }

                    // 确保目标目录存在
                    string destDirectory = Path.GetDirectoryName(destFilePath);
                    if (!string.IsNullOrEmpty(destDirectory))
                    {
                        Directory.CreateDirectory(destDirectory);
                    }

                    // 复制文件
                    File.Copy(sourceFilePath, destFilePath, true);
                    copiedCount++;

#if UNITY_DEBUG
                    Debug.Log($"[IncrementalPatchGenerator] 复制资源包: {bundle.FileName} ({bundle.FileSize / 1024}K)");
#endif
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IncrementalPatchGenerator] 复制资源包文件失败: {bundle.FileName}, 错误: {ex.Message}");
                }
            }

            Debug.Log($"[IncrementalPatchGenerator] 包 '{packageName}' 复制完成: {copiedCount}/{allBundles.Count} 个文件");
        }
    }
}

