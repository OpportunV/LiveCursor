using Opportunv.LiveCursor;
using Opportunv.LiveCursor.Samples;
using Opportunv.LiveCursor.UGUI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Dev.Editor
{
    internal static class DemoSceneBuilder
    {
        private const string Root = "Assets/LiveCursorSamples/Demo";
        private const string PanelSettingsPath = Root + "/UI/DemoPanelSettings.asset";

        private static readonly Color _background = new(0.17f, 0.2f, 0.27f);

        [MenuItem("Live Cursor Dev/Samples/Build Demo Scenes")]
        private static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (LoadSkins() == null)
            {
                return;
            }

            BuildUIToolkitScene();
            BuildUGUIScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Live Cursor Dev] Demo scenes built.");
        }

        private static CursorSet[] LoadSkins()
        {
            var twinkle = AssetDatabase.LoadAssetAtPath<CursorSet>($"{Root}/Cursors/Twinkle/Twinkle.cursorset");
            var midnight = AssetDatabase.LoadAssetAtPath<CursorSet>($"{Root}/Cursors/Midnight/Midnight.cursorset");
            if (twinkle && midnight)
            {
                return new[] { twinkle, midnight };
            }

            Debug.LogError("[Live Cursor Dev] Demo cursor sets are missing; run Tools/DemoCursors/generate.py first.");
            return null;
        }

        private static void BuildUIToolkitScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var skins = LoadSkins();
            CreateCamera(Vector3.zero, Quaternion.identity, false);
            var cursor = CreateCursor(skins[0]);

            GameObject ui = new("UI Toolkit Demo");
            var document = ui.AddComponent<UIDocument>();
            document.panelSettings = LoadOrCreatePanelSettings();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{Root}/UI/DemoPanel.uxml");
            var demo = ui.AddComponent<UIToolkitDemo>();
            SerializedObject serialized = new(demo);
            serialized.FindProperty("_cursor").objectReferenceValue = cursor;
            SetArray(serialized.FindProperty("_skins"), skins);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, $"{Root}/UI Toolkit Demo.unity");
        }

        private static void BuildUGUIScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var skins = LoadSkins();
            CreateCamera(new(0f, 7f, -7.5f), Quaternion.Euler(42f, 0f, 0f), true);

            GameObject light = new("Directional Light");
            var lightComponent = light.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cursor = CreateCursor(skins[0]);

            GameObject eventSystem = new("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<DemoInputModule>();

            GameObject world = new("World");
            SetReference(world.AddComponent<DemoWorld>(), "_cursor", cursor);

            var canvas = CreateCanvas();
            var demo = canvas.gameObject.AddComponent<UGUIDemo>();
            var panel = CreatePanel(canvas.transform);

            AddText(panel, "uGUI and scene demo", 22, FontStyle.Bold);
            AddText(panel, "Hover the buttons, drag the crates, aim at the sphere.", 14, FontStyle.Normal);
            AddHoverButton(panel, "Link", cursor, DemoCursorStates.Pointer, default);
            AddHoverButton(panel, "Press and hold", cursor, DemoCursorStates.Grab, DemoCursorStates.Grabbing);
            AddHoverButton(panel, "Not available", cursor, DemoCursorStates.Blocked, default).interactable = false;
            AddInputField(panel, cursor);
            UnityEventTools.AddPersistentListener(
                AddHoverButton(panel, "Run a 3 s task", cursor, DemoCursorStates.Pointer, default).onClick,
                demo.RunTask);
            UnityEventTools.AddPersistentListener(
                AddHoverButton(panel, "Toggle idle", cursor, DemoCursorStates.Pointer, default).onClick,
                demo.ToggleIdle);
            for (var i = 0; i < skins.Length; i++)
            {
                UnityEventTools.AddIntPersistentListener(
                    AddHoverButton(panel, $"Skin: {skins[i].name}", cursor, DemoCursorStates.Pointer, default).onClick,
                    demo.SetSkin, i);
            }

            var status = CreateStatus(canvas.transform);
            SerializedObject serialized = new(demo);
            serialized.FindProperty("_cursor").objectReferenceValue = cursor;
            serialized.FindProperty("_status").objectReferenceValue = status;
            SetArray(serialized.FindProperty("_skins"), skins);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, $"{Root}/uGUI/uGUI and Scene Demo.unity");
        }

        private static void CreateCamera(Vector3 position, Quaternion rotation, bool physicsRaycaster)
        {
            GameObject camera = new("Main Camera") { tag = "MainCamera" };
            camera.transform.SetPositionAndRotation(position, rotation);
            var component = camera.AddComponent<Camera>();
            component.clearFlags = CameraClearFlags.SolidColor;
            component.backgroundColor = _background;
            if (physicsRaycaster)
            {
                camera.AddComponent<PhysicsRaycaster>();
            }
        }

        private static CursorAnimator CreateCursor(CursorSet set)
        {
            GameObject cursor = new("Cursor");
            var animator = cursor.AddComponent<CursorAnimator>();
            SetReference(animator, "_cursorSet", set);
            return animator;
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>($"{Root}/UI/DemoTheme.tss");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new(1280, 720);
            settings.match = 0.5f;
            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            return settings;
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static Transform CreatePanel(Transform canvas)
        {
            var panel = DefaultControls.CreatePanel(UiResources());
            panel.name = "Panel";
            panel.transform.SetParent(canvas, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new(0f, 0f);
            rect.anchorMax = new(0f, 1f);
            rect.pivot = new(0f, 0.5f);
            rect.sizeDelta = new(320f, -40f);
            rect.anchoredPosition = new(20f, 10f);
            panel.GetComponent<Image>().color = new(0.1f, 0.12f, 0.17f, 0.85f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new(16, 16, 16, 16);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return panel.transform;
        }

        private static void AddText(Transform parent, string text, int size, FontStyle style)
        {
            var label = DefaultControls.CreateText(UiResources());
            label.name = "Label";
            label.transform.SetParent(parent, false);
            var component = label.GetComponent<Text>();
            component.text = text;
            component.fontSize = size;
            component.fontStyle = style;
            component.color = Color.white;
            component.raycastTarget = false;
        }

        private static Button AddHoverButton(Transform parent, string text, CursorAnimator cursor, CursorStateId state,
            CursorStateId pressedState)
        {
            var buttonObject = DefaultControls.CreateButton(UiResources());
            buttonObject.name = text;
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponentInChildren<Text>().text = text;
            buttonObject.AddComponent<LayoutElement>().preferredHeight = 38f;
            buttonObject.AddComponent<CursorHover>().Configure(cursor, state, pressedState);
            return buttonObject.GetComponent<Button>();
        }

        private static void AddInputField(Transform parent, CursorAnimator cursor)
        {
            var field = DefaultControls.CreateInputField(UiResources());
            field.name = "Input Field";
            field.transform.SetParent(parent, false);
            field.AddComponent<LayoutElement>().preferredHeight = 38f;
            field.GetComponent<InputField>().text = "Type here";
            field.AddComponent<CursorHover>().Configure(cursor, DemoCursorStates.Text);
        }

        private static Text CreateStatus(Transform canvas)
        {
            var status = DefaultControls.CreateText(UiResources());
            status.name = "Status";
            status.transform.SetParent(canvas, false);
            var rect = (RectTransform)status.transform;
            rect.anchorMin = new(0f, 0f);
            rect.anchorMax = new(1f, 0f);
            rect.pivot = new(0.5f, 0f);
            rect.sizeDelta = new(-380f, 30f);
            rect.anchoredPosition = new(180f, 12f);
            var text = status.GetComponent<Text>();
            text.fontSize = 14;
            text.color = new(0.8f, 0.85f, 0.95f);
            text.raycastTarget = false;
            return text;
        }

        private static DefaultControls.Resources UiResources()
        {
            return new()
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
            };
        }

        private static void SetReference(Object target, string property, Object value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(SerializedProperty property, CursorSet[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
