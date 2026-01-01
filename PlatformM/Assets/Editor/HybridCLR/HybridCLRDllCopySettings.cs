#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Astorise.Editor.HybridCLR
{
    /// <summary>
    /// HybridCLR DLL 拷贝工具配置。
    /// </summary>
    [CreateAssetMenu(fileName = "HybridCLRDllCopySettings", menuName = "HybridCLR/DLL Copy Settings", order = 1)]
    public class HybridCLRDllCopySettings : ScriptableObject
    {
        /// <summary>AOT Metadata 源目录</summary>
        public string aotMetadataSourceDir = "HybridCLRData/AssembliesPostIl2CppStrip";

        /// <summary>AOT Metadata 目标目录</summary>
        public string aotMetadataTargetDir = "Assets/Framework/Dll/AOTMetadata";

        /// <summary>HotUpdate 源目录</summary>
        public string hotUpdateSourceDir = "HybridCLRData/HotUpdateDlls";

        /// <summary>HotUpdate 目标目录（Framework DLL 使用）</summary>
        public string hotUpdateTargetDir = "Assets/Framework/Dll/HotUpdate";

        /// <summary>Framework HotUpdate DLL 名称列表（拷贝到 Framework 目录）</summary>
        public List<string> frameworkDllNames = new List<string> { "Framework" };

        /// <summary>小游戏 HotUpdate DLL 名称列表（拷贝到各自游戏目录）</summary>
        public List<string> miniGameDllNames = new List<string> { "BBQHotUpdate" };

        /// <summary>HotUpdate DLL 名称列表（已废弃，保留用于向后兼容）</summary>
        [System.Obsolete("使用 frameworkDllNames 和 miniGameDllNames 代替")]
        public List<string> hotUpdateDllNames
        {
            get
            {
                // 向后兼容：返回合并后的列表
                var merged = new List<string>();
                if (frameworkDllNames != null) merged.AddRange(frameworkDllNames);
                if (miniGameDllNames != null) merged.AddRange(miniGameDllNames);
                return merged;
            }
            set
            {
                // 向后兼容：如果设置了旧列表，自动迁移到新列表
                if (value != null && value.Count > 0)
                {
                    if (frameworkDllNames == null) frameworkDllNames = new List<string>();
                    if (miniGameDllNames == null) miniGameDllNames = new List<string>();

                    // 清空现有列表（避免重复）
                    frameworkDllNames.Clear();
                    miniGameDllNames.Clear();

                    // 根据名称判断：Framework 相关的放到 frameworkDllNames，其他放到 miniGameDllNames
                    foreach (string dllName in value)
                    {
                        if (string.IsNullOrEmpty(dllName)) continue;

                        // Framework 相关的 DLL 放到 Framework 列表
                        if (dllName.Equals("Framework", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!frameworkDllNames.Contains(dllName))
                                frameworkDllNames.Add(dllName);
                        }
                        else
                        {
                            // 其他 DLL 假设是小游戏 DLL
                            if (!miniGameDllNames.Contains(dllName))
                                miniGameDllNames.Add(dllName);
                        }
                    }

                    Debug.Log($"[HybridCLRDllCopySettings] 已从旧配置迁移: Framework={frameworkDllNames.Count}, MiniGame={miniGameDllNames.Count}");
                }
            }
        }

        private const string SettingsPath = "Assets/Editor/HybridCLR/HybridCLRDllCopySettings.asset";

        /// <summary>
        /// 获取或创建配置实例。
        /// </summary>
        public static HybridCLRDllCopySettings GetOrCreateSettings()
        {
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<HybridCLRDllCopySettings>(SettingsPath);
            if (settings == null)
            {
                settings = CreateInstance<HybridCLRDllCopySettings>();
                string directory = System.IO.Path.GetDirectoryName(SettingsPath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }
                UnityEditor.AssetDatabase.CreateAsset(settings, SettingsPath);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            return settings;
        }

        /// <summary>
        /// 重置为默认值。
        /// </summary>
        public void ResetToDefaults()
        {
            aotMetadataSourceDir = "HybridCLRData/AssembliesPostIl2CppStrip";
            aotMetadataTargetDir = "Assets/Framework/Dll/AOTMetadata";
            hotUpdateSourceDir = "HybridCLRData/HotUpdateDlls";
            hotUpdateTargetDir = "Assets/Framework/Dll/HotUpdate";
            
            if (frameworkDllNames == null) frameworkDllNames = new List<string>();
            if (miniGameDllNames == null) miniGameDllNames = new List<string>();
            
            frameworkDllNames.Clear();
            frameworkDllNames.AddRange(new[] { "Framework" });
            
            miniGameDllNames.Clear();
            miniGameDllNames.AddRange(new[] { "BBQHotUpdate" });
        }
    }
}
#endif

