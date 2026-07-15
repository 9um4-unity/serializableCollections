using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gum4.SerializableCollections
{
    // PropertyDrawer는 open-generic을 타겟할 수 없으므로 비제너릭 베이스를 경유한다.
    [Serializable]
    public abstract class SerializableBiDictionaryBase { }

    // 양방향 조회를 위해 Key/Value 양쪽 모두 유일해야 한다 — 한쪽이라도 중복되면
    // 역방향 캐시 구축 시 첫 번째 쌍만 유효해지고 나머지는 무시된다.
    [Serializable]
    public class SerializableBiDictionary<TKey, TValue> : SerializableBiDictionaryBase, ISerializationCallbackReceiver
    {
        [Serializable]
        public struct Pair
        {
            public TKey Key;
            public TValue Value;
        }

        [SerializeField] private List<Pair> _pairs = new();

        // 역직렬화 시 무효화되고 첫 조회에서 재구축 — 조회는 amortized O(1).
        private Dictionary<TKey, TValue> _forward;
        private Dictionary<TValue, TKey> _reverse;

        public IReadOnlyList<Pair> Pairs => _pairs;

        private Dictionary<TKey, TValue> Forward { get { EnsureCaches(); return _forward; } }
        private Dictionary<TValue, TKey> Reverse { get { EnsureCaches(); return _reverse; } }

        private void EnsureCaches()
        {
            if (_forward != null) return;
            (_forward, _reverse) = BuildCaches();
        }

        public bool TryGetValue(TKey key, out TValue value) => Forward.TryGetValue(key, out value);

        public bool TryGetKey(TValue value, out TKey key) => Reverse.TryGetValue(value, out key);

        public Dictionary<TKey, TValue> ToDictionary() => new(Forward);

        public Dictionary<TValue, TKey> ToReverseDictionary() => new(Reverse);

        // Key/Value 양쪽의 null·중복은 Inspector 입력 실수 — 캐시 재구축(역직렬화 후 첫 조회)마다 1회 요란하게 알린다.
        // 에디터에서 Inspector 편집 중에는 조용히 처리하고, 런타임(Play/빌드)에서만 경고한다 —
        // 이 클래스는 MonoBehaviour가 아니므로 소유 컴포넌트의 Awake에 훅을 걸 수 없어,
        // "언제 처음 쓰이는가"를 곧 검증 시점으로 삼는다.
        private (Dictionary<TKey, TValue> forward, Dictionary<TValue, TKey> reverse) BuildCaches()
        {
            bool logErrors = Application.isPlaying;
            var forward = new Dictionary<TKey, TValue>(_pairs.Count);
            var reverse = new Dictionary<TValue, TKey>(_pairs.Count);

            foreach (var p in _pairs)
            {
                if (p.Key is null)
                {
                    if (logErrors)
                        Debug.LogError(
                            $"SerializableBiDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>: " +
                            "null 키 발견 — 해당 항목은 무시됩니다. Inspector 데이터를 정리하세요.");
                    continue;
                }
                if (p.Value is null)
                {
                    if (logErrors)
                        Debug.LogError(
                            $"SerializableBiDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>: " +
                            "null 값 발견 — 해당 항목은 무시됩니다. Inspector 데이터를 정리하세요.");
                    continue;
                }
                if (forward.ContainsKey(p.Key))
                {
                    if (logErrors)
                        Debug.LogError(
                            $"SerializableBiDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>: " +
                            $"중복 키 '{p.Key}' 발견 — 첫 번째 쌍만 사용됩니다. Inspector 데이터를 정리하세요.");
                    continue;
                }
                if (reverse.ContainsKey(p.Value))
                {
                    if (logErrors)
                        Debug.LogError(
                            $"SerializableBiDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>: " +
                            $"중복 값 '{p.Value}' 발견 — 첫 번째 쌍만 사용됩니다. Inspector 데이터를 정리하세요.");
                    continue;
                }
                forward.Add(p.Key, p.Value);
                reverse.Add(p.Value, p.Key);
            }

            return (forward, reverse);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _forward = null;
            _reverse = null;
        }
    }
}
