#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Astorise.Editor.Proto
{
    /// <summary>
    /// Protocol 发布工具窗口。
    /// </summary>
    public class ProtocolPublisherWindow : EditorWindow
    {
        private const string SourceDirectory = "Proto/Protocol";
        private const string TargetDirectory = "Assets/AOT/Runtime/Protocol";
        private const string ProtocCommand = "protoc";
        private static readonly string[] ExcludeDirs = { "google/protobuf" };
        private static readonly string[] ExcludeFiles = { };

        private System.Diagnostics.Process _compileProcess;
        private bool _isCompiling = false;
        private System.DateTime _compileStartTime;
        private List<string> _compileProtoFiles;
        private string _compileSourceDir;
        private string _compileTargetDir;
        private System.Text.StringBuilder _compileOutput;
        private System.Text.StringBuilder _compileError;

        /// <summary>
        /// 打开窗口。
        /// </summary>
        //[MenuItem("Tools/发布Protocol", false, priority = 100)]
        public static void ShowWindow()
        {
            ProtocolPublisherWindow window = GetWindow<ProtocolPublisherWindow>("Protocol 发布工具");
            window.minSize = new Vector2(500, 200);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("源目录:", GUILayout.Width(100));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(SourceDirectory);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标目录:", GUILayout.Width(100));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(TargetDirectory);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(_isCompiling);
            if (GUILayout.Button("发布Protocol", GUILayout.Height(35)))
            {
                PublishProtocol();
            }
            EditorGUI.EndDisabledGroup();

            if (_isCompiling)
            {
                EditorGUILayout.HelpBox("正在编译中，请稍候...", MessageType.Info);
            }
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_compileProcess != null && _isCompiling)
            {
                // 使用 WaitForExit(0) 非阻塞检查进程是否已退出
                bool hasExited = _compileProcess.WaitForExit(0);
                
                if (hasExited)
                {
                    _isCompiling = false;
                    OnCompileFinished();
                    _compileProcess = null;
                }
                else
                {
                    // 检查超时（120秒，因为编译可能较慢）
                    System.TimeSpan elapsed = System.DateTime.Now - _compileStartTime;
                    if (elapsed.TotalSeconds > 120)
                    {
                        _isCompiling = false;
                        try
                        {
                            if (!_compileProcess.HasExited)
                            {
                                _compileProcess.Kill();
                            }
                        }
                        catch (System.Exception)
                        {
                            // 忽略 Kill 异常
                        }
                        Debug.LogError("[ProtocolPublisherWindow] protoc 执行超时（120秒）");
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("失败", "编译超时（120秒），请查看 Console 日志", "确定");
                        _compileProcess = null;
                    }
                }
            }
        }

        /// <summary>
        /// 发布 Protocol。
        /// </summary>
        private void PublishProtocol()
        {
            string projectRoot = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            string sourceDir = Path.Combine(projectRoot, SourceDirectory).Replace('\\', '/');
            string targetDir = Path.Combine(projectRoot, TargetDirectory).Replace('\\', '/');

            if (!Directory.Exists(sourceDir))
            {
                EditorUtility.DisplayDialog("错误", $"源目录不存在：{sourceDir}", "确定");
                Debug.LogError($"[ProtocolPublisherWindow] 源目录不存在：{sourceDir}");
                return;
            }

            if (!CheckProtocAvailable())
            {
                EditorUtility.DisplayDialog("错误", "protoc 编译器未找到，请确保已安装并配置到系统 PATH 中", "确定");
                Debug.LogError("[ProtocolPublisherWindow] protoc 编译器未找到");
                return;
            }

            try
            {
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }

                Directory.CreateDirectory(targetDir);

                List<string> protoFiles = ScanProtoFiles(sourceDir, ExcludeDirs);
                if (protoFiles.Count == 0)
                {
                    EditorUtility.DisplayDialog("提示", "未找到任何 .proto 文件", "确定");
                    Debug.Log("[ProtocolPublisherWindow] 未找到任何 .proto 文件");
                    return;
                }

                // 保存编译参数
                _compileProtoFiles = protoFiles;
                _compileSourceDir = sourceDir;
                _compileTargetDir = targetDir;

                // 启动异步编译
                StartCompileAsync();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"发布失败：{ex.Message}", "确定");
                Debug.LogError($"[ProtocolPublisherWindow] 发布失败：{ex.Message}\n{ex.StackTrace}");
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

            // 检查文件排除（预留）
            string fileName = Path.GetFileName(normalizedPath);
            foreach (string excludeFile in ExcludeFiles)
            {
                if (fileName == excludeFile)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 扫描 proto 文件，支持排除目录和文件。
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

                    // 检查是否应该排除
                    if (ShouldExclude(normalizedPath, excludeDirs))
                    {
                        continue;
                    }

                    protoFiles.Add(normalizedPath);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ProtocolPublisherWindow] 扫描 proto 文件失败：{ex.Message}");
            }

            return protoFiles;
        }

        /// <summary>
        /// 启动异步编译。
        /// </summary>
        private void StartCompileAsync()
        {
            try
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = ProtocCommand;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                // 构建参数：参考 gen_client.sh 的方式，使用绝对路径传递文件
                // --proto_path 用于指定 import 文件的搜索路径
                string arguments = $"--proto_path=\"{_compileSourceDir}\" --csharp_out=\"{_compileTargetDir}\"";
                foreach (string protoFile in _compileProtoFiles)
                {
                    // 使用绝对路径，确保 protoc 能正确找到文件
                    string absolutePath = Path.GetFullPath(protoFile).Replace('\\', '/');
                    arguments += $" \"{absolutePath}\"";
                }

                process.StartInfo.Arguments = arguments;

                Debug.Log($"[ProtocolPublisherWindow] 执行命令: {ProtocCommand} {arguments}");

                // 初始化输出收集器
                _compileOutput = new System.Text.StringBuilder();
                _compileError = new System.Text.StringBuilder();

                // 异步读取输出，避免缓冲区满导致进程阻塞
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        _compileOutput.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        _compileError.AppendLine(e.Data);
                    }
                };

                process.Start();
                
                // 开始异步读取
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _compileProcess = process;
                _isCompiling = true;
                _compileStartTime = System.DateTime.Now;

                EditorUtility.DisplayProgressBar("编译 Protocol", "正在编译 proto 文件...", 0.5f);
            }
            catch (System.Exception ex)
            {
                _isCompiling = false;
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[ProtocolPublisherWindow] 启动编译时发生异常：{ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("错误", $"启动编译失败：{ex.Message}", "确定");
            }
        }

        /// <summary>
        /// 编译完成回调。
        /// </summary>
        private void OnCompileFinished()
        {
            EditorUtility.ClearProgressBar();

            if (_compileProcess == null)
            {
                return;
            }

            try
            {
                // 使用已收集的输出（异步读取应该已经完成）
                string output = _compileOutput != null ? _compileOutput.ToString() : string.Empty;
                string error = _compileError != null ? _compileError.ToString() : string.Empty;
                int exitCode = _compileProcess.ExitCode;

                // 检查是否有文件生成（即使有错误，可能部分文件已经生成）
                bool hasGeneratedFiles = Directory.Exists(_compileTargetDir) && 
                    Directory.GetFiles(_compileTargetDir, "*.cs", SearchOption.AllDirectories).Length > 0;

                if (exitCode == 0)
                {
                    if (!string.IsNullOrEmpty(output))
                    {
                        Debug.Log($"[ProtocolPublisherWindow] protoc 输出：{output}");
                    }
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("完成", $"编译完成：成功编译 {_compileProtoFiles.Count} 个 proto 文件", "确定");
                    Debug.Log($"[ProtocolPublisherWindow] 编译完成：成功编译 {_compileProtoFiles.Count} 个 proto 文件");
                }
                else
                {
                    // 检查是否是因为文件重名冲突导致的错误（这是 protoc 的已知限制）
                    bool isFileConflictError = error.Contains("Tried to write the same file twice");
                    
                    if (isFileConflictError && hasGeneratedFiles)
                    {
                        // 文件冲突错误，但部分文件已生成，记录警告但继续
                        Debug.LogWarning($"[ProtocolPublisherWindow] 编译完成，但有文件重名冲突（protoc 限制）：{error}");
                        AssetDatabase.Refresh();
                        EditorUtility.DisplayDialog("完成（有警告）", $"编译完成，但有部分文件重名冲突（protoc 限制）。已生成 {Directory.GetFiles(_compileTargetDir, "*.cs", SearchOption.AllDirectories).Length} 个文件。", "确定");
                        Debug.Log($"[ProtocolPublisherWindow] 编译完成（有冲突）：已生成部分文件");
                    }
                    else
                    {
                        // 其他错误
                        Debug.LogError($"[ProtocolPublisherWindow] 编译失败（退出码：{exitCode}）：{error}");
                        if (!string.IsNullOrEmpty(output))
                        {
                            Debug.LogError($"[ProtocolPublisherWindow] protoc 输出：{output}");
                        }
                        EditorUtility.DisplayDialog("失败", "编译失败，请查看 Console 日志", "确定");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ProtocolPublisherWindow] 读取编译结果时发生异常：{ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("错误", $"读取编译结果失败：{ex.Message}", "确定");
            }
        }
    }
}
#endif

