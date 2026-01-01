#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;

namespace Astorise.Editor.BBQ
{
    /// <summary>
    /// BBQ 热更新 DLL 自动复制工具。
    /// 监听 Assembly 编译完成事件，自动将编译后的 DLL 复制到 HotUpdate 目录并添加 .bytes 后缀。
    /// </summary>
    [InitializeOnLoad]
    public static class BBQHotUpdateDllCopier
    {
        private const string AssemblyName = "BBQHotUpdate";
        private const string HotUpdateDllsDir = "HybridCLRData/HotUpdateDlls";
        private const string TargetDir = "Assets/MiniGames/BBQ/HotUpdate";

        static BBQHotUpdateDllCopier()
        {
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] compilerMessages)
        {
            // 只处理 BBQHotUpdate Assembly
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (assemblyName != AssemblyName)
                return;

            // 检查编译是否有错误
            foreach (var message in compilerMessages)
            {
                if (message.type == CompilerMessageType.Error)
                {
                    UnityEngine.Debug.LogError($"[BBQHotUpdateDllCopier] Assembly {AssemblyName} 编译失败，跳过 DLL 复制");
                    return;
                }
            }

            // 等待一下，确保文件写入完成
            EditorApplication.delayCall += () =>
            {
                CopyDllToHotUpdateDir();
            };
        }

        private static void CopyDllToHotUpdateDir()
        {
            try
            {
                // 源文件路径：HybridCLRData/HotUpdateDlls/BBQHotUpdate.dll
                string sourceDllPath = Path.Combine(HotUpdateDllsDir, $"{AssemblyName}.dll");
                if (!File.Exists(sourceDllPath))
                {
                    UnityEngine.Debug.LogWarning($"[BBQHotUpdateDllCopier] 源 DLL 文件不存在: {sourceDllPath}");
                    return;
                }

                // 确保目标目录存在
                if (!Directory.Exists(TargetDir))
                {
                    Directory.CreateDirectory(TargetDir);
                }

                // 目标文件路径：Assets/MiniGames/BBQ/HotUpdate/BBQHotUpdate.dll.bytes
                string targetDllPath = Path.Combine(TargetDir, $"{AssemblyName}.dll.bytes");

                // 复制文件
                File.Copy(sourceDllPath, targetDllPath, true);
                UnityEngine.Debug.Log($"[BBQHotUpdateDllCopier] ✓ DLL 已复制: {sourceDllPath} -> {targetDllPath}");

                // 刷新 AssetDatabase
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[BBQHotUpdateDllCopier] DLL 复制失败: {ex.Message}");
            }
        }
    }
}
#endif

