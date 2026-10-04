using Dev.Diagnostics;
using Opportunv.LiveCursor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dev.Editor
{
    internal static class DiagnosticsSceneBuilder
    {
        private const string ScenePath = "Assets/Dev/Diagnostics/CursorDiagnostics.unity";
        private const string CursorsRoot = "Assets/LiveCursorSamples/Demo/Cursors";

        [MenuItem("Live Cursor Dev/Diagnostics/Build Diagnostics Scene")]
        private static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var twinkle = AssetDatabase.LoadAssetAtPath<CursorSet>($"{CursorsRoot}/Twinkle/Twinkle.cursorset");
            var midnight = AssetDatabase.LoadAssetAtPath<CursorSet>($"{CursorsRoot}/Midnight/Midnight.cursorset");
            if (!twinkle || !midnight)
            {
                Debug.LogError(
                    "[Live Cursor Dev] Demo cursor sets are missing; run Tools/DemoCursors/generate.py first.");
                return;
            }

            GameObject camera = new("Main Camera") { tag = "MainCamera" };
            var cameraComponent = camera.AddComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new(0.17f, 0.2f, 0.27f);

            GameObject diagnostics = new("Cursor Diagnostics");
            SerializedObject serialized = new(diagnostics.AddComponent<CursorDiagnostics>());
            var sets = serialized.FindProperty("_sets");
            sets.arraySize = 2;
            sets.GetArrayElementAtIndex(0).objectReferenceValue = twinkle;
            sets.GetArrayElementAtIndex(1).objectReferenceValue = midnight;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[Live Cursor Dev] Saved '{ScenePath}' and made it the only scene in the build.");
        }
    }
}
