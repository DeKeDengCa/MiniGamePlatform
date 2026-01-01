#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Astorise.Editor.HybridCLR;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;
using YooAsset;

namespace Astorise.Editor.Package
{
    /// <summary>
    /// 自动化打包工具窗口。
    /// </summary>
    public class AutomatedBuildToolWindow : EditorWindow
    {
        private const string PrefKeySelectedTab = "AutomatedBuildTool.SelectedTab";
        private const string PrefKeySelectedGames = "AutomatedBuildTool.SelectedGames";
        private const string PrefKeyExpandedGames = "AutomatedBuildTool.ExpandedGames";
        private const string PrefKeySelectedGameIndex = "AutomatedBuildTool.SelectedGameIndex";
        private const string PrefKeyIsAddingNewGame = "AutomatedBuildTool.IsAddingNewGame";
        private const string PrefKeyEditingGameName = "AutomatedBuildTool.EditingGameName";
        private const string PrefKeyEditingDllPath = "AutomatedBuildTool.EditingDllPath";
        private const string PrefKeyEditingSubPackages = "AutomatedBuildTool.EditingSubPackages";

        private static readonly GUIContent RebuildDllLabel = new GUIContent("重新编译DLL:", "如果勾选，会执行预构建步骤（Auto Configure HotUpdate Assemblies、Generate HybridCLR files等）。如果不勾选，则直接进行打包。");
        private static readonly GUIContent PlatformNameLabel = new GUIContent("平台名称:", "平台名称用于母包构建后，将打包生成的资源拷贝到对应目录。");
        private static readonly GUIContent PreviousVersionLabel = new GUIContent("上一个版本号:", "上一个版本号用于增量更新包对比。如果为空，将跳过增量补丁生成。");
        private static readonly GUIContent IncrementVersionButtonLabel = new GUIContent("版本+1", "将当前版本号写入上一个版本号，并将版本号末段+1");
        private static readonly GUIContent IncrementMainPackageVersionButtonLabel = new GUIContent("版本+1", "母包版本号末段+1");
        private static readonly GUIContent DecrementPreviousVersionButtonLabel = new GUIContent("版本-1", "将上一个版本号写入版本号，并将上一个版本号末段-1");
        private static readonly GUIContent HybridClrHeaderTip = new GUIContent("?", "头部按钮可执行自动配置和生成操作。");
        private static readonly GUIContent AotMetadataTip = new GUIContent("?", "DLL 列表来自 AOTGenericReferences.PatchedAOTAssemblyList");
        private static readonly GUIContent MiniGameDllTip = new GUIContent("?", "小游戏 DLL 会自动拷贝到 Assets/MiniGames/{GameName}/HotUpdate 目录\n例如：BBQHotUpdate -> Assets/MiniGames/BBQ/HotUpdate");

        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "打包", "配置", "HybridCLR" };

        // 配置数据（从 XML 读取，在内存中编辑）
        private List<GamePackageConfig> _gameConfigs = new List<GamePackageConfig>();

        // 打包页签的选中状态
        private Dictionary<string, bool> _selectedGames = new Dictionary<string, bool>();

        // 打包页签的展开状态
        private Dictionary<string, bool> _expandedGames = new Dictionary<string, bool>();

        // Settings
        private BuildToolSettings _settings;
        private HybridCLRDllCopySettings _hybridclrSettings;

        // 配置页签的编辑状态
        private int _selectedGameIndex = -1;
        private bool _isAddingNewGame = false;
        private string _editingGameName = "";
        private string _editingDllPath = "";
        private List<SubPackageConfig> _editingSubPackages = new List<SubPackageConfig>();

        // 打包页签的游戏列表滚动位置
        private Vector2 _gameListScrollPosition = Vector2.zero;
        private Vector2 _hybridclrScrollPosition = Vector2.zero;
        private string _newHybridFrameworkDllName = "";
        private string _newHybridMiniGameDllName = "";

        /// <summary>
        /// 打开窗口。
        /// </summary>
        [MenuItem("Tools/Build Tool", false, 100)]
        public static void ShowWindow()
        {
            AutomatedBuildToolWindow window = GetWindow<AutomatedBuildToolWindow>("自动化打包工具");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = BuildToolSettings.GetOrCreateSettings();
            _hybridclrSettings = HybridCLRDllCopySettings.GetOrCreateSettings();
            LoadConfigsFromXml();
            LoadUIPreferences();
        }

        private void OnDisable()
        {
            SaveUIPreferences();
        }

        private void OnGUI()
        {
            // 页签选择
            int newSelectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            if (newSelectedTab != _selectedTab)
            {
                _selectedTab = newSelectedTab;
                SaveUIPreferences(); // 页签切换时立即保存
            }

            EditorGUILayout.Space(10);

            // 根据选中的页签显示不同内容
            switch (_selectedTab)
            {
                case 0:
                    DrawBuildTab();
                    break;
                case 1:
                    DrawConfigTab();
                    break;
                case 2:
                    DrawHybridClrTab();
                    break;
            }
        }

        /// <summary>
        /// 绘制打包页签。
        /// </summary>
        private void DrawBuildTab()
        {
            DrawHybridClrHeaderActions();

            // ========== 第一部分：公共设置 ==========
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("公共设置", EditorStyles.boldLabel);
            EditorGUILayout.Separator();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(RebuildDllLabel, GUILayout.Width(100));
            bool newRebuildDll = EditorGUILayout.Toggle(_settings.RebuildDll);
            if (newRebuildDll != _settings.RebuildDll)
            {
                _settings.RebuildDll = newRebuildDll;
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(PlatformNameLabel, GUILayout.Width(100));
            string newPlatformName = EditorGUILayout.TextField(_settings.PlatformName);
            if (newPlatformName != _settings.PlatformName)
            {
                _settings.PlatformName = newPlatformName;
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // ========== 第二部分：打更新包 ==========
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("打更新包", EditorStyles.boldLabel);
            EditorGUILayout.Separator();
            
            // 版本号输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("版本号:", GUILayout.Width(100));
            string newVersion = EditorGUILayout.TextField(_settings.Version);
            if (newVersion != _settings.Version)
            {
                _settings.Version = newVersion;
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button(IncrementVersionButtonLabel, GUILayout.Width(80)))
            {
                string currentVersion = newVersion;
                string increasedVersion;
                if (TryIncreaseVersion(currentVersion, out increasedVersion))
                {
                    _settings.PreviousVersion = currentVersion;
                    _settings.Version = increasedVersion;
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "版本号格式不合法，无法自增，请使用数字版本（如 1.0.1）。", "确定");
                }
            }
            EditorGUILayout.EndHorizontal();

            // 上一个版本号输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(PreviousVersionLabel, GUILayout.Width(100));
            string newPreviousVersion = EditorGUILayout.TextField(_settings.PreviousVersion);
            if (newPreviousVersion != _settings.PreviousVersion)
            {
                _settings.PreviousVersion = newPreviousVersion;
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button(DecrementPreviousVersionButtonLabel, GUILayout.Width(80)))
            {
                string currentPreviousVersion = newPreviousVersion;
                string decreasedPreviousVersion;
                if (TryDecreaseVersion(currentPreviousVersion, out decreasedPreviousVersion))
                {
                    _settings.Version = currentPreviousVersion;
                    _settings.PreviousVersion = decreasedPreviousVersion;
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "上一个版本号格式不合法或无法减 1，请使用数字版本（如 1.0.1）。", "确定");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 打更新包按钮
            GUI.enabled = _gameConfigs.Count > 0 && HasSelectedGames();
            if (GUILayout.Button("打更新包", GUILayout.Height(30)))
            {
                BuildUpdatePackage();
            }
            GUI.enabled = true;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // ========== 第三部分：打母包 ==========
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("打母包", EditorStyles.boldLabel);
            EditorGUILayout.Separator();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("母包路径:", GUILayout.Width(100));
            string newMainPackagePath = EditorGUILayout.TextField(_settings.MainPackagePath);
            if (newMainPackagePath != _settings.MainPackagePath)
            {
                _settings.MainPackagePath = newMainPackagePath;
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("母包版本号:", GUILayout.Width(100));
            string newMainPackageVersion = EditorGUILayout.TextField(_settings.MainPackageVersion);
            if (newMainPackageVersion != _settings.MainPackageVersion)
            {
                _settings.MainPackageVersion = newMainPackageVersion;
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button(IncrementMainPackageVersionButtonLabel, GUILayout.Width(80)))
            {
                string currentVersion = newMainPackageVersion;
                string increasedVersion;
                if (TryIncreaseVersion(currentVersion, out increasedVersion))
                {
                    _settings.MainPackageVersion = increasedVersion;
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "母包版本号格式不合法，无法自增，请使用数字版本（如 1.0.1）。", "确定");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 打母包按钮
            GUI.enabled = _gameConfigs.Count > 0 && HasSelectedGames();
            if (GUILayout.Button("打母包", GUILayout.Height(30)))
            {
                BuildMainPackage();
            }
            GUI.enabled = true;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // ========== 共享的游戏列表 ==========
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("需要打包的游戏（从 XML 读取）:", EditorStyles.boldLabel);
            EditorGUILayout.Separator();
            
            if (_gameConfigs.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到游戏配置，请先在配置页签中添加配置。", MessageType.Warning);
            }
            else
            {
                // 使用滚动视图包装游戏列表
                _gameListScrollPosition = EditorGUILayout.BeginScrollView(_gameListScrollPosition, GUILayout.Height(200));
                for (int i = 0; i < _gameConfigs.Count; i++)
                {
                    DrawGameNameItem(_gameConfigs[i], i);
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制 GameName 项（可展开显示详情）。
        /// </summary>
        private void DrawGameNameItem(GamePackageConfig config, int index)
        {
            string gameName = config.GameName;
            if (string.IsNullOrEmpty(gameName))
                return;

            // 确保选中状态存在
            if (!_selectedGames.ContainsKey(gameName))
            {
                _selectedGames[gameName] = false;
            }

            // 确保展开状态存在
            if (!_expandedGames.ContainsKey(gameName))
            {
                _expandedGames[gameName] = false;
            }

            EditorGUILayout.BeginVertical("box");

            // 第一行：复选框 + GameName + 展开/折叠按钮
            EditorGUILayout.BeginHorizontal();
            bool newSelected = EditorGUILayout.Toggle(_selectedGames[gameName], GUILayout.Width(20));
            if (newSelected != _selectedGames[gameName])
            {
                _selectedGames[gameName] = newSelected;
                SaveUIPreferences(); // 选中状态改变时立即保存
            }
            
            string expandIcon = _expandedGames[gameName] ? "▼" : "▶";
            if (GUILayout.Button(expandIcon, GUILayout.Width(20)))
            {
                _expandedGames[gameName] = !_expandedGames[gameName];
                SaveUIPreferences(); // 展开状态改变时立即保存
            }

            EditorGUILayout.LabelField(gameName, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // 展开后显示详情
            if (_expandedGames[gameName])
            {
                EditorGUI.indentLevel++;
                
                // DLL 路径
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("DLL:", GUILayout.Width(50));
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(config.DllPath);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();

                // Packages 列表
                EditorGUILayout.LabelField("Packages:");
                EditorGUI.indentLevel++;
                foreach (var subPackage in config.SubPackages)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("• " + subPackage.PackageName, GUILayout.Width(200));
                    EditorGUILayout.LabelField($"AppendExtension: {(subPackage.AppendExtension ? "✓" : "✗")}");
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制配置页签。
        /// </summary>
        private void DrawConfigTab()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("配置来源: GamePackageMapping.xml", EditorStyles.boldLabel);
            if (GUILayout.Button("从 XML 重新加载", GUILayout.Width(120)))
            {
                LoadConfigsFromXml();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 配置列表
            EditorGUILayout.LabelField("游戏配置列表:", EditorStyles.boldLabel);
            
            if (_gameConfigs.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到配置，请添加游戏配置。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginVertical("box");
                for (int i = 0; i < _gameConfigs.Count; i++)
                {
                    DrawConfigItem(_gameConfigs[i], i);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // 添加新游戏按钮（仅在未编辑状态下显示）
            if (_selectedGameIndex < 0 && !_isAddingNewGame)
            {
                if (GUILayout.Button("添加新游戏", GUILayout.Height(30)))
                {
                    AddNewGame();
                }
            }

            EditorGUILayout.Space(10);

            // 编辑区域（仅在编辑或添加模式下显示）
            if (_selectedGameIndex >= 0 || _isAddingNewGame)
            {
                EditorGUILayout.LabelField(_isAddingNewGame ? "添加新游戏:" : "编辑配置:", EditorStyles.boldLabel);
                DrawEditArea();
            }

            EditorGUILayout.Space(10);

            // 生成 XML 按钮
            if (GUILayout.Button("生成 XML 配置", GUILayout.Height(30)))
            {
                GenerateConfigToXml();
            }
        }

        /// <summary>
        /// 绘制 HybridCLR 配置页签。
        /// </summary>
        private void DrawHybridClrTab()
        {
            if (_hybridclrSettings == null)
            {
                EditorGUILayout.HelpBox("无法加载配置，请检查配置文件是否存在。", MessageType.Error);
                if (GUILayout.Button("创建默认配置"))
                {
                    _hybridclrSettings = HybridCLRDllCopySettings.GetOrCreateSettings();
                    EditorUtility.SetDirty(_hybridclrSettings);
                }
                return;
            }

            _hybridclrScrollPosition = EditorGUILayout.BeginScrollView(_hybridclrScrollPosition);

            EditorGUILayout.Space(10);

            // AOT Metadata 配置区域
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("AOT Metadata 配置", EditorStyles.boldLabel);
            GUILayout.Label(AotMetadataTip, GUILayout.Width(18));
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel++;

            _hybridclrSettings.aotMetadataSourceDir = EditorGUILayout.TextField("源目录", _hybridclrSettings.aotMetadataSourceDir);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("目标目录", _hybridclrSettings.aotMetadataTargetDir);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("选择目标目录", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = "Assets" + path.Replace(Application.dataPath, "").Replace('\\', '/');
                    _hybridclrSettings.aotMetadataTargetDir = relativePath;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(10);

            // HotUpdate 配置区域
            EditorGUILayout.LabelField("HotUpdate 配置", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            _hybridclrSettings.hotUpdateSourceDir = EditorGUILayout.TextField("源目录", _hybridclrSettings.hotUpdateSourceDir);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("目标目录", _hybridclrSettings.hotUpdateTargetDir);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("选择目标目录", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = "Assets" + path.Replace(Application.dataPath, "").Replace('\\', '/');
                    _hybridclrSettings.hotUpdateTargetDir = relativePath;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Framework DLL 列表
            EditorGUILayout.LabelField("Framework DLL 列表（拷贝到 Framework 目录）：", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (_hybridclrSettings.frameworkDllNames == null)
            {
                _hybridclrSettings.frameworkDllNames = new List<string>();
            }

            for (int i = 0; i < _hybridclrSettings.frameworkDllNames.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _hybridclrSettings.frameworkDllNames[i] = EditorGUILayout.TextField($"Framework DLL {i + 1}", _hybridclrSettings.frameworkDllNames[i]);
                if (GUILayout.Button("删除", GUILayout.Width(60)))
                {
                    _hybridclrSettings.frameworkDllNames.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 添加 Framework DLL 名称
            EditorGUILayout.BeginHorizontal();
            _newHybridFrameworkDllName = EditorGUILayout.TextField("新 Framework DLL 名称", _newHybridFrameworkDllName);
            if (GUILayout.Button("添加", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_newHybridFrameworkDllName))
                {
                    if (!_hybridclrSettings.frameworkDllNames.Contains(_newHybridFrameworkDllName))
                    {
                        _hybridclrSettings.frameworkDllNames.Add(_newHybridFrameworkDllName);
                        _newHybridFrameworkDllName = "";
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
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("小游戏 DLL 列表（拷贝到各自游戏目录）：", EditorStyles.boldLabel);
            GUILayout.Label(MiniGameDllTip, GUILayout.Width(18));
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel++;

            if (_hybridclrSettings.miniGameDllNames == null)
            {
                _hybridclrSettings.miniGameDllNames = new List<string>();
            }

            for (int i = 0; i < _hybridclrSettings.miniGameDllNames.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _hybridclrSettings.miniGameDllNames[i] = EditorGUILayout.TextField($"小游戏 DLL {i + 1}", _hybridclrSettings.miniGameDllNames[i]);
                if (GUILayout.Button("删除", GUILayout.Width(60)))
                {
                    _hybridclrSettings.miniGameDllNames.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 添加小游戏 DLL 名称
            EditorGUILayout.BeginHorizontal();
            _newHybridMiniGameDllName = EditorGUILayout.TextField("新小游戏 DLL 名称", _newHybridMiniGameDllName);
            if (GUILayout.Button("添加", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_newHybridMiniGameDllName))
                {
                    if (!_hybridclrSettings.miniGameDllNames.Contains(_newHybridMiniGameDllName))
                    {
                        _hybridclrSettings.miniGameDllNames.Add(_newHybridMiniGameDllName);
                        _newHybridMiniGameDllName = "";
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
                SaveHybridClrSettings();
            }

            if (GUILayout.Button("重置为默认值", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("确认", "确定要重置为默认值吗？", "确定", "取消"))
                {
                    _hybridclrSettings.ResetToDefaults();
                    EditorUtility.SetDirty(_hybridclrSettings);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("提示", "已重置为默认值", "确定");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
        }

        private static void DrawHybridClrHeaderActions()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("快捷操作", EditorStyles.boldLabel);
            GUILayout.Label(HybridClrHeaderTip, GUILayout.Width(18));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("一键生成所有", GUILayout.Height(28)))
            {
                HybridCLRDllCopyUtils.GenerateAll();
            }

            if (GUILayout.Button("自动配置热更新程序集", GUILayout.Height(28)))
            {
                AutoConfigureHotUpdateAssemblies.Configure();
            }

            if (GUILayout.Button("拷贝AOT元数据", GUILayout.Height(28)))
            {
                HybridCLRDllCopyUtils.GenerateAOTMetadata();
            }

            if (GUILayout.Button("拷贝dll.bytes", GUILayout.Height(28)))
            {
                HybridCLRDllCopyUtils.GenerateHotUpdate();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void SaveHybridClrSettings()
        {
            if (_hybridclrSettings == null)
            {
                EditorUtility.DisplayDialog("错误", "配置为空，无法保存", "确定");
                return;
            }

            EditorUtility.SetDirty(_hybridclrSettings);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("提示", "配置已保存", "确定");
            Debug.Log("[HybridCLR] 配置已保存");
        }

        /// <summary>
        /// 绘制配置项。
        /// </summary>
        private void DrawConfigItem(GamePackageConfig config, int index)
        {
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField($"• {config.GameName}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"DLL: {config.DllPath}", GUILayout.Width(300));
            EditorGUILayout.LabelField($"Packages: {string.Join(", ", config.SubPackages.ConvertAll(sp => sp.PackageName))}");
            
            if (_selectedGameIndex == index)
            {
                // 正在编辑此项，显示"取消编辑"按钮
                if (GUILayout.Button("取消编辑", GUILayout.Width(80)))
                {
                    ClearEditArea();
                    SaveUIPreferences();
                }
            }
            else
            {
                // 未编辑此项，显示"编辑"按钮
                if (GUILayout.Button("编辑", GUILayout.Width(60)))
                {
                    _isAddingNewGame = false;
                    _selectedGameIndex = index;
                    _editingGameName = config.GameName;
                    _editingDllPath = config.DllPath;
                    _editingSubPackages = new List<SubPackageConfig>();
                    foreach (var sp in config.SubPackages)
                    {
                        _editingSubPackages.Add(new SubPackageConfig(sp.PackageName, sp.AppendExtension));
                    }
                    SaveUIPreferences(); // 编辑状态改变时立即保存
                }
            }

            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                _gameConfigs.RemoveAt(index);
                if (_selectedGameIndex == index)
                {
                    ClearEditArea();
                }
                else if (_selectedGameIndex > index)
                {
                    _selectedGameIndex--;
                }
                SaveUIPreferences();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制编辑区域。
        /// </summary>
        private void DrawEditArea()
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GameName:", GUILayout.Width(100));
            string newEditingGameName = EditorGUILayout.TextField(_editingGameName);
            if (newEditingGameName != _editingGameName)
            {
                _editingGameName = newEditingGameName;
                SaveUIPreferences(); // 编辑内容改变时立即保存
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("DLL 路径:", GUILayout.Width(100));
            string newEditingDllPath = EditorGUILayout.TextField(_editingDllPath);
            if (newEditingDllPath != _editingDllPath)
            {
                _editingDllPath = newEditingDllPath;
                SaveUIPreferences(); // 编辑内容改变时立即保存
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("PackageNames:");
            EditorGUI.indentLevel++;

            for (int i = 0; i < _editingSubPackages.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                string newPackageName = EditorGUILayout.TextField("包名:", _editingSubPackages[i].PackageName);
                bool newAppendExtension = EditorGUILayout.Toggle("AppendExtension:", _editingSubPackages[i].AppendExtension);
                
                if (newPackageName != _editingSubPackages[i].PackageName || newAppendExtension != _editingSubPackages[i].AppendExtension)
                {
                    _editingSubPackages[i].PackageName = newPackageName;
                    _editingSubPackages[i].AppendExtension = newAppendExtension;
                    SaveUIPreferences(); // 编辑内容改变时立即保存
                }
                
                if (GUILayout.Button("删除", GUILayout.Width(60)))
                {
                    _editingSubPackages.RemoveAt(i);
                    i--;
                    SaveUIPreferences(); // 删除时立即保存
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("添加 Package", GUILayout.Width(120)))
            {
                _editingSubPackages.Add(new SubPackageConfig("", false));
                SaveUIPreferences(); // 添加时立即保存
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存", GUILayout.Width(80)))
            {
                SaveEditConfig();
            }
            if (GUILayout.Button("取消", GUILayout.Width(80)))
            {
                ClearEditArea();
                SaveUIPreferences(); // 取消时立即保存
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 从 XML 加载配置。
        /// </summary>
        private void LoadConfigsFromXml()
        {
            _gameConfigs = GamePackageMappingGenerator.ReadFromXml();
            Debug.Log($"[AutomatedBuildToolWindow] 从 XML 加载了 {_gameConfigs.Count} 个游戏配置");
        }

        /// <summary>
        /// 生成配置到 XML。
        /// </summary>
        private void GenerateConfigToXml()
        {
            if (GamePackageMappingGenerator.GenerateToXml(_gameConfigs))
            {
                Debug.Log("[AutomatedBuildToolWindow] 配置已成功生成到 GamePackageMapping.xml");
            }
            else
            {
                EditorUtility.DisplayDialog("失败", "生成配置失败，请查看 Console 日志", "确定");
            }
        }

        /// <summary>
        /// 保存编辑的配置。
        /// </summary>
        private void SaveEditConfig()
        {
            if (string.IsNullOrEmpty(_editingGameName))
            {
                EditorUtility.DisplayDialog("错误", "GameName 不能为空", "确定");
                return;
            }

            if (_editingSubPackages.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "至少需要添加一个 Package", "确定");
                return;
            }

            GamePackageConfig config = new GamePackageConfig(_editingGameName, _editingDllPath);
            foreach (var sp in _editingSubPackages)
            {
                if (!string.IsNullOrEmpty(sp.PackageName))
                {
                    config.SubPackages.Add(new SubPackageConfig(sp.PackageName, sp.AppendExtension));
                }
            }

            if (_selectedGameIndex >= 0 && _selectedGameIndex < _gameConfigs.Count)
            {
                // 更新现有配置
                _gameConfigs[_selectedGameIndex] = config;
            }
            else
            {
                // 添加新配置
                _gameConfigs.Add(config);
            }

            ClearEditArea();
            SaveUIPreferences(); // 保存配置后更新UI偏好设置
            Debug.Log($"[AutomatedBuildToolWindow] 保存配置: {config.GameName}");
        }

        /// <summary>
        /// 清空编辑区域。
        /// </summary>
        private void ClearEditArea()
        {
            _selectedGameIndex = -1;
            _isAddingNewGame = false;
            _editingGameName = "";
            _editingDllPath = "";
            _editingSubPackages.Clear();
        }

        /// <summary>
        /// 加载UI偏好设置。
        /// </summary>
        private void LoadUIPreferences()
        {
            // 加载选中的标签页
            _selectedTab = EditorPrefs.GetInt(PrefKeySelectedTab, 0);

            // 加载游戏的选中状态
            string selectedGamesJson = EditorPrefs.GetString(PrefKeySelectedGames, "");
            if (!string.IsNullOrEmpty(selectedGamesJson))
            {
                try
                {
                    _selectedGames = JsonUtility.FromJson<SerializableDictionary<string, bool>>(selectedGamesJson).ToDictionary();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AutomatedBuildToolWindow] 加载选中状态失败: {ex.Message}");
                    _selectedGames = new Dictionary<string, bool>();
                }
            }

            // 加载游戏的展开状态
            string expandedGamesJson = EditorPrefs.GetString(PrefKeyExpandedGames, "");
            if (!string.IsNullOrEmpty(expandedGamesJson))
            {
                try
                {
                    _expandedGames = JsonUtility.FromJson<SerializableDictionary<string, bool>>(expandedGamesJson).ToDictionary();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AutomatedBuildToolWindow] 加载展开状态失败: {ex.Message}");
                    _expandedGames = new Dictionary<string, bool>();
                }
            }

            // 加载配置页签的编辑状态
            _selectedGameIndex = EditorPrefs.GetInt(PrefKeySelectedGameIndex, -1);
            _isAddingNewGame = EditorPrefs.GetBool(PrefKeyIsAddingNewGame, false);
            _editingGameName = EditorPrefs.GetString(PrefKeyEditingGameName, "");
            _editingDllPath = EditorPrefs.GetString(PrefKeyEditingDllPath, "");

            // 加载编辑中的子包列表
            string editingSubPackagesJson = EditorPrefs.GetString(PrefKeyEditingSubPackages, "");
            if (!string.IsNullOrEmpty(editingSubPackagesJson))
            {
                try
                {
                    _editingSubPackages = JsonUtility.FromJson<SerializableList<SubPackageConfig>>(editingSubPackagesJson).ToList();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AutomatedBuildToolWindow] 加载编辑子包列表失败: {ex.Message}");
                    _editingSubPackages = new List<SubPackageConfig>();
                }
            }
        }

        /// <summary>
        /// 保存UI偏好设置。
        /// </summary>
        private void SaveUIPreferences()
        {
            // 保存选中的标签页
            EditorPrefs.SetInt(PrefKeySelectedTab, _selectedTab);

            // 保存游戏的选中状态
            try
            {
                string selectedGamesJson = JsonUtility.ToJson(new SerializableDictionary<string, bool>(_selectedGames));
                EditorPrefs.SetString(PrefKeySelectedGames, selectedGamesJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AutomatedBuildToolWindow] 保存选中状态失败: {ex.Message}");
            }

            // 保存游戏的展开状态
            try
            {
                string expandedGamesJson = JsonUtility.ToJson(new SerializableDictionary<string, bool>(_expandedGames));
                EditorPrefs.SetString(PrefKeyExpandedGames, expandedGamesJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AutomatedBuildToolWindow] 保存展开状态失败: {ex.Message}");
            }

            // 保存配置页签的编辑状态
            EditorPrefs.SetInt(PrefKeySelectedGameIndex, _selectedGameIndex);
            EditorPrefs.SetBool(PrefKeyIsAddingNewGame, _isAddingNewGame);
            EditorPrefs.SetString(PrefKeyEditingGameName, _editingGameName);
            EditorPrefs.SetString(PrefKeyEditingDllPath, _editingDllPath);

            // 保存编辑中的子包列表
            try
            {
                string editingSubPackagesJson = JsonUtility.ToJson(new SerializableList<SubPackageConfig>(_editingSubPackages));
                EditorPrefs.SetString(PrefKeyEditingSubPackages, editingSubPackagesJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AutomatedBuildToolWindow] 保存编辑子包列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加新游戏。
        /// </summary>
        private void AddNewGame()
        {
            _selectedGameIndex = -1;
            _isAddingNewGame = true;
            _editingGameName = "";
            _editingDllPath = "";
            _editingSubPackages.Clear();
            _editingSubPackages.Add(new SubPackageConfig("", false));
            SaveUIPreferences();
        }

        /// <summary>
        /// 检查是否有选中的游戏。
        /// </summary>
        private bool HasSelectedGames()
        {
            foreach (var kvp in _selectedGames)
            {
                if (kvp.Value)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取选中的游戏列表。
        /// </summary>
        private List<GamePackageConfig> GetSelectedGames()
        {
            List<GamePackageConfig> selected = new List<GamePackageConfig>();
            foreach (var config in _gameConfigs)
            {
                if (_selectedGames.ContainsKey(config.GameName) && _selectedGames[config.GameName])
                {
                    selected.Add(config);
                }
            }
            return selected;
        }

        /// <summary>
        /// 将构建生成的资源拷贝到平台名字对应的目录。
        /// </summary>
        private void CopyPackagesToPlatformDirectory(List<string> packageNames, string version)
        {
            if (string.IsNullOrEmpty(_settings.PlatformName))
            {
                Debug.LogWarning("[AutomatedBuildToolWindow] 平台名称为空，跳过资源拷贝");
                return;
            }

            try
            {
                string buildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
                BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
                string platformDirectory = $"{buildOutputRoot}/{buildTarget}/{_settings.PlatformName}";

                // 如果平台目录存在，先删除
                if (Directory.Exists(platformDirectory))
                {
                    Debug.Log($"[AutomatedBuildToolWindow] 检测到已存在的平台目录，正在删除: {platformDirectory}");
                    Directory.Delete(platformDirectory, true);
                }

                // 创建平台目录
                Directory.CreateDirectory(platformDirectory);
                Debug.Log($"[AutomatedBuildToolWindow] 创建平台目录: {platformDirectory}");

                // 拷贝每个包的输出目录
                int copySuccessCount = 0;
                foreach (string packageName in packageNames)
                {
                    string sourceDirectory = $"{buildOutputRoot}/{buildTarget}/{packageName}/{version}";
                    string targetDirectory = $"{platformDirectory}/{packageName}";

                    if (Directory.Exists(sourceDirectory))
                    {
                        try
                        {
                            // 确保目标目录的父目录存在
                            string targetParentDir = Path.GetDirectoryName(targetDirectory);
                            if (!string.IsNullOrEmpty(targetParentDir) && !Directory.Exists(targetParentDir))
                            {
                                Directory.CreateDirectory(targetParentDir);
                            }

                            // 拷贝整个目录
                            CopyDirectory(sourceDirectory, targetDirectory);
                            Debug.Log($"[AutomatedBuildToolWindow] 成功拷贝包 {packageName} 到平台目录: {targetDirectory}");
                            copySuccessCount++;
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[AutomatedBuildToolWindow] 拷贝包 {packageName} 失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[AutomatedBuildToolWindow] 源目录不存在，跳过拷贝: {sourceDirectory}");
                    }
                }

                Debug.Log($"[AutomatedBuildToolWindow] 资源拷贝完成: 成功 {copySuccessCount}/{packageNames.Count} 个包");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AutomatedBuildToolWindow] 拷贝资源到平台目录失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 生成增量更新包。
        /// </summary>
        private void GenerateIncrementalPatch(List<string> packageNames, string previousVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(previousVersion))
            {
                Debug.Log("[AutomatedBuildToolWindow] 上一个版本号为空，跳过增量补丁生成");
                return;
            }

            try
            {
                string buildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
                BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;

                int totalChangedCount = 0;
                int totalNewCount = 0;
                int successCount = 0;

                foreach (string packageName in packageNames)
                {
                    try
                    {
                        // 构建 manifest 文件路径
                        string previousManifestFileName = YooAssetSettingsData.GetManifestBinaryFileName(packageName, previousVersion);
                        string currentManifestFileName = YooAssetSettingsData.GetManifestBinaryFileName(packageName, currentVersion);
                        
                        string previousManifestPath = $"{buildOutputRoot}/{buildTarget}/{packageName}/{previousVersion}/{previousManifestFileName}";
                        string currentManifestPath = $"{buildOutputRoot}/{buildTarget}/{packageName}/{currentVersion}/{currentManifestFileName}";

                        // 检查 manifest 文件是否存在
                        if (!File.Exists(previousManifestPath))
                        {
                            Debug.LogWarning($"[AutomatedBuildToolWindow] 上一个版本的 manifest 文件不存在: {previousManifestPath}");
                            continue;
                        }

                        if (!File.Exists(currentManifestPath))
                        {
                            Debug.LogWarning($"[AutomatedBuildToolWindow] 当前版本的 manifest 文件不存在: {currentManifestPath}");
                            continue;
                        }

                        // 加载 manifest 文件
                        byte[] previousBytes = FileUtility.ReadAllBytes(previousManifestPath);
                        byte[] currentBytes = FileUtility.ReadAllBytes(currentManifestPath);
                        
                        PackageManifest previousManifest = ManifestTools.DeserializeFromBinary(previousBytes, null);
                        PackageManifest currentManifest = ManifestTools.DeserializeFromBinary(currentBytes, null);

                        if (previousManifest == null || currentManifest == null)
                        {
                            Debug.LogError($"[AutomatedBuildToolWindow] 加载 manifest 文件失败: {packageName}");
                            continue;
                        }

                        // 对比差异
                        List<PackageBundle> changeList = new List<PackageBundle>();
                        List<PackageBundle> newList = new List<PackageBundle>();
                        CompareManifests(previousManifest, currentManifest, changeList, newList);

                        totalChangedCount += changeList.Count;
                        totalNewCount += newList.Count;

                        Debug.Log($"[AutomatedBuildToolWindow] 包 '{packageName}' 差异: 变更 {changeList.Count} 个, 新增 {newList.Count} 个");

                        // 构建 Patch 目录
                        string patchDirectory = $"{buildOutputRoot}/{buildTarget}/{packageName}/Patch/{currentVersion}";
                        if (!Directory.Exists(patchDirectory))
                        {
                            Directory.CreateDirectory(patchDirectory);
                        }

                        // 拷贝 manifest 相关文件
                        string currentPackageDirectory = $"{buildOutputRoot}/{buildTarget}/{packageName}/{currentVersion}";
                        CopyManifestFiles(packageName, currentVersion, currentPackageDirectory, patchDirectory);

                        // 拷贝增量 bundle 文件
                        CopyIncrementalBundles(currentPackageDirectory, patchDirectory, changeList, newList);

                        // 拷贝 patch 到平台目录
                        if (!string.IsNullOrEmpty(_settings.PlatformName))
                        {
                            string platformPackageDirectory = $"{buildOutputRoot}/{buildTarget}/{_settings.PlatformName}/{packageName}";
                            try
                            {
                                // 确保目标目录的父目录存在
                                string targetParentDir = Path.GetDirectoryName(platformPackageDirectory);
                                if (!string.IsNullOrEmpty(targetParentDir) && !Directory.Exists(targetParentDir))
                                {
                                    Directory.CreateDirectory(targetParentDir);
                                }

                                // 拷贝整个 patch 目录到平台目录（覆盖现有文件）
                                CopyDirectory(patchDirectory, platformPackageDirectory);
                                Debug.Log($"[AutomatedBuildToolWindow] 成功拷贝 patch 到平台目录: {platformPackageDirectory}");
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[AutomatedBuildToolWindow] 拷贝 patch 到平台目录失败: {ex.Message}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[AutomatedBuildToolWindow] 平台名称为空，跳过 patch 拷贝到平台目录");
                        }

                        successCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[AutomatedBuildToolWindow] 处理包 '{packageName}' 的增量补丁失败: {ex.Message}");
                    }
                }

                Debug.Log($"[AutomatedBuildToolWindow] 增量补丁生成完成: 成功 {successCount}/{packageNames.Count} 个包, 总变更 {totalChangedCount} 个, 总新增 {totalNewCount} 个");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AutomatedBuildToolWindow] 生成增量补丁失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 对比两个 manifest，找出差异。
        /// </summary>
        private void CompareManifests(PackageManifest previousManifest, PackageManifest currentManifest, List<PackageBundle> changeList, List<PackageBundle> newList)
        {
            changeList.Clear();
            newList.Clear();

            // 遍历当前版本的资源包
            foreach (PackageBundle bundle2 in currentManifest.BundleList)
            {
                if (previousManifest.TryGetPackageBundleByBundleName(bundle2.BundleName, out PackageBundle bundle1))
                {
                    // 如果文件哈希不同，说明资源包已变更
                    if (bundle2.FileHash != bundle1.FileHash)
                    {
                        changeList.Add(bundle2);
                    }
                }
                else
                {
                    // 如果上一个版本中不存在，说明是新增的资源包
                    newList.Add(bundle2);
                }
            }

            // 按字母重新排序
            changeList.Sort((x, y) => string.Compare(x.BundleName, y.BundleName));
            newList.Sort((x, y) => string.Compare(x.BundleName, y.BundleName));
        }

        /// <summary>
        /// 拷贝 manifest 相关文件。
        /// </summary>
        private void CopyManifestFiles(string packageName, string version, string sourceDirectory, string targetDirectory)
        {
            try
            {
                // 拷贝 manifest 二进制文件
                string manifestFileName = YooAssetSettingsData.GetManifestBinaryFileName(packageName, version);
                CopyFileIfExists($"{sourceDirectory}/{manifestFileName}", $"{targetDirectory}/{manifestFileName}");

                // 拷贝 manifest 哈希文件
                string hashFileName = YooAssetSettingsData.GetPackageHashFileName(packageName, version);
                CopyFileIfExists($"{sourceDirectory}/{hashFileName}", $"{targetDirectory}/{hashFileName}");

                // 拷贝版本文件
                string versionFileName = YooAssetSettingsData.GetPackageVersionFileName(packageName);
                CopyFileIfExists($"{sourceDirectory}/{versionFileName}", $"{targetDirectory}/{versionFileName}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AutomatedBuildToolWindow] 拷贝 manifest 文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 拷贝增量 bundle 文件。
        /// </summary>
        private void CopyIncrementalBundles(string sourceDirectory, string targetDirectory, List<PackageBundle> changeList, List<PackageBundle> newList)
        {
            // 合并变更和新增列表
            List<PackageBundle> allBundles = new List<PackageBundle>();
            allBundles.AddRange(changeList);
            allBundles.AddRange(newList);

            if (allBundles.Count == 0)
            {
                Debug.Log("[AutomatedBuildToolWindow] 没有增量资源");
                return;
            }

            int copiedCount = 0;
            foreach (PackageBundle bundle in allBundles)
            {
                try
                {
                    string sourceFilePath = $"{sourceDirectory}/{bundle.FileName}";
                    string targetFilePath = $"{targetDirectory}/{bundle.FileName}";

                    CopyFileIfExists(sourceFilePath, targetFilePath);
                    copiedCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[AutomatedBuildToolWindow] 拷贝 bundle 文件失败: {bundle.FileName}, 错误: {ex.Message}");
                }
            }

            Debug.Log($"[AutomatedBuildToolWindow] 增量 bundle 文件拷贝完成: {copiedCount}/{allBundles.Count} 个文件");
        }

        /// <summary>
        /// 如果源文件存在则拷贝。
        /// </summary>
        private void CopyFileIfExists(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[AutomatedBuildToolWindow] 源文件不存在，跳过拷贝: {sourcePath}");
                return;
            }

            // 确保目标目录存在
            string targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            File.Copy(sourcePath, targetPath, true);
        }

        /// <summary>
        /// 递归拷贝目录。
        /// </summary>
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            // 拷贝所有文件
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            // 递归拷贝所有子目录
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDir);
                string targetSubDir = Path.Combine(targetDir, subDirName);
                CopyDirectory(subDir, targetSubDir);
            }
        }

        /// <summary>
        /// 打母包。
        /// </summary>
        private void BuildMainPackage()
        {
            Debug.Log("[AutomatedBuildToolWindow] 开始打母包...");
            List<GamePackageConfig> selectedGames = GetSelectedGames();
            if (selectedGames.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请至少选择一个游戏", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认", $"确定要打母包吗？\n选中游戏: {string.Join(", ", selectedGames.ConvertAll(g => g.GameName))}\n使用版本号: {_settings.MainPackageVersion}", "确定", "取消"))
            {
                return;
            }

            try
            {
                // 1. 根据配置决定是否执行预构建步骤
                if (_settings.RebuildDll)
                {
                    Debug.Log("[AutomatedBuildToolWindow] 重新编译DLL已启用，执行预构建步骤...");
                    BuildToolExecutor.ExecutePreBuildSteps();
                }
                else
                {
                    Debug.Log("[AutomatedBuildToolWindow] 重新编译DLL已禁用，跳过预构建步骤，直接进行打包");
                }

                // 2. 生成 XML 配置
                GenerateConfigToXml();

                // 3. 构建所有选中的包
                List<string> allPackageNames = new List<string>();
                foreach (var gameConfig in selectedGames)
                {
                    foreach (var subPackage in gameConfig.SubPackages)
                    {
                        if (!string.IsNullOrEmpty(subPackage.PackageName))
                        {
                            allPackageNames.Add(subPackage.PackageName);
                        }
                    }
                }

                int successCount = BuildToolExecutor.BuildPackages(allPackageNames, _settings.MainPackageVersion, isMainPackage: true);

                if (successCount == allPackageNames.Count)
                {
                    Debug.Log($"[AutomatedBuildToolWindow] 母包构建完成！成功构建 {successCount} 个包");
                }
                else
                {
                    Debug.LogWarning($"[AutomatedBuildToolWindow] 母包构建完成，但有部分失败。成功: {successCount}/{allPackageNames.Count}");
                }

                // 4. 将构建生成的资源拷贝到平台名字对应的目录
                if (successCount > 0 && !string.IsNullOrEmpty(_settings.PlatformName))
                {
                    CopyPackagesToPlatformDirectory(allPackageNames, _settings.MainPackageVersion);
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"构建失败: {ex.Message}", "确定");
                Debug.LogError($"[AutomatedBuildToolWindow] 构建失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 打更新包。
        /// </summary>
        private void BuildUpdatePackage()
        {
            Debug.Log("[AutomatedBuildToolWindow] 开始打更新包...");
            List<GamePackageConfig> selectedGames = GetSelectedGames();
            if (selectedGames.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请至少选择一个游戏", "确定");
                return;
            }

            try
            {
                // 1. 根据配置决定是否执行预构建步骤
                if (_settings.RebuildDll)
                {
                    Debug.Log("[AutomatedBuildToolWindow] 重新编译DLL已启用，执行预构建步骤...");
                    BuildToolExecutor.ExecutePreBuildSteps();
                }
                else
                {
                    Debug.Log("[AutomatedBuildToolWindow] 重新编译DLL已禁用，跳过预构建步骤，直接进行打包");
                }

                // 2. 生成 XML 配置
                GenerateConfigToXml();

                // 3. 构建所有选中的包
                List<string> allPackageNames = new List<string>();
                foreach (var gameConfig in selectedGames)
                {
                    foreach (var subPackage in gameConfig.SubPackages)
                    {
                        if (!string.IsNullOrEmpty(subPackage.PackageName))
                        {
                            allPackageNames.Add(subPackage.PackageName);
                        }
                    }
                }

                int successCount = BuildToolExecutor.BuildPackages(allPackageNames, _settings.Version, isMainPackage: false);

                if (successCount == allPackageNames.Count)
                {
                    Debug.Log($"[AutomatedBuildToolWindow] 更新包构建完成！成功构建 {successCount} 个包");
                }
                else
                {
                    Debug.LogWarning($"[AutomatedBuildToolWindow] 更新包构建完成，但有部分失败。成功: {successCount}/{allPackageNames.Count}");
                }

                // 4. 生成增量补丁包
                if (successCount > 0)
                {
                    GenerateIncrementalPatch(allPackageNames, _settings.PreviousVersion, _settings.Version);
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"构建失败: {ex.Message}", "确定");
                Debug.LogError($"[AutomatedBuildToolWindow] 构建失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static bool TryIncreaseVersion(string currentVersion, out string increasedVersion)
        {
            increasedVersion = currentVersion;
            if (string.IsNullOrEmpty(currentVersion))
            {
                return false;
            }

            string[] parts = currentVersion.Split('.');
            if (parts.Length == 0)
            {
                return false;
            }

            int lastIndex = parts.Length - 1;
            int lastNumber;
            if (!int.TryParse(parts[lastIndex], out lastNumber))
            {
                return false;
            }

            lastNumber++;
            parts[lastIndex] = lastNumber.ToString();
            increasedVersion = string.Join(".", parts);
            return true;
        }

        private static bool TryDecreaseVersion(string currentVersion, out string decreasedVersion)
        {
            decreasedVersion = currentVersion;
            if (string.IsNullOrEmpty(currentVersion))
            {
                return false;
            }

            string[] parts = currentVersion.Split('.');
            if (parts.Length == 0)
            {
                return false;
            }

            int lastIndex = parts.Length - 1;
            int lastNumber;
            if (!int.TryParse(parts[lastIndex], out lastNumber))
            {
                return false;
            }

            if (lastNumber <= 0)
            {
                return false;
            }

            lastNumber--;
            parts[lastIndex] = lastNumber.ToString();
            decreasedVersion = string.Join(".", parts);
            return true;
        }
    }

    /// <summary>
    /// 可序列化的字典（用于 EditorPrefs）。
    /// </summary>
    [System.Serializable]
    public class SerializableDictionary<TKey, TValue>
    {
        [System.Serializable]
        public class KeyValuePair
        {
            public TKey key;
            public TValue value;
        }

        public List<KeyValuePair> items = new List<KeyValuePair>();

        public SerializableDictionary()
        {
        }

        public SerializableDictionary(Dictionary<TKey, TValue> dict)
        {
            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    items.Add(new KeyValuePair { key = kvp.Key, value = kvp.Value });
                }
            }
        }

        public Dictionary<TKey, TValue> ToDictionary()
        {
            Dictionary<TKey, TValue> dict = new Dictionary<TKey, TValue>();
            foreach (var item in items)
            {
                if (item.key != null)
                {
                    dict[item.key] = item.value;
                }
            }
            return dict;
        }
    }

    /// <summary>
    /// 可序列化的列表（用于 EditorPrefs）。
    /// </summary>
    [System.Serializable]
    public class SerializableList<T>
    {
        public List<T> items = new List<T>();

        public SerializableList()
        {
        }

        public SerializableList(List<T> list)
        {
            if (list != null)
            {
                items = new List<T>(list);
            }
        }

        public List<T> ToList()
        {
            return new List<T>(items);
        }
    }
}
#endif

