#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MetaVoiceChat.Core.Editor.Utils
{
    /// <summary>
    /// Safely updates Unity's package manifest without relying on a JSON package.
    /// </summary>
    public static class ManifestUtils
    {
        public static void AddDependency(string packageName, string version)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("A package name is required.", nameof(packageName));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("A package version is required.", nameof(version));
            }

            string manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath))
            {
                return;
            }

            JsonObject root = ParseManifest(manifestPath);
            JsonObject dependencies;
            bool changed = false;

            if (!root.TryGetValue("dependencies", out JsonValue dependenciesValue) || !(dependenciesValue is JsonObject))
            {
                dependencies = new JsonObject();
                root["dependencies"] = dependencies;
                changed = true;
            }
            else
            {
                dependencies = (JsonObject)dependenciesValue;
            }

            if (!dependencies.TryGetValue(packageName, out JsonValue existingValue) ||
                !(existingValue is JsonString) ||
                ((JsonString)existingValue).Value != version)
            {
                dependencies[packageName] = new JsonString(version);
                changed = true;
            }

            SaveIfChanged(manifestPath, root, changed);
        }

        public static void AddScopedRegistry(string name, string url, string[] scopes)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A registry name is required.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("A registry URL is required.", nameof(url));
            }

            string manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath))
            {
                return;
            }

            JsonObject root = ParseManifest(manifestPath);
            JsonArray registries;
            bool changed = false;

            if (!root.TryGetValue("scopedRegistries", out JsonValue registriesValue) || !(registriesValue is JsonArray))
            {
                registries = new JsonArray();
                root["scopedRegistries"] = registries;
                changed = true;
            }
            else
            {
                registries = (JsonArray)registriesValue;
            }

            JsonObject registry = FindRegistryByUrl(registries, url);
            if (registry == null)
            {
                registry = new JsonObject();
                registry.Add("name", new JsonString(name));
                registry.Add("url", new JsonString(url));
                registry.Add("scopes", CreateScopes(scopes));
                registries.Add(registry);
                changed = true;
            }
            else
            {
                if (!registry.TryGetValue("name", out JsonValue nameValue) ||
                    !(nameValue is JsonString) ||
                    ((JsonString)nameValue).Value != name)
                {
                    registry["name"] = new JsonString(name);
                    changed = true;
                }

                JsonArray registryScopes;
                if (!registry.TryGetValue("scopes", out JsonValue scopesValue) || !(scopesValue is JsonArray))
                {
                    registryScopes = new JsonArray();
                    registry["scopes"] = registryScopes;
                    changed = true;
                }
                else
                {
                    registryScopes = (JsonArray)scopesValue;
                }

                HashSet<string> existingScopes = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonValue scopeValue in registryScopes)
                {
                    JsonString scopeString = scopeValue as JsonString;
                    if (scopeString != null)
                    {
                        existingScopes.Add(scopeString.Value);
                    }
                }

                foreach (string scope in GetValidScopes(scopes))
                {
                    if (existingScopes.Add(scope))
                    {
                        registryScopes.Add(new JsonString(scope));
                        changed = true;
                    }
                }
            }

            SaveIfChanged(manifestPath, root, changed);
        }

        private static string GetManifestPath()
        {
            return Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
        }

        private static JsonObject ParseManifest(string manifestPath)
        {
            JsonValue manifest = JsonParser.Parse(File.ReadAllText(manifestPath));
            JsonObject root = manifest as JsonObject;
            if (root == null)
            {
                throw new InvalidDataException("Packages/manifest.json must contain a JSON object at its root.");
            }

            return root;
        }

        private static void SaveIfChanged(string manifestPath, JsonObject root, bool changed)
        {
            if (!changed)
            {
                return;
            }

            File.WriteAllText(manifestPath, JsonWriter.Write(root, true));
            AssetDatabase.Refresh();
        }

        private static JsonObject FindRegistryByUrl(JsonArray registries, string url)
        {
            foreach (JsonValue registryValue in registries)
            {
                JsonObject registry = registryValue as JsonObject;
                if (registry != null && registry.TryGetValue("url", out JsonValue urlValue))
                {
                    JsonString registryUrl = urlValue as JsonString;
                    if (registryUrl != null && registryUrl.Value == url)
                    {
                        return registry;
                    }
                }
            }

            return null;
        }

        private static JsonArray CreateScopes(string[] scopes)
        {
            JsonArray result = new JsonArray();
            foreach (string scope in GetValidScopes(scopes))
            {
                result.Add(new JsonString(scope));
            }

            return result;
        }

        private static IEnumerable<string> GetValidScopes(string[] scopes)
        {
            if (scopes == null)
            {
                yield break;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string scope in scopes)
            {
                if (!string.IsNullOrWhiteSpace(scope) && seen.Add(scope))
                {
                    yield return scope;
                }
            }
        }

        private abstract class JsonValue
        {
        }

        private sealed class JsonObject : JsonValue
        {
            private readonly Dictionary<string, JsonValue> values = new Dictionary<string, JsonValue>(StringComparer.Ordinal);

            public JsonValue this[string key]
            {
                get { return values[key]; }
                set { values[key] = value; }
            }

            public void Add(string key, JsonValue value)
            {
                values.Add(key, value);
            }

            public bool TryGetValue(string key, out JsonValue value)
            {
                return values.TryGetValue(key, out value);
            }

            public IEnumerable<KeyValuePair<string, JsonValue>> Properties
            {
                get { return values; }
            }
        }

        private sealed class JsonArray : JsonValue, IEnumerable<JsonValue>
        {
            private readonly List<JsonValue> values = new List<JsonValue>();

            public void Add(JsonValue value)
            {
                values.Add(value);
            }

            public IEnumerator<JsonValue> GetEnumerator()
            {
                return values.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class JsonString : JsonValue
        {
            public JsonString(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private sealed class JsonNumber : JsonValue
        {
            public JsonNumber(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private sealed class JsonBool : JsonValue
        {
            public JsonBool(bool value)
            {
                Value = value;
            }

            public bool Value { get; }
        }

        private sealed class JsonNull : JsonValue
        {
        }

        private sealed class JsonParser
        {
            private readonly string text;
            private int position;

            private JsonParser(string text)
            {
                this.text = text ?? throw new ArgumentNullException(nameof(text));
            }

            public static JsonValue Parse(string text)
            {
                JsonParser parser = new JsonParser(text);
                JsonValue result = parser.ParseValue();
                parser.SkipWhitespace();
                if (!parser.IsAtEnd)
                {
                    throw parser.Error("Unexpected content after the JSON value.");
                }

                return result;
            }

            private JsonValue ParseValue()
            {
                SkipWhitespace();
                if (IsAtEnd)
                {
                    throw Error("Expected a JSON value.");
                }

                switch (text[position])
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return new JsonString(ParseString());
                    case 't': ConsumeLiteral("true"); return new JsonBool(true);
                    case 'f': ConsumeLiteral("false"); return new JsonBool(false);
                    case 'n': ConsumeLiteral("null"); return new JsonNull();
                    default:
                        if (text[position] == '-' || IsDigit(text[position]))
                        {
                            return new JsonNumber(ParseNumber());
                        }

                        throw Error("Expected a JSON value.");
                }
            }

            private JsonObject ParseObject()
            {
                Expect('{');
                JsonObject result = new JsonObject();
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (IsAtEnd || text[position] != '"')
                    {
                        throw Error("Expected an object property name.");
                    }

                    string key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    JsonValue value = ParseValue();
                    if (result.TryGetValue(key, out _))
                    {
                        throw Error("Duplicate object property '" + key + "'.");
                    }

                    result.Add(key, value);
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private JsonArray ParseArray()
            {
                Expect('[');
                JsonArray result = new JsonArray();
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                StringBuilder result = new StringBuilder();
                while (!IsAtEnd)
                {
                    char character = text[position++];
                    if (character == '"')
                    {
                        return result.ToString();
                    }

                    if (character < 0x20)
                    {
                        throw Error("Control characters are not allowed in JSON strings.");
                    }

                    if (character != '\\')
                    {
                        result.Append(character);
                        continue;
                    }

                    if (IsAtEnd)
                    {
                        throw Error("Unterminated JSON string escape.");
                    }

                    switch (text[position++])
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append(ParseUnicodeEscape()); break;
                        default: throw Error("Invalid JSON string escape.");
                    }
                }

                throw Error("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (position + 4 > text.Length)
                {
                    throw Error("Incomplete unicode escape.");
                }

                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    int digit = HexValue(text[position++]);
                    if (digit < 0)
                    {
                        throw Error("Invalid unicode escape.");
                    }

                    value = (value << 4) | digit;
                }

                return (char)value;
            }

            private string ParseNumber()
            {
                int start = position;
                TryConsume('-');
                if (TryConsume('0'))
                {
                    if (!IsAtEnd && IsDigit(text[position]))
                    {
                        throw Error("Numbers cannot have leading zeroes.");
                    }
                }
                else
                {
                    ConsumeDigits("Expected a digit in number.");
                }

                if (TryConsume('.'))
                {
                    ConsumeDigits("Expected a digit after decimal point.");
                }

                if (!IsAtEnd && (text[position] == 'e' || text[position] == 'E'))
                {
                    position++;
                    if (!IsAtEnd && (text[position] == '+' || text[position] == '-'))
                    {
                        position++;
                    }

                    ConsumeDigits("Expected an exponent digit.");
                }

                return text.Substring(start, position - start);
            }

            private void ConsumeDigits(string errorMessage)
            {
                int start = position;
                while (!IsAtEnd && IsDigit(text[position]))
                {
                    position++;
                }

                if (start == position)
                {
                    throw Error(errorMessage);
                }
            }

            private void ConsumeLiteral(string literal)
            {
                if (position + literal.Length > text.Length ||
                    string.CompareOrdinal(text, position, literal, 0, literal.Length) != 0)
                {
                    throw Error("Invalid JSON literal.");
                }

                position += literal.Length;
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (!TryConsume(expected))
                {
                    throw Error("Expected '" + expected + "'.");
                }
            }

            private bool TryConsume(char expected)
            {
                if (!IsAtEnd && text[position] == expected)
                {
                    position++;
                    return true;
                }

                return false;
            }

            private void SkipWhitespace()
            {
                while (!IsAtEnd && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n'))
                {
                    position++;
                }
            }

            private bool IsAtEnd
            {
                get { return position >= text.Length; }
            }

            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9') return value - '0';
                if (value >= 'a' && value <= 'f') return value - 'a' + 10;
                if (value >= 'A' && value <= 'F') return value - 'A' + 10;
                return -1;
            }

            private InvalidDataException Error(string message)
            {
                return new InvalidDataException(message + " Position: " + position + ".");
            }
        }

        private static class JsonWriter
        {
            public static string Write(JsonValue value, bool indented)
            {
                StringBuilder result = new StringBuilder();
                WriteValue(result, value, indented, 0);
                return result.ToString();
            }

            private static void WriteValue(StringBuilder result, JsonValue value, bool indented, int depth)
            {
                JsonObject jsonObject = value as JsonObject;
                if (jsonObject != null)
                {
                    WriteObject(result, jsonObject, indented, depth);
                    return;
                }

                JsonArray jsonArray = value as JsonArray;
                if (jsonArray != null)
                {
                    WriteArray(result, jsonArray, indented, depth);
                    return;
                }

                JsonString jsonString = value as JsonString;
                if (jsonString != null)
                {
                    WriteString(result, jsonString.Value);
                    return;
                }

                JsonNumber jsonNumber = value as JsonNumber;
                if (jsonNumber != null)
                {
                    result.Append(jsonNumber.Value);
                    return;
                }

                JsonBool jsonBool = value as JsonBool;
                if (jsonBool != null)
                {
                    result.Append(jsonBool.Value ? "true" : "false");
                    return;
                }

                if (value is JsonNull)
                {
                    result.Append("null");
                    return;
                }

                throw new InvalidOperationException("Unknown JSON value type.");
            }

            private static void WriteObject(StringBuilder result, JsonObject value, bool indented, int depth)
            {
                List<KeyValuePair<string, JsonValue>> properties = new List<KeyValuePair<string, JsonValue>>(value.Properties);
                if (properties.Count == 0)
                {
                    result.Append("{}");
                    return;
                }

                result.Append('{');
                for (int i = 0; i < properties.Count; i++)
                {
                    WriteNewLineAndIndent(result, indented, depth + 1);
                    WriteString(result, properties[i].Key);
                    result.Append(indented ? ": " : ":");
                    WriteValue(result, properties[i].Value, indented, depth + 1);
                    if (i < properties.Count - 1)
                    {
                        result.Append(',');
                    }
                }

                WriteNewLineAndIndent(result, indented, depth);
                result.Append('}');
            }

            private static void WriteArray(StringBuilder result, JsonArray value, bool indented, int depth)
            {
                List<JsonValue> items = new List<JsonValue>(value);
                if (items.Count == 0)
                {
                    result.Append("[]");
                    return;
                }

                result.Append('[');
                for (int i = 0; i < items.Count; i++)
                {
                    WriteNewLineAndIndent(result, indented, depth + 1);
                    WriteValue(result, items[i], indented, depth + 1);
                    if (i < items.Count - 1)
                    {
                        result.Append(',');
                    }
                }

                WriteNewLineAndIndent(result, indented, depth);
                result.Append(']');
            }

            private static void WriteNewLineAndIndent(StringBuilder result, bool indented, int depth)
            {
                if (!indented)
                {
                    return;
                }

                result.Append('\n');
                result.Append(' ', depth * 2);
            }

            private static void WriteString(StringBuilder result, string value)
            {
                result.Append('"');
                foreach (char character in value)
                {
                    switch (character)
                    {
                        case '"': result.Append("\\\""); break;
                        case '\\': result.Append("\\\\"); break;
                        case '\b': result.Append("\\b"); break;
                        case '\f': result.Append("\\f"); break;
                        case '\n': result.Append("\\n"); break;
                        case '\r': result.Append("\\r"); break;
                        case '\t': result.Append("\\t"); break;
                        default:
                            if (character < 0x20 || char.IsSurrogate(character))
                            {
                                result.Append("\\u");
                                result.Append(((int)character).ToString("X4"));
                            }
                            else
                            {
                                result.Append(character);
                            }
                            break;
                    }
                }

                result.Append('"');
            }
        }
    }
}
#endif
