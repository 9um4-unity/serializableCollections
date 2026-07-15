using System.Text.RegularExpressions;
using NUnit.Framework;
using Gum4.SerializableCollections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gum4.SerializableCollections.Tests
{
    // 중복/null 키·값 경고는 Application.isPlaying일 때만 발생한다 (에디터 편집 중엔 조용).
    // 아래 테스트는 EditMode/PlayMode 양쪽에서 실행되도록 작성되어, 각 모드에서
    // 실제로 그 계약이 지켜지는지를 그 자리에서 확인한다.
    public class SerializableBiDictionaryTests
    {
        static SerializableBiDictionary<string, int> FromJson(string json)
            => JsonUtility.FromJson<SerializableBiDictionary<string, int>>(json);

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
        public void TryGetKey_ExistingValue_ReturnsTrueAndKey()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""fear"",""Value"":2}]}");

            Assert.IsTrue(dict.TryGetKey(2, out var key));
            Assert.AreEqual("fear", key);
        }

        [Test]
        public void TryGetKey_MissingValue_ReturnsFalseAndDefault()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1}]}");

            Assert.IsFalse(dict.TryGetKey(99, out var key));
            Assert.AreEqual(default(string), key);
        }

        [Test]
        public void TryGetKey_EmptyDictionary_ReturnsFalse()
        {
            var dict = new SerializableBiDictionary<string, int>();

            Assert.IsFalse(dict.TryGetKey(1, out _));
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
        public void ToReverseDictionary_ContainsAllPairs()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""fear"",""Value"":2}]}");

            var result = dict.ToReverseDictionary();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("joy", result[1]);
            Assert.AreEqual("fear", result[2]);
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
        public void ToReverseDictionary_ReturnsIndependentCopy()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1}]}");

            var copy = dict.ToReverseDictionary();
            copy[1] = "sorrow";

            dict.TryGetKey(1, out var key);
            Assert.AreEqual("joy", key);
        }

        [Test]
        public void JsonRoundTrip_PreservesPairs()
        {
            var original = FromJson(@"{""_pairs"":[{""Key"":""awe"",""Value"":7}]}");

            var restored = JsonUtility.FromJson<SerializableBiDictionary<string, int>>(JsonUtility.ToJson(original));

            Assert.AreEqual(1, restored.Pairs.Count);
            Assert.IsTrue(restored.TryGetValue("awe", out var value));
            Assert.AreEqual(7, value);
            Assert.IsTrue(restored.TryGetKey(7, out var key));
            Assert.AreEqual("awe", key);
        }

        [Test]
        public void Pairs_ReflectsSerializedOrder()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""b"",""Value"":2},{""Key"":""a"",""Value"":1}]}");

            Assert.AreEqual("b", dict.Pairs[0].Key);
            Assert.AreEqual("a", dict.Pairs[1].Key);
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

        // ── 중복 값: Key는 유일해도 Value가 겹치면 역방향 조회가 깨진다 ──

        [Test]
        public void ToReverseDictionary_DuplicateValues_FirstPairWins_WarnsOnlyWhenPlaying()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""fear"",""Value"":1}]}");
            if (Application.isPlaying)
                LogAssert.Expect(LogType.Error, new Regex("중복 값 '1'"));

            var result = dict.ToReverseDictionary();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("joy", result[1]);
            if (!Application.isPlaying)
                LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TryGetKey_DuplicateValues_FirstPairWins_WarnsOnlyWhenPlaying()
        {
            var dict = FromJson(@"{""_pairs"":[{""Key"":""joy"",""Value"":1},{""Key"":""fear"",""Value"":1}]}");
            if (Application.isPlaying)
                LogAssert.Expect(LogType.Error, new Regex("중복 값 '1'"));

            var found = dict.TryGetKey(1, out var key);

            Assert.IsTrue(found);
            Assert.AreEqual("joy", key);
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
            dict.TryGetKey(1, out _);
            dict.ToDictionary();
            dict.ToReverseDictionary();

            LogAssert.NoUnexpectedReceived();
        }
    }
}
