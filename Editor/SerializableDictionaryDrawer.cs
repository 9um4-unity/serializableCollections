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
            var keyH    = EditorGUI.GetPropertyHeight(keyProp, true);

            var keyRect = new Rect(rect.x, rect.y,              rect.width, keyH);
            var valRect = new Rect(rect.x, rect.y + keyH + Spacing,
                                   rect.width, rect.height - keyH - Spacing);

            if (isDup)
            {
                EditorGUI.DrawRect(rect, new Color(1f, 0.15f, 0.15f, 0.15f));
                EditorGUI.DrawRect(keyRect, new Color(1f, 0.2f, 0.2f, 0.35f));
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                EditorGUI.PropertyField(keyRect, keyProp, new GUIContent("Key"), true);
                GUI.backgroundColor = prev;
            }
            else
            {
                EditorGUI.PropertyField(keyRect, keyProp, new GUIContent("Key"), true);
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

            EditorGUI.PropertyField(rect, valProp, label, true);
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
                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
                GUI.backgroundColor = prev;
            }
            else
            {
                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
            }

            var prevCol = GUI.color;
            GUI.color = new Color(0.55f, 0.55f, 0.55f);
            EditorGUI.LabelField(arrRect, "→", EditorStyles.centeredGreyMiniLabel);
            GUI.color = prevCol;

            EditorGUI.PropertyField(valRect, valProp, GUIContent.none);
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
            return EditorGUI.GetPropertyHeight(keyProp, true)
                 + Spacing
                 + ValueHeight(valProp);
        }

        private float ValueHeight(SerializedProperty valProp)
        {
            if (!IsTextAreaValue(valProp)) return EditorGUI.GetPropertyHeight(valProp, true);

            var lineH   = EditorGUIUtility.singleLineHeight;
            var width   = Mathf.Max(EditorGUIUtility.currentViewWidth - 60f, 50f);
            var content = new GUIContent(valProp.stringValue);
            var textH   = EditorStyles.textArea.CalcHeight(content, width);
            var minH    = lineH * _textArea.minLines;
            var maxH    = lineH * _textArea.maxLines;
            var clamped = Mathf.Clamp(textH, minH, maxH);

            return lineH + clamped; // 라벨 한 줄 + 텍스트 영역
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
            return GetList(property).GetHeight();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ResolveTextArea();
            EditorGUI.BeginProperty(position, label, property);
            // ReorderableList는 indentLevel을 무시하므로, 중첩된 필드일 때 부모 들여쓰기에 맞춰 직접 보정한다.
            position = EditorGUI.IndentedRect(position);
            GetList(property).DoList(position);
            EditorGUI.EndProperty();
        }
    }
}
