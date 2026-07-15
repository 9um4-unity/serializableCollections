using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gum4.SerializableCollections
{
    [Serializable]
    public abstract class SerializableHashSetBase { }

    [Serializable]
    public class SerializableHashSet<T> : SerializableHashSetBase, ISerializationCallbackReceiver
    {
        [SerializeField] private List<T> _items = new();

        // 역직렬화 시 무효화되고 첫 조회에서 재구축 — 조회는 amortized O(1).
        private HashSet<T> _cache;

        public IReadOnlyList<T> Items => _items;

        private HashSet<T> Cache => _cache ??= BuildCache();

        public bool Contains(T item) => Cache.Contains(item);

        public HashSet<T> ToHashSet() => new(Cache);

        // 중복 항목은 Inspector 입력 실수 — 캐시 재구축(역직렬화 후 첫 조회)마다 1회 요란하게 알린다.
        // 에디터에서 Inspector 편집 중에는 조용히 처리하고, 런타임(Play/빌드)에서만 경고한다 —
        // 이 클래스는 MonoBehaviour가 아니므로 소유 컴포넌트의 Awake에 훅을 걸 수 없어,
        // "언제 처음 쓰이는가"를 곧 검증 시점으로 삼는다.
        private HashSet<T> BuildCache()
        {
            bool logErrors = Application.isPlaying;
            var set = new HashSet<T>(_items.Count);
            foreach (var item in _items)
            {
                if (!set.Add(item) && logErrors)
                    Debug.LogError(
                        $"SerializableHashSet<{typeof(T).Name}>: " +
                        $"중복 항목 '{item}' 발견 — Inspector 데이터를 정리하세요.");
            }
            return set;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() => _cache = null;
    }
}
