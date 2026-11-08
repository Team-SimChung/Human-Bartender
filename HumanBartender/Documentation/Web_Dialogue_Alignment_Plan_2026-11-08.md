# 웹 최신 대사 반영 실행 계획 및 전체 진행 검토

작성일: 2026-11-08. 최신 상태: 사용자의 “그것만 진행해” 지시에 따라 **기존 연결 장면의 대사 3파일 부분 반영을 완료**했다. Python 검사 18개, 관련 Unity EditMode 검사 29개가 통과했다. 아래 전체 계획은 조사 이력이며 UI·씬·리소스 작업은 실행하지 않았다. 실제 범위/결과는 `CSV_Web_Alignment_2026-11-08.md`의 후속 작업 절과 `CSV_Dialogue_Partial_2026-11-08.validation.json`을 우선한다. 기존 계획의 `.validation.json`은 당시 전체 범위의 조사 기록이다.

## 최신 범위 결정 — UI 구조 정리를 대사 반입에서 제외

사용자 후속 지시: 고정 UI 배치, 주변 말풍선 생성/재사용 구조, CanvasGroup 연결, 메뉴 Scale, 배치 숫자 노출(목록 1~5)을 이번 작업에서 제외한다. **아래 단계 3 및 그 변경에만 필요한 UI 수명/구조 테스트는 실행하지 않는다.** 기존 분석은 조사 이력으로 유지하며 대사 교체의 선행 조건으로 사용하지 않는다. 이 결정이 이전 단계별 계획보다 우선한다.

현재 UI를 유지한 채 기존에 연결된 장면의 대본·선택지를 갱신하는 것은 가능하다. 대사 반영에 필요한 검사는 효과 보존, 화자·순서·분기/명령 참조, 최종 카탈로그 로드 및 해당 장면 재생이다. 텍스트만 같은 행 ID에 복사하면 화자/순서가 바뀐 장면이 어긋날 수 있으므로 의미 단위로 병합한다.

남은 차이는 따로 다룬다. 삼호 drunk 4곳은 리소스가 준비되지 않은 상태로 표정 지정까지 반입하지 않는다. 해당 지정은 명시적 보류 차이로 남기거나 리소스 연결을 먼저 해결한다. 새 주변 대화 5그룹 및 추가 TV 방송 연결은 Sao의 Home/OutSide 점검과 겹치므로 기존 연결에 영향이 없는 범위만 반영하고, 보류한 장면/flow 변경은 기록한다. 9 CSV 전체를 무조건 덮어쓰는 방식으로 축소 범위를 구현하지 않는다.

사용자가 이후 이 축소 범위의 실행을 요청했다. `steps.csv`, `bar_choices.csv`, `street_choices.csv`만 부분 반영하며 전체 반입 보류와 씬 수정 제외는 유지한다.

## 1. 기준과 작업 범위

- 저장소: `C:/Users/sster/Documents/Git/Human-Bartender`
- Unity 프로젝트: `C:/Users/sster/Documents/Git/Human-Bartender/HumanBartender`
- 웹 저장소: `C:/Users/sster/Documents/Git/junseo874.github.io`
- 웹 기준: `d9a34746761bc3a0f3a67bcd04cdf737d9884d70`.
- 최초 조사 Unity 기준: `6e680dadde428e68f97e12b55abf08c42b07c22f`.
- 검토 도중 병합된 현재 Unity 기준: `9caaa4245bfbb72626bf44f7127abbdb11445009`.
- 이 병합은 선택지 거부 표시/글리치 코드를 포함한다. CSV·반입 manifest는 바뀌지 않아 이전 대사 비교 수치는 유효하다. 구현 전에 HEAD와 작업 트리를 다시 기록한다.
- 아래 소스 경로는 별도 표시가 없으면 Unity 프로젝트 기준이다.

목표는 웹 대사 본문·선택지·화자·순서와 관련 장면 연결을 반영하면서 Unity가 작성한 대사 효과와 기존 플레이 동작을 보존하는 것이다. 현재 요청은 전체 계획 검토다. 기존의 **전체 CSV 반입 보류**를 이번 계획 작성만으로 해제하지 않는다.

대상 CSV는 다음 9개를 하나의 연결된 변경으로 다룬다.

```text
story/steps.csv
story/scenes.csv
story/script_files.csv
story/bar_choices.csv
story/street_choices.csv
interaction/interact_points.csv
interaction/dialogue_flows.csv
system/data_sets.csv
system/row_formats.csv
```

`system/` 두 파일은 기존 ambient 묶음 제거에 수반하는 정리다. 새 테이블·열·명령·데이터셋을 추가하지 않는다. `guests/random_waves.csv`의 주문 4행 차이는 별도 변경으로 남긴다. 이미 반영한 제조 CSV 4개는 다시 반입하지 않는다.

## 2. 전체 진행 상태 재분류

| 작업 | 확인된 현재 상태 | 남은 확인 |
|---|---|---|
| #116 일차별 신규 재료·레시피 안내, Bar Seat HUD | `d74552a8`에 씬·코드·테스트가 커밋됨. 사용자가 HUD 메뉴 적용·저장·테스트 완료를 보고함 | 현재 HEAD에서의 테스트 영수증과 실제 화면 확인은 별도로 기록 |
| 웹 기준 제조 CSV·누락 선반 | `e79626eb`에 제조 4파일·선반 프리팹·관련 코드가 커밋됨. 사용자가 두 메뉴 적용 완료를 보고함 | 테스트 통과/실패 개수는 대화에서 아직 제공되지 않음 |
| 개시바 초상 배치 | `6e680dad`에 반영됨 | 현재 기준 3해상도 실제 화면 확인 |
| 선택지 거부 표시 | 조사 중 `9caaa424` 병합으로 합류 | 새 반입/뷰 연결에서 이 변경을 보존하고 관련 회귀 실행 |
| 최신 대사 | 아직 미반영. 기본 반입 기록은 `24022cd`, 제조만 `d9a3474` 부분 반입 | 아래 단계 전체 |

과거 기록의 11개 실패는 당시의 기준선이지 현재 실패 수가 아니다. 특히 HUD는 이후 수정되었다. 관련 테스트 결과는 새로 수집하고, 무관한 기존 실패는 원인·기존 재현 여부를 구분해 기록한다.

`Documentation/CSV_Web_Alignment_2026-11-08.md`에는 제조 데이터까지 미적용으로 기술된 부분과 “2일차 불량배 네이티브 구현 없음”이라는 잘못된 설명이 있다. 과거 검사 이력을 지우지 않고 현재 상태 절을 추가해 정정한다. 현재 OutSide에는 `day2-corridor-thug`, `day2-thug-information`이 배치되어 있다.

## 3. AGENTS.md 적용 기준

1. 씬 구조·UI 부품·컴포넌트·참조는 에디터에서 작성하고 저장한다. 재생 중 누락 구조를 만드는 보정 코드를 추가하지 않는다.
2. 기능별 기존 소유자를 유지한다. 대사/조건/일차/완료 규칙은 CSV와 현재 실행기, 외형 연결은 SO/리소스 보정표, 위치·반경·표시 설정은 씬 Inspector가 소유한다.
3. 새 NPC 묶음은 `04.Gameplay/InteractionObjects`의 현재 구조에 작성한다. `03.World`는 보기 전용이다. 주변 대화에는 연출용 `NpcInteraction`을 붙이지 않는다. 기존 검사가 이를 거부한다.
4. 말풍선·선택지·힌트·이미지 뷰는 기존 공용 프리팹 원본을 재사용한다. 한 씬의 고정 묶음을 위해 새 중첩 프리팹/Variant를 만들지 않는다. 참조는 컴포넌트 타입 `[SerializeField]`로 연결한다.
5. 위치·크기·색·읽기 속도·반경 등 조절값에는 Inspector 설명과 범위를 둔다. UI Scale 1, 장식 Raycast 해제, 이름 공백 제거를 확인한다. UI 고정 배치를 숫자로 코드에 복사하지 않는다.
6. 에디터 작업은 `SerializedObject`, `Undo`, `PrefabUtility`, `SetDirty`/`MarkSceneDirty`를 사용한다. 두 번째 실행은 기존 수동 배치·값을 덮어쓰지 않는다. 누락된 기존 에셋은 경고하고 임의의 SO/머티리얼로 대체하지 않는다.
7. Main Camera를 직접 수정하지 않는다. 본 계획의 대사 반입 때문에 카메라/좌석/선반을 재배치하지 않는다.
8. 씬·프리팹·meta는 Unity API로 작성한다. 파일 이동 시 GUID를 보존한다. 이번 작업용 일회성 도구는 적용·저장 확인 후 제거한다.
9. 현재 Unity 에디터가 열려 있고 창 제목에 미저장 표시가 있다. 배치 테스트·자동 씬 닫기·일괄 저장을 실행하지 않는다. 필요한 에디터 작업은 준비를 끝낸 뒤 구체적인 메뉴/대상과 함께 진행한다.
10. 디스크 AGENTS.md는 BarSeatHud 10/DailyUnlocks 12이고, 세션에서 제공한 지침은 BarSeatHud 12다. 이 계획은 Play HUD 정렬을 변경하지 않는다. 해당 설정을 변경할 별도 작업에서는 세션의 명시적 지침이 우선한다.

AGENTS의 목록 행 재사용 예외와 고정 화면 생성을 구분한다. 신규 레시피 카드처럼 개수가 바뀌는 목록을 이번 조사에서 일괄 금지 대상으로 취급하지 않는다. 아래에서 고치는 대상은 Home/OutSide의 고정 UI 인스턴스와 대화마다 생성·파괴되는 주변 대화 세션 UI다.

## 4. 재검토에서 추가한 핵심 사항

### 4-A. 기존 주변 대화 UI를 그대로 연결하면 규칙을 만족하지 못함

소스로 확인한 현재 경로:

- `02.Scripts/04.Features/07.Outside/WorldInteractionView.cs:55`: `CreateAmbientView`가 말풍선 프리팹을 Instantiate하고 View를 AddComponent.
- 같은 파일 `Initialize`: 이미지 뷰·일반 말풍선·힌트·선택지 고정 부품도 런타임 Instantiate.
- `WorldAmbientDialogueController.cs:112`: 재생마다 DialogueRunner를 AddComponent하고 종료 시 View GameObject를 Destroy.
- `WorldAmbientDialogueView.cs:43`: CanvasGroup이 없으면 런타임 AddComponent.

따라서 “기존 주변 대화 실행기를 재사용하므로 런타임 코드는 변경하지 않는다”는 이전 제안은 수정한다. **기존 실행 책임은 유지하되 위 UI 생성/파괴 부분을 에디터에 저장된 인스턴스 연결로 바꾼다.** 모든 NPC 구조를 다시 만드는 리팩터링으로 확대하지 않는다.

### 4-B. 삼호 표정은 실제 웹 원본과 배치를 함께 처리해야 함

- CSV는 이미 `samho/drunk → sprite: samho_drunk`를 정의한다.
- 해당 키는 현재 Unity Addressables 및 `StreamingAssets/unity/resource_bindings.csv`에 없다.
- 웹 실제 파일: `bar-playtest/assets/characters/expressions/expr-samho-drunk.png`, 530×338.
- SHA-256: `fa38221de5ce303b7890a1d473d191e307e4ffae2ec8bb95cd539cd54f316768`.
- 실제 이미지를 열어 확인했다. Unity Assets 전체 PNG에 바이트가 같은 파일은 없다.
- 웹 `features/bar/bar-views.js`의 `poseOffset`은 이 잘린 이미지에 표정별 위치 보정을 한다. 삼호 전체에 같은 높이를 적용하면 다른 표정까지 움직일 수 있다.
- 기존 표정 로드 실패는 `BarStoryPresenter.TrySetCharacterAsync`의 텍스트 전용 전환으로 이어진다. 반입 후 실제 재현은 미확인이다.

### 4-C. 구조 검사 통과와 전체 동작 일치는 다름

현재 `node tools/check-engine-data-contract.cjs` 결과는 표 44개, 데이터셋 37개, 스텝 1664개, **errors 0 / gaps 17**이다.

| 그룹 | 수 | 취급 |
|---|---:|---|
| 2일차 바 `story_cue`의 전환 대기 | 1 | 기존 effect 플래그 변경과 웹의 0.9초 대기는 별개. 대사 반입으로 연출까지 해결했다고 하지 않음 |
| 신문/신도 보상의 아이템 지급·알림 | 2 | 기존 플래그 효과는 지원. effect.arg만으로 인벤토리/알림이 실행되는 것은 아님. 실제 호출 경로와 보상 중복 여부 검증 대상으로 기록 |
| `alive.haru`, `cocktail.*` 조건 | 14 | 현재 Unity CSV와 같은 값. 웹 신규 회귀로 분류하지 않음. ConditionUtil 외에 실제 소비자의 전용 평가 경로까지 확인하기 전 전체 기능 실패로 단정하지 않음 |

이 17건을 해결하기 위해 대사·보상 의미를 바꾸거나 새 명령/스키마를 추가하지 않는다. 새 대사 반영 필수 조건과 별도 실행 기능 개선을 구분한다. 기존 테스트에서 의도적으로 검사하지 않는 조건 영역도 있으므로 DTO 검사만으로 전체 게임 동등성을 선언하지 않는다.

추가 검색에서 `NewEndingDataSO`/`NewOrderRuleDataSO`/`NewTasteDataSO`는 모델 정의와 `NewDataLoadManager`의 채우기 경로가 확인되었고, 해당 식을 실제 평가하는 소비자는 확인되지 않았다. 따라서 14건은 “반입하면 새로 예외가 14번 발생한다”가 아니라 기존 실행 연결/지원 확인 항목으로 남긴다.

## 5. 단계별 실행 계획

### 단계 0 — 기준 고정, 비교 증거와 회귀 기준선

1. 양쪽 HEAD, 작업 트리, Unity 버전·열린 씬·미저장 상태를 다시 기록한다. 변경이 추가되면 해시와 호출부를 재확인한다.
2. 웹 `d9a3474` 원본과 정규화한 사본을 분리해 스테이징한다. 실제 게시 폴더는 변경하지 않는다.
3. 비교 결과를 다시 생성해 다음 값을 확인한다: 한국어 본문 170필드(say 168/choice 질문 2), 영어 본문 656, 태그만 차이 KO 74/EN 17, 추가 say 3/삭제 say 11+effect 1, 화자 32, 표정 4. QA99와 본편을 구분한다.
4. Git에 있는 현재 기준으로 관련 테스트 결과를 수집한다. 과거 11실패를 그대로 새 기준선으로 복사하지 않는다.

완료 기준: 비교 입력을 재현할 수 있고, 이번 반영 대상 9파일과 보류 파일이 명확하며, 변경 전 실패가 식별되어 있다.

### 단계 1 — 효과 보존 병합안과 게시 보호

대상: `ContentAuthoring/import_web_content.py`, `test_import_web_content.py`, `web_content_source.json`, `web_id_map.json`, `README.md`, 관련 검증 테스트.

1. 기본 반입 `24022cd`, 현재 Unity, 웹 `d9a3474`의 3방향 비교로 Unity 효과 변경을 식별한다. 파일 전체를 새 웹 버전으로 덮어쓰지 않는다.
2. 화자·본문·대사 종류가 같은 태그 전용 차이는 원래 효과를 유지한다. 본문까지 달라진 KO 5/EN 61필드는 문장 단위 병합안을 만들고 적용 위치·유지/재배치 근거를 기록한다.
3. `steps_commute2_25`는 삼호에서 루나로 바뀌었다. 행 ID 하나만으로 효과를 옮기지 않는다. 삭제/이동된 행의 효과도 대응 문장이 있는지 찾는다. 대응이 불명확하면 미해결 항목으로 남기고 게시하지 않는다. 번역이 바뀐 영어의 태그는 새 번역 구간에 대응시킨다.
4. 최종 병합본의 태그를 제외한 본문은 해당 웹 본문과 같아야 한다. 허용한 태그 이외의 화자·분기·효과·조건·순서 차이는 없어야 한다. 현재 정상 진행 플래그와 선택지 의미를 임의로 교정하지 않는다.
5. 기존 반입기에 명시적 **검토된 부분 병합 게시 경로**를 추가한다. 기본 전체 반입 보호는 유지한다. 게시 계획에는 웹 SHA, 원본/정규화/병합 결과 해시, 대상 9파일, 현재 파일·manifest·ID map 해시, 허용 필드 차이를 넣는다. 이 경로는 현재 도구에 아직 없는 구현 대상이다.
6. 웹 원본은 고정 Git blob 검사를 받고, Unity 검사 영수증은 최종 병합 스테이지의 모든 파일 해시를 대상으로 한다. 원본 웹 영수증을 병합본에 재사용하지 않는다. 검토 후 수정이나 동시 변경은 게시를 거부한다.
7. manifest의 `files`는 실제 Unity 게시 바이트, `web_files`는 실제 웹 출처 바이트로 기록한다. 부분 반입 이력에 9파일과 d9a3474를 추가하고 제조 4파일 이력을 보존한다. `story/steps.csv` 효과 차이는 기존 `unity_overrides` 보호에 명시한다. 모든 파일을 d9a3474와 동일하다고 기록하지 않는다.
8. 쓰기 실패 시 대상 CSV·manifest·ID map을 함께 복구한다. 보호가 작동하는 테스트와 미선택 파일 불변 검사를 추가한다.

`WebCsvCompatibilityTests.PublishedFilesAreExactlyThePinnedWebSnapshot`은 의미에 맞게 게시 해시/웹 출처/승인된 효과 차이/부분 반입을 구분하는 검사로 보완한다. 비교를 제거하거나 무조건 현재 해시에 맞추는 변경은 금지한다.

완료 기준: 미해결 효과 매핑 0개, 비허용 필드 차이 0개, 이전 제조 파일 불변, 오염된 스테이지·오래된 영수증·중간 쓰기 실패 테스트가 보호를 입증한다.

### 단계 2 — 삼호 drunk 리소스와 표정별 시각 배치

1. 웹 PNG의 Git 출처와 이전 Unity 제거 이력을 확인한다. Legacy 복원에 해당하면 AGENTS의 복원 이유·담당자 알림 요건을 이행할 수 있도록 기록한다. 새 그림을 만들거나 기존 파츠를 임의 합성하지 않는다.
2. 필요한 실제 PNG 한 장을 Unity 캐릭터 아트 폴더에 원본 바이트로 가져온다. 기존 아트 이동/교체는 하지 않는다. Unity importer로 PPU 100, Point, 무압축, Sprite 타입·이름·GUID를 확인한다.
3. 실제 Sprite에 Addressables 주소를 연결하고 `StreamingAssets/unity/resource_bindings.csv`로 `samho_drunk`를 보정한다. 주소 존재만이 아니라 Sprite 타입의 로드 성공까지 확인한다.
4. 기존 `NewExpressionDataSO.PortraitPlacement`에 선택적 표정 키를 추가하는 최소 확장을 사용한다. 빈 표정 키는 현재 캐릭터 공통 설정으로 유지하고, 정확한 캐릭터+표정 설정이 우선하도록 한다. 대사/조건 데이터는 SO에 저장하지 않는다.
5. `DialogueCharacterManager`와 `GuestCharacterView`의 배치 조회에 표정을 전달한다. 시바의 기존 (0,35) 설정과 다른 캐릭터 설정을 보존한다. 삼호 drunk 보정값은 원본 여백/현재 좌석 기준을 측정해서 SO에 저장하고, 웹 화면 좌표를 그대로 코드에 넣지 않는다.
6. `PortraitPlacementTests`에 표정별 우선순위·공통값 복귀·반복 전환 시 누적 없음 검사를 추가한다. `default → drunk → success/fail → default`, 좌석 변경과 장면 재진입을 실제 재생한다.

완료 기준: CSV를 바꾸지 않고 drunk가 실제로 로드됨, 텍스트 전용 fallback으로 들어가지 않음, 기본 표정·시바·좌석 기준이 유지됨.

### 단계 3 — Home/OutSide의 고정 UI를 에디터에 저장

대상: `WorldInteractionView.cs`, `WorldAmbientDialogueController.cs`, `WorldAmbientDialogueView.cs`, `Home.unity`, `OutSide.unity`, 관련 에디터 연결/수명 테스트.

1. 기존 View 아래 고정 이미지 뷰·말풍선·힌트·선택지 인스턴스를 에디터에서 작성하고 컴포넌트 참조를 저장한다. 기존 프리팹 아트/레이아웃을 사용한다. Initialize는 참조 검사·콜백·런타임 서비스 연결만 수행한다.
2. 주변 대화 뷰는 대상별로 하나씩 에디터에서 배치한다. 대상과 View/DialogueRunner를 Inspector에서 명시적으로 연결한다. Home은 같은 TV를 공유하는 두 방송에 같은 뷰를 사용한다. OutSide는 5그룹 각각 별도 뷰로 동시 표시를 유지한다. 기존 ambient 대상도 전수 확인해 누락시키지 않는다.
3. 각 뷰의 CanvasGroup, Bubble, Runner를 미리 저장한다. 컨트롤러는 기존 조건/진행/취소 처리를 유지하고 저장된 인스턴스를 빌려 재생한 뒤 상태만 초기화한다. 런타임 Instantiate/AddComponent/Destroy fallback을 남기지 않는다.
4. 컴포넌트는 활성 소유자에 두고 자식 Root/CanvasGroup만 표시를 바꾼다. 취소 완료 전 인스턴스 재사용을 막고, 과거 세션 finally가 새 세션을 숨기지 않게 한다. 재진입 때 화자·텍스트·타이핑·선택 상태가 남지 않게 한다.
5. 동적 선택지 행은 기존 재사용 정책과 최신 거부 연출을 유지한다. 이 작업을 이유로 `ChoiceTextGlitch`/입력 확정 규칙을 재작성하지 않는다.
6. 신규 에디터 셋업은 미저장 씬을 덮어쓰지 않고, 대상 씬만 다룬다. 두 번 실행해 객체·참조·수동 위치가 같음을 검사한다. 예외 후에는 실제 디스크 diff를 확인해 부분 저장과 카메라 직렬화 변경을 구분한다.

이 경로의 기존 `menuScale=.7`과 `choices.Rect.localScale` 적용은 UI Scale 1 원칙과 다르다. 고정 뷰를 저장하는 단계에서 메뉴의 실제 표시 크기를 캡처한 뒤 폭·높이·글자 크기·간격 설정으로 옮긴다. 기존 일반 선택지와 축소 메뉴를 같은 크기로 뭉개지 않는다. 화면 여백 20/8, 고정 말풍선 하단 거리 33 같은 연출값도 수정 경로에서는 이름 있는 직렬화 설정으로 노출하고 두 View가 중복 기본값을 소유하지 않게 한다. 변경되는 공용 프리팹의 원본과 Home/OutSide 화면을 함께 검증한다.

완료 기준: 고정 부품과 ambient 객체 수가 재생/종료 반복으로 변하지 않음, 두 그룹 동시 재생 가능, 수동 대화/일시정지 중 정지·복귀, 취소/씬 종료 뒤 남은 작업 없음. 새 UI 구조와 기존 선택지 회귀가 통과해야 한다.

### 단계 4 — 주변 대화 5그룹 및 Home TV 연결

| source ID | 등장 시점 | 화자 |
|---|---|---|
| outside_day0_store_passers | 0일차 퇴근 | store-pair-left/right |
| outside_day0_building_residents | 0일차 퇴근 | resident-left/right |
| outside_day0_port_pair | 0일차 퇴근 | bar-pair-left/right |
| outside_day1_commute_passers2 | 1일차 출근 | commute-pair-left/right |
| outside_day2_commute_reopening | 2일차 출근 | reopening-pair-left/right |

1. 현재 씬에 같은 역할의 인물이 있으면 먼저 재사용 가능성을 확인한다. 과거 문서의 `Neighbours_AutoDialogue`/CitizenA/B는 현재 씬 검색에서 확인되지 않았으므로 지금도 있다고 가정하지 않는다. 현재 authoring 구조와 Git 이력을 참고해 중복 배치를 피한다.
2. 각 그룹에 기존 `WorldInteractionContent`, `WorldInteractionTarget`, `WorldDialogueSpeakers`를 연결한다. `Ambient` + `SpeechBubble`, 배우별 앵커, 시각 Renderer/Animator를 에디터에서 저장한다. 이동 연출이 없는 배경 대화에 NpcInteraction을 붙이지 않는다.
3. 일차·출퇴근·반복 여부는 CSV에서 읽는다. 위치/반경은 현재 씬의 Inspector 값이 우선한다. 웹의 좌표를 기준으로 최초 배치를 확인하되 사람이 고친 값은 재실행 시 보존한다. OutSide의 정해진 Sorting Layer를 사용한다.
4. Home TV는 기존 source `home_tv`와 `tv_announcer` 앵커를 재사용한다. `contentStartOverrides`에 누락된 `p_outside_day0_tv_news1 = Ambient`만 추가하고 기존 수동 모드와 `p_home_tv_morning` 설정을 보존한다.
5. 기존 `WorldInteractionStartSettingsSetup.MigrateTarget`은 `HasAuthoredStartSettings`이면 건너뛰므로 기존 메뉴 재실행만으로 TV가 고쳐지지 않는다. 시작 설정 버전을 초기화해서 전체 값을 덮지 말고 새 override만 추가한다.
6. once는 씬 방문당 한 번이며 영구 세이브 플래그를 추가하지 않는다. TV repeat는 범위를 벗어나 재진입할 때 재생한다. 시나리오 완료 전에 취소되면 완료 이력으로 저장하지 않는다.
7. 복도 주민의 웹 “엘리베이터 이동 중 화면 노출” 시작 방식은 단순 반경과 다르다. 기존 엔진 proximity 연결을 기본 구현으로 명시하고, 동일한 노출 타이밍까지 구현한 것으로 보고하지 않는다. 보이는 위치/높이에서 대사가 읽히는지 확인한다.
8. TV 자동 뉴스가 선택되는 일차에 기존 수동 TV/다큐 진입이 어떻게 되는지 회귀 확인한다. 새 메뉴 구현 없이 자동/수동이 완전히 동시에 지원된다고 약속하지 않는다. 충돌이 생기면 해당 방송 연결을 완료로 표시하지 않고 기존 동작과 최소 수정안을 기록한다.

완료 기준: 7장면 모두 데이터셋/flow/source/대상/화자/뷰 연결이 확인되고, 5그룹 once와 TV repeat가 일차별 실제 화면에서 검증됨.

### 단계 5 — 최종 스테이지 검증 후 게시

1. 단계 1의 최종 전체 카탈로그 스테이지에 Unity의 `CsvContentValidator.ValidateCatalog`를 실행한다. 웹 파일 9개만 따로 검사하지 않고 기존 나머지 파일과 합친 카탈로그를 검사한다.
2. 현재 열린 에디터에서는 `ValidateBatch`를 메뉴에서 호출하지 않는다. 이 함수는 마지막에 `EditorApplication.Exit`를 실행한다. 기존 ValidateCatalog를 호출하고 파일 해시 영수증을 쓰는 편집 모드 경로를 준비한다.
3. 실제 게시 직전에 현재 파일/manifest/ID map 해시를 다시 검사하고, 검토된 9파일과 관련 기록만 게시한다. 게시 후 같은 검증을 반복하고 실패 시 데이터 일괄 복구한다.
4. 씬 저장과 CSV 쓰기는 하나의 원자적 작업이라고 주장하지 않는다. 구조/리소스 준비 단계와 최종 데이터 게시 단계를 분리해 각각 diff·저장·복구 경계를 둔다.
5. 게시 후 `WebCsvCompatibilityTests`의 정확한 해시 및 허용 차이 검사가 통과해야 한다. 이 테스트는 이번 반입과 직접 관련되므로 과거 실패로 계속 방치하지 않는다.

### 단계 6 — 테스트, 실제 재생, 기록 정리

자동 검사:

- Python: 기존 반입기 테스트 + 효과 병합/부분 게시 보호/롤백 신규 사례.
- Unity 데이터: `WebCsvCompatibilityTests` + 최종 카탈로그 검증.
- 주변 대화: `WorldInteractionStartSettingsTests` + 신규 authored UI 수명/7장면 도달 테스트.
- 대사/선택지: `OutsideStoryEventTests`, `NpcSequenceCommandTests`, `SceneDialogueContinuationTests`, `DialogueChoiceRejectionTests`, `ChoiceRejectionTests`, `ChoiceRejectionRuntimeTests` 중 실제 변경 경로에 해당하는 검사.
- 표정: `PortraitPlacementTests` + 실제 Sprite 리소스 로드 검사.
- 회귀: `DailyUnlocksTests`, `WebCraftingAlignmentTests`, `BarSeatHudTests`, `BarSeatSignalRulesTests`.
- 씬 이동: `WebContentRuntimeSmokeTests`는 EditMode에서 Play 모드로 진입하는 테스트이므로 현재 씬 저장 상태를 먼저 확보한 뒤 실행한다.

실제 재생 표:

| 구간 | 확인할 결과 |
|---|---|
| 0일차 퇴근 | 주변 대화 3그룹, Home TV 뉴스, 퇴장·재입장·반복 규칙 |
| 1일차 출근 | TV 뉴스, 행인 대화, 삼호/밀수업자 변경 문장과 효과 |
| 2일차 출근/거리 | 재개점 소문, 삼호 대사 재배열, 불량배 정보 선택/결제/화자 |
| 3일차 출근/바 | 계약 대사·기존 NPC 행동 순서, 삼호 drunk 4곳과 표정 전환 |
| KO/EN | 번역 516칸, 긴 영어 줄, 효과·태그 노출 없음, 선택지 거부 표시 |
| 공통 | 일시정지·수동 대화 중 배경 대화 정지/복귀, 씬 종료 취소, 중복 보상 없음 |
| 1280×720 / 1920×1080 / 2560×1440 | 말풍선·선택지·초상 배치, 기존 HUD/해금 화면 회귀 |

Unity 에디터가 열려 있으면 AGENTS에 따라 준비된 Test Runner 묶음을 사용자에게 요청한다. 현재 계획 검토 중에는 테스트를 요청하거나 에디터를 조작하지 않는다. 컴파일, EditMode, 실제 재생 증거를 별도로 기록한다.

정리 대상:

- `CSV_Web_Alignment_2026-11-08.md`의 현재 상태·잘못된 NPC 설명 정정.
- `web_runtime_contract.json`에 terrace-view.js 등 실제 검토 범위를 기록할 때 적용/보류 판단도 문서에 함께 남김. reviewed_commit 갱신은 기능 반영 완료를 뜻하지 않음.
- 사용 완료 확인 후 `WebCraftingImportOnce.cs`와 meta 제거. 본 대사 작업의 일회성 편집기 스크립트도 제거. 반복 유지보수 도구와 일회성 도구를 구분함.
- 기존 커밋에 들어간 `ContentAuthoring/__pycache__/*.pyc`는 재현 산출물이므로 별도 정리 대상으로 기록. 대사 변경에 묻어 삭제하지 않음.
- 기존 병합의 폰트 fallback 등 무관한 변경은 이번 작업 변경으로 되돌리거나 포함하지 않음.

## 6. 별도 유지할 차이

- 집 TV의 본다/끈다/취소 메뉴, 전원 상태: 사용자 결정대로 문서만 유지하고 별도 이슈 생성 안 함.
- 이웃 선택지 상태별 잠금 사유: 사용자 결정대로 문서만 유지. 이번 병합의 거부 UI와 사유 문구의 구체성은 구분.
- 테라스 6.8초 진입 팬: 최신 웹 변경을 기록하되 대사 게시 필수 변경에 포함하지 않음. 향후 연출 작업이면 CM_ 가상 카메라와 Inspector 수치로 구현.
- 복도 주민 화면 노출 시작 타이밍: 기본 proximity와 차이를 명시.
- 웹 계약 gaps 17건: 기존/신규와 실행 경로를 분리해서 기록. 대사 반영 완료와 전체 웹 동등성 완료를 구분.
- 일반 손님 주문 4행: 별도 변경. 제조 4파일과 혼동하지 않음.

## 7. 커밋과 복구 경계

실제 구현 요청 후 브랜치는 현재 HEAD에서 `codex/` 접두사로 분리한다. 최종 검증 전 자동 커밋/푸시는 하지 않는다.

권장 검토 단위는 (1) 효과 병합/반입 보호와 검사, (2) 표정 리소스 및 Home/OutSide authored 뷰·배치, (3) 최종 9 CSV 게시·manifest·기록이다. 실제 커밋은 각 단위가 독립적으로 컴파일/초기화 가능하게 조정한다. 단계 2·3 코드/씬/리소스 중 서로 의존하는 파일은 함께 묶는다.

메시지는 AGENTS 형식 `[#이슈번호] 한국어 요약`과 동작 bullet을 따른다. 대사 반영 이슈 번호는 실제 연결된 번호를 사용하고 #116이라는 이유만으로 신규 대사 작업까지 확정하지 않는다.

실패 시 전체 저장소 reset을 하지 않는다. 게시기는 이번 파일 집합만 원상 복구하고, 씬 작업은 작업 전 백업/Undo와 실제 diff를 기준으로 복구한다. 미저장 사용자 변경과 병합된 선택지 변경을 보존한다.

## 8. 현재 확인 수준

- 소스/설정/Git/원본 이미지 확인과 읽기 전용 데이터 계약 검사를 수행했다.
- 계약 검사 errors 0은 스키마/명령 비교 결과이며 17개 실행 차이를 해결했다는 뜻이 아니다.
- 이 계획을 적용한 컴파일·Unity 테스트·인게임 재생은 아직 수행하지 않았다.
- 계획의 근거 JSON 검사 결과는 문서 구조/기록 일관성 검사이며 기능 검증 결과가 아니다.
