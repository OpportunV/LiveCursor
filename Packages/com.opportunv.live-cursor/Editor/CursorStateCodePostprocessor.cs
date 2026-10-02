using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorStateCodePostprocessor : AssetPostprocessor
    {
        public static void Regenerate(string codePath)
        {
            List<string> stateNames = new();
            List<string> sources = new();
            string className = null;
            string @namespace = null;

            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(CursorSet)}"))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith($".{CursorSetImporter.Extension}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var definition = TryRead(assetPath);
                if (definition == null || ResolveCodePath(assetPath, definition) != codePath)
                {
                    continue;
                }

                if (className == null)
                {
                    className = definition.code.className;
                    @namespace = definition.code.@namespace;
                }
                else if (className != definition.code.className || @namespace != definition.code.@namespace)
                {
                    Debug.LogWarning(
                        $"[Live Cursor] '{assetPath}' writes state constants to '{codePath}' with a different class name or namespace; using {@namespace}.{className}.");
                }

                sources.Add(Path.GetFileName(assetPath));
                foreach (var state in definition.states ?? Array.Empty<CursorStateDefinition>())
                {
                    if (!string.IsNullOrEmpty(state.name) && !stateNames.Contains(state.name))
                    {
                        stateNames.Add(state.name);
                    }
                }
            }

            if (className == null || !CursorStateCodeGenerator.IsValidIdentifier(className))
            {
                return;
            }

            sources.Sort(StringComparer.Ordinal);
            var code = CursorStateCodeGenerator.Generate(className, @namespace, stateNames, sources);
            if (File.Exists(codePath) && File.ReadAllText(codePath) == code)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(codePath) ?? string.Empty);
            File.WriteAllText(codePath, code);
            AssetDatabase.ImportAsset(codePath);
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            HashSet<string> codePaths = new(StringComparer.Ordinal);
            foreach (var assetPath in importedAssets)
            {
                if (!assetPath.EndsWith($".{CursorSetImporter.Extension}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var definition = TryRead(assetPath);
                var codePath = definition != null ? ResolveCodePath(assetPath, definition) : null;
                if (codePath != null)
                {
                    codePaths.Add(codePath);
                }
            }

            foreach (var codePath in codePaths)
            {
                Regenerate(codePath);
            }
        }

        private static CursorSetDefinition TryRead(string assetPath)
        {
            try
            {
                return JsonUtility.FromJson<CursorSetDefinition>(File.ReadAllText(assetPath));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ResolveCodePath(string assetPath, CursorSetDefinition definition)
        {
            if (definition.code == null || string.IsNullOrEmpty(definition.code.path))
            {
                return null;
            }

            var directory = Path.GetDirectoryName(assetPath) ?? string.Empty;
            var fullPath = Path.GetFullPath(Path.Combine(directory, definition.code.path));
            return Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath).Replace('\\', '/');
        }
    }
}
