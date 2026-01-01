#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Astorise.Editor.Git
{
    /// <summary>
    /// Git 子模块管理窗口。
    /// </summary>
    public class GitSubmoduleWindow : EditorWindow
    {
        private List<SubmoduleInfo> _submodules = new List<SubmoduleInfo>();

        [MenuItem("Tools/Git 子模块管理", priority = 100)]
        public static void ShowWindow()
        {
            GitSubmoduleWindow window = GetWindow<GitSubmoduleWindow>("Git 子模块管理");
            window.minSize = new Vector2(400, 200);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSubmodules();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            if (GUILayout.Button("更新所有子模块", GUILayout.Height(35)))
            {
                UpdateAllSubmodules();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("子模块列表", EditorStyles.boldLabel);

            if (_submodules.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到子模块", MessageType.Info);
            }
            else
            {
                foreach (SubmoduleInfo submodule in _submodules)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField(submodule.Path, GUILayout.Width(250));
                    if (GUILayout.Button("更新", GUILayout.Width(80)))
                    {
                        UpdateSubmodule(submodule.Path);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("刷新列表"))
            {
                RefreshSubmodules();
            }
        }

        private void RefreshSubmodules()
        {
            _submodules.Clear();

            Process process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = "submodule status";
            process.StartInfo.WorkingDirectory = Application.dataPath.Replace("/Assets", "");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            try
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    string[] lines = output.Split('\n');
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        string[] parts = line.Trim().Split(' ');
                        if (parts.Length >= 2)
                        {
                            _submodules.Add(new SubmoduleInfo { Path = parts[1] });
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[GitSubmoduleWindow] 获取子模块列表失败: " + ex.Message);
            }
        }

        private void UpdateAllSubmodules()
        {
            ExecuteGitCommand("submodule update --init --recursive", "所有子模块");
        }

        private void UpdateSubmodule(string path)
        {
            ExecuteGitCommand($"submodule update --init {path}", path);
        }

        private void ExecuteGitCommand(string arguments, string targetName)
        {
            Process process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = Application.dataPath.Replace("/Assets", "");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            try
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    UnityEngine.Debug.Log($"[GitSubmoduleWindow] {targetName} 更新成功\n{output}");
                    EditorUtility.DisplayDialog("成功", $"{targetName} 已更新", "确定");
                    AssetDatabase.Refresh();
                    RefreshSubmodules();
                }
                else
                {
                    UnityEngine.Debug.LogError($"[GitSubmoduleWindow] {targetName} 更新失败\n{error}");
                    EditorUtility.DisplayDialog("失败", $"{targetName} 更新失败，请查看 Console", "确定");
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[GitSubmoduleWindow] 执行失败: {ex.Message}");
                EditorUtility.DisplayDialog("错误", $"执行失败: {ex.Message}", "确定");
            }
        }

        private class SubmoduleInfo
        {
            public string Path;
        }
    }
}
#endif
