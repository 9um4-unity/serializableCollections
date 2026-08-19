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
    // [ElementAttribute]가 딕셔너리/해시셋 필드에 붙은 Unity 내장 PropertyAttribute(Range 등)를
    // Key/Value/Item에 실제로 전달하는지 확인한다.
    public class ElementAttributeForwarderTests
    {
        private class TestHolder : ScriptableObject
        {
            [SerializeField, ElementAttribute(ElementTarget.Value, typeof(UnityEngine.RangeAttribute), 0f, 10f)]
            public SerializableDictionary<string, float> ratios = new();

            [SerializeField, ElementAttribute(ElementTarget.Item, typeof(UnityEngine.RangeAttribute), 0, 10)]
            public SerializableHashSet<int> levels = new();
        }

        private TestHolder _holder;

        [SetUp]
        public void SetUp() => _holder = ScriptableObject.CreateInstance<TestHolder>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_holder);

        private static void AddDictPair(TestHolder holder, string key, float value)
        {
            var pairsField = typeof(SerializableDictionary<string, float>)
                .GetField("_pairs", BindingFlags.NonPublic | BindingFlags.Instance);
            var pairs = (List<SerializableDictionary<string, float>.Pair>)pairsField.GetValue(holder.ratios);
            pairs.Add(new SerializableDictionary<string, float>.Pair { Key = key, Value = value });
        }

        private static void AddSetItem(TestHolder holder, int value)
        {
            var itemsField = typeof(SerializableHashSet<int>)
                .GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
            var items = (List<int>)itemsField.GetValue(holder.levels);
            items.Add(value);
        }

        [Test]
        public void GetPropertyHeight_DictionaryValueWithRange_DoesNotThrow()
        {
            AddDictPair(_holder, "joy", 3f);
            var so = new SerializedObject(_holder);
            var prop = so.FindProperty(nameof(TestHolder.ratios));

            var height = 0f;
            Assert.DoesNotThrow(() => height = EditorGUI.GetPropertyHeight(prop, true));

            LogAssert.NoUnexpectedReceived();
            Assert.Greater(height, 0f);
        }

        [Test]
        public void GetPropertyHeight_HashSetItemWithRange_DoesNotThrow()
        {
            AddSetItem(_holder, 5);
            var so = new SerializedObject(_holder);
            var prop = so.FindProperty(nameof(TestHolder.levels));

            var height = 0f;
            Assert.DoesNotThrow(() => height = EditorGUI.GetPropertyHeight(prop, true));

            LogAssert.NoUnexpectedReceived();
            Assert.Greater(height, 0f);
        }
    }
}
