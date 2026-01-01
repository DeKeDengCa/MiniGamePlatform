#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Astorise.Editor.HybridCLR
{
    /// <summary>
    /// HybridCLR DLL 拷贝工具配置窗口。
    /// </summary>
    public class HybridCLRDllCopyWindow : EditorWindow
    {
        private HybridCLRDllCopySettings _settings;
        private Vector2 _scrollPosition;
        private string _newFrameworkDllName = "";
        private string _newMiniGameDllName = "";

        /// <summary>
        /// 打开配置窗口。
        /// </summary>
        [MenuItem("Window/HybridCLR/DLL Copy Settings", priority = 300)]
        public static void ShowWindow()
        {
            HybridCLRDllCopyWindow window = GetWindow<HybridCLRDllCopyWindow>("HybridCLR DLL Copy Settings");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("无法加载配置，请检查配置文件是否存在。", MessageType.Error);
                if (GUILayout.Button("创建默认配置"))
                {
                    _settings = HybridCLRDllCopySettings.GetOrCreateSettings();
                    EditorUtility.SetDirty(_settings);
                }
                return;
            }

            DrawHeaderActions();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);

            // AOT Metadata 配置区域
            EditorGUILayout.LabelField("AOT Metadata 配置", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            _settings.aotMetadataSourceDir = EditorGUILayout.TextField("源目录", _settings.aotMetadataSourceDir);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("目标目录", _settings.aotMetadataTargetDir);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("选择目标目录", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = "Assets" + path.Replace(Application.dataPath, "").Replace('\\', '/');
                    _settings.aotMetadataTargetDir = relativePath;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("DLL 列表来自 AOTGenericReferences.PatchedAOTAssemblyList", MessageType.Info);

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(10);

            // HotUpdate 配置区域
            EditorGUILayout.LabelField("HotUpdate 配置", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            _settings.hotUpdateSourceDir = EditorGUILayout.TextField("源目录", _settings.hotUpdateSourceDir);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("目标目录", _settings.hotUpdateTargetDir);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("选择目标目录", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = "Assets" + path.Replace(Application.dataPath, "").Replace('\\', '/');
                    _settings.hotUpdateTargetDir = relativePath;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            
            // Framework DLL 列表
            EditorGUILayout.LabelField("Framework DLL 列表（拷贝到 Framework 目录）:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            if (_settings.frameworkDllNames == null)
            {
                _settings.frameworkDllNames = new List<string>();
            }

            for (int i = 0; i < _settings.frameworkDllNames.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _settings.frameworkDllNames[i] = EditorGUILayout.TextField($"Framework DLL {i + 1}", _settings.frameworkDllNames[i]);
                if (GUILayout.Button("删除", GUILayout.Width(60)))
                {
                    _settings.frameworkDllNames.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 添加 Framework DLL 名称
            EditorGUILayout.BeginHorizontal();
            _newFrameworkDllName = EditorGUILayout.TextField("新 Framework DLL 名称", _newFrameworkDllName);
            if (GUILayout.Button("添加", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_newFrameworkDllName))
                {
                    if (!_settings.frameworkDllNames.Contains(_newFrameworkDllName))
                    {
                        _settings.frameworkDllNames.Add(_newFrameworkDllName);
                        _newFrameworkDllName = "";
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("提示", "DLL 名称已存在", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
            
            // 小游戏 DLL 列表
            EditorGUILayout.LabelField("小游戏 DLL 列表（拷贝到各自游戏目录）:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox("小游戏 DLL 会自动拷贝到 Assets/MiniGames/{GameName}/HotUpdate 目录\n例如：BBQHotUpdate -> Assets/MiniGames/BBQ/HotUpdate", MessageType.Info);
            
            if (_settings.miniGameDllNames == null)
            {
                _settings.miniGameDllNames = new List<string>();
            }

            for (int i = 0; i < _settings.miniGameDllNames.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _settings.miniGameDllNames[i] = EditorGUILayout.TextField($"小游戏 DLL {i + 1}", _settings.miniGameDllNames[i]);
                if (GUILayout.Button("删除", GUILayout.Width(60)))
                {
                    _settings.miniGameDllNames.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 添加小游戏 DLL 名称
            EditorGUILayout.BeginHorizontal();
            _newMiniGameDllName = EditorGUILayout.TextField("新小游戏 DLL 名称", _newMiniGameDllName);
            if (GUILayout.Button("添加", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_newMiniGameDllName))
                {
                    if (!_settings.miniGameDllNames.Contains(_newMiniGameDllName))
                    {
                        _settings.miniGameDllNames.Add(_newMiniGameDllName);
                        _newMiniGameDllName = "";
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("提示", "DLL 名称已存在", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(10);

            // 当前平台显示
            EditorGUILayout.LabelField("当前平台", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            BuildTarget currentPlatform = EditorUserBuildSettings.activeBuildTarget;
            string platformName = HybridCLRDllCopyUtils.GetPlatformDirectoryName(currentPlatform);
            EditorGUILayout.LabelField($"平台: {currentPlatform} -> {platformName}", EditorStyles.wordWrappedLabel);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(10);

            EditorGUILayout.EndScrollView();

            // 操作按钮
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("保存配置", GUILayout.Height(30)))
            {
                SaveSettings();
            }

            if (GUILayout.Button("重置为默认值", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("确认", "确定要重置为默认值吗？", "确定", "取消"))
                {
                    _settings.ResetToDefaults();
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("提示", "已重置为默认值", "确定");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("提示：头部按钮可执行自动配置和生成操作。", MessageType.Info);
        }

        private static void DrawHeaderActions()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("快捷操作", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("自动配置热更新程序集", GUILayout.Height(28)))
            {
                AutoConfigureHotUpdateAssemblies.Configure();
            }

            if (GUILayout.Button("生成全部DLL", GUILayout.Height(28)))
            {
                HybridCLRDllCopyUtils.GenerateAll();
            }

            if (GUILayout.Button("生成AOT元数据", GUILayout.Height(28)))
            {
                HybridCLRDllCopyUtils.GenerateAOTMetadata();
            }

            if (GUILayout.Button("生成HotUpdate", GUILayout.Height(28)))
            {
                HybridCLRDllCopyUtils.GenerateHotUpdate();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void LoadSettings()
        {
            _settings = HybridCLRDllCopySettings.GetOrCreateSettings();
        }

        private void SaveSettings()
        {
            if (_settings == null)
            {
                EditorUtility.DisplayDialog("错误", "配置为空，无法保存", "确定");
                return;
            }

            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("提示", "配置已保存", "确定");
            Debug.Log("[HybridCLRDllCopyWindow] 配置已保存");
        }
    }
}
#endif

