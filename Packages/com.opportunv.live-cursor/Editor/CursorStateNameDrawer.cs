using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    [CustomPropertyDrawer(typeof(CursorStateNameAttribute))]
    internal sealed class CursorStateNameDrawer : PropertyDrawer
    {
        private static List<string> _names;

        static CursorStateNameDrawer()
        {
            CursorSetImportWatcher.Imported += _ => _names = null;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var names = Names();
            if (property.propertyType != SerializedPropertyType.String || names.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var allowNone = ((CursorStateNameAttribute)attribute).AllowNone;
            List<GUIContent> options = new();
            if (allowNone)
            {
                options.Add(new("None"));
            }

            foreach (var name in names)
            {
                options.Add(new(name));
            }

            var value = property.stringValue;
            var index = string.IsNullOrEmpty(value) ? (allowNone ? 0 : -1) : names.IndexOf(value);
            if (index >= 0 && allowNone && !string.IsNullOrEmpty(value))
            {
                index++;
            }

            var missing = index < 0 && !string.IsNullOrEmpty(value);
            if (missing)
            {
                options.Add(new($"{value} (not in any cursor set)"));
                index = options.Count - 1;
            }

            using var scope = new EditorGUI.PropertyScope(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUI.Popup(position, scope.content, index, options.ToArray());
            EditorGUI.showMixedValue = false;
            if (!EditorGUI.EndChangeCheck() || (missing && selected == options.Count - 1))
            {
                return;
            }

            property.stringValue = allowNone && selected == 0 ? string.Empty : names[selected - (allowNone ? 1 : 0)];
        }

        private static List<string> Names()
        {
            if (_names != null)
            {
                return _names;
            }

            _names = new();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(CursorSet)}"))
            {
                var set = AssetDatabase.LoadAssetAtPath<CursorSet>(AssetDatabase.GUIDToAssetPath(guid));
                if (!set)
                {
                    continue;
                }

                for (var i = 0; i < set.StateCount; i++)
                {
                    var name = set.GetStateId(i).Name;
                    if (!_names.Contains(name))
                    {
                        _names.Add(name);
                    }
                }
            }

            return _names;
        }
    }
}
