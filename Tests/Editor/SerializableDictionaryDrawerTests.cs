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
    // SerializableDictionaryDrawer가 [SerializableTextArea]를 딕셔너리 필드에서 읽어 Value에만
    // 적용하는지, 그리고 Unity 내장 [TextArea]처럼 Key(enum)로 잘못 전파되지 않는지 확인한다.
    public class SerializableDictionaryDrawerTests
    {
        private enum TestPhase { Intro, Middle, Outro }

        private class TestHolder : ScriptableObject
        {
            [SerializeField, SerializableTextArea(3, 5)]
            public SerializableDictionary<TestPhase, string> dialogues = new();
        }

        private TestHolder _holder;

        [SetUp]
        public void SetUp() => _holder = ScriptableObject.CreateInstance<TestHolder>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_holder);

        private static void AddPair(TestHolder holder, TestPhase key, string value)
        {
            var pairsField = typeof(SerializableDictionary<TestPhase, string>)
                .GetField("_pairs", BindingFlags.NonPublic | BindingFlags.Instance);
            var pairs = (List<SerializableDictionary<TestPhase, string>.Pair>)pairsField.GetValue(holder.dialogues);
            pairs.Add(new SerializableDictionary<TestPhase, string>.Pair { Key = key, Value = value });
        }

        [Test]
        public void GetPropertyHeight_TextAreaValue_DoesNotLeakToEnumKey()
        {
            AddPair(_holder, TestPhase.Middle, "여러 줄로 이어지는 아주 긴 대사 텍스트입니다.");
            var so = new SerializedObject(_holder);
            var dialoguesProp = so.FindProperty(nameof(TestHolder.dialogues));

            var height = 0f;
            // Unity 내장 [TextArea]를 썼을 때 재현되던 버그: Key(enum)에도 TextArea가 전파되어
            // EditorGUI.GetPropertyHeight가 "type is not a supported string value" 에러를 던졌다.
            Assert.DoesNotThrow(() => height = EditorGUI.GetPropertyHeight(dialoguesProp, true));

            LogAssert.NoUnexpectedReceived();
            Assert.Greater(height, 0f);
        }

        [Test]
        public void GetPropertyHeight_EmptyDictionary_DoesNotThrow()
        {
            var so = new SerializedObject(_holder);
            var dialoguesProp = so.FindProperty(nameof(TestHolder.dialogues));

            Assert.DoesNotThrow(() => EditorGUI.GetPropertyHeight(dialoguesProp, true));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GetPropertyHeight_LongText_TallerThanShortText()
        {
            // 드로어가 (target, propertyPath) 단위로 ReorderableList를 캐싱하므로,
            // 같은 오브젝트를 재사용하면 캐시된 높이가 재계산 없이 그대로 나올 수 있다 — 별도 오브젝트로 비교한다.
            var shortHolder = ScriptableObject.CreateInstance<TestHolder>();
            var longHolder = ScriptableObject.CreateInstance<TestHolder>();
            try
            {
                AddPair(shortHolder, TestPhase.Intro, "짧다");
                var shortHeight = EditorGUI.GetPropertyHeight(
                    new SerializedObject(shortHolder).FindProperty(nameof(TestHolder.dialogues)), true);

                // minLines(3) 클램프보다 확실히 많은 줄로 감싸이도록 충분히 길게 반복한다.
                AddPair(longHolder, TestPhase.Intro, string.Concat(System.Linq.Enumerable.Repeat(
                    "이것은 여러 줄에 걸쳐 자동으로 줄바꿈이 일어날 만큼 충분히 긴 대사 문자열입니다. ", 5)));
                var longHeight = EditorGUI.GetPropertyHeight(
                    new SerializedObject(longHolder).FindProperty(nameof(TestHolder.dialogues)), true);

                Assert.Greater(longHeight, shortHeight);
            }
            finally
            {
                Object.DestroyImmediate(shortHolder);
                Object.DestroyImmediate(longHolder);
            }
        }
    }
}
