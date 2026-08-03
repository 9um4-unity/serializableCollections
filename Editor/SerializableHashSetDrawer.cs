using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Gum4.SerializableCollections;
using Object = UnityEngine.Object;

namespace Gum4.SerializableCollections.Editor
{
    [CustomPropertyDrawer(typeof(SerializableHashSetBase), true)]
    public class SerializableHashSetDrawer : PropertyDrawer
    {
        private const string ItemsField = "_items";
        private const float Pad = 2f;

        private readonly Dictionary<(Object obj, string path), ReorderableList> _cache = new();

        private ReorderableList GetList(SerializedProperty setProp)
        {
            var target = setProp.serializedObject.targetObject;
            var cacheKey = (target, setProp.propertyPath);
            if (!_cache.TryGetValue(cacheKey, out var list))
            {
                list = BuildList(setProp);
                _cache[cacheKey] = list;
            }
            list.serializedProperty = setProp.FindPropertyRelative(ItemsField);
            return list;
        }

        private static ReorderableList BuildList(SerializedProperty setProp)
        {
            var itemsProp = setProp.FindPropertyRelative(ItemsField);
            var list = new ReorderableList(
                setProp.serializedObject, itemsProp,
                draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true);

            list.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, setProp.displayName);

            list.elementHeightCallback = index =>
            {
                var elem = list.serializedProperty.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(elem, true) + Pad * 2f;
            };

            list.drawElementCallback = (rect, index, _, _) =>
            {
                var itemsRef = list.serializedProperty;
                var elem     = itemsRef.GetArrayElementAtIndex(index);
                var isDup    = IsDuplicate(itemsRef, index, elem);

                rect.y      += Pad;
                rect.height -= Pad * 2f;

                // 복합 타입은 "Element N" 레이블을 붙여 foldout을 표시한다
                var label = elem.hasVisibleChildren
                    ? new GUIContent($"Element {index}")
                    : GUIContent.none;

                if (isDup)
                {
                    EditorGUI.DrawRect(rect, new Color(1f, 0.15f, 0.15f, 0.15f));
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                    EditorGUI.PropertyField(rect, elem, label, true);
                    GUI.backgroundColor = prev;
                }
                else
                {
                    EditorGUI.PropertyField(rect, elem, label, true);
                }
            };

            return list;
        }

        // ── 중복 판정 ─────────────────────────────────────────────

        private static bool IsDuplicate(SerializedProperty itemsProp, int self, SerializedProperty elem)
        {
            var target = SerializedPropertyCompare.ValueString(elem);
            var n = itemsProp.arraySize;
            for (var i = 0; i < n; i++)
            {
                if (i == self) continue;
                if (SerializedPropertyCompare.ValueString(itemsProp.GetArrayElementAtIndex(i)) == target) return true;
            }
            return false;
        }

        // ── PropertyDrawer 진입점 ────────────────────────────────

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => GetList(property).GetHeight();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            // ReorderableList는 indentLevel을 무시하므로, 중첩된 필드일 때 부모 들여쓰기에 맞춰 직접 보정한다.
            position = EditorGUI.IndentedRect(position);
            GetList(property).DoList(position);
            EditorGUI.EndProperty();
        }
    }
}
