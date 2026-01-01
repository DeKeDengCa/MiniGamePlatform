#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Google.Protobuf.Reflection;
using UnityEngine;

namespace Astorise.Editor.Proto
{
    /// <summary>
    /// 从 protoc 输出的 descriptor_set 生成 RPC method → (ReqType, RspType) 的静态注册表。
    /// </summary>
    public static class RpcMethodRegistryGenerator
    {
        private const string Tag = "[RpcMethodRegistryGenerator]";

        /// <summary>
        /// 根据 descriptor 文件生成 registry C# 文件。
        /// </summary>
        /// <param name="descriptorFilePath">protoc 输出的 descriptor_set 路径</param>
        /// <param name="outputCsFilePath">生成的 C# 文件路径</param>
        public static void Generate(string descriptorFilePath, string outputCsFilePath)
        {
            if (string.IsNullOrEmpty(descriptorFilePath))
                throw new ArgumentException("descriptorFilePath is null or empty", nameof(descriptorFilePath));
            if (string.IsNullOrEmpty(outputCsFilePath))
                throw new ArgumentException("outputCsFilePath is null or empty", nameof(outputCsFilePath));

            if (!File.Exists(descriptorFilePath))
                throw new FileNotFoundException("Descriptor file not found", descriptorFilePath);

            FileDescriptorSet descriptorSet = LoadDescriptorSet(descriptorFilePath);
            Dictionary<string, string> protoToCSharpTypeMap = BuildProtoToCSharpTypeMap(descriptorSet);
            List<RpcEntry> rpcEntries = BuildRpcEntries(descriptorSet, protoToCSharpTypeMap);

            WriteRegistryFile(outputCsFilePath, rpcEntries);

#if UNITY_DEBUG
            Debug.Log($"{Tag} 生成完成: {outputCsFilePath}, entries={rpcEntries.Count}");
#endif
        }

        private static FileDescriptorSet LoadDescriptorSet(string descriptorFilePath)
        {
            using (FileStream stream = File.OpenRead(descriptorFilePath))
            {
                return FileDescriptorSet.Parser.ParseFrom(stream);
            }
        }

        private static Dictionary<string, string> BuildProtoToCSharpTypeMap(FileDescriptorSet descriptorSet)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(4096);

            int fileCount = descriptorSet != null && descriptorSet.File != null ? descriptorSet.File.Count : 0;
            for (int i = 0; i < fileCount; i++)
            {
                FileDescriptorProto file = descriptorSet.File[i];
                string csharpNamespace = ResolveCSharpNamespace(file);
                string package = file.Package ?? string.Empty;

                int msgCount = file.MessageType != null ? file.MessageType.Count : 0;
                for (int m = 0; m < msgCount; m++)
                {
                    DescriptorProto message = file.MessageType[m];
                    string protoFullName = BuildProtoFullName(package, message.Name);
                    string csharpFullName = BuildCSharpFullName(csharpNamespace, message.Name);
                    TryAdd(map, protoFullName, csharpFullName);

                    AddNestedTypes(map, package, csharpNamespace, message, message.Name);
                }
            }

            return map;
        }

        private static void AddNestedTypes(
            Dictionary<string, string> map,
            string package,
            string csharpNamespace,
            DescriptorProto parent,
            string parentProtoNameChain)
        {
            int nestedCount = parent.NestedType != null ? parent.NestedType.Count : 0;
            for (int i = 0; i < nestedCount; i++)
            {
                DescriptorProto nested = parent.NestedType[i];

                // proto: .pkg.Outer.Inner
                string nestedProtoChain = parentProtoNameChain + "." + nested.Name;
                string protoFullName = BuildProtoFullName(package, nestedProtoChain);

                // csharp: Namespace.Outer.Types.Inner (递归嵌套)
                string csharpType = BuildCSharpNestedTypeName(csharpNamespace, parentProtoNameChain, nested.Name);
                TryAdd(map, protoFullName, csharpType);

                AddNestedTypes(map, package, csharpNamespace, nested, nestedProtoChain);
            }
        }

        private static string BuildCSharpNestedTypeName(string csharpNamespace, string outerChain, string innerName)
        {
            // outerChain: Outer 或 Outer.InnerOuter（proto链）
            // C# nested uses .Types. between each nesting level
            // Outer.InnerOuter -> Outer.Types.InnerOuter
            string[] parts = outerChain.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder(128);

            if (!string.IsNullOrEmpty(csharpNamespace))
            {
                sb.Append(csharpNamespace);
                sb.Append('.');
            }

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(".Types.");
                }
                sb.Append(parts[i]);
            }

            sb.Append(".Types.");
            sb.Append(innerName);
            return sb.ToString();
        }

        private static List<RpcEntry> BuildRpcEntries(FileDescriptorSet descriptorSet, Dictionary<string, string> protoToCSharpTypeMap)
        {
            List<RpcEntry> entries = new List<RpcEntry>(512);

            int fileCount = descriptorSet != null && descriptorSet.File != null ? descriptorSet.File.Count : 0;
            for (int i = 0; i < fileCount; i++)
            {
                FileDescriptorProto file = descriptorSet.File[i];
                int serviceCount = file.Service != null ? file.Service.Count : 0;
                for (int s = 0; s < serviceCount; s++)
                {
                    ServiceDescriptorProto service = file.Service[s];
                    string serviceName = service.Name ?? string.Empty;

                    int methodCount = service.Method != null ? service.Method.Count : 0;
                    for (int m = 0; m < methodCount; m++)
                    {
                        MethodDescriptorProto rpc = service.Method[m];
                        string rpcName = rpc.Name ?? string.Empty;
                        string methodKey = serviceName + "." + rpcName;

                        string inputProto = rpc.InputType ?? string.Empty;
                        string outputProto = rpc.OutputType ?? string.Empty;

                        if (!protoToCSharpTypeMap.TryGetValue(inputProto, out string inputCSharp))
                        {
                            throw new InvalidOperationException($"无法解析输入类型: method='{methodKey}', input='{inputProto}'");
                        }

                        if (!protoToCSharpTypeMap.TryGetValue(outputProto, out string outputCSharp))
                        {
                            throw new InvalidOperationException($"无法解析输出类型: method='{methodKey}', output='{outputProto}'");
                        }

                        RpcEntry entry = new RpcEntry(methodKey, inputCSharp, outputCSharp);
                        entries.Add(entry);
                    }
                }
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.MethodKey, b.MethodKey));
            return entries;
        }

        private static void WriteRegistryFile(string outputCsFilePath, List<RpcEntry> entries)
        {
            string directory = Path.GetDirectoryName(outputCsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            StringBuilder sb = new StringBuilder(1024 + entries.Count * 140);

            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// 该文件由 RpcMethodRegistryGenerator 自动生成，请勿手工修改。");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace Astorise.Proto.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// RPC method → (ReqType, RspType) 映射表。");
            sb.AppendLine("    /// key 格式：{ProtoServiceName}.{RpcName}，例如 RoomLevelMgr.CreateLevelSlot");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class RpcMethodRegistry");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// RPC 类型对。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public readonly struct RpcTypePair");
            sb.AppendLine("        {");
            sb.AppendLine("            public readonly Type RequestType;");
            sb.AppendLine("            public readonly Type ResponseType;");
            sb.AppendLine();
            sb.AppendLine("            public RpcTypePair(Type requestType, Type responseType)");
            sb.AppendLine("            {");
            sb.AppendLine("                RequestType = requestType;");
            sb.AppendLine("                ResponseType = responseType;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static readonly Dictionary<string, RpcTypePair> Map = new Dictionary<string, RpcTypePair>(" + entries.Count + ")");
            sb.AppendLine("        {");

            for (int i = 0; i < entries.Count; i++)
            {
                RpcEntry e = entries[i];
                sb.Append("            { \"");
                sb.Append(EscapeString(e.MethodKey));
                sb.Append("\", new RpcTypePair(typeof(global::");
                sb.Append(e.RequestCSharpFullName);
                sb.Append("), typeof(global::");
                sb.Append(e.ResponseCSharpFullName);
                sb.Append(")) }");

                if (i < entries.Count - 1)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 根据 method 获取 Req/Rsp 类型。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static bool TryGet(string method, out RpcTypePair pair)");
            sb.AppendLine("        {");
            sb.AppendLine("            return Map.TryGetValue(method, out pair);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 根据 method 获取请求类型。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"method\">RPC method 名称，格式：{ProtoServiceName}.{RpcName}</param>");
            sb.AppendLine("        /// <returns>请求类型，如果找不到则返回 null</returns>");
            sb.AppendLine("        public static Type GetRequestType(string method)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Map.TryGetValue(method, out RpcTypePair pair))");
            sb.AppendLine("            {");
            sb.AppendLine("                return pair.RequestType;");
            sb.AppendLine("            }");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 根据 method 获取响应类型。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"method\">RPC method 名称，格式：{ProtoServiceName}.{RpcName}</param>");
            sb.AppendLine("        /// <returns>响应类型，如果找不到则返回 null</returns>");
            sb.AppendLine("        public static Type GetResponseType(string method)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Map.TryGetValue(method, out RpcTypePair pair))");
            sb.AppendLine("            {");
            sb.AppendLine("                return pair.ResponseType;");
            sb.AppendLine("            }");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            // 统一 CRLF
            string content = sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
            File.WriteAllText(outputCsFilePath, content, new UTF8Encoding(false));
        }

        private static string ResolveCSharpNamespace(FileDescriptorProto file)
        {
            if (file == null)
                return string.Empty;

            if (file.Options != null && !string.IsNullOrEmpty(file.Options.CsharpNamespace))
            {
                return file.Options.CsharpNamespace;
            }

            string pkg = file.Package ?? string.Empty;
            if (string.IsNullOrEmpty(pkg))
            {
                return string.Empty;
            }

            // package: api.astrorise.platform.user -> Api.Astrorise.Platform.User
            string[] parts = pkg.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder sb = new StringBuilder(64);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append('.');
                sb.Append(ToPascalCase(parts[i]));
            }
            return sb.ToString();
        }

        private static string BuildProtoFullName(string package, string nameChain)
        {
            if (string.IsNullOrEmpty(package))
            {
                return "." + nameChain;
            }
            return "." + package + "." + nameChain;
        }

        private static string BuildCSharpFullName(string csharpNamespace, string messageName)
        {
            if (string.IsNullOrEmpty(csharpNamespace))
            {
                return messageName;
            }
            return csharpNamespace + "." + messageName;
        }

        private static void TryAdd(Dictionary<string, string> map, string protoFullName, string csharpFullName)
        {
            if (string.IsNullOrEmpty(protoFullName) || string.IsNullOrEmpty(csharpFullName))
                return;

            if (!map.ContainsKey(protoFullName))
            {
                map[protoFullName] = csharpFullName;
            }
        }

        private static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ToPascalCase(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            // 同时处理 '_' 与 '-' 分隔
            string[] parts = s.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Length == 0) continue;

                char first = p[0];
                if (first >= 'a' && first <= 'z')
                {
                    first = (char)(first - 32);
                }
                sb.Append(first);

                if (p.Length > 1)
                {
                    sb.Append(p.Substring(1));
                }
            }
            return sb.ToString();
        }

        private readonly struct RpcEntry
        {
            public readonly string MethodKey;
            public readonly string RequestCSharpFullName;
            public readonly string ResponseCSharpFullName;

            public RpcEntry(string methodKey, string requestCSharpFullName, string responseCSharpFullName)
            {
                MethodKey = methodKey;
                RequestCSharpFullName = requestCSharpFullName;
                ResponseCSharpFullName = responseCSharpFullName;
            }
        }
    }
}
#endif


