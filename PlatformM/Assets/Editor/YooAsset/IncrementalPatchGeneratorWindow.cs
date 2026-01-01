using System.IO;
using UnityEngine;
using UnityEditor;

namespace YooAsset.Editor
{
    /// <summary>
    /// 增量补丁生成工具窗口。
    /// </summary>
    public class IncrementalPatchGeneratorWindow : EditorWindow
    {
        private static IncrementalPatchGeneratorWindow _thisInstance;

        private const string PrefKeyGameName = "IncrementalPatchGenerator.GameName";
        private const string PrefKeyRootDirectory = "IncrementalPatchGenerator.RootDirectory";
        private const string PrefKeyBaseVersion = "IncrementalPatchGenerator.BaseVersion";
        private const string PrefKeyCurrentVersion = "IncrementalPatchGenerator.CurrentVersion";

        private string _gameName = string.Empty;
        private string _rootDirectory = string.Empty;
        private string _baseVersion = string.Empty;
        private string _currentVersion = string.Empty;

        private Vector2 _scrollPosition;

        /// <summary>
        /// 显示窗口。
        /// </summary>
        static void ShowWindow()
        {
            if (_thisInstance == null)
            {
                _thisInstance = EditorWindow.GetWindow(typeof(IncrementalPatchGeneratorWindow), false, "增量补丁生成工具", true) as IncrementalPatchGeneratorWindow;
                _thisInstance.minSize = new Vector2(600, 400);
            }
            _thisInstance.Show();
        }

        private void OnEnable()
        {
            LoadPreferences();
        }

        private void OnDisable()
        {
            SavePreferences();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);

            // 标题
            EditorGUILayout.LabelField("增量补丁生成工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("根据游戏名称、根目录、母包版本和当前版本，自动生成增量补丁包。", MessageType.Info);
            EditorGUILayout.Space(10);

            // 游戏名称
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("游戏名称:", GUILayout.Width(120));
            _gameName = EditorGUILayout.TextField(_gameName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 根目录
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("根目录:", GUILayout.Width(120));
            EditorGUILayout.TextField(_rootDirectory, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("选择目录", GUILayout.MaxWidth(100)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择根目录", _rootDirectory, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _rootDirectory = selectedPath;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 母包版本
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("母包版本:", GUILayout.Width(120));
            _baseVersion = EditorGUILayout.TextField(_baseVersion);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 当前版本
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("当前版本:", GUILayout.Width(120));
            _currentVersion = EditorGUILayout.TextField(_currentVersion);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            // 生成按钮
            EditorGUI.BeginDisabledGroup(!CanGenerate());
            if (GUILayout.Button("生成增量补丁", GUILayout.Height(40)))
            {
                GeneratePatch();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            // 提示信息
            if (!CanGenerate())
            {
                EditorGUILayout.HelpBox("请填写所有必填项后再生成补丁包。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox($"将生成游戏 '{_gameName}' 从版本 '{_baseVersion}' 到 '{_currentVersion}' 的增量补丁包。", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 检查是否可以生成补丁。
        /// </summary>
        private bool CanGenerate()
        {
            return !string.IsNullOrEmpty(_gameName) &&
                   !string.IsNullOrEmpty(_rootDirectory) &&
                   !string.IsNullOrEmpty(_baseVersion) &&
                   !string.IsNullOrEmpty(_currentVersion) &&
                   Directory.Exists(_rootDirectory);
        }

        /// <summary>
        /// 生成补丁。
        /// </summary>
        private void GeneratePatch()
        {
            if (!CanGenerate())
            {
                EditorUtility.DisplayDialog("错误", "请填写所有必填项后再生成补丁包。", "确定");
                return;
            }

            // 确认对话框
            bool confirmed = EditorUtility.DisplayDialog(
                "确认生成",
                $"确定要生成游戏 '{_gameName}' 的增量补丁包吗？\n\n" +
                $"根目录: {_rootDirectory}\n" +
                $"母包版本: {_baseVersion}\n" +
                $"当前版本: {_currentVersion}",
                "确定",
                "取消"
            );

            if (!confirmed)
            {
                return;
            }

            // 调用生成函数
            try
            {
                IncrementalPatchGenerator.GenerateIncrementalPatch(_gameName, _rootDirectory, _baseVersion, _currentVersion);
                EditorUtility.DisplayDialog("成功", "增量补丁包生成完成！请查看 Console 日志了解详细信息。", "确定");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"生成增量补丁包时发生错误：\n{ex.Message}", "确定");
                Debug.LogError($"[IncrementalPatchGeneratorWindow] 生成补丁包失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 加载配置。
        /// </summary>
        private void LoadPreferences()
        {
            _gameName = EditorPrefs.GetString(PrefKeyGameName, "");
            _rootDirectory = EditorPrefs.GetString(PrefKeyRootDirectory, "");
            _baseVersion = EditorPrefs.GetString(PrefKeyBaseVersion, "");
            _currentVersion = EditorPrefs.GetString(PrefKeyCurrentVersion, "");
        }

        /// <summary>
        /// 保存配置。
        /// </summary>
        private void SavePreferences()
        {
            EditorPrefs.SetString(PrefKeyGameName, _gameName);
            EditorPrefs.SetString(PrefKeyRootDirectory, _rootDirectory);
            EditorPrefs.SetString(PrefKeyBaseVersion, _baseVersion);
            EditorPrefs.SetString(PrefKeyCurrentVersion, _currentVersion);
        }
    }
}

