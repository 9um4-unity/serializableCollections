using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Gum4.SerializableCollections;
using Object = UnityEngine.Object;

namespace Gum4.SerializableCollections.Editor
{
    [CustomPropertyDrawer(typeof(SerializableBiDictionaryBase), true)]
    public class SerializableBiDictionaryDrawer : PropertyDrawer
    {
        private const string PairsField = "_pairs";
        private const float Pad     = 2f;
        private const float Spacing = 2f;   // Key 행과 Value 행 사이 간격

        private readonly Dictionary<(Object obj, string path), ReorderableList> _cache = new();

        // [ElementAttribute(ElementTarget.Key/Value, ...)]로 딕셔너리 필드에 붙은 PropertyAttribute를
        // Key/Value에 대신 적용하기 위해 미리 풀어둔다.
        private PropertyAttribute[] _keyAttrs = Array.Empty<PropertyAttribute>();
        private PropertyAttribute[] _valueAttrs = Array.Empty<PropertyAttribute>();
        private Type _keyType;
        private Type _valueType;
        private bool _elementAttrsResolved;

        private void ResolveElementAttributes()
        {
            if (_elementAttrsResolved) return;
            _elementAttrsResolved = true;
            if (fieldInfo == null) return;
            var byTarget = ElementAttributeForwarder.ResolveAll(fieldInfo);
            if (byTarget.TryGetValue(ElementTarget.Key, out var k)) _keyAttrs = k;
            if (byTarget.TryGetValue(ElementTarget.Value, out var v)) _valueAttrs = v;

            var args = ElementAttributeForwarder.ResolveElementTypes(fieldInfo, typeof(SerializableBiDictionary<,>));
            if (args != null) (_keyType, _valueType) = (args[0], args[1]);
        }

        private ReorderableList GetList(SerializedProperty dictProp)
        {
            var target   = dictProp.serializedObject.targetObject;
            var cacheKey = (target, dictProp.propertyPath);
            if (!_cache.TryGetValue(cacheKey, out var list))
            {
                list = BuildList(dictProp);
                _cache[cacheKey] = list;
            }
            list.serializedProperty = dictProp.FindPropertyRelative(PairsField);
            return list;
        }

        private ReorderableList BuildList(SerializedProperty dictProp)
        {
            var pairsProp = dictProp.FindPropertyRelative(PairsField);
            var list = new ReorderableList(
                dictProp.serializedObject, pairsProp,
                draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true);

            list.drawHeaderCallback = rect => DrawHeaderLabel(rect, dictProp.displayName);

            list.elementHeightCallback = index =>
            {
                var elem = list.serializedProperty.GetArrayElementAtIndex(index);
                return PairHeight(elem) + Pad * 2f;
            };

            list.drawElementCallback = (rect, index, _, _) =>
            {
                var pairsRef = list.serializedProperty;
                var elem     = pairsRef.GetArrayElementAtIndex(index);
                var keyProp  = elem.FindPropertyRelative("Key");
                var valProp  = elem.FindPropertyRelative("Value");
                var dupKey   = IsDuplicate(pairsRef, index, keyProp, "Key");
                var dupVal   = IsDuplicate(pairsRef, index, valProp, "Value");

                rect.y      += Pad;
                rect.height -= Pad * 2f;

                DrawPair(rect, elem, dupKey, dupVal);
            };

            return list;
        }

        // ── 렌더링 ────────────────────────────────────────────────

        private void DrawPair(Rect rect, SerializedProperty elem, bool dupKey, bool dupVal)
        {
            if (IsSimple(elem))
            {
                if (dupKey || dupVal) EditorGUI.DrawRect(rect, new Color(1f, 0.15f, 0.15f, 0.15f));
                DrawInline(rect, elem, dupKey, dupVal);
                return;
            }

            // 복합형: Key 한 줄 → Value 한 줄(또는 펼쳐지는 foldout)
            var keyProp = elem.FindPropertyRelative("Key");
            var valProp = elem.FindPropertyRelative("Value");
            var keyH    = ElementAttributeForwarder.GetPropertyHeight(keyProp, new GUIContent("Key"), _keyAttrs);

            var keyRect = new Rect(rect.x, rect.y,              rect.width, keyH);
            var valRect = new Rect(rect.x, rect.y + keyH + Spacing,
                                   rect.width, rect.height - keyH - Spacing);

            if (dupKey || dupVal) EditorGUI.DrawRect(rect, new Color(1f, 0.15f, 0.15f, 0.15f));

            DrawField(keyRect, keyProp, new GUIContent("Key"), dupKey, _keyAttrs);

            EditorGUI.indentLevel++;
            DrawField(valRect, valProp, new GUIContent("Value"), dupVal, _valueAttrs);
            EditorGUI.indentLevel--;
        }

        private void DrawInline(Rect rect, SerializedProperty elem, bool dupKey, bool dupVal)
        {
            const float Arrow = 20f;
            var keyW = (rect.width - Arrow) * 0.38f;
            var valW = rect.width - Arrow - keyW;
            var h    = EditorGUIUtility.singleLineHeight;

            var keyRect = new Rect(rect.x,                rect.y, keyW,  h);
            var arrRect = new Rect(rect.x + keyW,          rect.y, Arrow, h);
            var valRect = new Rect(rect.x + keyW + Arrow,  rect.y, valW,  h);

            var keyProp = elem.FindPropertyRelative("Key");
            var valProp = elem.FindPropertyRelative("Value");

            DrawField(keyRect, keyProp, GUIContent.none, dupKey, _keyAttrs);

            var prevCol = GUI.color;
            GUI.color = new Color(0.55f, 0.55f, 0.55f);
            EditorGUI.LabelField(arrRect, "⇄", EditorStyles.centeredGreyMiniLabel);
            GUI.color = prevCol;

            DrawField(valRect, valProp, GUIContent.none, dupVal, _valueAttrs);
        }

        private static void DrawField(Rect rect, SerializedProperty prop, GUIContent label, bool isDup, PropertyAttribute[] attrs)
        {
            if (isDup)
            {
                EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y, rect.width + 1, rect.height),
                    new Color(1f, 0.2f, 0.2f, 0.35f));
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                ElementAttributeForwarder.PropertyField(rect, prop, label, attrs);
                GUI.backgroundColor = prev;
            }
            else
            {
                ElementAttributeForwarder.PropertyField(rect, prop, label, attrs);
            }
        }

        // ── 유틸 ─────────────────────────────────────────────────

        // 리스트 박스(테이블) 자체는 부모 들여쓰기를 따라 이미 밀려 있으므로,
        // 헤더 제목까지 LabelField가 indentLevel을 또 반영하면 이중으로 들여써진다 — 헤더만 0으로 리셋.
        private static void DrawHeaderLabel(Rect rect, string text)
        {
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.LabelField(rect, text);
            EditorGUI.indentLevel = indent;
        }

        private bool IsSimple(SerializedProperty elem)
        {
            var k = elem.FindPropertyRelative("Key");
            var v = elem.FindPropertyRelative("Value");
            if (k == null || v == null) return false;
            return ElementAttributeForwarder.IsInlineDrawn(k, _keyType, _keyAttrs)
                && ElementAttributeForwarder.IsInlineDrawn(v, _valueType, _valueAttrs);
        }

        private float PairHeight(SerializedProperty elem)
        {
            if (IsSimple(elem)) return EditorGUIUtility.singleLineHeight;
            var keyProp = elem.FindPropertyRelative("Key");
            var valProp = elem.FindPropertyRelative("Value");
            return ElementAttributeForwarder.GetPropertyHeight(keyProp, new GUIContent("Key"), _keyAttrs)
                 + Spacing
                 + ElementAttributeForwarder.GetPropertyHeight(valProp, new GUIContent("Value"), _valueAttrs);
        }

        private static bool IsDuplicate(SerializedProperty pairsProp, int self, SerializedProperty fieldProp, string fieldName)
        {
            if (fieldProp == null) return false;
            var target = SerializedPropertyCompare.ValueString(fieldProp);
            var n      = pairsProp.arraySize;
            for (var i = 0; i < n; i++)
            {
                if (i == self) continue;
                var other = pairsProp.GetArrayElementAtIndex(i).FindPropertyRelative(fieldName);
                if (other != null && SerializedPropertyCompare.ValueString(other) == target) return true;
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
