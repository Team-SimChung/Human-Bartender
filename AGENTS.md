# AGENTS.md — Human Bartender

이 저장소에서 작업하는 코딩 에이전트(Codex 등)가 따르는 규칙입니다. 팀의 Unity 가이드(1권 에디터 작업, 2권 UI·에디터)에 정리된 Sao(LeeDongHun)의 작업 방식을 요약했습니다.

- Unity 프로젝트 루트는 `HumanBartender/`입니다. 아래 경로는 모두 이 폴더 기준입니다.
- Unity 6000.3.14f1, URP 2D, uGUI + TextMeshPro, VContainer.

## 핵심 원칙

1. **구조는 에디터에서 만들어 저장하고, 런타임 코드는 값만 바꾼다.** 실행 중에 UI 구조를 생성하거나 지우지 않습니다.
2. **조절하는 숫자는 코드에 두지 않는다.** 연출 수치, 색, 타이밍은 `[SerializeField]`, ScriptableObject, 머티리얼 속성으로 노출하고 기본값은 한 곳에서만 정의합니다.
3. **Inspector가 사용 설명서다.** `[Header]`, `[Tooltip]`, `[Min]`/`[Range]`를 붙이고, 설정이 까다로우면 Custom Inspector(HelpBox, 필요한 칸만 표시)를 둡니다.
4. **누가 열어도 같은 자리에서 찾을 수 있게.** 아래 씬·이름·폴더 규칙을 지킵니다.
5. **필요한 만큼만.** 작은 시스템을 과하게 쪼개지 않습니다. 실제로 문제를 일으키는 중복·결합만 정리합니다.

## 씬 구조

- 모든 씬의 최상위: `01.Systems`(보이지 않는 관리자, SceneScope, SceneInput) / `02.Cameras` / `03.World`(보기 전용 배경·조명·충돌체) / `04.Gameplay`(로직 + 월드 캐릭터, 기능별) / `05.UI`.
- 폴더 오브젝트는 (0,0,0)에 놓인 빈 GameObject입니다.
- Main Camera를 직접 고치지 않습니다(Cinemachine Brain이 덮어씀). `CM_` 가상 카메라를 고칩니다.

## UI 규칙

- **캔버스**: 기능 하나에 하나, 이름은 `Canvas_기능명`. 화면에 붙으면 Screen Space Overlay, 월드 물체를 따라가면 World Space(Scale 약 0.02).
- **바(Play) 화면 Overlay 캔버스**: Canvas Scaler Scale With Screen Size, **960×540, Match 0**, Layer UI. 1280×720이나 1920×1080 기준으로 새로 만들지 않습니다.
- **Play 씬 Sort Order**: Dialogue 8, Coaster_Tray 9, ServicePanel·BarSeatHud·말풍선 10, RecipeBrowser 11, DailyUnlocks 12, DialogueHistory 13, Settlement 14, Gimmick HUD 100, Serve 103, Pause 105, Tutorial 4000. 새 캔버스는 위아래 이웃 사이의 빈 번호를 씁니다.
- **EventSystem**은 씬에 하나만 둡니다.
- **켜고 끄기**: 화면 스크립트는 항상 켜져 있는 캔버스에 붙이고, 그 아래 `XxxRoot` 하나만 `SetActive`로 켜고 끕니다.
- **배치 패턴**:
  - 팝업: Root(Stretch) → BackgroundDimOverlay(Stretch, Raycast 켬) → 가운데 패널(피벗 0.5,0.5, 고정 크기). 패널 안은 왼쪽 위 앵커·피벗 (0,1), 안쪽 여백 24, 시안 좌표를 Pos(x, -y)로 그대로 옮깁니다.
  - 사이드 패널: 붙는 벽 방향으로 Stretch합니다.
  - HUD: 가장 가까운 화면 모서리에 앵커를 둡니다.
  - 관련 요소는 빈 RectTransform으로 묶습니다.
- **만드는 순서**: 부모 먼저, 자식은 나중에(캔버스 → 화면 전체 루트 → 묶음 → 내용물). 자식 위치는 항상 부모 사각형 기준입니다. 그림이 없는 묶음은 빈 오브젝트(Create Empty)로 만듭니다.
- **RectTransform은 질문 세 개로 정합니다**: 앵커(부모의 어디에 붙나) → 피벗(내 몸의 어느 점이 기준인가) → Pos·Size(그 사이 거리와 내 크기).
- **앵커·피벗 고르는 표**:

  | 만드는 것 | 앵커 | 피벗 |
  |---|---|---|
  | 화면 전체 루트, 반투명 Dim 배경 | 전체 Stretch, 여백 0 | 0.5, 0.5 |
  | 가운데 팝업 패널 | 가운데 점 | 0.5, 0.5 |
  | 팝업 안 글자·줄 (시안 좌표 그대로) | 왼쪽 위 점 | 0, 1 |
  | 헤더 / 하단 바 | 가로 Stretch, 위 / 아래 | 0.5, 1 / 0.5, 0 |
  | 왼쪽(오른쪽) 사이드 패널 | 세로 Stretch, 그 벽 | 0, 0.5 (1, 0.5) |
  | 모서리 HUD 버튼 | 가장 가까운 모서리 점 | 0.5, 0.5 또는 그 모서리 |
  | 부모와 항상 같은 크기(테두리, 칸 안 글자) | 전체 Stretch, 여백 0 | 기본값 |
  | 스크롤 목록의 Content | 가로 Stretch, 위쪽 | 0.5, 1 |

  가운데에서 커지는 것(팝업·아이콘·버튼, DOTween 스케일)은 피벗을 가운데에 둡니다.
- **Layout Group 쓰는 곳**: 개수가 바뀌는 목록(대화 기록, 레시피 목록, 메뉴 버튼)과 같은 칸을 줄 세우는 묶음에만 씁니다. 줄 수와 자리가 정해진 화면(정산창 등)은 손으로 배치합니다.
  - 가로로 줄 세우면 Horizontal Layout Group(예: 좌석 점 3개), 세로 목록·메뉴 버튼은 Vertical Layout Group입니다.
  - **"줄 세울 것만 들어 있는 빈 부모"에 붙입니다.** 배경 Image나 테두리가 있는 프레임에 바로 붙이면 테두리까지 한 칸으로 취급돼 밀립니다. 프레임 아래에 Stretch된 빈 자식(예: `Seats`)을 두고 거기에 붙입니다.
- **Layout Group 기본 설정**:

  | 옵션 | 값 | 이유 |
  |---|---|---|
  | Padding / Spacing | 시안 값 그대로(예: 칸 22 + 간격 2 = 중심 간격 24, 대화 기록 줄 간격 4~5) | |
  | Child Alignment | 목적에 맞게 지정(HUD 줄은 Middle Center, 목록은 Upper Left/Center) | |
  | Control Child Size W/H | **끔** (글자 길이에 맞춰 줄 높이를 바꿔야 하는 목록만 Height 켬) | 칸 크기는 자식이 정함 |
  | Use Child Scale | 끔 | Scale은 항상 1 |
  | Child Force Expand W/H | **끔** | 켜면 남는 공간을 나눠 가져 간격이 벌어짐 |

  - 자식의 앵커·Pos가 회색으로 잠기는 게 정상입니다(Layout이 정함). 자식은 Width/Height만 정합니다. 자동 배치에서 빼야 하는 자식은 LayoutElement의 Ignore Layout을 켭니다.
  - 칸 하나를 완성한 뒤 Ctrl+D로 복제하고 이름만 바꿉니다. 위치는 손대지 않습니다.
  - Content Size Fitter는 자식 크기에 맞춰 커져야 하는 부모(스크롤 Content)에만 붙입니다. 부모와 자식이 서로 크기를 정하게 만들지 않습니다.
  - Layout은 다음 프레임에 계산됩니다. 코드에서 줄을 추가한 직후 위치가 필요하면 `LayoutRebuilder.ForceRebuildLayoutImmediate(content)`를 부릅니다.
- **스크롤 목록 구조**: `ScrollRect`(Vertical만) › `Viewport`(전체 Stretch, RectMask2D) › `Content`(가로 Stretch·위쪽, 피벗 0.5,1, VerticalLayoutGroup + ContentSizeFitter Vertical: Preferred Size) › 줄 프리팹. 모양대로 잘라야 할 때만 Mask를 씁니다.
- **그리는 순서**: Hierarchy에서 아래에 있을수록 위에 그려집니다(예: 링 다음에 점).
- 묶음 투명도·입력 차단은 CanvasGroup 하나로 처리합니다.
- **값**: 숫자는 정수, Scale은 항상 1(크기는 Width/Height로). UI에 Scale 도구를 쓰지 않습니다.
- **글자**: TextMeshPro만 씁니다(Legacy Text 금지). 폰트는 Paperlogy(UI), PFStardust(대사·선택지·이름), Pretendard. 크기는 9~12, 제목 20~22. Auto Size는 끄고, 글자 그대로 보여야 하는 곳은 Rich Text를 끕니다.
- **Raycast Target**: 장식 Image와 글자는 끕니다.
- **버튼**: Inspector OnClick이 아니라 코드에서 `onClick.AddListener`로 연결합니다(사용처를 검색할 수 있게).
- **애니메이션**: UI 등장·사라짐은 DOTween으로 합니다.
- **이름**: 역할 + 종류(`…Root/Panel/Group/Row/Text/Button`), **띄어쓰기 없음**.

## 프리팹

- 프리팹은 세 경우에만 만듭니다: ① 실행 중 여러 개 찍어내는 한 줄(DialogueHistoryEntry 등), ② 필요할 때 생겼다 사라지는 독립 단위(TableServeSpot, CraftPrepStage, 미니게임), ③ 여러 씬이 같이 쓰는 부품(PlayDialogueTextBox, ChoicePanel, Canvas_Logo). 씬에 하나뿐인 화면은 씬 오브젝트로 둡니다.
- 평평하게 만들고, 중첩이나 Variant는 피합니다.
- 스크립트는 루트에 두고, 자식은 `[SerializeField]`로 연결합니다(`transform.Find` 금지). 프리팹 필드는 컴포넌트 타입으로 선언합니다.
- 프리팹은 바깥을 모르게 합니다. 필요한 동작은 생성한 쪽이 콜백으로 넣습니다.
- 목록은 줄을 재사용합니다. 모자랄 때만 생성하고 남는 줄은 숨깁니다.
- 위치: `Assets/01.Game/03.Content/08.Prefabs/` 기능별 폴더.
- 씬 인스턴스가 아니라 **프리팹 원본**을 고칩니다(의도치 않은 Override 금지).

## 코드

- 폴더: `02.Scripts/{01.Bootstrap, 02.Core, 03.Data, 04.Features, 05.Presentation}`.
- 로그와 예외 메시지 앞에 `[기능명]` 태그를 붙입니다.
- 콘텐츠 정본은 CSV(`Assets/StreamingAssets/csv`)이고, SO는 시각 자료·연결용입니다. 리소스 주소는 `StreamingAssets/unity/resource_bindings.csv`에 있습니다.
- 입력은 `Assets/01.Game/05.Settings/01.Input/PlayerInput.inputactions`(맵: Outside / Play / CraftPrep / CraftGimmick / UI)에 정의돼 있습니다. 팀 공용 파일입니다.
- 셰이더는 `Assets/01.Game/03.Content/06.Shared/03.Shaders/`(UI용은 `UI/`)에 둡니다. 파일명은 `SH_` 접두사를 씁니다. UI 셰이더는 Stencil, `_ClipRect`, 정점색(Image 색·CanvasGroup)을 지원해야 합니다.

## 에디터 확장

- 위치: `Assets/01.Game/Editor/{01.ContentTools, 02.SceneSetup, 03.AnimationTools, 04.Inspectors, 05.BuildTools}`.
- 셋업 스크립트 규칙:
  - `[MenuItem("Tools/...")]`, `SerializedObject`/`FindProperty`, `Undo.*`, `PrefabUtility.InstantiatePrefab`, `SetDirty`/`MarkSceneDirty`를 씁니다.
  - **두 번 실행해도 안전해야 합니다.** 이미 연결된 값이나 사람이 손본 값은 덮어쓰지 않습니다.
  - 로직은 static 함수 하나에 두고, 메뉴와 Inspector 버튼이 같이 씁니다.
  - 에셋(머티리얼, SO)을 몰래 만들지 않습니다. 없으면 경고 로그를 남기고 비워 둡니다.
- 일회성·임시 에디터 스크립트는 작업이 끝나면 지웁니다.

## 에셋·git 규칙

- **파일 이동·이름 변경은 Unity 안에서** 합니다. 부득이하게 밖에서 할 때는 `.meta`를 함께 옮겨 GUID를 유지합니다.
- 새 파일은 `.meta`까지 커밋합니다.
- **커밋하지 않는 것**: 의도하지 않은 변경(예: `LiberationSans SDF - Fallback.asset`, 줄바꿈만 바뀐 파일), 연습 씬(`Assets/02.Dev/01.Scenes/`).
- 레이어(`ProjectSettings/TagManager.asset`)나 Input Actions를 바꾸면 팀에 알리고 커밋 메시지에 적습니다.
- 스프라이트는 PPU 100이고, 픽셀아트는 Point 필터·무압축입니다. Play 씬은 Default 정렬 레이어 + Order in Layer, OutSide/Home은 정해진 Sorting Layer 체계를 따릅니다.
- 다른 팀원이 Legacy로 지운 리소스를 되살릴 때는 이유를 PR에 적고 지운 사람에게 알립니다.
- 커밋 메시지: `[#이슈번호] 한국어 요약` + 바뀐 동작 bullet.

## 검증

- 테스트: `Assets/01.Game/06.Tests/Editor/`(EditMode). UI·프리팹 구조를 바꾸면 관련 테스트(`VisualUIReviewTests`, `ChoiceFocusArrowTests` 등)를 함께 고칩니다.
- Unity 에디터가 열려 있으면 배치 모드(`-batchmode -runTests`)를 돌릴 수 없습니다. 이때는 사용자에게 Test Runner 실행을 요청합니다.
- 해상도: Game 창을 1280×720, 1920×1080, 2560×1440으로 바꿔 가며 앵커가 맞는지 확인합니다.
- 커밋 전 체크리스트:
  - 정수 값인가
  - Scale이 1인가
  - 장식의 Raycast Target을 껐나
  - 이름에 띄어쓰기가 없나
  - Play 모드를 끄고 저장했나
  - 프리팹 Override를 정리했나
  - 의도하지 않은 변경이 섞이지 않았나

## 알려진 예외

- `Assets/01.Game/Editor/02.SceneSetup/VisualUISetup.cs`는 10-20 UI 개편용 일회성 도구입니다.
  - "Fix dialogue and choice frames" 메뉴는 ChoicePanel 프리팹의 글자 크기·색·여백을 **덮어씁니다**.
  - 타이틀(Main 씬) 오브젝트 이름에 띄어쓰기가 남아 있습니다.
  - 새 코드는 이 파일을 본보기로 삼지 않습니다.
