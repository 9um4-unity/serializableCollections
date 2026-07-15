# Serializable Collections

Unity Inspector에서 편집 가능한 `Dictionary`/`HashSet` 대체 타입.

## 설치

Package Manager → `Add package from git URL...`:

```
https://github.com/<your-account>/SerializableCollections.git
```

로컬 개발 중에는 대상 프로젝트의 `Packages/manifest.json`에 직접 추가:

```json
"com.gum4.serializablecollections": "file:../../path/to/03_SerializableCollections"
```

## 사용법

```csharp
using Gum4.SerializableCollections;

[SerializeField] private SerializableDictionary<string, int> scoresByName;
[SerializeField] private SerializableHashSet<string> unlockedIds;

void Awake()
{
    if (scoresByName.TryGetValue("joy", out var score)) { ... }
    if (unlockedIds.Contains("region_01")) { ... }
}
```

## 동작 계약

- **조회는 amortized O(1)** — 내부적으로 `Dictionary`/`HashSet` 캐시를 두고, 역직렬화(Inspector 편집 포함) 시 무효화 후 다음 조회에서 재구축합니다.
- **중복 키/항목은 첫 번째 값이 유효합니다.** Inspector에서는 빨간 하이라이트로 표시됩니다.
- **중복 경고는 Runtime(Play 모드/빌드)에서만 콘솔에 출력됩니다.** 에디터에서 Inspector를 편집하는 동안은 조용합니다 — `TryGetValue`/`Contains`/`ToDictionary`/`ToHashSet` 호출 시 `Application.isPlaying`이 참일 때만 `Debug.LogError`가 발생하며, 캐시 재구축당 1회입니다(매 프레임 호출해도 도배되지 않음).
- `ToDictionary()`/`ToHashSet()`은 내부 캐시의 **독립된 복사본**을 반환합니다.

## 구조

```
Runtime/     Gum4.SerializableCollections       — 순수 로직, Editor 비의존
Editor/      Gum4.SerializableCollections.Editor — PropertyDrawer (중복 하이라이트 포함)
Tests/Runtime/ Gum4.SerializableCollections.Tests — EditMode/PlayMode 겸용
```
