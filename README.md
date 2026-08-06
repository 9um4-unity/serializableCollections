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
[SerializeField] private SerializableBiDictionary<string, int> idByName;

void Awake()
{
    if (scoresByName.TryGetValue("joy", out var score)) { ... }
    if (unlockedIds.Contains("region_01")) { ... }
    if (idByName.TryGetValue("joy", out var id)) { ... }   // 정방향: 이름 → id
    if (idByName.TryGetKey(1, out var name)) { ... }       // 역방향: id → 이름

    // ToDictionary()/ToHashSet() 없이 바로 순회 가능
    foreach (var (name, score) in scoresByName) { ... }
    foreach (var id in unlockedIds) { ... }
}
```

여러 줄 문자열을 담는 `SerializableDictionary<TKey, string>` 필드에는 `[SerializableTextArea]`를 붙이면 Value가 여러 줄 편집 영역으로 그려집니다.
(Unity 내장 `[TextArea]`는 쓰지 마세요 — List 필드에 붙이면 하위의 모든 프로퍼티(Key 포함)에도 전파되어, 문자열이 아닌 Key에까지 TextAreaDrawer가 적용되며 오작동합니다.)

```csharp
[SerializeField, SerializableTextArea(3, 5)]
private SerializableDictionary<DialoguePhase, string> dialogues;
```

### Element attribute forwarding

`Key`/`Value`(또는 `SerializableHashSet`의 원소)는 제네릭 타입 파라미터의 실제 필드라 컴파일 타임에
직접 `[Range]`, `[Min]` 같은 Unity 내장 `PropertyAttribute`를 붙일 수 없습니다. 대신 컬렉션 필드
자체에 `[ElementAttribute(대상, 붙이고_싶은_attribute_타입, 생성자_인자...)]`를 붙이면, 그 attribute를
전담하는 `PropertyDrawer`를 찾아 Key/Value/Item을 그릴 때 대신 적용합니다.

```csharp
using UnityEngine;
using Gum4.SerializableCollections;

[SerializeField, ElementAttribute(ElementTarget.Value, typeof(RangeAttribute), 0f, 1f)]
private SerializableDictionary<string, float> weightByName;

[SerializeField, ElementAttribute(ElementTarget.Item, typeof(RangeAttribute), 0, 10)]
private SerializableHashSet<int> levels;
```

- `ElementTarget.Key`/`Value`는 `SerializableDictionary`·`SerializableBiDictionary`에, `ElementTarget.Item`은
  `SerializableHashSet`에 사용합니다.
- 같은 대상에 여러 `[ElementAttribute]`를 붙일 수 있지만, 실제로는 매칭되는 `PropertyDrawer`가 있는
  첫 번째 attribute만 적용됩니다(스태킹 미지원).
- `Tooltip`처럼 전용 `PropertyDrawer`없이 Unity 내부 경로로만 동작하는 attribute는 이 방식으로 전달되지
  않습니다. `PropertyDrawer`가 실제로 등록된 attribute(Range, Min, Multiline 등)에서만 동작을 보장합니다.

## 동작 계약

- **조회는 amortized O(1)** — 내부적으로 `Dictionary`/`HashSet` 캐시를 두고, 역직렬화(Inspector 편집 포함) 시 무효화 후 다음 조회에서 재구축합니다.
- **중복 키/항목은 첫 번째 값이 유효합니다.** Inspector에서는 빨간 하이라이트로 표시됩니다.
- **중복 경고는 Runtime(Play 모드/빌드)에서만 콘솔에 출력됩니다.** 에디터에서 Inspector를 편집하는 동안은 조용합니다 — `TryGetValue`/`Contains`/`ToDictionary`/`ToHashSet` 호출 시 `Application.isPlaying`이 참일 때만 `Debug.LogError`가 발생하며, 캐시 재구축당 1회입니다(매 프레임 호출해도 도배되지 않음).
- `ToDictionary()`/`ToHashSet()`은 내부 캐시의 **독립된 복사본**을 반환합니다.

### SerializableBiDictionary

`SerializableDictionary`와 같은 규칙에 더해, **Key뿐 아니라 Value도 유일해야** `TryGetKey`(역방향 조회)가 성립합니다.

- Key 또는 Value가 중복되면 **먼저 등록된 쌍만 유효**하고 이후 쌍은 무시됩니다. Inspector에서는 Key/Value 어느 쪽이 중복이든 해당 필드가 빨간 하이라이트로 표시됩니다.
- `TryGetKey`/`ToReverseDictionary`로 값→키 역방향 조회를 수행합니다. `ToDictionary`/`TryGetValue`는 기존과 동일하게 정방향입니다.
- null 키·null 값 모두 무시 대상이며, 중복과 동일하게 Runtime에서만 경고합니다.

## 구조

```
Runtime/     Gum4.SerializableCollections       — 순수 로직, Editor 비의존
Editor/      Gum4.SerializableCollections.Editor — PropertyDrawer (중복 하이라이트 포함)
Tests/Runtime/ Gum4.SerializableCollections.Tests — EditMode/PlayMode 겸용
```
