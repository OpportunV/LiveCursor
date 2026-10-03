using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorStateCodePostprocessor : AssetPostprocessor
    {
        public static List<CursorStateCodeTarget> FindTargets()
        {
            List<CursorStateCodeTarget> targets = new();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(CursorSet)}"))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith($".{CursorSetImporter.Extension}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var definition = TryRead(assetPath);
                var codePath = definition != null ? ResolveCodePath(assetPath, definition) : null;
                if (codePath == null)
                {
                    continue;
                }

                var target = targets.Find(candidate => candidate.CodePath == codePath);
                if (target == null)
                {
                    target = new(codePath, definition.code.className, definition.code.@namespace);
                    targets.Add(target);
                }
                else if (target.ClassName != definition.code.className ||
                         target.Namespace != (definition.code.@namespace ?? string.Empty))
                {
                    Debug.LogWarning(
                        $"[Live Cursor] '{assetPath}' writes state constants to '{codePath}' with a different class name or namespace; using {target.FullName}.");
                }

                target.Add(assetPath, definition);
            }

            targets.Sort((left, right) => string.CompareOrdinal(left.CodePath, right.CodePath));
            return targets;
        }

        public static void Regenerate(string codePath)
        {
            var target = FindTargets().Find(candidate => candidate.CodePath == codePath);
            if (target == null || !CursorStateCodeGenerator.IsValidIdentifier(target.ClassName))
            {
                return;
            }

            WarnMissingStates(target);

            List<string> sources = new();
            foreach (var setPath in target.SetPaths)
            {
                sources.Add(Path.GetFileName(setPath));
            }

            sources.Sort(StringComparer.Ordinal);
            var code = CursorStateCodeGenerator.Generate(target.ClassName, target.Namespace, target.StateNames,
                sources);
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

        private static void WarnMissingStates(CursorStateCodeTarget target)
        {
            for (var i = 0; i < target.SetPaths.Count; i++)
            {
                var missing = target.MissingStates(i);
                if (missing.Count == 0)
                {
                    continue;
                }

                var setPath = target.SetPaths[i];
                Debug.LogWarning(
                    $"[Live Cursor] '{Path.GetFileName(setPath)}' shares {target.FullName} but has no {string.Join(", ", missing)} state; setting it will be ignored while this set is active.",
                    AssetDatabase.LoadMainAssetAtPath(setPath));
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
