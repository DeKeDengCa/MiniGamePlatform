using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Astorise.Framework.SDK
{
    /// <summary>
    /// UnityBridge JSON 序列化器：用于把 BridgeMessage 按协议序列化为 JSON。
    /// 说明：Unity 的 JsonUtility 无法稳定支持 Dictionary/object 的序列化，这里提供最小实现。
    /// </summary>
    internal static class UnityBridgeJsonSerializer
    {
        private const int MaxDepth = 64;

        public static string Serialize(BridgeMessage message)
        {
            if (message == null)
            {
                return "null";
            }

            StringBuilder builder = new StringBuilder(256);
            builder.Append('{');

            AppendStringProperty(builder, "type", message.type);
            builder.Append(',');
            AppendStringProperty(builder, "id", message.id);
            builder.Append(',');
            AppendStringProperty(builder, "name", message.name);
            builder.Append(',');
            AppendNumberProperty(builder, "ts", message.ts);
            builder.Append(',');
            AppendPropertyName(builder, "data");
            AppendValue(builder, message.data, 0);

            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendPropertyName(StringBuilder builder, string name)
        {
            builder.Append('\"');
            builder.Append(EscapeString(name));
            builder.Append("\":");
        }

        private static void AppendStringProperty(StringBuilder builder, string name, string value)
        {
            AppendPropertyName(builder, name);
            AppendStringValue(builder, value);
        }

        private static void AppendNumberProperty(StringBuilder builder, string name, long value)
        {
            AppendPropertyName(builder, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendStringValue(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('\"');
            builder.Append(EscapeString(value));
            builder.Append('\"');
        }

        private static void AppendValue(StringBuilder builder, object value, int depth)
        {
            if (depth > MaxDepth)
            {
                builder.Append("null");
                return;
            }

            if (value == null)
            {
                builder.Append("null");
                return;
            }

            string stringValue = value as string;
            if (stringValue != null)
            {
                AppendStringValue(builder, stringValue);
                return;
            }

            if (value is bool boolValue)
            {
                builder.Append(boolValue ? "true" : "false");
                return;
            }

            if (value is int intValue)
            {
                builder.Append(intValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is long longValue)
            {
                builder.Append(longValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is float floatValue)
            {
                builder.Append(floatValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is double doubleValue)
            {
                builder.Append(doubleValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary<string, object> genericDictionary)
            {
                AppendDictionary(builder, genericDictionary, depth + 1);
                return;
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                AppendDictionary(builder, dictionary, depth + 1);
                return;
            }

            IList list = value as IList;
            if (list != null)
            {
                AppendList(builder, list, depth + 1);
                return;
            }

            // 兜底：转为字符串
            AppendStringValue(builder, value.ToString());
        }

        private static void AppendDictionary(StringBuilder builder, IDictionary<string, object> dictionary, int depth)
        {
            builder.Append('{');

            bool first = true;
            foreach (KeyValuePair<string, object> keyValuePair in dictionary)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                AppendPropertyName(builder, keyValuePair.Key);
                AppendValue(builder, keyValuePair.Value, depth);
            }

            builder.Append('}');
        }

        private static void AppendDictionary(StringBuilder builder, IDictionary dictionary, int depth)
        {
            builder.Append('{');

            bool first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key as string;
                if (key == null)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                AppendPropertyName(builder, key);
                AppendValue(builder, entry.Value, depth);
            }

            builder.Append('}');
        }

        private static void AppendList(StringBuilder builder, IList list, int depth)
        {
            builder.Append('[');

            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                AppendValue(builder, list[i], depth);
            }

            builder.Append(']');
        }

        private static string EscapeString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            StringBuilder builder = new StringBuilder(input.Length + 8);
            int length = input.Length;
            for (int i = 0; i < length; i++)
            {
                char c = input[i];
                switch (c)
                {
                    case '\"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 反序列化 JSON 字符串为 BridgeMessage。
        /// 注意：这是一个简化实现，仅支持 BridgeMessage 的基本结构。
        /// </summary>
        public static BridgeMessage Deserialize(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
            {
                return null;
            }

            BridgeMessage message = new BridgeMessage();
            int index = 0;
            int length = jsonString.Length;

            SkipWhitespace(jsonString, ref index, length);
            if (index >= length || jsonString[index] != '{')
            {
                return null;
            }
            index++;

            while (index < length)
            {
                SkipWhitespace(jsonString, ref index, length);
                if (index >= length)
                {
                    break;
                }

                if (jsonString[index] == '}')
                {
                    break;
                }

                string key = ParseString(jsonString, ref index, length);
                if (key == null)
                {
                    break;
                }

                SkipWhitespace(jsonString, ref index, length);
                if (index >= length || jsonString[index] != ':')
                {
                    break;
                }
                index++;

                SkipWhitespace(jsonString, ref index, length);

                if (key == "type")
                {
                    message.type = ParseString(jsonString, ref index, length);
                }
                else if (key == "id")
                {
                    message.id = ParseString(jsonString, ref index, length);
                }
                else if (key == "name")
                {
                    message.name = ParseString(jsonString, ref index, length);
                }
                else if (key == "ts")
                {
                    message.ts = ParseLong(jsonString, ref index, length);
                }
                else if (key == "data")
                {
                    message.data = ParseDataValue(jsonString, ref index, length);
                }
                else
                {
                    SkipValue(jsonString, ref index, length);
                }

                SkipWhitespace(jsonString, ref index, length);
                if (index < length && jsonString[index] == ',')
                {
                    index++;
                }
            }

            return message;
        }

        private static void SkipWhitespace(string json, ref int index, int length)
        {
            while (index < length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        private static string ParseString(string json, ref int index, int length)
        {
            SkipWhitespace(json, ref index, length);
            if (index >= length || json[index] != '"')
            {
                return null;
            }
            index++;

            StringBuilder builder = new StringBuilder();
            while (index < length && json[index] != '"')
            {
                if (json[index] == '\\' && index + 1 < length)
                {
                    index++;
                    char escaped = json[index];
                    switch (escaped)
                    {
                        case '"':
                            builder.Append('"');
                            break;
                        case '\\':
                            builder.Append('\\');
                            break;
                        case '/':
                            builder.Append('/');
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            if (index + 4 < length)
                            {
                                string hex = json.Substring(index + 1, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                                {
                                    builder.Append((char)code);
                                    index += 4;
                                }
                            }
                            break;
                        default:
                            builder.Append(escaped);
                            break;
                    }
                }
                else
                {
                    builder.Append(json[index]);
                }
                index++;
            }

            if (index >= length || json[index] != '"')
            {
                return null;
            }
            index++;

            return builder.ToString();
        }

        private static long ParseLong(string json, ref int index, int length)
        {
            SkipWhitespace(json, ref index, length);
            int start = index;
            while (index < length && (char.IsDigit(json[index]) || json[index] == '-' || json[index] == '+'))
            {
                index++;
            }
            if (index > start && long.TryParse(json.Substring(start, index - start), out long result))
            {
                return result;
            }
            return 0;
        }

        private static object ParseDataValue(string json, ref int index, int length)
        {
            SkipWhitespace(json, ref index, length);
            if (index >= length)
            {
                return new Dictionary<string, object>();
            }

            if (json[index] == '{')
            {
                return ParseDictionary(json, ref index, length);
            }
            else if (json[index] == '[')
            {
                return ParseList(json, ref index, length);
            }
            else if (json[index] == '"')
            {
                return ParseString(json, ref index, length);
            }
            else if (char.IsDigit(json[index]) || json[index] == '-')
            {
                int start = index;
                while (index < length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == '-' || json[index] == '+' || json[index] == 'e' || json[index] == 'E'))
                {
                    index++;
                }
                string numberStr = json.Substring(start, index - start);
                if (long.TryParse(numberStr, out long longValue))
                {
                    return longValue;
                }
                if (double.TryParse(numberStr, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
                {
                    return doubleValue;
                }
                return 0;
            }
            else if (json.Substring(index).StartsWith("true"))
            {
                index += 4;
                return true;
            }
            else if (json.Substring(index).StartsWith("false"))
            {
                index += 5;
                return false;
            }
            else if (json.Substring(index).StartsWith("null"))
            {
                index += 4;
                return null;
            }

            return new Dictionary<string, object>();
        }

        private static Dictionary<string, object> ParseDictionary(string json, ref int index, int length)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            if (index >= length || json[index] != '{')
            {
                return dict;
            }
            index++;

            while (index < length)
            {
                SkipWhitespace(json, ref index, length);
                if (index >= length)
                {
                    break;
                }

                if (json[index] == '}')
                {
                    index++;
                    break;
                }

                string key = ParseString(json, ref index, length);
                if (key == null)
                {
                    break;
                }

                SkipWhitespace(json, ref index, length);
                if (index >= length || json[index] != ':')
                {
                    break;
                }
                index++;

                SkipWhitespace(json, ref index, length);
                object value = ParseDataValue(json, ref index, length);
                dict[key] = value;

                SkipWhitespace(json, ref index, length);
                if (index < length && json[index] == ',')
                {
                    index++;
                }
            }

            return dict;
        }

        private static List<object> ParseList(string json, ref int index, int length)
        {
            List<object> list = new List<object>();
            if (index >= length || json[index] != '[')
            {
                return list;
            }
            index++;

            while (index < length)
            {
                SkipWhitespace(json, ref index, length);
                if (index >= length)
                {
                    break;
                }

                if (json[index] == ']')
                {
                    index++;
                    break;
                }

                object value = ParseDataValue(json, ref index, length);
                list.Add(value);

                SkipWhitespace(json, ref index, length);
                if (index < length && json[index] == ',')
                {
                    index++;
                }
            }

            return list;
        }

        private static void SkipValue(string json, ref int index, int length)
        {
            SkipWhitespace(json, ref index, length);
            if (index >= length)
            {
                return;
            }

            char ch = json[index];
            if (ch == '"')
            {
                ParseString(json, ref index, length);
            }
            else if (ch == '{')
            {
                int depth = 1;
                index++;
                while (index < length && depth > 0)
                {
                    if (json[index] == '{')
                    {
                        depth++;
                    }
                    else if (json[index] == '}')
                    {
                        depth--;
                    }
                    index++;
                }
            }
            else if (ch == '[')
            {
                int depth = 1;
                index++;
                while (index < length && depth > 0)
                {
                    if (json[index] == '[')
                    {
                        depth++;
                    }
                    else if (json[index] == ']')
                    {
                        depth--;
                    }
                    index++;
                }
            }
            else
            {
                while (index < length && json[index] != ',' && json[index] != '}' && json[index] != ']' && !char.IsWhiteSpace(json[index]))
                {
                    index++;
                }
            }
        }
    }
}


