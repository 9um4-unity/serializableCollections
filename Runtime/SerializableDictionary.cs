using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gum4.SerializableCollections
{
    // PropertyDrawer는 open-generic을 타겟할 수 없으므로 비제너릭 베이스를 경유한다.
    [Serializable]
    public abstract class SerializableDictionaryBase { }

    [Serializable]
    public class SerializableDictionary<TKey, TValue> : SerializableDictionaryBase, ISerializationCallbackReceiver,
        IEnumerable<KeyValuePair<TKey, TValue>>
    {
        [Serializable]
        public struct Pair
        {
            public TKey Key;
            public TValue Value;
        }

        [SerializeField] private List<Pair> _pairs = new();

        // 역직렬화 시 무효화되고 첫 조회에서 재구축 — 조회는 amortized O(1).
        private Dictionary<TKey, TValue> _cache;

        public IReadOnlyList<Pair> Pairs => _pairs;

        private Dictionary<TKey, TValue> Cache => _cache ??= BuildCache();

        public bool TryGetValue(TKey key, out TValue value) => Cache.TryGetValue(key, out value);

        public Dictionary<TKey, TValue> ToDictionary() => new(Cache);

        // 중복/null 키는 Inspector 입력 실수 — 캐시 재구축(역직렬화 후 첫 조회)마다 1회 요란하게 알린다.
        // 에디터에서 Inspector 편집 중에는 조용히 처리하고, 런타임(Play/빌드)에서만 경고한다 —
        // 이 클래스는 MonoBehaviour가 아니므로 소유 컴포넌트의 Awake에 훅을 걸 수 없어,
        // "언제 처음 쓰이는가"를 곧 검증 시점으로 삼는다.
        private Dictionary<TKey, TValue> BuildCache()
        {
            bool logErrors = Application.isPlaying;
            var dict = new Dictionary<TKey, TValue>(_pairs.Count);
            foreach (var p in _pairs)
            {
                if (p.Key is null)
                {
                    if (logErrors)
                        Debug.LogError(
                            $"SerializableDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>: " +
                            "null 키 발견 — 해당 항목은 무시됩니다. Inspector 데이터를 정리하세요.");
                    continue;
                }
                if (!dict.TryAdd(p.Key, p.Value) && logErrors)
                    Debug.LogError(
                        $"SerializableDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>: " +
                        $"중복 키 '{p.Key}' 발견 — 첫 번째 값만 사용됩니다. Inspector 데이터를 정리하세요.");
            }
            return dict;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() => _cache = null;

        // ToDictionary()로 복사본을 만들지 않아도 바로 foreach를 쓸 수 있도록 캐시를 직접 노출한다.
        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => Cache.GetEnumerator();

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => Cache.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Cache.GetEnumerator();
    }
}
