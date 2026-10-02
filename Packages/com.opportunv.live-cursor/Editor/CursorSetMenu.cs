using System.IO;
using UnityEditor;

namespace Opportunv.LiveCursor.Editor
{
    internal static class CursorSetMenu
    {
        [MenuItem("Assets/Create/Live Cursor/Cursor Set", priority = 200)]
        private static void CreateCursorSet()
        {
            CursorSetBuilderWindow.OpenForFolder(SelectedFolder());
        }

        [MenuItem("Assets/Live Cursor/Build Cursor Set From Folder", priority = 1000)]
        private static void BuildFromFolder()
        {
            CursorSetBuilderWindow.OpenForFolder(SelectedFolder());
        }

        [MenuItem("Assets/Live Cursor/Build Cursor Set From Folder", true)]
        private static bool CanBuildFromFolder()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return AssetDatabase.IsValidFolder(path) && path != "Assets";
        }

        [MenuItem("Window/Live Cursor/Cursor Set Builder")]
        private static void OpenBuilder()
        {
            CursorSetBuilderWindow.OpenForFolder(SelectedFolder());
        }

        private static string SelectedFolder()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (path.EndsWith($".{CursorSetImporter.Extension}"))
            {
                return Path.GetDirectoryName(path)?.Replace('\\', '/');
            }

            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/');
        }
    }
}
