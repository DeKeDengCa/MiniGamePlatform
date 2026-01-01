#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Astorise.Editor.HybridCLR
{
    /// <summary>
    /// HybridCLR DLL 拷贝工具类。
    /// </summary>
    public static class HybridCLRDllCopyUtils
    {
        /// <summary>
        /// 将 BuildTarget 转换为平台目录名称。
        /// </summary>
        public static string GetPlatformDirectoryName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                    return "StandaloneWindows64";
                case BuildTarget.StandaloneWindows:
                    return "StandaloneWindows";
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.StandaloneOSX:
                    return "StandaloneOSX";
                case BuildTarget.WebGL:
                    return "WebGL";
                default:
                    return target.ToString();
            }
        }

        /// <summary>
        /// 确保目录存在，如果不存在则创建。
        /// </summary>
        public static void EnsureDirectoryExists(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                throw new ArgumentException("路径不能为空", nameof(relativePath));
            }

            // 如果路径以 Assets/ 开头，需要转换为绝对路径
            string fullPath;
            if (relativePath.StartsWith("Assets/"))
            {
                fullPath = Path.Combine(Application.dataPath, "..", relativePath).Replace('\\', '/');
            }
            else
            {
                fullPath = Path.Combine(Application.dataPath, "..", relativePath).Replace('\\', '/');
            }
            fullPath = Path.GetFullPath(fullPath);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                Debug.Log($"[HybridCLRDllCopyUtils] 创建目录: {fullPath}");
            }
        }

        /// <summary>
        /// 拷贝 DLL 文件并添加 .bytes 扩展名。
        /// </summary>
        public static bool CopyDllWithBytesExtension(string sourcePath, string targetPath)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    Debug.LogWarning($"[HybridCLRDllCopyUtils] 源文件不存在: {sourcePath}");
                    return false;
                }

                string targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    EnsureDirectoryExists(targetDir);
                }

                File.Copy(sourcePath, targetPath, true);
                Debug.Log($"[HybridCLRDllCopyUtils] ✓ 拷贝成功: {Path.GetFileName(sourcePath)} -> {Path.GetFileName(targetPath)}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HybridCLRDllCopyUtils] ✗ 拷贝失败: {sourcePath} -> {targetPath}, 错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 拷贝 AOT Metadata DLL。
        /// </summary>
        public static void CopyAOTMetadataDlls(HybridCLRDllCopySettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("[HybridCLRDllCopyUtils] 配置为空，无法执行 AOT Metadata DLL 拷贝");
                return;
            }

            Debug.Log("[HybridCLRDllCopyUtils] === 开始拷贝 AOT Metadata DLL ===");

            // 读取 AOTGenericReferences.PatchedAOTAssemblyList
            // 注意：AOTGenericReferences 在 Assembly-CSharp 中，Editor 脚本需要使用反射访问
            System.Collections.Generic.IReadOnlyList<string> aotAssemblyList = null;
            
            try
            {
                var aotAssemblyListType = System.Type.GetType("AOTGenericReferences, Assembly-CSharp");
                if (aotAssemblyListType == null)
                {
                    // 尝试从所有已加载的程序集中查找
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        aotAssemblyListType = assembly.GetType("AOTGenericReferences");
                        if (aotAssemblyListType != null)
                        {
                            break;
                        }
                    }
                }

                if (aotAssemblyListType == null)
                {
                    Debug.LogError("[HybridCLRDllCopyUtils] 无法找到 AOTGenericReferences 类型");
                    return;
                }

                var patchedAOTAssemblyListField = aotAssemblyListType.GetField("PatchedAOTAssemblyList", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                if (patchedAOTAssemblyListField == null)
                {
                    Debug.LogError("[HybridCLRDllCopyUtils] 无法找到 PatchedAOTAssemblyList 字段");
                    return;
                }

                aotAssemblyList = patchedAOTAssemblyListField.GetValue(null) as System.Collections.Generic.IReadOnlyList<string>;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HybridCLRDllCopyUtils] 读取 AOTGenericReferences.PatchedAOTAssemblyList 失败: {ex.Message}");
                return;
            }

            if (aotAssemblyList == null || aotAssemblyList.Count == 0)
            {
                Debug.LogWarning("[HybridCLRDllCopyUtils] AOTGenericReferences.PatchedAOTAssemblyList 为空");
                return;
            }

            // 获取当前平台
            BuildTarget currentPlatform = EditorUserBuildSettings.activeBuildTarget;
            string platformName = GetPlatformDirectoryName(currentPlatform);
            Debug.Log($"[HybridCLRDllCopyUtils] 当前平台: {currentPlatform} -> {platformName}");

            // 构建源目录路径
            string sourceBaseDir = Path.Combine(Application.dataPath, "..", settings.aotMetadataSourceDir).Replace('\\', '/');
            sourceBaseDir = Path.GetFullPath(sourceBaseDir);
            string sourcePlatformDir = Path.Combine(sourceBaseDir, platformName).Replace('\\', '/');

            if (!Directory.Exists(sourcePlatformDir))
            {
                Debug.LogError($"[HybridCLRDllCopyUtils] 源平台目录不存在: {sourcePlatformDir}");
                return;
            }

            // 构建目标目录路径
            string targetDir = Path.Combine(Application.dataPath, "..", settings.aotMetadataTargetDir).Replace('\\', '/');
            targetDir = Path.GetFullPath(targetDir);
            EnsureDirectoryExists(settings.aotMetadataTargetDir);

            int successCount = 0;
            int failCount = 0;

            // 遍历 DLL 列表
            foreach (string dllName in aotAssemblyList)
            {
                // 移除 .dll 扩展名（如果存在）
                string dllNameWithoutExt = dllName;
                if (dllNameWithoutExt.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    dllNameWithoutExt = dllNameWithoutExt.Substring(0, dllNameWithoutExt.Length - 4);
                }

                string sourcePath = Path.Combine(sourcePlatformDir, $"{dllNameWithoutExt}.dll").Replace('\\', '/');
                string targetPath = Path.Combine(targetDir, $"{dllNameWithoutExt}.dll.bytes").Replace('\\', '/');

                if (CopyDllWithBytesExtension(sourcePath, targetPath))
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[HybridCLRDllCopyUtils] === AOT Metadata DLL 拷贝完成: 成功 {successCount} 个, 失败 {failCount} 个 ===");
        }

        /// <summary>
        /// 从 DLL 名称推断游戏名称。
        /// 例如：BBQHotUpdate -> BBQ, PinBallHotUpdate -> PinBall
        /// </summary>
        private static string ExtractGameNameFromDllName(string dllName)
        {
            if (string.IsNullOrEmpty(dllName))
                return null;

            // 移除 .dll 扩展名（如果存在）
            string nameWithoutExt = dllName;
            if (nameWithoutExt.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                nameWithoutExt = nameWithoutExt.Substring(0, nameWithoutExt.Length - 4);
            }

            // 如果以 "HotUpdate" 结尾，则去掉该后缀
            const string hotUpdateSuffix = "HotUpdate";
            if (nameWithoutExt.EndsWith(hotUpdateSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return nameWithoutExt.Substring(0, nameWithoutExt.Length - hotUpdateSuffix.Length);
            }

            // 如果无法推断，返回原名称（去掉扩展名）
            return nameWithoutExt;
        }

        /// <summary>
        /// 拷贝 HotUpdate DLL。
        /// </summary>
        public static void CopyHotUpdateDlls(HybridCLRDllCopySettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("[HybridCLRDllCopyUtils] 配置为空，无法执行 HotUpdate DLL 拷贝");
                return;
            }

            // 检查是否有任何 DLL 需要拷贝
            bool hasFrameworkDlls = settings.frameworkDllNames != null && settings.frameworkDllNames.Count > 0;
            bool hasMiniGameDlls = settings.miniGameDllNames != null && settings.miniGameDllNames.Count > 0;

            if (!hasFrameworkDlls && !hasMiniGameDlls)
            {
                Debug.LogWarning("[HybridCLRDllCopyUtils] Framework 和小游戏 DLL 名称列表都为空");
                return;
            }

            Debug.Log("[HybridCLRDllCopyUtils] === 开始拷贝 HotUpdate DLL ===");

            // 获取当前平台
            BuildTarget currentPlatform = EditorUserBuildSettings.activeBuildTarget;
            string platformName = GetPlatformDirectoryName(currentPlatform);
            Debug.Log($"[HybridCLRDllCopyUtils] 当前平台: {currentPlatform} -> {platformName}");

            // 构建源目录路径
            string sourceBaseDir = Path.Combine(Application.dataPath, "..", settings.hotUpdateSourceDir).Replace('\\', '/');
            sourceBaseDir = Path.GetFullPath(sourceBaseDir);
            string sourcePlatformDir = Path.Combine(sourceBaseDir, platformName).Replace('\\', '/');

            if (!Directory.Exists(sourcePlatformDir))
            {
                Debug.LogError($"[HybridCLRDllCopyUtils] 源平台目录不存在: {sourcePlatformDir}");
                return;
            }

            int totalSuccessCount = 0;
            int totalFailCount = 0;

            // 1. 拷贝 Framework DLL 到 Framework 目录
            if (hasFrameworkDlls)
            {
                Debug.Log("[HybridCLRDllCopyUtils] --- 开始拷贝 Framework DLL ---");
                string frameworkTargetDir = Path.Combine(Application.dataPath, "..", settings.hotUpdateTargetDir).Replace('\\', '/');
                frameworkTargetDir = Path.GetFullPath(frameworkTargetDir);
                EnsureDirectoryExists(settings.hotUpdateTargetDir);

                int frameworkSuccessCount = 0;
                int frameworkFailCount = 0;

                foreach (string dllName in settings.frameworkDllNames)
                {
                    if (string.IsNullOrEmpty(dllName))
                        continue;

                    // 移除 .dll 扩展名（如果存在）
                    string dllNameWithoutExt = dllName;
                    if (dllNameWithoutExt.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        dllNameWithoutExt = dllNameWithoutExt.Substring(0, dllNameWithoutExt.Length - 4);
                    }

                    string sourcePath = Path.Combine(sourcePlatformDir, $"{dllNameWithoutExt}.dll").Replace('\\', '/');
                    string targetPath = Path.Combine(frameworkTargetDir, $"{dllNameWithoutExt}.dll.bytes").Replace('\\', '/');

                    if (CopyDllWithBytesExtension(sourcePath, targetPath))
                    {
                        frameworkSuccessCount++;
                        totalSuccessCount++;
                    }
                    else
                    {
                        frameworkFailCount++;
                        totalFailCount++;
                    }
                }

                Debug.Log($"[HybridCLRDllCopyUtils] Framework DLL 拷贝完成: 成功 {frameworkSuccessCount} 个, 失败 {frameworkFailCount} 个");
            }

            // 2. 拷贝小游戏 DLL 到各自游戏目录
            if (hasMiniGameDlls)
            {
                Debug.Log("[HybridCLRDllCopyUtils] --- 开始拷贝小游戏 DLL ---");
                int miniGameSuccessCount = 0;
                int miniGameFailCount = 0;

                foreach (string dllName in settings.miniGameDllNames)
                {
                    if (string.IsNullOrEmpty(dllName))
                        continue;

                    // 从 DLL 名称推断游戏名称
                    string gameName = ExtractGameNameFromDllName(dllName);
                    if (string.IsNullOrEmpty(gameName))
                    {
                        Debug.LogWarning($"[HybridCLRDllCopyUtils] 无法从 DLL 名称推断游戏名称: {dllName}");
                        miniGameFailCount++;
                        totalFailCount++;
                        continue;
                    }

                    // 构建小游戏目标目录：Assets/MiniGames/{GameName}/HotUpdate
                    string miniGameTargetDir = $"Assets/MiniGames/{gameName}/HotUpdate";
                    string miniGameTargetFullPath = Path.Combine(Application.dataPath, "..", miniGameTargetDir).Replace('\\', '/');
                    miniGameTargetFullPath = Path.GetFullPath(miniGameTargetFullPath);
                    EnsureDirectoryExists(miniGameTargetDir);

                    // 移除 .dll 扩展名（如果存在）
                    string dllNameWithoutExt = dllName;
                    if (dllNameWithoutExt.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        dllNameWithoutExt = dllNameWithoutExt.Substring(0, dllNameWithoutExt.Length - 4);
                    }

                    string sourcePath = Path.Combine(sourcePlatformDir, $"{dllNameWithoutExt}.dll").Replace('\\', '/');
                    string targetPath = Path.Combine(miniGameTargetFullPath, $"{dllNameWithoutExt}.dll.bytes").Replace('\\', '/');

                    if (CopyDllWithBytesExtension(sourcePath, targetPath))
                    {
                        miniGameSuccessCount++;
                        totalSuccessCount++;
                    }
                    else
                    {
                        miniGameFailCount++;
                        totalFailCount++;
                    }
                }

                Debug.Log($"[HybridCLRDllCopyUtils] 小游戏 DLL 拷贝完成: 成功 {miniGameSuccessCount} 个, 失败 {miniGameFailCount} 个");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[HybridCLRDllCopyUtils] === HotUpdate DLL 拷贝完成: 总计成功 {totalSuccessCount} 个, 失败 {totalFailCount} 个 ===");
        }

        /// <summary>
        /// 读取 AOTGenericReferences.PatchedAOTAssemblyList（通过反射）。
        /// </summary>
        /// <returns>程序集列表，如果读取失败则返回 null</returns>
        private static IReadOnlyList<string> ReadAOTGenericReferencesAssemblyList()
        {
            try
            {
                System.Type aotAssemblyListType = System.Type.GetType("AOTGenericReferences, Assembly-CSharp");
                if (aotAssemblyListType == null)
                {
                    // 尝试从所有已加载的程序集中查找
                    foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        aotAssemblyListType = assembly.GetType("AOTGenericReferences");
                        if (aotAssemblyListType != null)
                        {
                            break;
                        }
                    }
                }

                if (aotAssemblyListType == null)
                {
                    Debug.LogWarning("[HybridCLRDllCopyUtils] 无法找到 AOTGenericReferences 类型，可能尚未生成");
                    return null;
                }

                System.Reflection.FieldInfo patchedAOTAssemblyListField = aotAssemblyListType.GetField("PatchedAOTAssemblyList", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                if (patchedAOTAssemblyListField == null)
                {
                    Debug.LogError("[HybridCLRDllCopyUtils] 无法找到 PatchedAOTAssemblyList 字段");
                    return null;
                }

                IReadOnlyList<string> aotAssemblyList = patchedAOTAssemblyListField.GetValue(null) as IReadOnlyList<string>;
                return aotAssemblyList;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HybridCLRDllCopyUtils] 读取 AOTGenericReferences.PatchedAOTAssemblyList 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 生成 PatchAOTAssemblies.xml 文件。
        /// </summary>
        /// <param name="assemblyList">程序集列表</param>
        private static void GeneratePatchAOTAssembliesXml(IReadOnlyList<string> assemblyList)
        {
            if (assemblyList == null || assemblyList.Count == 0)
            {
                Debug.LogWarning("[HybridCLRDllCopyUtils] 程序集列表为空，跳过 PatchAOTAssemblies.xml 生成");
                return;
            }

            try
            {
                // 创建 XML 文档
                XDocument doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("PatchAOTAssemblies",
                        assemblyList.Select(assembly => new XElement("Assembly", new XAttribute("Name", assembly)))
                    )
                );

                // 保存到文件
                string xmlPath = Path.Combine(Application.dataPath, "Framework", "Config", "PatchAOTAssemblies.xml");
                string xmlDir = Path.GetDirectoryName(xmlPath);
                if (!Directory.Exists(xmlDir))
                {
                    Directory.CreateDirectory(xmlDir);
                }

                doc.Save(xmlPath);
                AssetDatabase.Refresh();

                Debug.Log($"[HybridCLRDllCopyUtils] ✓ PatchAOTAssemblies.xml 生成成功: {assemblyList.Count} 个程序集");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HybridCLRDllCopyUtils] 生成 PatchAOTAssemblies.xml 失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 生成所有 DLL（AOT Metadata + HotUpdate）。
        /// </summary>
        [MenuItem("Window/HybridCLR/Generate All", priority = 301)]
        public static void GenerateAll()
        {
            try
            {
                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "正在自动配置热更新程序集...", 0.0f);

                // 第一步：自动配置热更新程序集
                Debug.Log("[HybridCLRDllCopyUtils] === 步骤 1: 自动配置热更新程序集 ===");
                AutoConfigureHotUpdateAssemblies.Configure();

                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "正在生成所有 DLL...", 0.1f);

                // 第二步：调用 HybridCLR 的标准生成流程（生成 AOTGenericReferences.cs）
                global::HybridCLR.Editor.Commands.PrebuildCommand.GenerateAll();

                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "正在生成 PatchAOTAssemblies.xml...", 0.3f);

                // 读取 AOTGenericReferences.PatchedAOTAssemblyList 并生成 XML
                IReadOnlyList<string> assemblyList = ReadAOTGenericReferencesAssemblyList();
                if (assemblyList != null && assemblyList.Count > 0)
                {
                    GeneratePatchAOTAssembliesXml(assemblyList);
                }

                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "正在拷贝 AOT Metadata DLL...", 0.4f);

                HybridCLRDllCopySettings settings = HybridCLRDllCopySettings.GetOrCreateSettings();
                CopyAOTMetadataDlls(settings);
                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "正在生成 HotUpdate DLL...", 0.8f);
                CopyHotUpdateDlls(settings);

                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "完成", 1.0f);
                EditorUtility.ClearProgressBar();
                Debug.Log("[HybridCLRDllCopyUtils] === 所有 DLL 生成完成 ===");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[HybridCLRDllCopyUtils] 生成失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 只生成 AOT Metadata DLL。
        /// </summary>
        [MenuItem("Window/HybridCLR/Generate AOTMetadata", priority = 302)]
        public static void GenerateAOTMetadata()
        {
            try
            {
                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "正在生成 AOT Metadata DLL...", 0.0f);

                var settings = HybridCLRDllCopySettings.GetOrCreateSettings();
                CopyAOTMetadataDlls(settings);

                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "完成", 1.0f);
                EditorUtility.ClearProgressBar();
                Debug.Log("[HybridCLRDllCopyUtils] === AOT Metadata DLL 生成完成 ===");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[HybridCLRDllCopyUtils] 生成失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 只生成 HotUpdate DLL。
        /// </summary>
        [MenuItem("Window/HybridCLR/Generate HotUpdate", priority = 303)]
        public static void GenerateHotUpdate()
        {
            try
            {
                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "正在生成 HotUpdate DLL...", 0.0f);

                var settings = HybridCLRDllCopySettings.GetOrCreateSettings();
                CopyHotUpdateDlls(settings);

                EditorUtility.DisplayProgressBar("HybridCLR DLL Copy", "完成", 1.0f);
                EditorUtility.ClearProgressBar();
                Debug.Log("[HybridCLRDllCopyUtils] === HotUpdate DLL 生成完成 ===");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[HybridCLRDllCopyUtils] 生成失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
#endif

