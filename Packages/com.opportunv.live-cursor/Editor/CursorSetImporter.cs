using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    [ScriptedImporter(3, Extension)]
    public sealed class CursorSetImporter : ScriptedImporter
    {
        public const string Extension = "cursorset";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            CursorImportReport report = new();
            var definition = Parse(ctx.assetPath, report);
            using CursorFileFrameLoader loader = new(Path.GetDirectoryName(ctx.assetPath) ?? string.Empty);
            CursorSetBaker baker = new(loader, report);
            var set = baker.Bake(definition, Path.GetFileNameWithoutExtension(ctx.assetPath));

            foreach (var dependency in loader.Dependencies)
            {
                if (dependency.StartsWith("Assets/", StringComparison.Ordinal) ||
                    dependency.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    ctx.DependsOnSourceAsset(dependency);
                }
                else
                {
                    report.Warning($"'{dependency}' is outside the project; changes to it will not reimport the set.");
                }
            }

            foreach (var texture in baker.Textures)
            {
                ctx.AddObjectToAsset(texture.name, texture);
            }

            ctx.AddObjectToAsset("CursorSet", set, FindIcon(set));
            ctx.SetMainObject(set);

            foreach (var error in report.Errors)
            {
                ctx.LogImportError(error, set);
            }

            foreach (var warning in report.Warnings)
            {
                ctx.LogImportWarning(warning, set);
            }
        }

        private static Texture2D FindIcon(CursorSet set)
        {
            if (set.StateCount == 0 || set.SizeCount == 0)
            {
                return null;
            }

            var loop = set.GetState(0).Loop;
            return loop is { FrameCount: > 0 } ? loop.GetFrame(0).GetTexture(set.SizeCount - 1) : null;
        }

        private static CursorSetDefinition Parse(string assetPath, CursorImportReport report)
        {
            try
            {
                return JsonUtility.FromJson<CursorSetDefinition>(File.ReadAllText(assetPath));
            }
            catch (Exception exception)
            {
                report.Error($"Could not read '{assetPath}': {exception.Message}");
                return null;
            }
        }
    }
}
