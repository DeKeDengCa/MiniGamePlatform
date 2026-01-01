#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Astorise.Editor.Package
{
    /// <summary>
    /// 自动化打包工具配置。
    /// </summary>
    [CreateAssetMenu(fileName = "BuildToolSettings", menuName = "Build Tool/Settings", order = 1)]
    public class BuildToolSettings : ScriptableObject
    {
        [Header("版本配置")]
        /// <summary>
        /// 用户输入的版本号（用于更新包）
        /// </summary>
        public string Version = "1.0.0";

        /// <summary>
        /// 上一个版本号（用于增量更新包对比）
        /// </summary>
        public string PreviousVersion = "";

        [Header("构建配置")]
        /// <summary>
        /// 是否重新编译DLL（如果勾选，会执行预构建步骤包括Auto Configure HotUpdate Assemblies和Generate HybridCLR files）
        /// </summary>
        public bool RebuildDll = true;

        /// <summary>
        /// 平台名称（用于母包构建后的资源拷贝）
        /// </summary>
        public string PlatformName = "AstroRise";

        [Header("母包配置")]
        /// <summary>
        /// 母包路径
        /// </summary>
        public string MainPackagePath = "C:\\Work\\platform\\Bundles\\StandaloneWindows64";

        /// <summary>
        /// 母包版本号
        /// </summary>
        public string MainPackageVersion = "1.0.0";

        /// <summary>
        /// 获取当前打包的版本号（用于显示，与 Version 相同）
        /// </summary>
        public string CurrentBuildVersion => Version;

        private const string SettingsPath = "Assets/Editor/Package/BuildToolSettings.asset";

        /// <summary>
        /// 获取或创建配置实例。
        /// </summary>
        public static BuildToolSettings GetOrCreateSettings()
        {
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildToolSettings>(SettingsPath);
            if (settings == null)
            {
                settings = CreateInstance<BuildToolSettings>();
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
    }
}
#endif

