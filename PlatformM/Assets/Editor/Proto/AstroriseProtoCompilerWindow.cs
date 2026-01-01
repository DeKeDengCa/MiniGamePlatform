#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Astorise.Editor.Proto
{
    /// <summary>
    /// Astrorise Proto 编译工具窗口。
    /// </summary>
    public class AstroriseProtoCompilerWindow : EditorWindow
    {
        private const string ProtocolSourceDir = "Proto/Protocol";
        private const string ProtocolTargetDir = "Assets/AOT/Runtime/Proto/Protocol";
        private const string ProtocolAsmdefPath = ProtocolTargetDir + "/Protocol.asmdef";
        private const string AstroriseSourceDir = "Proto/AstrorisePlatform/shared/message/proto";
        private const string AstroriseTargetDir = "Assets/Framework/Runtime/Proto/AstrorisePlatform";
        private const string AstroriseAsmdefPath = AstroriseTargetDir + "/AstrorisePlatform.asmdef";
        private const string ProtocCommand = "protoc";
        private const string GoogleProtobufDllPath = "Plugins/ProtoBuf/Google.Protobuf.dll";
        private static readonly string[] ExcludeDirs = { "google/protobuf", "mgr/platform/voiceroom" };

        /// <summary>
        /// 打开窗口。
        /// </summary>
        [MenuItem("Tools/Proto编译协议", priority = 101)]
        public static void ShowWindow()
        {
            AstroriseProtoCompilerWindow window = GetWindow<AstroriseProtoCompilerWindow>("Astrorise Proto 编译工具");
            window.minSize = new Vector2(500, 250);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Protocol 依赖库", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("源目录:", GUILayout.Width(100));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(ProtocolSourceDir);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标目录:", GUILayout.Width(100));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(ProtocolTargetDir);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("AstrorisePlatform 协议", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("源目录:", GUILayout.Width(100));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(AstroriseSourceDir);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标目录:", GUILayout.Width(100));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(AstroriseTargetDir);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("编译协议", GUILayout.Height(35)))
            {
                CompileProtos();
            }
        }

        /// <summary>
        /// 编译协议。
        /// </summary>
        private void CompileProtos()
        {
            Debug.Log("[AstroriseProtoCompiler] ===== 开始编译协议 =====");
            string projectRoot = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            Debug.Log($"[AstroriseProtoCompiler] 项目根目录: {projectRoot}");

            if (!CheckProtocAvailable())
            {
                EditorUtility.DisplayDialog("错误", "protoc 未找到，请确保已安装 Protocol Buffers 编译器并添加到系统 PATH", "确定");
                Debug.LogError("[AstroriseProtoCompiler] protoc 未找到");
                return;
            }

            try
            {
                // 禁用自动刷新，避免在文件操作过程中触发刷新导致集合修改错误
                AssetDatabase.DisallowAutoRefresh();
                
                EditorUtility.DisplayProgressBar("编译协议", "准备编译...", 0f);

                bool protocolSuccess = CompileProtocolLibrary(projectRoot);
                if (!protocolSuccess)
                {
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.AllowAutoRefresh();
                    EditorUtility.DisplayDialog("失败", "Protocol 依赖库编译失败，请查看 Console 日志", "确定");
                    return;
                }

                bool astroriseSuccess = CompileAstrorisePlatform(projectRoot);
                if (!astroriseSuccess)
                {
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.AllowAutoRefresh();
                    EditorUtility.DisplayDialog("失败", "AstrorisePlatform 协议编译失败，请查看 Console 日志", "确定");
                    return;
                }

                EditorUtility.DisplayProgressBar("编译协议", "生成程序集定义...", 0.9f);
                GenerateAssemblyDefinitions(projectRoot);

                EditorUtility.ClearProgressBar();
                
                // 等待文件系统操作完成
                System.Threading.Thread.Sleep(200);
                
                // 重新允许自动刷新并手动刷新
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("完成", "协议编译完成", "确定");
                Debug.Log("[AstroriseProtoCompiler] 协议编译完成");
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                // 确保在异常情况下也恢复自动刷新
                AssetDatabase.AllowAutoRefresh();
                EditorUtility.DisplayDialog("错误", $"编译失败：{ex.Message}", "确定");
                Debug.LogError($"[AstroriseProtoCompiler] 编译失败：{ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 编译 Protocol 依赖库。
        /// </summary>
        private bool CompileProtocolLibrary(string projectRoot)
        {
            Debug.Log("[AstroriseProtoCompiler] 开始编译 Protocol 依赖库");
            EditorUtility.DisplayProgressBar("编译协议", "编译 Protocol 依赖库...", 0.3f);

            string sourceDir = Path.Combine(projectRoot, ProtocolSourceDir).Replace('\\', '/');
            string targetDir = Path.Combine(projectRoot, ProtocolTargetDir).Replace('\\', '/');
            Debug.Log($"[AstroriseProtoCompiler] Protocol 源目录: {sourceDir}");
            Debug.Log($"[AstroriseProtoCompiler] Protocol 目标目录: {targetDir}");

            if (!Directory.Exists(sourceDir))
            {
                Debug.LogError($"[AstroriseProtoCompiler] Protocol 源目录不存在：{sourceDir}");
                return false;
            }

            ClearOutputDirectory(targetDir);
            Directory.CreateDirectory(targetDir);

            List<string> protoFiles = ScanProtoFiles(sourceDir, ExcludeDirs);
            if (protoFiles.Count == 0)
            {
                Debug.LogWarning("[AstroriseProtoCompiler] Protocol 目录未找到 proto 文件");
                return true;
            }

            Debug.Log($"[AstroriseProtoCompiler] 扫描到 {protoFiles.Count} 个 Protocol proto 文件");
            bool success = ExecuteProtocBatch(protoFiles, new[] { sourceDir }, targetDir);
            if (!success)
            {
                return false;
            }

            return GenerateRpcRegistryOrFail(targetDir);
        }

        /// <summary>
        /// 编译 AstrorisePlatform 协议。
        /// </summary>
        private bool CompileAstrorisePlatform(string projectRoot)
        {
            Debug.Log("[AstroriseProtoCompiler] 开始编译 AstrorisePlatform 协议");
            EditorUtility.DisplayProgressBar("编译协议", "编译 AstrorisePlatform 协议...", 0.7f);

            string protocolDir = Path.Combine(projectRoot, ProtocolSourceDir).Replace('\\', '/');
            string sourceDir = Path.Combine(projectRoot, AstroriseSourceDir).Replace('\\', '/');
            string targetDir = Path.Combine(projectRoot, AstroriseTargetDir).Replace('\\', '/');
            Debug.Log($"[AstroriseProtoCompiler] AstrorisePlatform 源目录: {sourceDir}");
            Debug.Log($"[AstroriseProtoCompiler] AstrorisePlatform 目标目录: {targetDir}");

            if (!Directory.Exists(sourceDir))
            {
                Debug.LogError($"[AstroriseProtoCompiler] AstrorisePlatform 源目录不存在：{sourceDir}");
                return false;
            }

            ClearOutputDirectory(targetDir);
            Directory.CreateDirectory(targetDir);

            List<string> protoFiles = ScanProtoFiles(sourceDir, null);
            if (protoFiles.Count == 0)
            {
                Debug.LogWarning("[AstroriseProtoCompiler] AstrorisePlatform 目录未找到 proto 文件");
                return true;
            }

            Debug.Log($"[AstroriseProtoCompiler] 扫描到 {protoFiles.Count} 个 AstrorisePlatform proto 文件");
            bool success = ExecuteProtocBatch(protoFiles, new[] { protocolDir, sourceDir }, targetDir);
            if (!success)
            {
                return false;
            }

            return GenerateRpcRegistryOrFail(targetDir);
        }

        /// <summary>
        /// 扫描 proto 文件。
        /// </summary>
        private List<string> ScanProtoFiles(string directory, string[] excludeDirs)
        {
            List<string> protoFiles = new List<string>();

            try
            {
                string[] files = Directory.GetFiles(directory, "*.proto", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string normalizedPath = file.Replace('\\', '/');
                    
                    if (ShouldExclude(normalizedPath, excludeDirs))
                    {
                        continue;
                    }

                    protoFiles.Add(normalizedPath);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AstroriseProtoCompiler] 扫描 proto 文件失败：{ex.Message}");
            }

            return protoFiles;
        }

        /// <summary>
        /// 检查文件路径是否应该被排除。
        /// </summary>
        private bool ShouldExclude(string filePath, string[] excludeDirs)
        {
            string normalizedPath = filePath.Replace('\\', '/');

            // 使用传入的排除目录列表，如果为 null 则使用默认的 ExcludeDirs
            string[] dirsToCheck = excludeDirs ?? ExcludeDirs;

            // 检查目录排除
            foreach (string excludeDir in dirsToCheck)
            {
                string normalizedExcludeDir = excludeDir.TrimEnd('/');

                // 路径前缀匹配：${excludeDir}/
                if (normalizedPath.StartsWith(normalizedExcludeDir + "/"))
                {
                    return true;
                }

                // 路径中间匹配：/${excludeDir}/
                if (normalizedPath.Contains("/" + normalizedExcludeDir + "/"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 批量执行 protoc 编译。
        /// </summary>
        private bool ExecuteProtocBatch(List<string> protoFiles, string[] protoPaths, string outputPath)
        {
            try
            {
                // protoc 不会自动创建父目录，提前创建 Generated 目录用于 descriptor/registry 输出
                string generatedDir = Path.Combine(outputPath, "Generated").Replace('\\', '/');
                Directory.CreateDirectory(generatedDir);
                string descriptorPath = Path.Combine(generatedDir, "proto.descriptor.pb").Replace('\\', '/');

                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = ProtocCommand;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                string arguments = "";
                foreach (string protoPath in protoPaths)
                {
                    arguments += $"--proto_path=\"{protoPath}\" ";
                }
                arguments += $"--csharp_out=\"{outputPath}\" --csharp_opt=base_namespace= ";
                arguments += $"--descriptor_set_out=\"{descriptorPath}\" --include_imports";

                foreach (string protoFile in protoFiles)
                {
                    string absolutePath = Path.GetFullPath(protoFile).Replace('\\', '/');
                    arguments += $" \"{absolutePath}\"";
                }

                process.StartInfo.Arguments = arguments;

#if UNITY_DEBUG
                Debug.Log($"[AstroriseProtoCompiler] 执行命令: {ProtocCommand} {arguments}");
#endif

                process.Start();

                string output = string.Empty;
                string error = string.Empty;
                System.Threading.Thread outputThread = new System.Threading.Thread(() =>
                {
                    output = process.StandardOutput.ReadToEnd();
                });
                System.Threading.Thread errorThread = new System.Threading.Thread(() =>
                {
                    error = process.StandardError.ReadToEnd();
                });

                outputThread.Start();
                errorThread.Start();

                bool exited = process.WaitForExit(60000);
                outputThread.Join(5000);
                errorThread.Join(5000);

                if (!exited)
                {
                    Debug.LogError("[AstroriseProtoCompiler] protoc 执行超时");
                    process.Kill();
                    return false;
                }

                if (process.ExitCode == 0)
                {
                    if (!string.IsNullOrEmpty(output))
                    {
                        Debug.Log($"[AstroriseProtoCompiler] protoc 输出：{output}");
                    }
                    return true;
                }
                else
                {
                    Debug.LogError($"[AstroriseProtoCompiler] 编译失败（退出码：{process.ExitCode}）：{error}");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Debug.LogError($"[AstroriseProtoCompiler] protoc 输出：{output}");
                    }
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AstroriseProtoCompiler] 编译时发生异常：{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private bool GenerateRpcRegistryOrFail(string outputPath)
        {
            try
            {
                string generatedDir = Path.Combine(outputPath, "Generated").Replace('\\', '/');
                string descriptorPath = Path.Combine(generatedDir, "proto.descriptor.pb").Replace('\\', '/');
                string registryPath = Path.Combine(generatedDir, "RpcMethodRegistry.g.cs").Replace('\\', '/');

                RpcMethodRegistryGenerator.Generate(descriptorPath, registryPath);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AstroriseProtoCompiler] 生成 RpcMethodRegistry 失败：{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 清空输出目录（删除目录及其所有内容，编译前清理旧文件）。
        /// </summary>
        private void ClearOutputDirectory(string path)
        {
            Debug.Log($"[AstroriseProtoCompiler] 准备清空输出目录：{path}");
            try
            {
                if (Directory.Exists(path))
                {
                    Debug.Log($"[AstroriseProtoCompiler] 目录存在，正在删除：{path}");
                    Directory.Delete(path, true);
                    // 等待文件系统完成删除操作
                    System.Threading.Thread.Sleep(100);
                    Debug.Log($"[AstroriseProtoCompiler] 目录删除完成：{path}");
                }
                else
                {
                    Debug.Log($"[AstroriseProtoCompiler] 目录不存在，无需删除：{path}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AstroriseProtoCompiler] 清空目录失败：{ex.Message}\n路径：{path}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 生成/更新程序集定义文件。
        /// </summary>
        private void GenerateAssemblyDefinitions(string projectRoot)
        {
            Debug.Log($"[AstroriseProtoCompiler] 开始生成程序集定义，项目根目录: {projectRoot}");
            try
            {
                GenerateProtocolAsmdef(projectRoot);
                GenerateAstrorisePlatformAsmdef(projectRoot);
                Debug.Log("[AstroriseProtoCompiler] 程序集定义生成完成");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AstroriseProtoCompiler] 生成程序集定义失败：{ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("警告", $"生成程序集定义时出现错误：{ex.Message}\n请查看 Console 获取详细信息", "确定");
            }
        }

        /// <summary>
        /// 生成/更新 Protocol 程序集定义。
        /// </summary>
        private void GenerateProtocolAsmdef(string projectRoot)
        {
            string asmdefPath = Path.Combine(projectRoot, ProtocolAsmdefPath).Replace('\\', '/');
            // 使用字符串操作获取目录路径，避免 Path.GetDirectoryName 在 Windows 上的路径格式问题
            int lastSlashIndex = asmdefPath.LastIndexOf('/');
            string asmdefDir = lastSlashIndex >= 0 ? asmdefPath.Substring(0, lastSlashIndex) : projectRoot;
            
            if (!Directory.Exists(asmdefDir))
            {
                Directory.CreateDirectory(asmdefDir);
            }

            string asmdefContent = @"{
    ""name"": ""Protocol"",
    ""rootNamespace"": """",
    ""references"": [],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [
        """ + GoogleProtobufDllPath + @"""
    ],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}
";

            try
            {
                File.WriteAllText(asmdefPath, asmdefContent);
                Debug.Log($"[AstroriseProtoCompiler] 已生成/更新 Protocol 程序集定义：{ProtocolAsmdefPath} (完整路径: {asmdefPath})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AstroriseProtoCompiler] 写入 Protocol 程序集定义失败：{ex.Message}\n路径: {asmdefPath}");
                throw;
            }
        }

        /// <summary>
        /// 生成/更新 AstrorisePlatform 程序集定义。
        /// </summary>
        private void GenerateAstrorisePlatformAsmdef(string projectRoot)
        {
            string asmdefPath = Path.Combine(projectRoot, AstroriseAsmdefPath).Replace('\\', '/');
            Debug.Log($"[AstroriseProtoCompiler] 准备生成 AstrorisePlatform.asmdef");
            Debug.Log($"[AstroriseProtoCompiler] 项目根目录: {projectRoot}");
            Debug.Log($"[AstroriseProtoCompiler] 相对路径: {AstroriseAsmdefPath}");
            Debug.Log($"[AstroriseProtoCompiler] 完整路径: {asmdefPath}");
            
            // 使用字符串操作获取目录路径，避免 Path.GetDirectoryName 在 Windows 上的路径格式问题
            int lastSlashIndex = asmdefPath.LastIndexOf('/');
            string asmdefDir = lastSlashIndex >= 0 ? asmdefPath.Substring(0, lastSlashIndex) : projectRoot;
            Debug.Log($"[AstroriseProtoCompiler] 目标目录: {asmdefDir}");
            
            if (!Directory.Exists(asmdefDir))
            {
                Debug.Log($"[AstroriseProtoCompiler] 目录不存在，正在创建: {asmdefDir}");
                Directory.CreateDirectory(asmdefDir);
                Debug.Log($"[AstroriseProtoCompiler] 目录创建成功");
            }
            else
            {
                Debug.Log($"[AstroriseProtoCompiler] 目录已存在: {asmdefDir}");
            }

            string asmdefContent = @"{
    ""name"": ""AstrorisePlatform"",
    ""rootNamespace"": """",
    ""references"": [
        ""Protocol""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [
        """ + GoogleProtobufDllPath + @"""
    ],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}
";

            try
            {
                File.WriteAllText(asmdefPath, asmdefContent);
                Debug.Log($"[AstroriseProtoCompiler] 已生成/更新 AstrorisePlatform 程序集定义：{AstroriseAsmdefPath} (完整路径: {asmdefPath})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AstroriseProtoCompiler] 写入 AstrorisePlatform 程序集定义失败：{ex.Message}\n路径: {asmdefPath}");
                throw;
            }
        }

        /// <summary>
        /// 检查 protoc 工具是否可用。
        /// </summary>
        private bool CheckProtocAvailable()
        {
            try
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = ProtocCommand;
                process.StartInfo.Arguments = "--version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                process.WaitForExit(3000);

                return process.ExitCode == 0;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
#endif
