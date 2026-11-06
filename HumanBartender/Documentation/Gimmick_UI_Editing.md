# 기믹 UI 화면에서 편집하기

2026-11-06 기준. 버튼·텍스트·게이지·목표선은 프리팹이나 씬에 저장되어 있다. 플레이를 시작하지 않고 Scene 뷰에서 선택해 이동하고 크기를 조절할 수 있다.

## 편집 방법

1. 아래 프리팹을 Project 창에서 더블 클릭한다.
2. Prefab Mode의 Hierarchy에서 원하는 항목을 선택한다. UI는 Scene 뷰의 **2D**를 켜고 **Rect Tool(T)** 로 옮기거나 늘린다.
3. Inspector의 **Rect Transform → Pos X/Y, Width/Height, Anchors**로 수치를 조절한다. 월드에 있는 잔·병·목표선·셰이크 경로는 일반 **Transform**을 사용한다.
4. 프리팹을 저장한다. Play 중 변경은 종료 시 사라지므로 편집 모드에서 저장한다.

## 공통 HUD: 목표량·현재량, 재료, 시간, 마치기 버튼

**따르기·필업은 아래 ‘따르기·필업’ 절의 프리팹 안에서 편집한다.** 이 절의 Play 씬 HUD는 다른 기믹의 공통 표시와 전용 HUD가 없는 개발 씬에 사용한다.

`Assets/01.Game/01.Scenes/Play.unity`를 열고 다음 위치를 선택한다.

```text
04.Gameplay
└─ Crafting
   └─ Crafting
      └─ Gimmick HUD
         └─ Panel
            ├─ Progress
            ├─ Pour Quantity Readout
            │  ├─ Pour Target Caption / Pour Target Value
            │  └─ Pour Current Caption / Pour Current Value
            ├─ Pour Ingredient Readout
            │  ├─ Pour Ingredient Caption
            │  └─ Subject
            ├─ Pour Tilt Meter
            │  ├─ Pour Tilt Caption
            │  └─ Pour Tilt Track
            │     ├─ Pour Flow Range / Pour Tilt Warning
            │     └─ Pour Tilt Marker
            ├─ Timer
            │  ├─ Timer Icon
            │  └─ Time
            ├─ OK
            └─ Next Button
```

목표량·현재량 전체는 `Pour Quantity Readout`, 재료 이름은 `Pour Ingredient Readout`, 기울기 바는 `Pour Tilt Meter`에서 조절한다. 시간 전체는 `Timer`, 아이콘과 숫자 사이 간격은 그 안의 두 자식에서 조절한다. Panel은 제조 중에만 켜지므로 편집 모드에서 배치를 확인하려면 Panel을 잠시 켠다.

공통 HUD의 Canvas Scaler는 **1280×720 / Expand**를 사용한다. 1280×720으로 저장된 Panel과 기준 해상도가 맞아야 수동 배치가 화면에서 같은 비율로 보인다. Play 씬의 누락된 수량·기울기 참조를 복구했고, 실제 씬 자산을 기준으로 표시·버튼 연결·세 해상도의 화면 범위를 검증한다.

`CraftGimmickHud`는 타이머 위치·배율이나 Panel 크기를 매 프레임 덮어쓰지 않는다. 기울기 표시가 연결된 경우 마커의 X 앵커와 유량 구간의 X 앵커만 값에 따라 움직인다. 높이·여백·부모의 위치는 편집한 값을 유지한다. 화면 크기 대응은 Canvas Scaler가 담당한다.

## 병따기

프리팹: `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/04.Cap/NewCapGame.prefab`

기준 위치: `NewCapGame/Cap Root/Cap Manager/Cap Web Canvas/Web Root`

| 오브젝트 | 편집 대상 |
| --- | --- |
| `Phase` | 상단 진행 안내 |
| `Timing Hint` | 타이밍 입력 안내 |
| `Feedback` | PERFECT / POP / MISS 글자의 위치·크기·폰트 |
| `Gauge Label` | 힘 게이지 제목 |
| `Power Gauge` | 힘 게이지 전체 위치와 크기 |
| `Timing Rings` | 타이밍 원의 위치와 크기 |
| `Opening Hit` | 하단 누르기 버튼. `Kbd`, `Label`은 내부 간격 |
| `Gimmick Finish` | 다음 버튼 |
| `Bottle Assembly` | 병과 관련 연출 전체의 위치·크기 |
| `Bottle Assembly/Bottle`, `Cap`, `Opener` | 각 그림의 위치·크기 |
| `Opening Lesson/Copy` | 학습 안내 상자 |
| `Opening Lesson/Copy/Text`, `Kbd`, `Arrow` | 안내문·키 표시·화살표 |

안내 상자는 기본으로 꺼져 있다. 편집할 때 `Opening Lesson`을 잠시 켜서 확인할 수 있다. `Cap Manager`의 `CapOpenView → Editor Preview → Preview Timing`을 켜면 타이밍 링, 끄면 힘 게이지가 보인다. 안내 대상 주변 가림막 구멍도 대상 RectTransform을 따라가므로 게이지를 옮겨도 이전 좌표에 남지 않는다.

버튼과 안내문에 자동 Layout Group / Content Size Fitter가 없으므로 자식도 직접 배치할 수 있다. 힘 게이지와 원은 고정된 RectTransform 안에서 판정 값에 따른 모양만 다시 그린다. 뚜껑 비행·실패 흔들림·화살표의 작은 움직임은 계속 재생된다.

## 스터

프리팹: `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/02.Stir/NewStirGame.prefab`

기준 위치: `NewStirGame/Game Canvas`

- `Playfield/Arena`: 잔·타이머 링·스푼·WASD 키를 묶은 조작 영역. 안의 각 오브젝트도 따로 편집할 수 있다.
- `Playfield/Result Gauge`: 진행 게이지. `Segment 0`부터 `Segment 9`까지 각각 이동·크기를 조절할 수 있다.
- 각 Segment의 `Next Segment Outline`: 다음 칸 테두리의 두께·색·여백.
- `Elapsed Icon`, `Elapsed Time`: 시간 아이콘과 숫자.
- `Start Hint`: 시작 안내.
- 캐릭터·확대 화면·프레임은 `Playfield` 안의 해당 RectTransform에서 조절한다.

`StirLayoutView`는 활성화·해상도 변경 시 배치를 다시 쓰지 않는다. 키 상태가 바뀌어도 키캡 크기를 덮어쓰지 않는다. `Reset To Web Layout (Editor Only)` 컨텍스트 메뉴는 명시적으로 기본 배치를 복구할 때만 사용한다.

스터 개발 씬 `Assets/02.Dev/01.Scenes/01.Minigames/Stir.unity`에도 저장된 UI와 강조 테두리를 적용했다.

## 따르기·필업

프리팹: `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/03.Pour/NewPourGame.prefab`

**새 UI의 실제 편집 위치는 `NewPourGame/Pour HUD/Panel`이다.** 프리팹을 열면 목표량·현재량·재료 이름·기울기 바·시계·마치기 버튼이 켜진 상태로 보인다.

| Panel 아래 항목 | 편집 대상 |
| --- | --- |
| `Pour Quantity Readout` | 목표량·현재량 패널 전체. 자식 Caption / Value는 각 글자 |
| `Pour Ingredient Readout` | 재료 이름과 제목 |
| `Pour Tilt Meter` | 기울기 바 전체. `Pour Tilt Track` 아래 구간과 마커를 개별 편집 |
| `Timer` | 시계 전체. 아이콘과 숫자는 자식에서 개별 편집 |
| `Next Button` | 따르기 마치기 버튼. `Label`은 버튼 글자 |
| `OK` | 목표량 도달 표시. 편집 시 잠시 켜서 위치 확인 |

선택 후 Rect Tool(T) 또는 RectTransform의 위치·크기를 조절하고 프리팹을 저장한다. 편집 모드에는 UI가 병·잔과 같은 크기의 월드 공간에 놓인다. 제조를 시작하면 같은 UI가 화면 공간에 표시되며 Panel 이하 위치·크기는 그대로 유지한다. 프리뷰의 ‘360 ml / 0.00 ml / 병맥주’는 배치를 확인하기 위한 예시이고, 실행하면 실제 재료·목표량·현재량으로 갱신된다.

이 프리팹의 옛 `Legacy Gauge Canvas`, `Legacy Button Canvas`는 비활성 상태로 보관한다.

```text
NewPourGame/Pour Root
├─ Glass/Glass Interior
│  └─ Target Height (Recipe Driven)
│     └─ Marker Layout
│        ├─ Arrow
│        └─ Guide Dashes
│           └─ Dash 01 …
└─ Bottle
   └─ Bottle Art Layout
      └─ Ingredient Bottle
```

- 목표선의 공통 위치 보정은 **Marker Layout**에서 한다. 화살표 크기는 `Arrow`의 Transform Scale, 점선의 길이·굵기·간격은 `Guide Dashes` 안의 각 Dash에서 편집한다. 색은 렌더러의 Color / Material에서 조절한다.
- `Target Height (Recipe Driven)`의 **Y는 레시피에 따른 높이**이므로 시작할 때 한 번 정해진다. 눈으로 보이는 위치를 더 조절하려면 자식 `Marker Layout`의 Y를 사용한다. 수면에 따른 실시간 보정은 계속 꺼져 있다.
- 병의 실제 위치·크기는 `Bottle`에서 조절한다. `Bottle Art Layout`은 재료 그림에 추가할 공통 시각 보정이다. `Ingredient Bottle`의 Sprite와 기본 피팅은 재료가 정해질 때 갱신되므로 그 부모에서 보정한다. 그림만 움직이면 물리적 출구 위치는 바뀌지 않는다.
- `PourManager.GimmickHud`가 프리팹의 새 HUD를 제공한다. 공통 HUD는 표시를 이쪽에 넘기고 같은 제조 시계·시간 숨김 규칙을 전달하며, 튜토리얼의 목표량·마치기 버튼 대상도 새 HUD를 가리킨다.

## 셰이크

본편 프리팹: `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/01.Shake/NewShakingMinigame.prefab`

기존 Canvas 안의 텍스트와 진행 게이지를 직접 편집한다. 경로 선은 `NewShakingMinigame/ShakerLine/Shake Path`의 **Line Renderer**에서 색·굵기·재질을 조절한다. 경로 점은 게임 진행에 따라 갱신된다. 다음 칸 테두리도 각 Segment 안에 저장되어 있다.

본편 진행 게이지는 현재 목표 20회에 맞춰 20칸을 저장했다. 실행 시 실제 목표 수만큼 활성화하며 위치·크기는 덮어쓰지 않는다. 목표를 저장된 칸 수보다 늘리면 칸과 강조 테두리도 프리팹에 추가한다.

## 구조와 유지보수

- 실행 중 빈 UI 오브젝트를 만들던 병따기 BuildUi / BuildWorld, 스터 진행 칸·테두리 생성, 따르기 목표선 생성, 셰이크 경로 오브젝트 생성을 제거했다. 실행 중에는 저장된 참조를 사용한다.
- 액체 입자, 이동하는 셰이크 판정 노드, 탄산 등 게임 진행과 효과에 필요한 동적 렌더링은 유지한다.
- `GimmickUiPrefabSetup`은 기존 자산을 한 번 변환하기 위한 에디터 도구다. 변경한 프리팹을 직접 쓰므로 별도로 실행할 필요가 없다. 다시 실행해도 이미 연결된 UI를 재생성하거나 저장된 위치를 초기화하지 않는다.
- 새 UI는 `Assets/01.Game/03.Content/01.ArtResources/03.Crafting/04.Minigames/EditableUI`의 저장된 스프라이트·재질을 사용한다.
- UI를 추가하거나 교체할 때는 해당 View의 Inspector 참조도 연결한다. 런타임 자동 생성으로 빈 참조를 메우지 않는다.
