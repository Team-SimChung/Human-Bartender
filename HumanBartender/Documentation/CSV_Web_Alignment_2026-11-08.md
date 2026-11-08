# 웹 CSV 반입 검토: `24022cd` → `e026c0e` (반입 보류)

## 후속 작업: 기존 연결 장면의 대사만 부분 반영

아래 본문은 최초 전체 반입 보류 당시 이력이다. 이후 제조 CSV 4파일은 `e79626eb`에서 부분 반입됐고,
사용자 요청으로 이번에는 웹 `d9a34746761bc3a0f3a67bcd04cdf737d9884d70`의 기존 본편 대사만 반영한다.
Unity 기준은 `9caaa424`다. UI 구조 정리(고정 UI·주변 말풍선·CanvasGroup·메뉴 크기·배치 숫자)는 제외한다.

반영 대상은 `story/steps.csv`, `story/bar_choices.csv`, `story/street_choices.csv` 세 파일이다.
본편 bar day0~3 / street / home의 기존 부모 장면만 반영하고 CSV의 연결 메타데이터와 씬은 유지한다.

| 변경 | 수 |
|---|---:|
| 기존 steps 행의 한국어 본문 필드 | 122 (say 120, choice 질문 2) |
| 기존 steps 행의 영어 본문 필드 | 623 |
| 영어 바 선택지 문구 | 12 |
| 한국어 거리 선택지 문구 | 1 |
| 화자 변경 | 17 |
| 추가 / 삭제 say 행 | 3 / 4 |

새 대사 3행은 신문 퀴즈 질문, 3일차 삼호 출근 대사의 루나 답변, 코라테크 정보 대사의 감사 인사다.
삭제 4행은 재작성된 `commute2` 안에서 합쳐지거나 다른 행으로 옮겨진 대사다.
현재 카탈로그는 38개 데이터셋을 유지하며 steps는 1673행에서 1672행으로 바뀐다.

효과 보존:

- 기존 한국어 79개·영어 78개 필드의 연출 태그를 보존한다. 일반 서식 태그까지 포함해 보호 검사하는 필드는 183개다.
- 본문이 함께 바뀐 KO 5/EN 61개 필드는 `ContentAuthoring/dialogue_merge_review.json`에 적용 구간을 기록했다.
- 삼호 자기소개 pop은 재사용된 `_25` ID의 루나에게 붙이지 않고 새 자기소개 `_22`에 옮겼다.
- 밀수업자 대금 압박 강조는 해당 내용이 옮겨간 3행에 적용했다. 4행의 새 의뢰 요청에 잘못 붙이지 않았다.
- 새 영어 번역의 대응 어구에 기존 연출을 적용하고, 웹의 색상/주문 태그도 보존했다.
- 웹 영어 칸 자체에 한국어 또는 이전 번역이 남은 곳은 임의로 새 번역하지 않았다.

유지/보류:

- Home/OutSide/Play 씬, 프리팹, 런타임 UI 및 캐릭터 스크립트는 변경하지 않는다.
- 주변 대화 5그룹·TV 2방송의 데이터셋 이동과 상호작용 연결은 보류한다. 기존 ambient 행도 유지한다.
- 삼호 drunk 지정 4곳은 현재 default를 유지한다. 이미지/Addressables/SO는 변경하지 않는다.
- QA99 변경, 미사용 자판기 삭제, 일반 손님 주문 4행은 제외한다.
- 제조 4파일, 리소스 보정표, ID map, 반입 실행 계약 파일은 유지한다.

반입 보호: 웹 Git blob을 직접 읽고 기존 실행 계약을 대조한 뒤 최종 병합 스테이지에 Unity DTO 검사를 수행한다.
현재 파일·manifest·영수증 해시를 확인하고 세 파일과 manifest만 게시한다. `files`와 `web_files`는 구분하며,
`partial_imports`와 `unity_overrides`에 이번 부분 반영을 명시한다. 일반 전체 반입 보호는 계속 유지한다.

부분 반영 완료. 변경 전 `WebCsvCompatibilityTests`는 4개 중 3개 통과, 1개 실패였고
실패는 기존 대사 효과로 인한 `story/steps.csv` manifest 해시 불일치였다.

- Python 반입기·부분 병합 검사: 18개 통과. 효과 이동, 범위 밖 파일 보존, 기존 CSV 서식,
  검토되지 않은 변경 거부, 오래된 영수증 거부, 게시 실패 시 복구를 확인했다.
- 최종 병합 스테이지의 Unity DTO 검사 성공. 게시된 CSV 48개의 바이트 해시가 영수증 및 manifest와 일치한다.
- 게시 후 Unity 6000.3.14f1 EditMode 관련 검사: 29개 통과, 실패/건너뜀 0개.
  `WebCsvCompatibilityTests`, `OutsideStoryEventTests`, `NpcSequenceCommandTests`,
  `SceneDialogueContinuationTests`, `CsvStoryMemoryTests`를 실행했다.
- 나머지 CSV 45개는 게시 전과 바이트 단위로 동일하다. 씬·프리팹·런타임 스크립트 변경은 없다.
- 기존 CRLF CSV 형식을 유지했다. `git -c core.whitespace=cr-at-eol diff --check` 통과.
- 실제 Game 화면 및 0~3일차 전체 플레이는 확인하지 않았다. 전체 프로젝트 테스트를 재실행한 결과가 아니다.

최종 해시와 검증 범위: `CSV_Dialogue_Partial_2026-11-08.validation.json`.

작업 완료 후 일회성 부분 반입 스크립트와 전용 Python 테스트를 삭제했다.
위 Python 18개 통과는 삭제 전 실행 이력이다. 효과 검토표와 Unity 회귀 테스트는 유지한다.

이전 설명 정정: 현재 OutSide에는 2일차 불량배·정보 대상과 네이티브 사건 처리가 있다.
아래 표의 “네이티브 구현 없음”은 최초 조사 오류이며 이번 작업의 근거로 사용하지 않는다.

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
