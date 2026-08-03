using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Gum4.SerializableCollections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gum4.SerializableCollections.Tests
{
    // 중복 항목 경고는 Application.isPlaying일 때만 발생한다 (에디터 편집 중엔 조용).
    // 아래 테스트는 EditMode/PlayMode 양쪽에서 실행되도록 작성되어, 각 모드에서
    // 실제로 그 계약이 지켜지는지를 그 자리에서 확인한다.
    public class SerializableHashSetTests
    {
        static SerializableHashSet<int> FromJson(string json)
            => JsonUtility.FromJson<SerializableHashSet<int>>(json);

        [Test]
        public void Contains_ExistingItem_ReturnsTrue()
        {
            var set = FromJson(@"{""_items"":[1,3,5]}");

            Assert.IsTrue(set.Contains(3));
        }

        [Test]
        public void Contains_MissingItem_ReturnsFalse()
        {
            var set = FromJson(@"{""_items"":[1,3,5]}");

            Assert.IsFalse(set.Contains(4));
        }

        [Test]
        public void Contains_EmptySet_ReturnsFalse()
        {
            var set = new SerializableHashSet<int>();

            Assert.IsFalse(set.Contains(1));
        }

        [Test]
        public void ToHashSet_ReturnsIndependentCopy()
        {
            var set = FromJson(@"{""_items"":[4]}");

            var copy = set.ToHashSet();
            copy.Add(99);

            Assert.IsFalse(set.Contains(99));
        }

        [Test]
        public void Items_ExposesSerializedListIncludingDuplicates()
        {
            var set = FromJson(@"{""_items"":[2,2,7]}");

            Assert.AreEqual(3, set.Items.Count);
        }

        [Test]
        public void JsonRoundTrip_PreservesItems()
        {
            var original = FromJson(@"{""_items"":[4,8]}");

            var restored = JsonUtility.FromJson<SerializableHashSet<int>>(JsonUtility.ToJson(original));

            Assert.AreEqual(2, restored.Items.Count);
            Assert.IsTrue(restored.Contains(4));
            Assert.IsTrue(restored.Contains(8));
        }

        // ── foreach: ToHashSet() 없이 직접 순회 ────────────────────────

        [Test]
        public void Foreach_YieldsAllItems_WithoutToHashSet()
        {
            var set = FromJson(@"{""_items"":[1,3,5]}");

            var seen = new HashSet<int>();
            foreach (var item in set) seen.Add(item);

            Assert.AreEqual(3, seen.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 3, 5 }, seen);
        }

        [Test]
        public void Foreach_EmptySet_YieldsNothing()
        {
            var set = new SerializableHashSet<int>();

            var count = 0;
            foreach (var _ in set) count++;

            Assert.AreEqual(0, count);
        }

        // ── 중복 항목: Runtime에서만 경고, Edit 모드에서는 조용 ────────

        [Test]
        public void ToHashSet_DeduplicatesItems_WarnsOnlyWhenPlaying()
        {
            var set = FromJson(@"{""_items"":[2,2,7,7,7]}");
            if (Application.isPlaying)
            {
                LogAssert.Expect(LogType.Error, new Regex("중복 항목 '2'"));
                LogAssert.Expect(LogType.Error, new Regex("중복 항목 '7'"));
                LogAssert.Expect(LogType.Error, new Regex("중복 항목 '7'"));
            }

            var result = set.ToHashSet();

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(2));
            Assert.IsTrue(result.Contains(7));
            if (!Application.isPlaying)
                LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DuplicateItems_WarnsOncePerCacheRebuild_WhenPlaying()
        {
            var set = FromJson(@"{""_items"":[2,2]}");
            if (Application.isPlaying)
                LogAssert.Expect(LogType.Error, new Regex("중복 항목 '2'"));

            set.Contains(2);
            set.Contains(2);
            set.ToHashSet();

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ToHashSet_NoDuplicates_LogsNothing()
        {
            var set = FromJson(@"{""_items"":[1,2,3]}");

            var result = set.ToHashSet();

            Assert.AreEqual(3, result.Count);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
