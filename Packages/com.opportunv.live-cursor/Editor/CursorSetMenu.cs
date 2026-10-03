using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

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

        [MenuItem("Assets/Live Cursor/Preview Cursor Set", priority = 1001)]
        private static void PreviewSelected()
        {
            CursorSetPreviewWindow.Open(Selection.activeObject as CursorSet);
        }

        [MenuItem("Assets/Live Cursor/Preview Cursor Set", true)]
        private static bool CanPreviewSelected()
        {
            return Selection.activeObject is CursorSet;
        }

        [MenuItem("Window/Live Cursor/Cursor Set Builder")]
        private static void OpenBuilder()
        {
            CursorSetBuilderWindow.OpenForFolder(SelectedFolder());
        }

        [MenuItem("Window/Live Cursor/Cursor Set Preview")]
        private static void OpenPreview()
        {
            CursorSetPreviewWindow.Open(Selection.activeObject as CursorSet);
        }

        [OnOpenAsset]
        private static bool OpenCursorSet(int instanceId, int line)
        {
            var path = AssetDatabase.GetAssetPath(instanceId);
            if (!path.EndsWith($".{CursorSetImporter.Extension}", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            CursorSetBuilderWindow.OpenForDefinition(path);
            CursorSetPreviewWindow.Open(AssetDatabase.LoadAssetAtPath<CursorSet>(path));
            return true;
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
