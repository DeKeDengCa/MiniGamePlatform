using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace Astorise.Framework.AOT
{
    /// <summary>
    /// 游戏包映射配置管理类，用于统一管理 XML 配置的加载、缓存和访问。
    /// </summary>
    public sealed class GamePackageMappingXML
    {
        /// <summary>
        /// XML 文档
        /// </summary>
        private XDocument _doc;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _doc != null && _doc.Root != null;

        /// <summary>
        /// 初始化管理类。
        /// </summary>
        /// <param name="doc">XML 文档</param>
        public void Initialize(XDocument doc)
        {
            if (doc == null || doc.Root == null)
            {
                throw new Exception("GamePackageMapping.xml 文档无效");
            }

            _doc = doc;
        }

        /// <summary>
        /// 获取游戏的 Package 元素。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <returns>Package XML 元素</returns>
        public XElement GetGamePackageElement(string gameName)
        {
            if (!IsInitialized)
            {
                throw new Exception("GamePackageMappingXML 未初始化");
            }

            if (_doc?.Root == null)
            {
                throw new Exception("GamePackageMappingXML 文档根节点为空");
            }

            XElement packageElement = _doc.Root
                .Elements("Package")
                .FirstOrDefault(p => p.Attribute("Name")?.Value == gameName);

            if (packageElement == null)
            {
                // 列出所有可用的游戏名称，方便调试
                var availableGames = _doc.Root
                    .Elements("Package")
                    .Select(p => p.Attribute("Name")?.Value)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToArray();
                
                string availableGamesList = availableGames.Length > 0 
                    ? string.Join(", ", availableGames) 
                    : "无";
                
                throw new Exception($"Game '{gameName}' not found in configuration. Available games: [{availableGamesList}]");
            }

#if UNITY_DEBUG && GAME_BOOTSTRAP
            // 直接使用 packageElement 获取信息，避免递归调用
            try
            {
                int packageCount = packageElement.Elements("SubPackage").Count();
                string dll = packageElement.Attribute("Dll")?.Value ?? string.Empty;
                Debug.Log($"[GamePackageMappingXML] 游戏 '{gameName}' 配置信息: 包数量={packageCount}, DLL={dll}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GamePackageMappingXML] 获取游戏 '{gameName}' 配置信息时出错: {ex.Message}");
            }
#endif

            return packageElement;
        }

        /// <summary>
        /// 获取游戏的包名列表。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <returns>包名数组</returns>
        public string[] GetGamePackages(string gameName)
        {
            XElement packageElement = GetGamePackageElement(gameName);
            return GetGamePackagesFromElement(packageElement);
        }

        /// <summary>
        /// 从 Package 元素中获取包名列表（内部方法）。
        /// </summary>
        /// <param name="packageElement">Package XML 元素</param>
        /// <returns>包名数组</returns>
        private string[] GetGamePackagesFromElement(XElement packageElement)
        {
            List<string> packagesList = new List<string>();
            foreach (XElement subPackageElement in packageElement.Elements("SubPackage"))
            {
                XAttribute nameAttr = subPackageElement.Attribute("Name");
                if (nameAttr != null && !string.IsNullOrEmpty(nameAttr.Value))
                {
                    packagesList.Add(nameAttr.Value);
                }
            }

            if (packagesList.Count == 0)
            {
                throw new Exception($"Game '{packageElement.Attribute("Name")?.Value ?? "Unknown"}' has no packages configured");
            }

            return packagesList.ToArray();
        }

        /// <summary>
        /// 获取游戏的 DLL 路径。
        /// </summary>
        /// <param name="gameName">游戏名称</param>
        /// <returns>DLL 路径，如果未配置则返回空字符串</returns>
        public string GetGameDll(string gameName)
        {
            XElement packageElement = GetGamePackageElement(gameName);
            return packageElement.Attribute("Dll")?.Value ?? string.Empty;
        }
    }
}

