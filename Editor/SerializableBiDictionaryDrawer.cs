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

        private static ReorderableList BuildList(SerializedProperty dictProp)
        {
            var pairsProp = dictProp.FindPropertyRelative(PairsField);
            var list = new ReorderableList(
                dictProp.serializedObject, pairsProp,
                draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true);

            list.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, dictProp.displayName);

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

        private static void DrawPair(Rect rect, SerializedProperty elem, bool dupKey, bool dupVal)
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
            var keyH    = EditorGUI.GetPropertyHeight(keyProp, true);

            var keyRect = new Rect(rect.x, rect.y,              rect.width, keyH);
            var valRect = new Rect(rect.x, rect.y + keyH + Spacing,
                                   rect.width, rect.height - keyH - Spacing);

            if (dupKey || dupVal) EditorGUI.DrawRect(rect, new Color(1f, 0.15f, 0.15f, 0.15f));

            DrawField(keyRect, keyProp, new GUIContent("Key"), dupKey);

            EditorGUI.indentLevel++;
            DrawField(valRect, valProp, new GUIContent("Value"), dupVal);
            EditorGUI.indentLevel--;
        }

        private static void DrawInline(Rect rect, SerializedProperty elem, bool dupKey, bool dupVal)
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

            DrawField(keyRect, keyProp, GUIContent.none, dupKey);

            var prevCol = GUI.color;
            GUI.color = new Color(0.55f, 0.55f, 0.55f);
            EditorGUI.LabelField(arrRect, "⇄", EditorStyles.centeredGreyMiniLabel);
            GUI.color = prevCol;

            DrawField(valRect, valProp, GUIContent.none, dupVal);
        }

        private static void DrawField(Rect rect, SerializedProperty prop, GUIContent label, bool isDup)
        {
            if (isDup)
            {
                EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y, rect.width + 1, rect.height),
                    new Color(1f, 0.2f, 0.2f, 0.35f));
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                EditorGUI.PropertyField(rect, prop, label, true);
                GUI.backgroundColor = prev;
            }
            else
            {
                EditorGUI.PropertyField(rect, prop, label, true);
            }
        }

        // ── 유틸 ─────────────────────────────────────────────────

        private static bool IsSimple(SerializedProperty elem)
        {
            var k = elem.FindPropertyRelative("Key");
            var v = elem.FindPropertyRelative("Value");
            return k != null && v != null && !k.hasVisibleChildren && !v.hasVisibleChildren;
        }

        private static float PairHeight(SerializedProperty elem)
        {
            if (IsSimple(elem)) return EditorGUIUtility.singleLineHeight;
            var keyProp = elem.FindPropertyRelative("Key");
            var valProp = elem.FindPropertyRelative("Value");
            return EditorGUI.GetPropertyHeight(keyProp, true)
                 + Spacing
                 + EditorGUI.GetPropertyHeight(valProp, true);
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
