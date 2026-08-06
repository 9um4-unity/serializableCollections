using System;
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

        // [ElementAttribute(ElementTarget.Item, ...)]로 HashSet 필드에 붙은 PropertyAttribute를
        // Item에 대신 적용하기 위해 미리 풀어둔다.
        private PropertyAttribute[] _itemAttrs = Array.Empty<PropertyAttribute>();
        private bool _elementAttrsResolved;

        private void ResolveElementAttributes()
        {
            if (_elementAttrsResolved) return;
            _elementAttrsResolved = true;
            if (fieldInfo == null) return;
            var byTarget = ElementAttributeForwarder.ResolveAll(fieldInfo);
            if (byTarget.TryGetValue(ElementTarget.Item, out var i)) _itemAttrs = i;
        }

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

        private ReorderableList BuildList(SerializedProperty setProp)
        {
            var itemsProp = setProp.FindPropertyRelative(ItemsField);
            var list = new ReorderableList(
                setProp.serializedObject, itemsProp,
                draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true);

            list.drawHeaderCallback = rect => DrawHeaderLabel(rect, setProp.displayName);

            list.elementHeightCallback = index =>
            {
                var elem = list.serializedProperty.GetArrayElementAtIndex(index);
                return ElementAttributeForwarder.GetPropertyHeight(elem, GUIContent.none, _itemAttrs) + Pad * 2f;
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
                    ElementAttributeForwarder.PropertyField(rect, elem, label, _itemAttrs);
                    GUI.backgroundColor = prev;
                }
                else
                {
                    ElementAttributeForwarder.PropertyField(rect, elem, label, _itemAttrs);
                }
            };

            return list;
        }

        // 리스트 박스(테이블) 자체는 부모 들여쓰기를 따라 이미 밀려 있으므로,
        // 헤더 제목까지 LabelField가 indentLevel을 또 반영하면 이중으로 들여써진다 — 헤더만 0으로 리셋.
        private static void DrawHeaderLabel(Rect rect, string text)
        {
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.LabelField(rect, text);
            EditorGUI.indentLevel = indent;
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
        {
            ResolveElementAttributes();
            return GetList(property).GetHeight();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ResolveElementAttributes();
            EditorGUI.BeginProperty(position, label, property);
            // ReorderableList는 indentLevel을 무시하므로, 중첩된 필드일 때 부모 들여쓰기에 맞춰 직접 보정한다.
            position = EditorGUI.IndentedRect(position);
            GetList(property).DoList(position);
            EditorGUI.EndProperty();
        }
    }
}
