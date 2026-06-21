#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaVoiceChat.Core.Editor
{
    public static class ManifestUtils
    {
        public static void AddDependency(string packageName, string version)
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                return;
            }

            string jsonContent = File.ReadAllText(manifestPath);

            // Check if dependency already exists with the same version
            string existingVersion = GetDependencyVersion(jsonContent, packageName);
            if (existingVersion == version)
            {
                return; // Already exists with correct version, do nothing
            }

            // Add or update the dependency
            string updatedJson = AddOrUpdateDependency(jsonContent, packageName, version);
            File.WriteAllText(manifestPath, updatedJson);

            AssetDatabase.Refresh();
        }

        public static void AddScopedRegistry(string name, string url, string[] scopes)
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                return;
            }

            string jsonContent = File.ReadAllText(manifestPath);

            // Extract scopedRegistries array
            string scopedJson = ExtractJsonArray(jsonContent, "scopedRegistries");
            List<ScopedRegistry> registries;

            if (!string.IsNullOrEmpty(scopedJson) && scopedJson != "[]")
            {
                ScopedRegistriesWrapper w = JsonUtility.FromJson<ScopedRegistriesWrapper>("{\"list\":" + scopedJson + "}");
                registries = w?.list ?? new List<ScopedRegistry>();
            }
            else
            {
                registries = new List<ScopedRegistry>();
            }

            // Check if registry with the same URL already exists
            ScopedRegistry existingRegistry = registries.FirstOrDefault(r => r.url == url);

            if (existingRegistry != null)
            {
                // Registry exists, merge scopes and remove duplicates
                HashSet<string> mergedScopes = new HashSet<string>(existingRegistry.scopes ?? new string[0]);
                foreach (string scope in scopes)
                {
                    mergedScopes.Add(scope);
                }
                existingRegistry.scopes = mergedScopes.ToArray();
            }
            else
            {
                // Add new registry if it doesn't exist
                ScopedRegistry newRegistry = new()
                {
                    name = name,
                    url = url,
                    scopes = scopes,
                };
                registries.Add(newRegistry);
            }

            // Serialize the updated registries
            ScopedRegistriesWrapper wrapper = new() { list = registries };
            string updatedScopedJson = JsonUtility.ToJson(wrapper, true);
            // Extract just the array part
            int arrayStart = updatedScopedJson.IndexOf('[');
            int arrayEnd = updatedScopedJson.LastIndexOf(']');
            updatedScopedJson = updatedScopedJson.Substring(arrayStart, arrayEnd - arrayStart + 1);

            // Replace the scopedRegistries section in the original JSON
            string updatedJson = ReplaceScopedRegistries(jsonContent, updatedScopedJson);
            File.WriteAllText(manifestPath, updatedJson);

            AssetDatabase.Refresh();
        }

        private static string GetDependencyVersion(string json, string packageName)
        {
            string dependenciesJson = ExtractJsonObject(json, "dependencies");
            if (string.IsNullOrEmpty(dependenciesJson))
            {
                return null;
            }

            string keyStr = "\"" + packageName + "\"";
            int keyIndex = dependenciesJson.IndexOf(keyStr);
            if (keyIndex == -1) return null;

            int colonIndex = dependenciesJson.IndexOf(':', keyIndex);
            if (colonIndex == -1) return null;

            // Find the version value (quoted string)
            int versionStart = dependenciesJson.IndexOf('\"', colonIndex) + 1;
            int versionEnd = dependenciesJson.IndexOf('\"', versionStart);

            return dependenciesJson.Substring(versionStart, versionEnd - versionStart);
        }

        private static string AddOrUpdateDependency(string originalJson, string packageName, string version)
        {
            string keyStr = "\"dependencies\"";
            int keyIndex = originalJson.IndexOf(keyStr);

            if (keyIndex == -1)
            {
                // Add dependencies if it doesn't exist
                int firstBrace = originalJson.IndexOf('{');
                string newDep = "\n  \"dependencies\": {\n    \"" + packageName + "\": \"" + version + "\"\n  },";
                return originalJson.Insert(firstBrace + 1, newDep);
            }

            int colonIndex = originalJson.IndexOf(':', keyIndex);
            int start = originalJson.IndexOf('{', colonIndex);

            // Find the matching closing brace
            int braceCount = 1;
            int end = start + 1;
            while (braceCount > 0 && end < originalJson.Length)
            {
                if (originalJson[end] == '{') braceCount++;
                else if (originalJson[end] == '}') braceCount--;
                end++;
            }

            string dependenciesContent = originalJson.Substring(start + 1, end - start - 2);

            // Extract just the content lines, preserving structure
            List<string> lines = new List<string>();
            string[] splitLines = dependenciesContent.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in splitLines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    lines.Add(line);
                }
            }

            string packageKeyStr = "\"" + packageName + "\"";
            bool packageFound = false;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains(packageKeyStr))
                {
                    // Update existing dependency
                    int packageKeyIndex = lines[i].IndexOf(packageKeyStr);
                    int packageColonIndex = lines[i].IndexOf(':', packageKeyIndex);
                    int versionStart = lines[i].IndexOf('\"', packageColonIndex) + 1;
                    int versionEnd = lines[i].IndexOf('\"', versionStart);

                    lines[i] = lines[i].Substring(0, versionStart) + version + lines[i].Substring(versionEnd);
                    packageFound = true;
                    break;
                }
            }

            if (!packageFound)
            {
                // Add new dependency
                if (lines.Count > 0)
                {
                    // Get the indentation from the last line before modifying it
                    int indentCount = 0;
                    for (int i = 0; i < lines[lines.Count - 1].Length; i++)
                    {
                        if (lines[lines.Count - 1][i] == ' ')
                            indentCount++;
                        else
                            break;
                    }
                    string indent = new string(' ', indentCount);

                    // Remove trailing comma from last line if it exists, then add it back
                    string lastLineTrimmed = lines[lines.Count - 1].Trim();
                    if (lastLineTrimmed.EndsWith(","))
                    {
                        lastLineTrimmed = lastLineTrimmed.Substring(0, lastLineTrimmed.Length - 1);
                    }

                    lines[lines.Count - 1] = indent + lastLineTrimmed + ",";
                    lines.Add(indent + "\"" + packageName + "\": \"" + version + "\"");
                }
                else
                {
                    lines.Add("    \"" + packageName + "\": \"" + version + "\"");
                }
            }

            string newDependenciesSection = "{\n" + string.Join("\n", lines) + "\n  }";
            return originalJson.Substring(0, start) + newDependenciesSection + originalJson.Substring(end);
        }

        private static string ExtractJsonArray(string json, string key)
        {
            string keyStr = "\"" + key + "\"";
            int keyIndex = json.IndexOf(keyStr);
            if (keyIndex == -1) return null;

            int colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex == -1) return null;

            // Find the opening bracket
            int start = json.IndexOf('[', colonIndex);
            if (start == -1) return null;

            // Find the matching closing bracket
            int bracketCount = 1;
            int end = start + 1;
            while (bracketCount > 0 && end < json.Length)
            {
                if (json[end] == '[') bracketCount++;
                else if (json[end] == ']') bracketCount--;
                end++;
            }

            return json.Substring(start, end - start);
        }

        private static string ReplaceScopedRegistries(string originalJson, string newScopedRegistriesArray)
        {
            string keyStr = "\"scopedRegistries\"";
            int keyIndex = originalJson.IndexOf(keyStr);

            if (keyIndex == -1)
            {
                // Add scopedRegistries if it doesn't exist
                int firstBrace = originalJson.IndexOf('{');
                return originalJson.Insert(firstBrace + 1, "\n  \"scopedRegistries\": " + newScopedRegistriesArray + ",");
            }

            int colonIndex = originalJson.IndexOf(':', keyIndex);
            int start = originalJson.IndexOf('[', colonIndex);

            // Find the matching closing bracket
            int bracketCount = 1;
            int end = start + 1;
            while (bracketCount > 0 && end < originalJson.Length)
            {
                if (originalJson[end] == '[') bracketCount++;
                else if (originalJson[end] == ']') bracketCount--;
                end++;
            }

            // Replace the array
            return originalJson.Substring(0, start) + newScopedRegistriesArray + originalJson.Substring(end);
        }

        private static string ExtractJsonObject(string json, string key)
        {
            string keyStr = "\"" + key + "\"";
            int keyIndex = json.IndexOf(keyStr);
            if (keyIndex == -1) return null;

            int colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex == -1) return null;

            // Find the opening brace
            int start = json.IndexOf('{', colonIndex);
            if (start == -1) return null;

            // Find the matching closing brace
            int braceCount = 1;
            int end = start + 1;
            while (braceCount > 0 && end < json.Length)
            {
                if (json[end] == '{') braceCount++;
                else if (json[end] == '}') braceCount--;
                end++;
            }

            return json.Substring(start, end - start);
        }

        [System.Serializable]
        private class ScopedRegistriesWrapper
        {
            public List<ScopedRegistry> list;
        }

        [System.Serializable]
        private class ScopedRegistry
        {
            public string name;
            public string url;
            public string[] scopes;
        }
    }
}
#endif