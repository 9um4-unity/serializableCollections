using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Gum4.SerializableCollections;
using Object = UnityEngine.Object;

namespace Gum4.SerializableCollections.Editor.Tests
{
    // 전용 PropertyDrawer가 한 줄로 그리는 사용자 타입은 Key/Value/Item 자리에서도 인라인 한 줄로
    // 배치되어야 한다 — 내부 필드가 있다는 이유만으로 foldout 2행으로 밀려나면, 참조 래퍼처럼
    // "한 줄짜리 값"으로 설계된 타입을 컬렉션 원소로 쓸 수 없다.
    public class InlineElementLayoutTests
    {
        [Serializable]
        public class SingleLineProbe
        {
            [SerializeField] public int a;
            [SerializeField] public int b;
        }

        [Serializable]
        public class TwoLineProbe
        {
            [SerializeField] public int a;
            [SerializeField] public int b;
        }

        // 전용 드로어가 없는 복합 타입 — 기존처럼 foldout 2행으로 그려져야 한다.
        [Serializable]
        public class PlainProbe
        {
            [SerializeField] public int a;
            [SerializeField] public int b;
        }

        private class Holder : ScriptableObject
        {
            public SerializableDictionary<string, int> baselineDict = new();
            public SerializableDictionary<string, SingleLineProbe> singleLineDict = new();
            public SerializableDictionary<string, TwoLineProbe> twoLineDict = new();
            public SerializableDictionary<string, PlainProbe> plainDict = new();

            public SerializableBiDictionary<string, int> baselineBiDict = new();
            public SerializableBiDictionary<string, SingleLineProbe> singleLineBiDict = new();

            public SerializableHashSet<int> baselineSet = new();
            public SerializableHashSet<SingleLineProbe> singleLineSet = new();
            public SerializableHashSet<PlainProbe> plainSet = new();
        }

        private readonly List<Object> _holders = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var holder in _holders)
                if (holder != null)
                    Object.DestroyImmediate(holder);
            _holders.Clear();
        }

        // 드로어가 (targetObject, propertyPath) 단위로 ReorderableList를 캐싱하므로
        // 비교 대상마다 새 오브젝트를 쓴다.
        private SerializedProperty Measure(string fieldName)
        {
            var holder = ScriptableObject.CreateInstance<Holder>();
            _holders.Add(holder);

            var field = typeof(Holder).GetField(fieldName);
            AddOneElement(field.GetValue(holder));

            return new SerializedObject(holder).FindProperty(fieldName);
        }

        // 접힌 foldout은 전용 드로어가 있든 없든 한 줄이라 인라인 여부를 구분하지 못한다 —
        // 원소를 펼친 상태에서 재야 "인라인 한 줄" vs "펼쳐지는 복합형"이 실제로 갈린다.
        private float Height(string fieldName)
        {
            var prop = Measure(fieldName);
            var backing = prop.FindPropertyRelative("_pairs") ?? prop.FindPropertyRelative("_items");
            var element = backing.GetArrayElementAtIndex(0);
            element.isExpanded = true;
            foreach (var child in new[] { "Key", "Value" })
            {
                var childProp = element.FindPropertyRelative(child);
                if (childProp != null) childProp.isExpanded = true;
            }
            return EditorGUI.GetPropertyHeight(prop, true);
        }

        // 컬렉션의 백킹 리스트(_pairs / _items)에 기본값 원소 하나를 넣는다.
        private static void AddOneElement(object collection)
        {
            var type = collection.GetType();
            var backing = type.GetField("_pairs", BindingFlags.NonPublic | BindingFlags.Instance)
                       ?? type.GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (IList)backing.GetValue(collection);
            var elementType = backing.FieldType.GetGenericArguments()[0];
            list.Add(Activator.CreateInstance(elementType));
        }

        [Test]
        public void Dictionary_SingleLineDrawerValue_LaysOutInline()
        {
            Assert.AreEqual(Height(nameof(Holder.baselineDict)),
                            Height(nameof(Holder.singleLineDict)), 0.01f);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Dictionary_MultiLineDrawerValue_StaysStacked()
        {
            Assert.Greater(Height(nameof(Holder.twoLineDict)),
                           Height(nameof(Holder.baselineDict)));
        }

        [Test]
        public void Dictionary_ValueWithoutDrawer_StaysStacked()
        {
            Assert.Greater(Height(nameof(Holder.plainDict)),
                           Height(nameof(Holder.baselineDict)));
        }

        [Test]
        public void BiDictionary_SingleLineDrawerValue_LaysOutInline()
        {
            Assert.AreEqual(Height(nameof(Holder.baselineBiDict)),
                            Height(nameof(Holder.singleLineBiDict)), 0.01f);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void HashSet_SingleLineDrawerItem_LaysOutInline()
        {
            Assert.AreEqual(Height(nameof(Holder.baselineSet)),
                            Height(nameof(Holder.singleLineSet)), 0.01f);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void HashSet_ItemWithoutDrawer_StaysStacked()
        {
            Assert.Greater(Height(nameof(Holder.plainSet)),
                           Height(nameof(Holder.baselineSet)));
        }
    }

    [CustomPropertyDrawer(typeof(InlineElementLayoutTests.SingleLineProbe))]
    public class SingleLineProbeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            => EditorGUI.PropertyField(position, property.FindPropertyRelative("a"), label);
    }

    [CustomPropertyDrawer(typeof(InlineElementLayoutTests.TwoLineProbe))]
    public class TwoLineProbeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight * 2f + 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            => EditorGUI.LabelField(position, label);
    }
}
