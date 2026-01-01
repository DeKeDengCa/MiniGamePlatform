using System;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace Astorise.Framework.AOT
{
    /// <summary>
    /// PatchAOTAssemblies 配置管理类，用于统一管理 XML 配置的加载、缓存和访问。
    /// </summary>
    public sealed class PatchAOTAssembliesXML
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
                throw new Exception("PatchAOTAssemblies.xml 文档无效");
            }

            _doc = doc;
        }

        /// <summary>
        /// 获取程序集名称列表。
        /// </summary>
        /// <returns>程序集名称数组</returns>
        public string[] GetAssemblyList()
        {
            if (!IsInitialized)
            {
                throw new Exception("PatchAOTAssembliesXML 未初始化");
            }

            if (_doc.Root == null)
            {
                return Array.Empty<string>();
            }

            return _doc.Root
                .Elements("Assembly")
                .Select(e => e.Attribute("Name")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToArray();
        }
    }
}

