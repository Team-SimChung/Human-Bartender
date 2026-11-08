# 웹 CSV 반입 검토: `24022cd` → `e026c0e` (반입 보류)

기준: `junseo874/junseo874.github.io` `e026c0eeec5060cc81c1704b05af59d3b7c2b15f`("칵테일 재료 데이터 수정").
절차: `ContentAuthoring/README.md`의 실행 계약 검토 → 읽기 전용 검사 → Unity 타입·참조 검사 → 반영 → EditMode 검사.

## 반입 시 바뀔 데이터 (미적용)

실제 반입 검사 기준 CSV 48개 중 13개 변경(계획에 적힌 14개를 정정).

- craft: `cocktails`, `shelf_items`, `recipes`, `cocktail_tags`
- story: `steps`, `scenes`, `bar_choices`, `street_choices`, `script_files`
- interaction: `dialogue_flows`, `interact_points`
- system: `data_sets`, `row_formats`

독립 `script/ambient` 데이터 묶음이 제거되어 38개에서 37개로 줄었다. Unity 런타임 C#에는 이 경로를 직접 읽는 참조가 없으며 거리·집 주변 대화는 각각의 상호작용 데이터로 읽는다.

반입 시 삭제될 재료 14개: `ginger_ale`, `brandy`, `cream`, `green_peppermint`, `white_cacao`, `coffee`, `campari`, `blue_curacao`, `pineapple_juice`, `coconut_milk`, `peach_brandy`, `melon_liqueur`, `malibu`, `banana_liqueur`.

반입 시 삭제될 칵테일 16개: `long_island`, `moscow_mule`, `margarita`, `white_lady`, `godmother`, `french_connection`, `brandy_sour`, `pink_lady`, `espresso_martini`, `grasshopper`, `irish_coffee`, `old_pal`, `blue_hawaii`, `oasis_sunset`, `blue_sapphire`, `june_bug`.

삭제된 ID의 런타임 C# 문자열 참조를 검색한 결과 `LiquidAppearance.cs`의 재료 색 표만 남는다. 삭제된 항목을 생성하거나 요구하는 코드 참조는 발견되지 않았으며 색 표와 시각 카탈로그 정리는 이번 범위에서 제외한다. 실제 플레이 영향까지 확인한 결과는 아니다.

## 웹 실행 계약

전달된 검토를 고정 커밋의 Git diff와 Unity 소스로 재확인했다. 변경 9개와 신규 추적 3개의 blob 값이 모두 계획과 일치한다. 이번 반입에 필요한 Unity 실행 코드 변경은 없다.

| 웹 파일 (`bar-playtest/src/…`) | 변경 | Unity 반영 |
|---|---|---|
| `features/campaign/campaign-engine.js` | 본편에서 `long_island`를 걸러 내던 런타임 필터 삭제. CSV에서 행 자체가 지워졌기 때문 | 없음. Unity의 `long_island`는 CSV에만 있고, 반입으로 함께 지워짐 |
| `features/campaign/cinema-flow.js` | 시네마 시작 때 `g.lang = 'ko'` 강제 설정 삭제 | 없음 (웹 언어 상태 전용) |
| `features/campaign/day-flow.js` | 집 TV 상호작용을 `LunaOutsideTV.open`(본다/끈다/취소 메뉴, 0일차 다큐 재생)으로 일원화 | 이번엔 없음. Unity TV는 `interaction/interact_points.csv`의 `p_home_tv_*` 대화 데이터로 동작하고 네이티브 TV 메뉴가 없음 → **후속 과제**로 기록 |
| `features/story/story-flow.js` | `say` 스텝의 `arg` 표정을 다음 표정 지정까지 유지 | 없음. 웹 주석대로 Unity `BarStoryPresenter.ShowSayAsync`(L82-83)가 이미 이렇게 동작 |
| `shared/data/runtime-projection.js` | 신문·신자 연출 매핑과 손님 외형 변환을 새 모듈(`outside-story-compiler.js`, `guest-artwork-view.js`)로 옮김. 거리 배경 대화·신문 경로 투영 추가 | 없음 (웹 화면 투영 전용) |
| `features/outside/events/outside-events.js` | NPC 위치를 엔진 월드 좌표에서 읽음. 부비 장면 중 2일차 복도 불량배 중복 그리기 방지. 불량배 정보 대사 2줄 변경 | 없음. 바뀐 대사는 `story/steps.csv`에도 있어 반입으로 들어옴. 2일차 불량배 정보 NPC는 Unity에 네이티브 구현이 없음(`day2_thug_done` 참조 없음) |
| `features/outside/events/outside-neighborhood.js` | NPC 위치를 엔진 좌표로. 선택지 잠금 규칙을 `neighborhood-choice-rules.js`로 분리하고 잠금 사유 3개 추가("이미 수리했습니다", "수리 중입니다", "이미 건네주었습니다") | 잠금 **조건**은 Unity `NeighborhoodStoryEvents.cs` L81-92가 이미 같음. 사유 문구는 Unity가 공통 문구 "조건이 충족되지 않았습니다."(`OutsideStoryEvents.cs:128`)를 써서 다름 → **후속 과제**로 기록(반입 차단 사유 아님) |
| `features/outside/events/outside-samho-commute.js` | 삼호 위치를 엔진 좌표로 | 없음 (웹 렌더링 전용) |
| `features/outside/events/outside-thug.js` | 위치를 엔진 좌표로. 선택지 "500원을 준다." → "500원을 준다 · 500원" | 없음. 문구는 `story/street_choices.csv`로 반입됨 |

`web_runtime_contract.json`의 `reviewed_commit`과 변경 9개 blob을 갱신했다. 옮겨 간 로직을 다음 반입에서도 검사하도록 `neighborhood-choice-rules.js`, `outside-story-compiler.js`, `outside-tv.js`를 추가했다.

## 남은 차이

- 집 TV의 본다/끈다/취소 메뉴와 시청 동작 통합.
- 이웃 선택지의 구체적인 잠금 사유. 정상 진행의 상태/표시 조건과 별개로 웹 규칙은 수리 완료 상태도 명시적으로 검사하며 Unity 선택지 조건은 수리비 전달 상태에 기대고 있다. 후속 검토에서 문구와 상태 조합을 함께 다룬다.

사용자 결정: 두 항목은 이 문서에만 남긴다. 별도 이슈를 생성하지 않는다.

## 반영 및 검증 상태

- 사전 검사 통과: 37개 데이터 묶음, CSV 48개, 변경 13개.
- 반입 전 전체 EditMode 테스트: 339개 중 327 통과, 11 실패, 1 건너뜀. 실패 상세는 아래에 기록한다.
- Unity `CsvContentValidator.ValidateBatch`: `CSV_BATCH_VALIDATION_OK`, 성공 영수증 생성.
- CSV 반영: 보류. 기존 `story/steps.csv`가 반입 manifest와 다르며 HEAD와는 일치한다. 새 화면 코드 추가 전부터 존재한 차이다.
- 반입 후 전체 테스트·커밋: 아직 수행하지 않음.

### 기존 대사 효과와의 충돌

`e3d01e97`의 반입본과 현재 HEAD를 CSV 필드 단위로 비교하면 한국어 텍스트 79개, 영어 텍스트 78개, 웹 메타데이터 불리언 대소문자 112개가 다르다. 새 웹 스테이지에도 현재 Unity의 텍스트 효과가 들어 있지 않다.
예: `steps_workshop_6`의 `<pop peak=1.16>…</pop>`, `steps_day1_corridor_thug_10`의 `<fx=emphasis>500원</fx>`.
`b723e0eb`(대사 효과), `aaf75107`(NPC 작업) 이력이 있으므로 기존 해시를 조용히 갱신하거나 덮어쓰기 보호를 우회하지 않는다.
일부 행은 웹 문구와 순서도 달라져 row_id만으로 효과를 옮기면 잘못된 대사에 적용될 수 있다.
사용자 결정: 기존 대사 효과를 보존하고 전체 CSV 반입을 보류한다. 웹 저장소는 읽기만 했으며 CSV나 반입 manifest는 수정하지 않았다. 실행 계약 갱신은 검토한 커밋의 기록이며 CSV 게시를 의미하지 않는다. #116은 현재 Unity CSV로 진행한다.


## 반입 전 EditMode 실패 (339개 중 327 통과, 11 실패, 1 건너뜀)

사용자 결정: 아래 기존 실패는 기록만 유지하고 이번 #116에서 수정하지 않는다.

### BarSeatHudTests.SeatHudIsDrawnWithSpritesNotCode

```text
좌석 표시는 코드로 그린 도형 대신 Image와 스프라이트를 쓴다.
  Expected: <empty>
  But was:  < <Seat Indicators (StoryTutorialShape)>, <HUD Border (StoryTutorialShape)>, <Selected Seat Left (StoryTutorialShape)>, <Seat Left (StoryTutorialShape)>, <Selected Seat Middle (StoryTutorialShape)>, <Seat Middle (StoryTutorialShape)>, <Selected Seat Right (StoryTutorialShape)>, <Seat Right (StoryTutorialShape)> >
```

### BarSeatHudTests.SeatHudSceneReferencesAreWired

```text
좌석 표시와 같은 오브젝트에 BarSeatHudPresenter가 있어야 합니다.
  Expected: not null
  But was:  null
```

### BarSeatHudTests.SeatsAreLaidOutByOneHorizontalLayoutGroup

```text
SeatIndicators/Seats 줄이 있어야 합니다.
  Expected: not null
  But was:  null
```

### DayOneThugInteractionTests.AuthoredDayOneThugUsesOneNpcOwnerAndPreservesItsSpeakerAndOtherThugs

```text
Expected: (0.85, 0.00)
  But was:  (1.00, 0.00)
```

### GimmickUiAuthoringTests.EditorSetupKeepsAnotherDirtySceneOpenAndActive

```text
System.InvalidOperationException : Cannot create a new scene additively with an untitled scene unsaved.
```

### OutsideGroundEffectsTests.SavedSceneHasSeparateCaptureAndAllRequestedCharacters

```text
Expected: equivalent to < "Luna", "Samho", "shiba", "Bubi", "CitizenA", "CitizenB" >
  But was:  < "Bubi", "shiba", "Samho", "Luna" >
```

### VisualUIReviewTests.CommonOptionsKeepsItsWidgetsAppliesDisplayExplicitlyAndRestoresFocus

```text
System.NullReferenceException : Object reference not set to an instance of an object
```

### VisualUIReviewTests.MainMenuHasRealSlotControlsAndSeparatesTheLogoEffectFromBackdrop

```text
로고에 글리치 머티리얼이 있어야 합니다.
  Expected string length 71 but was 76. Strings differ at index 51.
  Expected: "...03.Content/06.Shared/02.Materials/M_TitleGlitchPanel.mat"
  But was:  "...03.Content/06.Shared/02.Materials/M_MainTitleGlitchPannel.mat"
  --------------------------------------------------^
```

### VisualUIReviewTests.RuntimeMenuWiringAndGlitchRestoreUseTheActualUnityLifecycle

```text
direct minigame polling must use the saved key in the player loop
  Expected: True
  But was:  False
```

### VisualUIReviewTests.ServiceAndRecipeControlsKeepTheirOwnersAndUseCurrentArt

```text
Expected: String containing "/recipe_choice/"
  But was:  "Assets/01.Game/03.Content/01.ArtResources/05.UI/03.Crafting/01.Preparation/01.Recipes/WebGenerated/Web_BTN_recipe_card_background_v02.png"
```

### WebCsvCompatibilityTests.PublishedFilesAreExactlyThePinnedWebSnapshot

```text
story/steps.csv
  String lengths are both 64. Strings differ at index 0.
  Expected: "39db3294e70935c2129d4dcaf9a144f33a5d39cd15d2683dce4a1f66ecd3fc2e"
  But was:  "17cd79b99edb515cdd4bb5d1a64d2ebc88d5780531aad8d2181a9e79b62ad2f5"
  -----------^
```
