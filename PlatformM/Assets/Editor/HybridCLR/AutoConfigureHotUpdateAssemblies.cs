
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using HybridCLR.Editor.Settings;

namespace Astorise.Editor.HybridCLR
{
    /// <summary>
    /// 自动配置 HybridCLR 热更新程序集。
    /// 扫描所有小游戏的 Scripts 目录下的 Assembly Definition 文件，并自动配置到 HybridCLR 设置中。
    /// </summary>
    public static class AutoConfigureHotUpdateAssemblies
    {
        private const string MiniGamesRoot = "Assets/MiniGames";
        
        /// <summary>
        /// Common 部分的 Assembly 目录列表。
        /// 可以手动添加需要扫描的目录路径。
        /// </summary>
        private static readonly List<string> CommonAssemblyDirs = new List<string>
        {
            "Assets/Framework/Runtime",
            "Assets/Test/Example"
        };

        /// <summary>
        /// 自动配置热更新程序集。
        /// </summary>
        [MenuItem("Window/HybridCLR/Auto Configure HotUpdate Assemblies", priority = 300)]
        public static void Configure()
        {
            Debug.Log("[AutoConfigureHotUpdateAssemblies] === 开始自动配置 HybridCLR 热更新程序集 ===");

            try
            {
                // 1. 扫描所有小游戏的 Scripts 目录下的 .asmdef 文件
                List<AssemblyDefinitionAsset> foundAssemblies = ScanMiniGameAssemblies();
                
                // 2. 扫描 Common 部分的 Assembly Definition 文件
                List<AssemblyDefinitionAsset> commonAssemblies = ScanCommonAssemblies();
                foundAssemblies.AddRange(commonAssemblies);
                
                if (foundAssemblies.Count == 0)
                {
                    Debug.LogWarning("[AutoConfigureHotUpdateAssemblies] 未找到任何 Assembly Definition 文件");
                    return;
                }

                Debug.Log($"[AutoConfigureHotUpdateAssemblies] 找到 {foundAssemblies.Count} 个 Assembly Definition 文件:");
                foreach (var asm in foundAssemblies)
                {
                    Debug.Log($"  - {AssetDatabase.GetAssetPath(asm)}");
                }

                // 2. 读取 HybridCLR 设置
                HybridCLRSettings settings = HybridCLRSettings.Instance;
                if (settings == null)
                {
                    Debug.LogError("[AutoConfigureHotUpdateAssemblies] 无法获取 HybridCLR 设置实例");
                    return;
                }

                // 4. 获取现有的有效引用（用于日志显示）
                List<AssemblyDefinitionAsset> existingValidAssemblies = new List<AssemblyDefinitionAsset>();
                if (settings.hotUpdateAssemblyDefinitions != null)
                {
                    foreach (var existing in settings.hotUpdateAssemblyDefinitions)
                    {
                        if (existing != null)
                        {
                            existingValidAssemblies.Add(existing);
                        }
                    }
                }

                // 5. 构建需要保留的 assembly 集合（去重）
                HashSet<AssemblyDefinitionAsset> foundSet = new HashSet<AssemblyDefinitionAsset>(foundAssemblies);
                List<AssemblyDefinitionAsset> finalAssemblies = new List<AssemblyDefinitionAsset>(foundSet);

                // 6. 检查是否有被移除的 assembly
                List<AssemblyDefinitionAsset> removedAssemblies = new List<AssemblyDefinitionAsset>();
                foreach (var existing in existingValidAssemblies)
                {
                    if (!foundSet.Contains(existing))
                    {
                        removedAssemblies.Add(existing);
                    }
                }

                // 7. 更新设置（直接替换，不清除不在扫描范围内的）
                settings.hotUpdateAssemblyDefinitions = finalAssemblies.ToArray();
                
                // 使用 HybridCLRSettings.Save() 方法保存设置
                HybridCLRSettings.Save();
                
                // 刷新 AssetDatabase 确保 Unity 识别更改00000000
                AssetDatabase.Refresh();

                Debug.Log($"[AutoConfigureHotUpdateAssemblies] ✓ 配置完成！共配置 {finalAssemblies.Count} 个热更新程序集");
                Debug.Log($"[AutoConfigureHotUpdateAssemblies]   原有引用: {existingValidAssemblies.Count} 个");
                Debug.Log($"[AutoConfigureHotUpdateAssemblies]   新添加: {finalAssemblies.Count - (existingValidAssemblies.Count - removedAssemblies.Count)} 个");
                
                if (removedAssemblies.Count > 0)
                {
                    Debug.Log($"[AutoConfigureHotUpdateAssemblies]   已移除: {removedAssemblies.Count} 个不在扫描范围内的程序集:");
                    foreach (var removed in removedAssemblies)
                    {
                        string path = AssetDatabase.GetAssetPath(removed);
                        Debug.Log($"      - {path}");
                    }
                }
                
                // 打印所有配置的 assembly 路径
                Debug.Log("[AutoConfigureHotUpdateAssemblies] 已配置的热更新程序集列表:");
                for (int i = 0; i < finalAssemblies.Count; i++)
                {
                    string path = AssetDatabase.GetAssetPath(finalAssemblies[i]);
                    Debug.Log($"  [{i}] {path}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AutoConfigureHotUpdateAssemblies] ✗ 配置失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 扫描 MiniGamesRoot 目录下所有的 Assembly Definition 文件。
        /// 递归扫描所有子目录，查找所有 .asmdef 文件。
        /// </summary>
        private static List<AssemblyDefinitionAsset> ScanMiniGameAssemblies()
        {
            List<AssemblyDefinitionAsset> assemblies = new List<AssemblyDefinitionAsset>();

            if (!Directory.Exists(MiniGamesRoot))
            {
                Debug.LogWarning($"[AutoConfigureHotUpdateAssemblies] 小游戏根目录不存在: {MiniGamesRoot}");
                return assemblies;
            }

            // 递归查找 MiniGamesRoot 目录下所有的 .asmdef 文件
            string[] asmdefFiles = Directory.GetFiles(MiniGamesRoot, "*.asmdef", SearchOption.AllDirectories);
            foreach (string asmdefFile in asmdefFiles)
            {
                string assetPath = asmdefFile.Replace('\\', '/');
                if (assetPath.StartsWith("Assets/"))
                {
                    assetPath = assetPath.Substring(assetPath.IndexOf("Assets/"));
                }
                else
                {
                    // 转换为相对路径
                    string projectPath = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
                    if (asmdefFile.StartsWith(projectPath))
                    {
                        assetPath = "Assets" + asmdefFile.Substring(projectPath.Length).Replace('\\', '/');
                    }
                    else
                    {
                        continue;
                    }
                }

                AssemblyDefinitionAsset asmAsset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assetPath);
                if (asmAsset != null)
                {
                    assemblies.Add(asmAsset);
                }
                else
                {
                    Debug.LogWarning($"[AutoConfigureHotUpdateAssemblies] 无法加载 Assembly Definition: {assetPath}");
                }
            }

            return assemblies;
        }

        /// <summary>
        /// 扫描 Common 部分的 Assembly Definition 文件。
        /// </summary>
        private static List<AssemblyDefinitionAsset> ScanCommonAssemblies()
        {
            List<AssemblyDefinitionAsset> assemblies = new List<AssemblyDefinitionAsset>();

            foreach (string commonDir in CommonAssemblyDirs)
            {
                if (!Directory.Exists(commonDir))
                {
                    Debug.LogWarning($"[AutoConfigureHotUpdateAssemblies] Common 目录不存在: {commonDir}");
                    continue;
                }

                // 递归查找所有 .asmdef 文件
                string[] asmdefFiles = Directory.GetFiles(commonDir, "*.asmdef", SearchOption.AllDirectories);
                foreach (string asmdefFile in asmdefFiles)
                {
                    string assetPath = asmdefFile.Replace('\\', '/');
                    if (assetPath.StartsWith("Assets/"))
                    {
                        assetPath = assetPath.Substring(assetPath.IndexOf("Assets/"));
                    }
                    else
                    {
                        // 转换为相对路径
                        string projectPath = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
                        if (asmdefFile.StartsWith(projectPath))
                        {
                            assetPath = "Assets" + asmdefFile.Substring(projectPath.Length).Replace('\\', '/');
                        }
                        else
                        {
                            continue;
                        }
                    }

                    AssemblyDefinitionAsset asmAsset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assetPath);
                    if (asmAsset != null)
                    {
                        assemblies.Add(asmAsset);
                    }
                    else
                    {
                        Debug.LogWarning($"[AutoConfigureHotUpdateAssemblies] 无法加载 Assembly Definition: {assetPath}");
                    }
                }
            }

            return assemblies;
        }

        /// <summary>
        /// 在 Assembly 编译完成后自动执行配置（可选）。
        /// </summary>
        [InitializeOnLoad]
        private static class AutoConfigureOnCompile
        {
            static AutoConfigureOnCompile()
            {
                CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            }

            private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] compilerMessages)
            {
                // 检查是否有错误
                bool hasError = false;
                foreach (var message in compilerMessages)
                {
                    if (message.type == CompilerMessageType.Error)
                    {
                        hasError = true;
                        break;
                    }
                }

                if (hasError)
                {
                    return;
                }

                // 检查是否是 MiniGames 目录下的 Assembly
                string normalizedPath = assemblyPath.Replace('\\', '/');
                if (normalizedPath.Contains("MiniGames/"))
                {
                    // 延迟执行，避免在编译过程中修改设置
                    EditorApplication.delayCall += () =>
                    {
                        Debug.Log("[AutoConfigureHotUpdateAssemblies] 检测到小游戏 Assembly 编译完成，自动更新配置");
                        Configure();
                    };
                }
            }
        }
    }
}

