using System;
using UnityEditor;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorSetImportWatcher : AssetPostprocessor
    {
        public static event Action<string> Imported;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Imported == null)
            {
                return;
            }

            foreach (var assetPath in importedAssets)
            {
                if (assetPath.EndsWith($".{CursorSetImporter.Extension}", StringComparison.OrdinalIgnoreCase))
                {
                    Imported(assetPath);
                }
            }
        }
    }
}
