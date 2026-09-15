# 레거시 데이터 정리


- **브랜치** `RemoveRegacy`
- **커밋** `a0f828c` — 레거시 정리. 1차 (기준: `e1b3808`)
- **규모** 185 files changed, 566 insertions(+), 30,972 deletions(-)
- **작성** 2026-09-13

---

## 요약

구형 `DataLoadManager`가 읽던 JSON과 그것을 담던 ScriptableObject를 전부 걷어냈다.
`StreamingAssets`에는 이제 신형 `json/` 하나만 남고, 데이터 로더는 `NewDataLoadManager` 하나다.
이 클래스에서 구형 경로(`LoadLegacyData` / `LoadLegacyDataAsync` / `ApplyLegacyData`)가 통째로 사라졌다.

| 구분 | 수 |
|---|---|
| 삭제 | 131 (json 13, .cs 30, .asset 15, 나머지 .meta) |
| 수정 | 42 |
| 신규 | 10 (.cs 5 + .meta 5) |

---

## 삭제한 레거시 데이터

```
StreamingAssets/
  cocktails.json          cutscenes.json        cutscene_events.json
  character_tiers.json    skill_tiers.json
StreamingAssets/Outside/            ← 폴더째 제거
  shiba.json   bubi.json   samho.json   samhodeath.json
  outside_objects.json    elevator_radio.json   npc_characters.json
  cocktails.json          ← 루트와 바이트 단위 동일한 고아 사본
```

## 삭제한 스크립트 (30)

**구형 데이터 SO (10)**

```
Data/CocktailDataSO.cs          Data/CutSceneDataSO.cs
Data/CharacterAnimSO.cs         Data/TextTagDataSO.cs
Data/CharacterTierDataSO.cs     Data/SkillTierDataSO.cs
Data/Outside/NPCCharacterDayDataSO.cs    Data/Outside/OutsideObjectDataSO.cs
Data/Outside/OutsideRadioDataSO.cs       Data/Outside/OutsideTriggerCutSceneSO.cs
```

**트리거 시스템 (11) — `Command/` 폴더째**

```
DialogueCommandFactory  IDialogueCommand    StartCutSceneCommand
EffectCommand           CustomerEnterCommand  CustomerExitCommand
AddStatCommand          SetStatCommand        DayEndCommand
SetFlagCommand          MoneyChangeCommand
Dialogue/DialogueTriggerManager.cs
```

**도달 불가 코드 (9)**

```
Craft/UI/UICocktailDetailPanel.cs   Craft/UI/UICocktailSlot.cs
Event/CocktailDataEvent.cs          Event/CocktailDataEventListener.cs
Cutscene/TestCutScene.cs            Outside/OutsideDataManager.cs
DataNew/NewLegacyDataBridge.cs      DataNew/INewDataSwitcher.cs
```

`.asset` 15개도 함께 삭제 (`SO/`, `SO/Ch/`, `07.Event/`).

---

## 이관 내역

### 신형으로 출처를 옮긴 것

| 대상 | 이전 | 이후 |
|---|---|---|
| 텍스트 색 태그 | `TextTagDataSO` ← 브릿지 | `NewTextTagDataSO` ← `json/text_tags.json` |
| 표정·파츠 애니 | `CharacterAnimSO` ← 브릿지 | `NewExpressionDataSO` ← `json/character_anim.json` |
| 칵테일 | `CocktailDataSO` ← `cocktails.json` | `NewCocktailDataSO` ← `json/cocktails.json` |
| 컷씬 | `CutSceneDataSO` ← `cutscenes.json` | `NewCutSceneDataSO` ← `json/cutscenes.json` |

브릿지(`NewLegacyDataBridge`)는 신형 JSON을 구형 모양으로 변환해 넣어 주던 임시 계층이었고, 소비처를 신형 SO로 갈아끼우며 제거했다.

### 칵테일 필드 대응

```
targetCocktailData : CocktailData → NewCocktailData
cocktailDataSO.allCocktails[id]   → cocktailDataSO.TryGet(id, out _)
targetCocktailData.Keywords[i]    → targetCocktailData.Tags[i].Ko
```

미니게임 5종(`Cap` `Pour` `Shaker` `Stir` `Stur`) + `CraftStationData` + Editor 셋업 3종.

### 삭제만 한 것 (대체 없음)

- `character_tiers` / `skill_tiers` — 등급표를 물어보는 데이터(`min_tier`)가 0건, `AddSkillTier()` 호출처 0곳
- 구형 `cutscenes.json` — `start_cutscene` 트리거가 데이터에 0건
- `cutscene_events.json` — 읽는 코드가 주석 블록 안에만 존재
- `Outside/*.json` — 로더(`OutsideDataManager.Load()`) 호출처 0곳, 되살릴 계획 없음 확인

---

## 신규 파일 (5)

삭제 대상 파일이 **살아 있는 타입을 함께 들고 있어** 분리한 것들이다.

| 파일 | 옮긴 타입 | 쓰는 곳 |
|---|---|---|
| `Dialogue/CharacterAnimTypes.cs` | `EAnimationPart` `EAnimLoopMode` `PartAnimData` | `CharacterPart` `CharacterLoader` |
| `Cutscene/CutSceneAnchor.cs` | `AnchorType` | 컷씬 타임라인 트랙 6곳 |
| `Cutscene/CutSceneTimelineTypes.cs` | `EEneterPreset` `EExitPreset` `ECutSceneCameraMoveType` | 타임라인 트랙 5곳 |
| `Outside/OutsideDialogueTypes.cs` | `ESelectionType` `FlowData` `Condition` `OutsideCondition` | `InteractiveObjectEntity` `InteractiveEntityManager` |
| `DataNew/NewTextTagDataSO.cs` | 신형 태그 SO | `DialogueTypingService` |

> **열거형 값 순서는 원본 그대로 유지했다.**
> 프리팹과 TimelineAsset에 정수로 구워져 있어, 순서가 바뀌면 배치해 둔 연출이 조용히 어긋난다.

---

## 함께 고친 버그 (2)

**`SturManagerNew`** — `colors = new Color[...Keywords.Length]`가 `isTest` 블록 **밖**에 있어, 기믹 큐가 돌리는 정상 경로(목표 칵테일이 꽂히지 않음)에서 무조건 NullReference. 태그 없을 때 흰색 하나로 떨어지도록 가드 추가.

**`PlayerDataSO.GetCurCharacterAffinityTier`** — `ContainsKey` 검사보다 **먼저** `Characters[id]`를 인덱싱해, 등급표에 없는 인물에서 `KeyNotFoundException`. (해당 메서드는 이후 등급표와 함께 삭제)

## 새로 붙인 기능 (1)

2부 대본의 `timeline` 스텝이 `[Story] (미구현)` 로그만 남기고 지나가던 것을 실제 컷씬 재생에 연결했다.

```
StoryFlow.[Inject] ICutScenePlayer
  → StoryScriptRunner.Bind(..., cutScenePlayer)
    → case ENewStepType.Timeline → TimelineAsync()
      → CutSceneManager.PlayCutScene(step.Arg)
```

재생 실패해도 대본은 이어 가고, 끝나면 `ClearCutScene()`으로 화면을 비운다.

---

## 남은 작업

### 1. 에디터에서 지워야 할 Missing Script / 껍데기 오브젝트

| 위치 | 대상 |
|---|---|
| `Craft.prefab` | `UICocktailSlot` 6, `UICocktailDetailPanel` 1 |
| `Slot.prefab` | `UICocktailSlot` 1 |
| `Home` `LightTest` `OutSide` `Play` | `DialogueTriggerManager` 컴포넌트 |
| `Play.unity` | `CocktailCraftManager` 이름의 빈 오브젝트 (VoidEventListener 3개, 타깃 전부 null) |
| `Home.unity` | 죽은 `DialogueRunner` 오브젝트 |

씬 3개(`Home` `LightTest` `OutSide`)의 `obejcts` / `npcs` / `triggers` stale 키는 **씬을 한 번 저장하면 자동으로 사라진다.**

중첩 프리팹의 stripped 컴포넌트와 리스트 항목이 얽혀 있어 YAML 직접 편집 대신 에디터 작업으로 남겼다.

### 2. 컷씬 `resource_key` 불일치 ⚠️

배선은 끝났으나 `json/cutscenes.json`의 `resource_key` **23개 중 20개가 실제 어드레서블 주소와 다르다.**

대본이 실제로 부르는 5개는 전부 불일치:

| 대본 `arg` | `resource_key` | 실제 주소 |
|---|---|---|
| `tl_intro_lab` | `Intro lab` | `Intro_lab` |
| `tl_bubi_meet` | `Bubi_meet` | 없음 |
| `tl_catmilk` | `Catmilk` | 없음 |
| `sp_parttime_poster` | `Poster/parttime` | 없음 |
| `sp_experiment_poster` | `Poster/experiment` | 없음 |

현재는 `[CutsceneManager] 타임라인을 찾지 못했습니다` 경고 후 1초 대기하고 넘어간다.
`resource_key`를 주소에 맞출지, 어드레서블을 등록/개명할지 결정 필요.

### 3. 데이터 없이 컴파일만 되는 코드

`InteractiveObjectEntity`를 남기기로 하여 아래가 함께 남았다. 전부 데이터 0건.

- `DialogueRunner.PlayAsync(DialogueData[])` 계열 (구형 대사 사슬)
- `DayDataSO`의 `DialogueData` `FlowData` `TriggerData` `ETriggetType` 등
- `UIDialogueChoiceView.ShowChoice(ChoiceSelectData)` + `CheckCondition`

`DialogueRunner.PlayOutsideAsync(Step[])`는 신형 `NewStreetDataSO`를 쓰며 **살아 있다** (`InteractiveNPCEntity`, `OutsideElevatorRadio`).

### 4. 미구현 기능

`AddSkillTier()` 호출처가 0곳이라 숙련도 수치가 항상 0. 레거시가 아니라 기능 결정 사안.

---

## 작업 중 발생한 오판 (기록)

동일한 실수를 반복하지 않기 위해 남긴다.

1. **칵테일 `KeyNotFoundException`을 라이브 버그로 보고** — 실제로는 전 호출부가 `isTest` 블록 안. 심각도를 과장했다.
2. **`Assets/Editor`를 검색 범위에서 누락** — `Cap/Pour/StirSceneSetup` 3개가 깨졌다.
3. **티어 2종을 불필요하게 이관** — "소비처가 있는가"만 보고 "그 소비처가 도달 가능한가"를 확인하지 않았다. `min_tier` 데이터가 0건이라 처음부터 삭제가 맞았고, 결국 되돌렸다.
4. **`NPCCharacterDayDataSO.cs`의 동거 타입 4개 누락** — 컴파일 에러 6건. 파일명과 같은 타입만 확인한 탓.

**이후 적용한 절차**

- 삭제 전: 해당 파일이 정의하는 **모든 타입**을 `git show`로 뽑아 잔여 참조 대조 (82개 일괄 검사)
- 검색 범위: `Assets` 전체 (`Editor` 포함), 데이터는 `StreamingAssets` 전체
- 최종 검증: Unity `Editor.log`의 실제 `error CS` 목록 확인
