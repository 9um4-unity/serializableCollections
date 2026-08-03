using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Gum4.SerializableCollections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gum4.SerializableCollections.Tests
{
    // 중복/null 키 경고는 Application.isPlaying일 때만 발생한다 (에디터 편집 중엔 조용).
    // 아래 테스트는 EditMode/PlayMode 양쪽에서 실행되도록 작성되어, 각 모드에서
    // 실제로 그 계약이 지켜지는지를 그 자리에서 확인한다.
    public class SerializableDictionaryTests
    {
        static SerializableDictionary<string, int> FromJson(string json)
            => JsonUtility.FromJson<SerializableDictionary<string, int>>(json);

        [Test]
        public void TryGetValue_ExistingKey_ReturnsTrueAndValue()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""fear"",""Value"":2}]}");

            Assert.IsTrue(dict.TryGetValue("fear", out var value));
            Assert.AreEqual(2, value);
        }

        [Test]
        public void TryGetValue_MissingKey_ReturnsFalseAndDefault()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1}]}");

            Assert.IsFalse(dict.TryGetValue("sorrow", out var value));
            Assert.AreEqual(default(int), value);
        }

        [Test]
        public void TryGetValue_EmptyDictionary_ReturnsFalse()
        {
            var dict = new SerializableDictionary<string, int>();

            Assert.IsFalse(dict.TryGetValue("joy", out _));
        }

        [Test]
        public void ToDictionary_ContainsAllPairs()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""fear"",""Value"":2}]}");

            var result = dict.ToDictionary();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result["joy"]);
            Assert.AreEqual(2, result["fear"]);
        }

        [Test]
        public void ToDictionary_ReturnsIndependentCopy()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1}]}");

            var copy = dict.ToDictionary();
            copy["joy"] = 99;

            dict.TryGetValue("joy", out var value);
            Assert.AreEqual(1, value);
        }

        [Test]
        public void JsonRoundTrip_PreservesPairs()
        {
            var original = FromJson(@"{""_pairs"":[{""Key"":""awe"",""Value"":7}]}");

            var restored = JsonUtility.FromJson<SerializableDictionary<string, int>>(JsonUtility.ToJson(original));

            Assert.AreEqual(1, restored.Pairs.Count);
            Assert.IsTrue(restored.TryGetValue("awe", out var value));
            Assert.AreEqual(7, value);
        }

        [Test]
        public void Pairs_ReflectsSerializedOrder()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""b"",""Value"":2},{""Key"":""a"",""Value"":1}]}");

            Assert.AreEqual("b", dict.Pairs[0].Key);
            Assert.AreEqual("a", dict.Pairs[1].Key);
        }

        // ── foreach: ToDictionary() 없이 직접 순회 ─────────────────────

        [Test]
        public void Foreach_YieldsAllPairs_WithoutToDictionary()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""fear"",""Value"":2}]}");

            var seen = new Dictionary<string, int>();
            foreach (var pair in dict)
                seen[pair.Key] = pair.Value;

            Assert.AreEqual(2, seen.Count);
            Assert.AreEqual(1, seen["joy"]);
            Assert.AreEqual(2, seen["fear"]);
        }

        [Test]
        public void Foreach_EmptyDictionary_YieldsNothing()
        {
            var dict = new SerializableDictionary<string, int>();

            var count = 0;
            foreach (var _ in dict) count++;

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Foreach_AsIEnumerable_YieldsAllPairs()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1}]}");

            IEnumerable<KeyValuePair<string, int>> enumerable = dict;
            var seen = new List<KeyValuePair<string, int>>(enumerable);

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual("joy", seen[0].Key);
            Assert.AreEqual(1, seen[0].Value);
        }

        // ── 중복 키: Runtime에서만 경고, Edit 모드에서는 조용 ──────────

        [Test]
        public void ToDictionary_DuplicateKeys_FirstValueWins_WarnsOnlyWhenPlaying()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""joy"",""Value"":9}]}");
            if (Application.isPlaying)
                LogAssert.Expect(LogType.Error, new Regex("중복 키 'joy'"));

            var result = dict.ToDictionary();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result["joy"]);
            if (!Application.isPlaying)
                LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TryGetValue_DuplicateKeys_FirstValueWins_WarnsOnlyWhenPlaying()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""joy"",""Value"":9}]}");
            if (Application.isPlaying)
                LogAssert.Expect(LogType.Error, new Regex("중복 키 'joy'"));

            var found = dict.TryGetValue("joy", out var value);

            Assert.IsTrue(found);
            Assert.AreEqual(1, value);
            if (!Application.isPlaying)
                LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DuplicateKeys_WarnsOncePerCacheRebuild_WhenPlaying()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""joy"",""Value"":9}]}");
            if (Application.isPlaying)
                LogAssert.Expect(LogType.Error, new Regex("중복 키 'joy'"));

            dict.TryGetValue("joy", out _);
            dict.TryGetValue("joy", out _);
            dict.ToDictionary();

            LogAssert.NoUnexpectedReceived();
        }
    }
}
