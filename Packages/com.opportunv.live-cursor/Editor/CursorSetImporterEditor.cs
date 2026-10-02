using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    [CustomEditor(typeof(CursorSetImporter))]
    internal sealed class CursorSetImporterEditor : ScriptedImporterEditor
    {
        public override void OnInspectorGUI()
        {
            var importer = (CursorSetImporter)target;
            if (GUILayout.Button("Edit in Cursor Set Builder", GUILayout.Height(24f)))
            {
                CursorSetBuilderWindow.OpenForDefinition(importer.assetPath);
            }

            ApplyRevertGUI();
        }
    }
}
