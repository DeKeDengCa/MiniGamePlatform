using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Astorise.Editor.Package
{
    /// <summary>
    /// GameName 和 PackageName 映射配置的数据模型。
    /// </summary>
    [Serializable]
    public class GamePackageConfig
    {
        /// <summary>
        /// 游戏名称
        /// </summary>
        public string GameName;

        /// <summary>
        /// DLL 路径
        /// </summary>
        public string DllPath;

        /// <summary>
        /// 子包配置列表
        /// </summary>
        public List<SubPackageConfig> SubPackages = new List<SubPackageConfig>();

        public GamePackageConfig()
        {
        }

        public GamePackageConfig(string gameName, string dllPath)
        {
            GameName = gameName;
            DllPath = dllPath;
        }
    }

    /// <summary>
    /// 子包配置数据模型。
    /// </summary>
    [Serializable]
    public class SubPackageConfig
    {
        /// <summary>
        /// 包名称
        /// </summary>
        public string PackageName;

        /// <summary>
        /// 是否追加文件扩展名
        /// </summary>
        public bool AppendExtension;

        public SubPackageConfig()
        {
        }

        public SubPackageConfig(string packageName, bool appendExtension)
        {
            PackageName = packageName;
            AppendExtension = appendExtension;
        }
    }

    /// <summary>
    /// GamePackageMapping.xml 配置生成器。
    /// 提供读取和生成 XML 配置的功能。
    /// </summary>
    public static class GamePackageMappingGenerator
    {
        /// <summary>
        /// XML 配置文件路径
        /// </summary>
        private const string ConfigFilePath = "Assets/Framework/Config/GamePackageMapping.xml";

        /// <summary>
        /// 从 XML 文件读取配置。
        /// </summary>
        /// <returns>配置列表，如果文件不存在或解析失败则返回空列表</returns>
        public static List<GamePackageConfig> ReadFromXml()
        {
            List<GamePackageConfig> configs = new List<GamePackageConfig>();

            string fullPath = Path.Combine(Application.dataPath, "Framework/Config/GamePackageMapping.xml");
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[GamePackageMappingGenerator] XML 文件不存在: {fullPath}");
                return configs;
            }

            try
            {
                XDocument doc = XDocument.Load(fullPath);
                XElement root = doc.Root;

                if (root == null || root.Name != "ProjectConfig")
                {
                    Debug.LogError("[GamePackageMappingGenerator] XML 根节点无效");
                    return configs;
                }

                foreach (XElement packageElement in root.Elements("Package"))
                {
                    XAttribute nameAttr = packageElement.Attribute("Name");
                    XAttribute dllAttr = packageElement.Attribute("Dll");

                    if (nameAttr == null || string.IsNullOrEmpty(nameAttr.Value))
                    {
                        Debug.LogWarning("[GamePackageMappingGenerator] 跳过无效的 Package 元素（缺少 Name 属性）");
                        continue;
                    }

                    GamePackageConfig config = new GamePackageConfig(
                        nameAttr.Value,
                        dllAttr?.Value ?? string.Empty
                    );

                    // 读取 SubPackage 元素
                    foreach (XElement subPackageElement in packageElement.Elements("SubPackage"))
                    {
                        XAttribute subNameAttr = subPackageElement.Attribute("Name");
                        XAttribute appendExtAttr = subPackageElement.Attribute("AppendExtension");

                        if (subNameAttr == null || string.IsNullOrEmpty(subNameAttr.Value))
                        {
                            Debug.LogWarning($"[GamePackageMappingGenerator] 跳过无效的 SubPackage 元素（GameName: {config.GameName}）");
                            continue;
                        }

                        bool appendExtension = false;
                        if (appendExtAttr != null && bool.TryParse(appendExtAttr.Value, out bool parsed))
                        {
                            appendExtension = parsed;
                        }

                        config.SubPackages.Add(new SubPackageConfig(subNameAttr.Value, appendExtension));
                    }

                    configs.Add(config);
                }

                Debug.Log($"[GamePackageMappingGenerator] 成功读取 {configs.Count} 个游戏配置");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GamePackageMappingGenerator] 读取 XML 配置失败: {ex.Message}\n{ex.StackTrace}");
            }

            return configs;
        }

        /// <summary>
        /// 将配置列表生成到 XML 文件。
        /// </summary>
        /// <param name="configs">配置列表</param>
        /// <returns>是否成功</returns>
        public static bool GenerateToXml(List<GamePackageConfig> configs)
        {
            if (configs == null)
            {
                Debug.LogError("[GamePackageMappingGenerator] 配置列表为空");
                return false;
            }

            try
            {
                XDocument doc = new XDocument();
                XElement root = new XElement("ProjectConfig");

                foreach (GamePackageConfig config in configs)
                {
                    if (string.IsNullOrEmpty(config.GameName))
                    {
                        Debug.LogWarning("[GamePackageMappingGenerator] 跳过无效的配置（GameName 为空）");
                        continue;
                    }

                    XElement packageElement = new XElement("Package");
                    packageElement.SetAttributeValue("Name", config.GameName);

                    if (!string.IsNullOrEmpty(config.DllPath))
                    {
                        packageElement.SetAttributeValue("Dll", config.DllPath);
                    }

                    // 添加 SubPackage 元素
                    foreach (SubPackageConfig subPackage in config.SubPackages)
                    {
                        if (string.IsNullOrEmpty(subPackage.PackageName))
                        {
                            Debug.LogWarning($"[GamePackageMappingGenerator] 跳过无效的 SubPackage（GameName: {config.GameName}）");
                            continue;
                        }

                        XElement subPackageElement = new XElement("SubPackage");
                        subPackageElement.SetAttributeValue("Name", subPackage.PackageName);
                        subPackageElement.SetAttributeValue("AppendExtension", subPackage.AppendExtension.ToString().ToLower());
                        packageElement.Add(subPackageElement);
                    }

                    root.Add(packageElement);
                }

                doc.Add(root);

                // 确保目录存在
                string fullPath = Path.Combine(Application.dataPath, "Framework/Config/GamePackageMapping.xml");
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 保存文件
                doc.Save(fullPath);

                // 刷新 AssetDatabase
                AssetDatabase.Refresh();

                Debug.Log($"[GamePackageMappingGenerator] 成功生成 XML 配置，共 {configs.Count} 个游戏配置");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GamePackageMappingGenerator] 生成 XML 配置失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 验证 XML 文件格式是否正确。
        /// </summary>
        /// <param name="filePath">XML 文件路径</param>
        /// <returns>是否有效</returns>
        public static bool ValidateXmlFormat(string filePath = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.Combine(Application.dataPath, "Framework/Config/GamePackageMapping.xml");
            }

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[GamePackageMappingGenerator] XML 文件不存在: {filePath}");
                return false;
            }

            try
            {
                XDocument doc = XDocument.Load(filePath);
                XElement root = doc.Root;

                if (root == null || root.Name != "ProjectConfig")
                {
                    Debug.LogError("[GamePackageMappingGenerator] XML 根节点无效，应为 ProjectConfig");
                    return false;
                }

                foreach (XElement packageElement in root.Elements("Package"))
                {
                    if (packageElement.Attribute("Name") == null)
                    {
                        Debug.LogError("[GamePackageMappingGenerator] Package 元素缺少 Name 属性");
                        return false;
                    }

                    foreach (XElement subPackageElement in packageElement.Elements("SubPackage"))
                    {
                        if (subPackageElement.Attribute("Name") == null)
                        {
                            Debug.LogError("[GamePackageMappingGenerator] SubPackage 元素缺少 Name 属性");
                            return false;
                        }
                    }
                }

                Debug.Log("[GamePackageMappingGenerator] XML 格式验证通过");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GamePackageMappingGenerator] XML 格式验证失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 生成配置（菜单项）。
        /// 从当前 Editor 配置生成到 XML 文件。
        /// 注意：此方法当前仅作为菜单项占位符，实际生成逻辑需要在 EditorWindow 中实现。
        /// </summary>
        public static void GenerateConfig()
        {
            // 注意：此方法当前仅作为菜单项占位符
            // 实际生成逻辑需要在 EditorWindow 中实现，因为需要从 UI 获取配置数据
            Debug.Log("[GamePackageMappingGenerator] Generate config 菜单项已调用。请在 EditorWindow 中使用 GenerateToXml 方法生成配置。");
            
            // 验证现有 XML 文件
            if (ValidateXmlFormat())
            {
                var configs = ReadFromXml();
                Debug.Log($"[GamePackageMappingGenerator] 当前 XML 文件包含 {configs.Count} 个游戏配置");
                
                // 打印配置详情
                foreach (var config in configs)
                {
                    Debug.Log($"[GamePackageMappingGenerator] GameName: {config.GameName}, DLL: {config.DllPath}, Packages: {string.Join(", ", config.SubPackages.Select(sp => sp.PackageName))}");
                }
            }
        }

        /// <summary>
        /// 测试读取 XML 配置（菜单项，用于调试）。
        /// </summary>
        public static void TestReadXml()
        {
            Debug.Log("[GamePackageMappingGenerator] === 开始测试读取 XML 配置 ===");
            
            var configs = ReadFromXml();
            
            if (configs.Count == 0)
            {
                Debug.LogWarning("[GamePackageMappingGenerator] 未读取到任何配置");
                return;
            }

            Debug.Log($"[GamePackageMappingGenerator] 成功读取 {configs.Count} 个游戏配置:");
            foreach (var config in configs)
            {
                Debug.Log($"  - GameName: {config.GameName}");
                Debug.Log($"    DLL: {config.DllPath}");
                Debug.Log($"    Packages ({config.SubPackages.Count}):");
                foreach (var subPackage in config.SubPackages)
                {
                    Debug.Log($"      • {subPackage.PackageName} (AppendExtension: {subPackage.AppendExtension})");
                }
            }
            
            Debug.Log("[GamePackageMappingGenerator] === 测试完成 ===");
        }
    }
}

