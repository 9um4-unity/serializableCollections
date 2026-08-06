using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Gum4.SerializableCollections;
using Object = UnityEngine.Object;

namespace Gum4.SerializableCollections.Editor
{
    [CustomPropertyDrawer(typeof(SerializableDictionaryBase), true)]
    public class SerializableDictionaryDrawer : PropertyDrawer
    {
        private const string PairsField = "_pairs";
        private const float Pad     = 2f;
        private const float Spacing = 2f;   // Key 행과 Value 행 사이 간격

        private readonly Dictionary<(Object obj, string path), ReorderableList> _cache = new();

        // 제네릭 Pair.Value 필드에는 직접 속성을 붙일 수 없으므로, 딕셔너리 필드 자체에 붙은
        // [SerializableTextArea]를 대신 읽어 Value(문자열)에 적용한다.
        // Unity 내장 [TextArea]는 쓰지 않는다 — List 필드에 붙이면 하위 모든 프로퍼티(Key 포함)에도
        // 전파되어 문자열이 아닌 Key에까지 TextAreaDrawer가 오작동한다.
        private SerializableTextAreaAttribute _textArea;
        private bool _textAreaResolved;

        private void ResolveTextArea()
        {
            if (_textAreaResolved) return;
            _textAreaResolved = true;
            if (fieldInfo == null) return;
            var attrs = fieldInfo.GetCustomAttributes(typeof(SerializableTextAreaAttribute), true);
            if (attrs.Length > 0) _textArea = (SerializableTextAreaAttribute)attrs[0];
        }

        // [ElementAttribute(ElementTarget.Key/Value, ...)]로 딕셔너리 필드에 붙은 PropertyAttribute를
        // Key/Value에 대신 적용하기 위해 미리 풀어둔다.
        private PropertyAttribute[] _keyAttrs = Array.Empty<PropertyAttribute>();
        private PropertyAttribute[] _valueAttrs = Array.Empty<PropertyAttribute>();
        private bool _elementAttrsResolved;

        private void ResolveElementAttributes()
        {
            if (_elementAttrsResolved) return;
            _elementAttrsResolved = true;
            if (fieldInfo == null) return;
            var byTarget = ElementAttributeForwarder.ResolveAll(fieldInfo);
            if (byTarget.TryGetValue(ElementTarget.Key, out var k)) _keyAttrs = k;
            if (byTarget.TryGetValue(ElementTarget.Value, out var v)) _valueAttrs = v;
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
                var isDup    = IsDuplicate(pairsRef, index, keyProp);

                rect.y      += Pad;
                rect.height -= Pad * 2f;

                DrawPair(rect, elem, isDup);
            };

            return list;
        }

        // ── 렌더링 ────────────────────────────────────────────────

        private void DrawPair(Rect rect, SerializedProperty elem, bool isDup)
        {
            if (IsSimple(elem))
            {
                if (isDup) EditorGUI.DrawRect(rect, new Color(1f, 0.15f, 0.15f, 0.15f));
                DrawInline(rect, elem, isDup);
                return;
            }

            // 복합형: Key 한 줄 → Value 한 줄(또는 펼쳐지는 foldout/TextArea)
            var keyProp = elem.FindPropertyRelative("Key");
            var valProp = elem.FindPropertyRelative("Value");
            var keyH    = ElementAttributeForwarder.GetPropertyHeight(keyProp, new GUIContent("Key"), _keyAttrs);

            var keyRect = new Rect(rect.x, rect.y,              rect.width, keyH);
            var valRect = new Rect(rect.x, rect.y + keyH + Spacing,
                                   rect.width, rect.height - keyH - Spacing);

            if (isDup)
            {
                EditorGUI.DrawRect(rect, new Color(1f, 0.15f, 0.15f, 0.15f));
                EditorGUI.DrawRect(keyRect, new Color(1f, 0.2f, 0.2f, 0.35f));
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                ElementAttributeForwarder.PropertyField(keyRect, keyProp, new GUIContent("Key"), _keyAttrs);
                GUI.backgroundColor = prev;
            }
            else
            {
                ElementAttributeForwarder.PropertyField(keyRect, keyProp, new GUIContent("Key"), _keyAttrs);
            }

            EditorGUI.indentLevel++;
            DrawValue(valRect, valProp, new GUIContent("Value"));
            EditorGUI.indentLevel--;
        }

        private void DrawValue(Rect rect, SerializedProperty valProp, GUIContent label)
        {
            if (IsTextAreaValue(valProp))
            {
                var lineH = EditorGUIUtility.singleLineHeight;
                var labelRect = new Rect(rect.x, rect.y, rect.width, lineH);
                var areaRect  = new Rect(rect.x, rect.y + lineH, rect.width, rect.height - lineH);

                EditorGUI.LabelField(labelRect, label);
                var indentedAreaRect = EditorGUI.IndentedRect(areaRect);
                valProp.stringValue = EditorGUI.TextArea(indentedAreaRect, valProp.stringValue, EditorStyles.textArea);
                return;
            }

            ElementAttributeForwarder.PropertyField(rect, valProp, label, _valueAttrs);
        }

        private void DrawInline(Rect rect, SerializedProperty elem, bool dupKey)
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

            if (dupKey)
            {
                EditorGUI.DrawRect(new Rect(keyRect.x - 1, keyRect.y, keyRect.width + 1, keyRect.height),
                    new Color(1f, 0.2f, 0.2f, 0.35f));
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                ElementAttributeForwarder.PropertyField(keyRect, keyProp, GUIContent.none, _keyAttrs);
                GUI.backgroundColor = prev;
            }
            else
            {
                ElementAttributeForwarder.PropertyField(keyRect, keyProp, GUIContent.none, _keyAttrs);
            }

            var prevCol = GUI.color;
            GUI.color = new Color(0.55f, 0.55f, 0.55f);
            EditorGUI.LabelField(arrRect, "→", EditorStyles.centeredGreyMiniLabel);
            GUI.color = prevCol;

            ElementAttributeForwarder.PropertyField(valRect, valProp, GUIContent.none, _valueAttrs);
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

        private bool IsTextAreaValue(SerializedProperty valProp)
            => _textArea != null && valProp.propertyType == SerializedPropertyType.String;

        private bool IsSimple(SerializedProperty elem)
        {
            var k = elem.FindPropertyRelative("Key");
            var v = elem.FindPropertyRelative("Value");
            if (k == null || v == null) return false;
            if (k.hasVisibleChildren || v.hasVisibleChildren) return false;
            if (IsTextAreaValue(v)) return false; // TextArea는 항상 Key 아래 세로 배치로 그린다
            return true;
        }

        private float PairHeight(SerializedProperty elem)
        {
            if (IsSimple(elem)) return EditorGUIUtility.singleLineHeight;
            var keyProp = elem.FindPropertyRelative("Key");
            var valProp = elem.FindPropertyRelative("Value");
            return ElementAttributeForwarder.GetPropertyHeight(keyProp, new GUIContent("Key"), _keyAttrs)
                 + Spacing
                 + ValueHeight(valProp);
        }

        // 대략적인 줄바꿈 폭 추정치 — 실제 Inspector 너비에 따라 오차는 있지만,
        // EditorGUIUtility.currentViewWidth(OnGUI 컨텍스트 밖에서 호출 시 예외)에 기대지 않기 위해 고정값을 쓴다.
        private const int TextAreaCharsPerLine = 60;

        private float ValueHeight(SerializedProperty valProp)
        {
            if (!IsTextAreaValue(valProp))
                return ElementAttributeForwarder.GetPropertyHeight(valProp, new GUIContent("Value"), _valueAttrs);

            var lineH    = EditorGUIUtility.singleLineHeight;
            var lines    = CountWrappedLines(valProp.stringValue);
            var clamped  = Mathf.Clamp(lines, _textArea.minLines, _textArea.maxLines);

            return lineH + clamped * lineH; // 라벨 한 줄 + 텍스트 영역
        }

        private static int CountWrappedLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;

            var total = 0;
            foreach (var segment in text.Split('\n'))
                total += Mathf.Max(1, Mathf.CeilToInt(segment.Length / (float)TextAreaCharsPerLine));
            return Mathf.Max(1, total);
        }

        private static bool IsDuplicate(SerializedProperty pairsProp, int self, SerializedProperty keyProp)
        {
            if (keyProp == null) return false;
            var target = SerializedPropertyCompare.ValueString(keyProp);
            var n      = pairsProp.arraySize;
            for (var i = 0; i < n; i++)
            {
                if (i == self) continue;
                var other = pairsProp.GetArrayElementAtIndex(i).FindPropertyRelative("Key");
                if (other != null && SerializedPropertyCompare.ValueString(other) == target) return true;
            }
            return false;
        }

        // ── PropertyDrawer 진입점 ────────────────────────────────

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ResolveTextArea();
            ResolveElementAttributes();
            return GetList(property).GetHeight();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ResolveTextArea();
            ResolveElementAttributes();
            EditorGUI.BeginProperty(position, label, property);
            // ReorderableList는 indentLevel을 무시하므로, 중첩된 필드일 때 부모 들여쓰기에 맞춰 직접 보정한다.
            position = EditorGUI.IndentedRect(position);
            GetList(property).DoList(position);
            EditorGUI.EndProperty();
        }
    }
}
