# 중요 변경점·유지보수 가이드

기준일: **2026-10-20**. 기준 브랜치: **develop**, 병합 커밋 **f360d043**.

최근 작업에서 달라진 기능, 이전 구조, 실제 설정 위치를 한곳에 모았다. #57 기믹 개선뿐 아니라 앞서 적용한 팝업·폰트·CSV·사운드와 develop에서 함께 병합된 Home 시계·로고·빗물 표현도 포함한다. 아래 경로는 별도 설명이 없으면 `HumanBartender` 프로젝트 폴더 기준이다.

**현재 상태를 확인할 때는 이 문서를 먼저 보고, 세부 작업 과정은 연결된 개별 문서를 본다.** 예전 문서의 “임시본 사용”, “6칸”, “원본 환원 예정”, “99일차 테스트”는 당시 기록이다. 현재는 원본 리소스 연결, 스터 10칸, Play 1일차 정상 진행이 기준이다.

## 목차

1. [빠른 변경 요약](#1-빠른-변경-요약)
2. [수정할 위치 찾기](#2-수정할-위치-찾기)
3. [재료 선택·아웃라인·설명창](#3-재료-선택아웃라인설명창)
4. [제조 공용 HUD와 타이머](#4-제조-공용-hud와-타이머)
5. [따르기: 잔 범위와 기울기](#5-따르기-잔-범위와-기울기)
6. [병따기: 오프너와 탄산](#6-병따기-오프너와-탄산)
7. [스터: UI·화질·10회 목표](#7-스터-ui화질10회-목표)
8. [서빙 애니메이션](#8-서빙-애니메이션)
9. [Home: 테라스·취침·시계](#9-home-테라스취침시계)
10. [대화·선택지·튜토리얼·실외 표현](#10-대화선택지튜토리얼실외-표현)
11. [팝업·CSV·폰트](#11-팝업csv폰트)
12. [사운드 연결과 재생 조건](#12-사운드-연결과-재생-조건)
13. [임시 리소스 최종 정리](#13-임시-리소스-최종-정리)
14. [Git 문제와 develop 병합](#14-git-문제와-develop-병합)
15. [테스트 방법·검증 범위](#15-테스트-방법검증-범위)
16. [세부 문서·커밋·백업](#16-세부-문서커밋백업)

## 1. 빠른 변경 요약

| 영역 | 현재 적용된 동작 | 다시 조절할 곳 |
| --- | --- | --- |
| 재료 선택 | 마우스 호버는 하늘색, 선택은 금빛. A/D로 선반 이동 | CraftPrepStage, 공유 아웃라인 머티리얼 |
| 재료 설명 | 마우스를 따라다니며, 긴 글에는 배경 네모가 커짐 | CraftPrepItemTooltip |
| 제조 공용 시간 | 시계 아이콘 + `mm:ss`, 한 잔 전체 누적 시간 | Play의 Gimmick HUD, CraftTimer |
| 따르기 | 새 cup3 이미지·내부 범위·기울기를 공용 프리팹에 반영 | NewPourGame |
| 병따기 | 입력 시 오프너가 움직이고 성공 시 탄산 재생 | NewCapGame, CapManager |
| 스터 | 새 아트·화면 크기에 맞춘 배치·고화질 시트·목표 10회 | NewStirGame, StirLayoutView, balance_config.csv |
| 서빙 | 전용 연출 8종, 6×5의 30프레임. 배경은 1장 고정 | ServeScreen, 새 클립 8개 |
| Home 취침 | 암전으로 테라스 전환, 루나 아래/크리스 위 말풍선, 종료 후 침대에서 다음 날 | HomeTerracePresentation, HomeSofaAction |
| Home 시계 | PC 현재 시각 `HH:mm`, 콜론 0.4초 표시/0.6초 투명 | HomeClock |
| 선택지 | 활성 항목 중앙 정렬, 1초 홀드 확정 | Play 선택지 부모, ChoiceHoldButton |
| 팝업 | 7종 연결, 목표 종류를 텍스트로 전달, CSV 지원 | ProjectLifeScope, PopupCsvStep |
| 사운드 | BGM 5종·SE 19종 등록, 상황별 음악·환경음·팝업·제조 피드백 연결 | SoundManager, SceneAudioController |
| 아트 정리 | 임시 PNG 81개 정리, 참조 338개를 원본으로 이관 | 원본 아트, RESOURCE_FINALIZATION.json |
| 병합 | 57-Gimmick-Upgrade → develop, 원격 반영 완료 | f360d043 |

## 2. 수정할 위치 찾기

| 대상 | 현재 정본 위치 |
| --- | --- |
| 실제 게임 씬 | `Assets/01.Game/01.Scenes/Play.unity`, `Home.unity`, `OutSide.unity` |
| 재료 선택 프리팹 | `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/01.Preparation/CraftPrepStage.prefab` |
| 스터 공용 프리팹 | `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/02.Stir/NewStirGame.prefab` |
| 따르기 공용 프리팹 | 같은 `02.Minigames/03.Pour/NewPourGame.prefab` |
| 병따기 공용 프리팹 | 같은 `02.Minigames/04.Cap/NewCapGame.prefab` |
| 제조 기믹 독립 씬 | `Assets/03.Dev/01.Scenes/01.Minigames/`의 Stir / Pour / Cap |
| 병따기 VFX 확인 씬 | `Assets/03.Dev/01.Scenes/03.Vfx/Cap 1.unity` |
| 팝업·사운드 등록 | `Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab` |
| 기믹 공용 UI 코드 | `Assets/01.Game/02.Scripts/04.Features/01.Crafting/05.UI/CraftGimmickHud.cs` |
| 기믹별 코드 | `Assets/01.Game/02.Scripts/04.Features/02.Minigames/` |
| 런타임 데이터 | `Assets/StreamingAssets/csv/` |
| 콘텐츠 작성 원본 | `ContentAuthoring/HumanBartender_C_Content.xlsx`, `HumanBartender_Game_Settings.xlsx` |
| 원본 아트 | `Assets/01.Game/03.Content/01.ArtResources/` |
| TMP 폰트 | `Assets/01.Game/03.Content/05.Fonts/` |

독립 테스트 씬만 수정하면 Play에 반영되지 않을 수 있다. 실제 게임에서 쓰는 **공용 프리팹**에 적용하고, 씬 인스턴스의 같은 항목에 오버라이드가 남았는지 확인한다. Stir는 별도로 UI가 저장된 독립 씬도 함께 갱신한 상태다.

## 3. 재료 선택·아웃라인·설명창

### 표시와 조작

- 호버는 하늘색 `#62ECFF`, 선택은 금빛 `#FFDA7B`. 둘이 겹치면 선택 표시가 우선한다.
- 선반 이전/다음 이동은 **A / D**. 키를 뗄 때 중복 이동하지 않는다. 바 좌석 이동 Q/E와는 별도다.
- 외곽선은 화면 기준 Hover Width **1.35px**, 선택 번짐 **3.6px**, Highlight Strength **1.15**, 전환 **0.1초**다.
- 색은 슬롯과 병따개의 Hover Color / Selected Color에서, 선 굵기·진하기는 `m_CraftPrepOutline.mat`에서 조절한다.

### 설명창이 재료에 고정되던 구조 변경

기존 `World Item Info/Frame/Name/Description`을 재사용한다. 마우스 화면 좌표를 준비 화면 카메라의 월드 좌표로 변환하고 매 프레임 따라간다. 오른쪽·아래 공간이 부족하면 반대편으로 옮기고 화면 내부로 보정한다.

| CraftPrepItemTooltip 설정 | 값/동작 |
| --- | --- |
| Cursor Offset | `(18, -18)` 픽셀 |
| Screen Padding | `12` 픽셀 |
| Content Padding | `(16, 12)` 픽셀 |
| Text Spacing | `6` 픽셀 |
| Max Width | `480` 픽셀. 기존 배경이 더 넓으면 그 최소 크기를 유지 |
| 긴 문장 | 글씨를 줄이지 않고 줄바꿈·배경 높이 증가 |
| 표시 갱신 | 다른 재료 표시 때 원래 크기부터 계산하여 확대가 누적되지 않음 |

트레이를 갱신해도 같은 재료 위에 마우스가 있으면 설명을 유지한다. 마우스 이탈·선반 이동·준비 화면 종료 시 숨긴다. 설명 배경은 원본 `05.UI/05.Profile/profile_window/IMG_card_R.png`다.

### 추가·제거한 구조와 API

- 추가: 일반 C# 보조 클래스 `CraftPrepItemOutline`, 셰이더 `CraftPrepSpriteOutline.shader`, 공유 머티리얼 `m_CraftPrepOutline.mat`. 선택 가능한 SpriteRenderer 23개에 연결했다.
- 실행 중 Tight 메시의 외곽선이 잘리지 않도록 FullRect 표시용 Sprite를 만든다. 저장되는 에셋이 아니며 비활성화 시 제거·원본 복원한다.
- 제거: 슬롯·핫스폿의 `infoAnchor`, `infoDirection`, 슬롯의 InfoAnchor/InfoDirection 접근자, `CraftPrepItemTooltip.Direction`.
- 변경: `Show(item, anchor, direction)` → `Show(item, camera, pointerPosition)`. `CraftPrepStageScreen.ShowItemInfo`도 포인터 좌표를 받는다.
- 기존 앵커 자식은 남아 있지만 설명 위치 계산에 쓰지 않는다. 새 Canvas나 설명창 프리팹은 만들지 않았다.

세부 변경: [CRAFT_PREPARATION_UI.md](CRAFT_PREPARATION_UI.md).

## 4. 제조 공용 HUD와 타이머

공용 HUD는 **Play 씬의 Gimmick HUD**에서 관리한다. 기믹 프리팹마다 공용 글자를 복사하는 구조가 아니다.

`CraftGimmickHud`가 진행량·대상 이름·시간·O.K·다음 버튼을 표시한다. 기믹의 `ICraftGimmickProgress`, `ICraftGimmickManualEnd`, `ICraftGimmickOwnHud`를 통해 값과 표시 소유권을 받는다. 기믹이 특정 영역을 직접 표시하면 `ECraftHudPart`에 따라 공용 표시를 숨긴다. “다음”은 수동 종료가 가능한 기믹에서만 표시한다.

```text
Gimmick HUD
  Panel
    Timer          ← 추가, 아이콘과 글자를 함께 제어
      Time         ← 기존 텍스트를 이동, 연결 유지
      Timer Icon   ← 스터 시계 아이콘
```

- 이전 `TIME 경과초 / 제한초` → **시계 아이콘 + mm:ss**.
- 값은 `CraftTimer`의 **한 잔 누적 제조시간**이다. 다음 기믹으로 바뀌어도 초기화하지 않는다.
- `CraftTimer.FormatElapsed`를 공용 HUD와 독립 스터가 함께 쓴다. 59초→1분은 `00:59 → 01:00`이다.
- 정답 재료가 없어 시간을 가리는 경우 `??:??`로 표시한다. 실제 시간 측정은 계속한다.
- 시간 소유권으로 숨길 때 `timeRoot`에 연결한 Timer 묶음을 숨겨 아이콘만 남지 않게 했다. `timeInset=(100,78)`과 균등 배율로 화면 크기에 맞춘다.
- 공용 실행기로 켠 스터는 로컬 경과 시간·아이콘을 숨긴다. 독립 Stir 씬에서는 로컬 시계가 보인다.
- 스터의 **2초 원형 링**은 한 바퀴 제한시간이다. 공용 누적 시계와 역할이 다르므로 유지한다.

시간 제한에 따른 제조 판정은 유지한다. 표시에서 제한시간 숫자를 뺀 것이다. 세부 변경은 [STIR_UI.md](STIR_UI.md)의 공용 타이머 항목을 참고한다.

## 5. 따르기: 잔 범위와 기울기

새 `cup3.png`와 잔 안쪽 기준점을 **NewPourGame.prefab**에 반영하고 Pour 씬의 중복 오버라이드를 정리했다. 이미지 교체 시 보이는 잔과 액체를 판정하는 범위를 함께 맞춰야 한다.

| 항목 | 현재 프리팹 값 | 의미 |
| --- | --- | --- |
| 잔 Sprite | `03.Crafting/04.Minigames/03.pour/cup3.png` | 원본 이미지 |
| Glass Interior | 잔 아래 자식, local `(0, 0.08, 0)` | 액체 범위 중심 |
| glassInteriorHalfExtents | `(0.65, 1.27)` | 로컬 좌표의 반너비·반높이 |
| pourStartAngle | `95°` | 유량이 생기기 시작하는 각도 |
| pourFullOpenAngle | `110°` | 최대 유량에 도달하는 각도 |
| BottleTiltController.maxTiltAngle | `140°` | 조작으로 기울일 수 있는 최대 각도 |
| liftHeight / pourPivotHeight | `2.6 / 0.05` | 들어 올리는 높이 / 회전 기준 높이 |
| pour_emit_rate_ml_per_sec | CSV `70` | 기존 최대 출수량 유지 |

범위와 유량의 기준은 다음과 같다.

```text
local = glassCenter.InverseTransformPoint(입자 월드 위치)
잔 내부 = abs(local.x) <= 반너비 AND abs(local.y) <= 반높이
유량 비율 = Clamp01((현재 각도 - 시작 각도) / (최대 유량 각도 - 시작 각도))
실제 양 증가율 = CSV 최대 출수량 × 유량 비율 × 기존 수량 누적 계수
```

액체 경계는 `SphContainerBoundary`, 화면 표현은 `SphLiquidRenderer`가 같은 잔 중심·크기를 사용한다. 파티클 반경과 월드 배율도 경계 계산에 반영한다.

**설정 우선순위:** 현재 기울기 제한은 BottleTiltController의 프리팹 값 140°를 사용한다. CSV에 `pour_max_tilt_angle_deg=150`이 있어도 현재 이 컨트롤러가 그 값을 직접 읽지 않는다. C# 필드 초기값·CSV·저장된 프리팹 값을 구별한다. 출수량은 PourManager가 로드된 CSV Config에서 읽는다. 일부 코드 주석의 `balance.json`은 과거 명칭이며 런타임 데이터는 CSV다.

관련 커밋: `09681e28`, `4399f204`. develop의 액체 셰이딩 개선도 함께 병합되어 있다. 리소스·범위 변경에서 출수량은 변경하지 않았다.

### 문서 작성 시점의 추가 작업: 표시 단위 ml 통일

작성 중인 작업 트리에서 `PourManager.ProgressText`를 **현재량/목표량 모두 ml로 표시**하도록 바꾸는 미커밋 변경을 확인했다. 목표가 oz 등 다른 단위여도 표시용 목표량만 ml로 환산하며, 결과 기록·목표 판정용 `PouredInTargetUnit`은 기존 목표 단위 변환을 유지한다. `ICraftGimmickProgress`의 설명·예시도 ml로 맞춘 상태다. **이 변경은 기준 병합 커밋 f360d043에 포함된 내용과 구분한다.**

## 6. 병따기: 오프너와 탄산

입력 가능한 상태에서 클릭·스페이스를 누르면 오프너가 병 입구를 축으로 들렸다가 돌아온다. 성공·실패 입력 모두 움직이지만 입력 없이 고리를 놓친 경우에는 움직이지 않는다.

```text
NewCapGame / Cap Root / Ring Center
  Bottle
  Cap Piece
  Opener Pivot          ← 이동·회전 기준 추가
    Opener              ← SpriteRenderer 추가
  Bottle Mouth          ← 탄산 발생 기준 추가
```

| 설정 | 현재 값/동작 |
| --- | --- |
| 병·뚜껑 Scale | `4.139`, 이전 2의 2.0695배 |
| Opener Pivot Scale | `2.0695` |
| Opener 자식 Scale | `(-1.61, 1.61, 1.61)`, 반전 유지 |
| 오프너 아트 | 원본 `01.Preparation/02.Tools/01.X1/bottle_opener.png`, 단일 오프너 |
| 입력 모션 | `0.32초`, 상승 `0.24834`, 기본 회전 `-24°` |
| 반전 방향 | 음수 X Scale과 flipX를 고려. 현재 반전에서는 `+24°`로 손잡이가 올라감 |
| 성공 연출 | `06.Vfx/04.Prefabs/FX_Carbonated_Gintonic 1.prefab` |
| 효과 크기 / 분출 | `0.724325`, 새 입자 생성 `0.3초` 후 정지 |

CapManager의 `successEffectPrefab` / `successEffectAnchor`를 연결한다. `successEffectOverride`가 있으면 런타임 사본 대신 씬에 배치한 효과를 사용한다. Cap 1은 이 방식이다. 실패·시간 초과에는 탄산을 재생하지 않는다. 비활성화 시 오프너 자세·파티클을 정리한다.

공용 설정으로 옮긴 뒤 Cap 1의 오버라이드 16개, Cap의 오버라이드 7개를 정리했다. 고리의 판정 폭·속도와 기존 뚜껑 물리 발사는 유지했다. 상세: [CAP_FEEDBACK.md](CAP_FEEDBACK.md).

## 7. 스터: UI·화질·10회 목표

### 목표와 타이머

- `Assets/StreamingAssets/csv/settings/balance_config.csv`의 `stir_target_stacks`: **6 → 10**.
- 코드 fallback, NewStirGame, 독립 Stir 씬, StirSceneSetup의 기본 개수도 10으로 맞췄다.
- 기존 Segment 0~5를 유지하고 **Segment 6~9**를 추가했다. 다른 목표 개수를 주면 런타임에 칸 수를 맞춘다.
- 설정 엑셀 `BalanceConfig!C92`는 이미 10이라 변경하지 않았다. CSV와 원본 값을 일치시켰다.
- 조작 순서는 **W → D → S → A → W**. 2초 링·성공/실패 판정은 유지한다.

### 아트와 배치

`StirLayoutView`가 실제 Canvas 크기를 기준으로 배치한다. 960×540 고정 좌표만 사용하던 구조에서 바뀌었다.

| 항목 | 현재 기준 |
| --- | --- |
| CanvasScaler | Expand |
| 바깥 여백 / 패널 간격 | `18 / 16` UI 단위 |
| 왼쪽 패널 | 304 기준, 화면이 넓어지면 오른쪽 패널·게이지가 추가 폭 사용 |
| 원형 입력판 | 균등 배율, 원형 비율 유지 |
| 배경 | 좌·우 9-slice 프레임 + Tiled 격자 |
| 확대창 | 캐릭터와 같은 프레임, 테두리 원본 1205:2029 비율 |
| 결과 게이지 | 가로 배치, 성공 파랑/실패 빨강. 리사이즈해도 결과 이미지 유지 |

추가/변경 구조:

- Game Canvas에 **StirLayoutView** 추가.
- 기존 Stir Background → **Left Panel Frame**으로 재사용, **Right Panel Frame / Right Panel Grid** 추가.
- Timer Ring Base, Top View Glass, Stir Spoon, Elapsed Icon/Time, Start Hint, Stir Closeup/Character/Frame 추가.
- `StirAnimationView.linkedImages`로 원본 캐릭터와 확대창을 동기화한다.
- 기본 도형 잔·얼음·스푼, 이전 Side Glass·Start Overlay·중복 배경 등은 삭제 대신 비활성화했다.
- `StirKeyBadge`가 대기 C/현재 A/다음 B 이미지를 표시한다. 키별 자르기 크기가 달라 프레임 크기도 조절한다.

### 화질과 원본 리소스

- 캐릭터 `stir_Sheet.png`: **5820×550**, Max Size **8192**, 무압축·Point. 2048로 축소되던 문제를 수정했다.
- UI 배경 3840px와 가로 게이지 2124px는 Max Size 4096, 나머지 UI는 크기에 맞는 Max Size를 사용한다. 스터 UI 압축을 해제했다.
- BG_stir에 `BG_stir_left_frame`, `BG_stir_right_frame`, `BG_stir_grid_tile`을 추가했다.
- 성공/실패 게이지에는 `GAU_stir_success_fill`, `GAU_stir_fail_fill`을 추가했다. 기존 전체 Sprite ID는 유지했다.
- PSD에서 탑뷰 잔·스푼을 추출하여 원본 아트에 **IMG_stir_glass_top.png / IMG_stir_spoon.png**를 추가했다. PSD 자체는 유지했다.
- 원본 UI: `05.UI/03.Crafting/02.Minigames/01.Stir/`. 캐릭터·신규 잔·스푼: `03.Crafting/04.Minigames/02.Stir/01.Sprites/`.

상세: [STIR_UI.md](STIR_UI.md). 최초 고정 배치 표보다 해당 문서의 후속 화면 크기·여백 수정과 현재 StirLayoutView 설정이 우선한다.

## 8. 서빙 애니메이션

원본 폴더는 `01.ArtResources/04.Cutscenes/04.ServingAnime`다. 임시 폴더 이름이었던 `04.serving ani`는 더 이상 사용하지 않는다.

- 시트 5738×2707을 **가로 6 × 세로 5**, 총 **30칸**으로 자른다. 한 칸은 **956×541**, 중앙 피벗이다.
- 순서는 왼쪽→오른쪽, 위→아래. `i` 프레임 영역은 `x=(i%6)*956`, `y=(4-floor(i/6))*541`이다.
- 배경은 **background_0 한 장 고정**. 다른 29장의 배경을 재생하지 않는다.
- 전용 음료 8종: bottle_beer, cosmopolitan, dry_martini, gin_fizz, gin_tonic, kahlua_milk, screwdriver, tequila_sunrise.
- 각 클립은 **21fps·30프레임·약 1.43초**. 전용 아트가 없는 음료는 기존 코스모폴리탄 대체 규칙을 사용한다.

### 이전 구조 → 현재 구조

| 이전 | 현재 |
| --- | --- |
| Legacy Animation + 실행 중 복사본·AnimationEvent | 기존 Animator + PlayableGraph / AnimationClipPlayable |
| ServeScreen.Ani가 Animation 참조 | Animator 참조 |
| 서빙 목록 5개, 일부 클립 공유 | 전용 클립 8개 연결 |
| 음료 UI 375×528 | 960×540 프레임, 중앙 배치 |
| Resources 경로 위주의 Timeline 이미지 조회 | 직접 Sprite/AnimationClip 참조 우선, 기존 경로는 fallback |

적용 위치는 **Play → 05.UI → serve Canvas → BG → drink**다. 끝나면 마지막 프레임을 유지하고 등급·제공·버리기 버튼을 표시한다. Reset·비활성화·파괴 때 그래프를 정리한다. 이전 에셋 파일은 남겨 두었다.

새 클립: `07.Animations/02.Crafting/02.Serving/01.Clips/Serve_*.anim`.

기존 Timeline **Serve_Cosmopolitan / Serve_Dry / Serve_Ginfizz / Serve_kalua**도 연결했다. Timeline의 2.5초 구간·신호 시점은 유지하고 그 길이에 30프레임을 맞춘다. 보관용 Serve_Unknown은 유지했다.

`CutSceneBGBehaviour.backgroundSprite`, `CutSceneImageBehaviour.sourceSprite`, `CutSceneSpriteAnimBehaviour.animationClip`이 직접 연결용 필드다. Timeline이 필요에 따라 임시 추가한 Animator만 종료 시 제거한다. 상세: [SERVING_ANIMATION.md](SERVING_ANIMATION.md).

## 9. Home: 테라스·취침·시계

### 취침 흐름

```text
소파 → 수면 선택
 → 0.35초 페이드 아웃 → 테라스 카메라 전환 → 0.35초 페이드 인
 → 해당 날짜의 대화 전체 진행
 → 0.5초 페이드 아웃 → 날짜 증가·침대 앞으로 이동·실내 카메라 복원
 → 0.5초 페이드 인 → 다음 날 시작
```

- 테라스 대상 story ID: `night0`, `night1`, `night2`, `ending`. workshop은 기존 표현을 유지한다.
- 수면 선택 후에는 **Esc·패드 취소와 옵션창 진입을 막는다**. 선택 전 소파 메뉴는 취소할 수 있다. 씬 종료·오브젝트 종료에 필요한 강제 취소와 오류 복구는 유지한다.
- 대화가 끝나자마자 실내가 잠깐 보이던 전환을 제거했다. 암전 중 날짜·위치를 바꾼다.
- 0→1, 1→2, 2→3일차는 침대 앞에서 시작한다. 최종 3일차 종료의 TempEnding 분기는 유지한다.

### 추가한 연결

- `02.Cameras/CM_Terrace`: CinemachineCamera, Priority 200, Orthographic Size 1.35. 별도 렌더링 Camera를 추가하지 않았다.
- `Terrace_BG_City`에 HomeTerracePresentation / WorldDialogueSpeakers 연결. 기존 배경·난간·캐릭터 배치는 유지한다.
- `Terrace_0/LunaSpeechAnchor`: local `(-0.15,-0.35,0)`, 말풍선 아래쪽·추가 Y -8.
- `Terrace_0/ChrisSpeechAnchor`: local `(0.14,0.16,0)`, 말풍선 위쪽·추가 Y +8.
- `04.Gameplay/HomeWakeUpSpawn`: world `(-0.82,-0.7,-1)`. 침대를 옮기면 이 기준점도 옮긴다. 일반 입장용 HomeSafeSpawn과 구별한다.
- WorldInteractionController의 `Home Terrace` 필드를 연결했다. `WorldDialogueSpeakers`에 Placement / Bubble Offset을 추가했다.
- `IWorldInteractionCancellationPolicy`와 HomeSofaAction.CanCancel로 취침 취소를 제어한다. `WorldInteractionActionContext.PrepareMorning / FinishNight`로 암전 중 아침 준비와 취침 종료를 연결한다.

Home 단독 테스트는 소파 수면 경로에서 해당 날짜 대화를 진행하여 확인한다. 실행 시작 날짜·플래그·콘텐츠 로딩 상태에 따라 대화가 달라진다. 강제로 대화만 띄우는 테스트와 실제 날짜 증가 검사는 구별한다. 상세: [HOME_TERRACE_UI.md](HOME_TERRACE_UI.md).

### HomeClock

`HomeClock.cs`는 TMP 텍스트에 **PC 현재 시각(DateTime.Now), 24시간 HH:mm**를 출력한다. 게임 날짜나 제조 경과 시간이 아니다. 매초 밀리초 0~399는 콜론 표시, 400~999는 투명하게 숨긴다. 검은 콜론을 그리는 방식이 아니며 콜론 폭을 유지하여 숫자가 흔들리지 않는다. timeScale=0에서도 현재 시각을 갱신한다.

## 10. 대화·선택지·튜토리얼·실외 표현

### 선택지 표시·입력

- Play의 UIDialogueChoiceView에는 **4개의 ChoicePanel 슬롯**이 연결되어 있다. 표시 순서는 전달받은 목록과 슬롯 리스트 순서다.
- 선택지 부모 `ChoicePanel/ButtonPanel`의 가로 레이아웃은 **Middle Center**, Spacing **12**, Child Control/Force Expand Width·Height는 꺼져 있다. 비활성 항목은 레이아웃에서 빠진다.
- 현재 항목 RectTransform 저장 크기는 폭 450, 높이 약 48.7793, Scale 1.2다. 가로를 두고 원본 아트 비율에 맞춘 높이다. 새 아트로 바꾸면 이 크기와 텍스트·진행바를 함께 확인한다.
- 일반 ShowChoice는 조건 미달 항목을 회색 비활성으로 남길 수 있다. 슬롯을 넘는 선택지는 마지막 칸을 다음 페이지로 사용한다. ShowOutsideChoice는 별도 표시 경로이므로 넘는 항목의 처리도 구별한다.
- `ChoiceHoldButton`으로 **1초 홀드 시 확정**, 해제 후 전체 감소 시간 **0.2초**. 감소 도중 다시 누르면 남은 진행률부터 이어 간다.
- 기존 onClick 연결은 유지한다. 짧은 클릭은 홀드를 우회하지 않는다. 대사를 넘기는 일반 클릭에 1초 홀드를 적용한 것은 아니다.
- `DialogueInputPrompt`는 타이핑 완료 후 수동 입력 대기 중에만 아이콘을 띄우고 움직인다. 종료·취소 시 숨기고 위치를 복원한다.

이전 Button을 ChoiceHoldButton으로 교체하고 Hold Progress/Fill을 추가했다. 대화창에는 Input Wait Indicator를 추가했다. 상세 구현·이전 스냅샷: [CHOICE_DIALOGUE_UI_CHANGES_20261016.md](ContentAuthoring/CHOICE_DIALOGUE_UI_CHANGES_20261016.md).

### 튜토리얼 점선 방향

`StoryTutorialGuideGraphic`의 베지어 경로는 출발점에서 목표점 순서로 샘플링한다. 점선 위상을 더하던 방향을 빼는 방향으로 바꿨다. 현재 조건은 `(i - phase + 7) % 7 < 4`이며 `phase=(int)(Time.unscaledTime*12)%7`이다. 경로·도착점 화살촉은 유지한다. 관련 커밋: `4820ce4f`.

### 실외 카메라·로고·빗물

- OutSide 달리기 속도 **1.3**, 카메라 뷰포트 높이 **1**로 조정했다. 관련 커밋: `13c94009`.
- develop의 #75 변경: `03.UI/03.Outside/Canvas_Logo.prefab`으로 로고 이미지·팀 이름을 묶었다. LogoFade가 두 요소를 함께 페이드한다. 현재 타이틀 연출은 **이동 OFF**다.
- 로고 이동을 쓸 때는 enableMovement, 시작·도착 앵커 좌표, moveDuration, Ease를 설정한다. 위치는 상대 이동량이 아닌 앵커 좌표다. 시작·종료·취소 때 숨겨 편집용 Alpha가 게임에 남지 않게 했다.
- develop의 빗물 표현: OutSide Main Camera의 **WindowRainLayer**와 Renderer2D의 **WindowRainFeature**를 사용한다. 기존 FX_Rain의 실제 방출 상태로 새 물방울 생성을 제어한다.
- 빗물은 등록된 게임 카메라에서만 합성하며 Overlay 대화·선택지 UI보다 앞 단계에 적용한다. 조절 위치는 WindowRainLayer의 strength, shadingPixelSize, refraction, 바람·관성·trail 설정이다.
- 연결 복구 메뉴는 `Tools > Human Bartender > Window Rain > Connect Outside Scene`. 이 메뉴는 씬·Renderer 설정을 바꾸므로 현재 연결을 다시 만들 필요가 있을 때 사용한다.

## 11. 팝업·CSV·폰트

### 연결과 종류

ProjectLifeScope의 PopupCanvas / PopupService.Bindings에 7종을 연결했다. 일반 알림은 UIPopupView, 데이터가 필요한 팝업은 UIIngredientPopupView / UIAffinityPopupView / UIObjectivePopupView가 처리한다. 현재 Auto Hide Seconds는 **1초**이며 0으로 바꾸면 Hide 호출을 기다린다.

| 표시 | CSV arg | 전달할 text.ko |
| --- | --- | --- |
| 재료 획득 | `ingredient_acquired` | 재료 ID, 예: gin |
| 신규 인물 | `character_registered` | 생략 가능 |
| 호감도 상승 | `affinity_increased` | 인물 이름 |
| 호감도 하락 | `affinity_decreased` | 인물 이름 |
| 새로운 목표 | `new_objective` | 목표 종류, 예: 메인 목표 |
| 목표 완료 | `objective_completed` | 목표 종류 |
| 이벤트 발생 | `event_occurred` | 생략 가능 |

목표 2종의 큰 글씨는 전달받은 목표 종류를 그대로 표시한다. 재료는 shelf_items의 ID와 CocktailRecipeVisualCatalog의 이미지 ID가 일치해야 한다.

```csv
type,arg,text.ko,text.en,sync
popup,new_objective,메인 목표,Main Objective,nowait
popup,ingredient_acquired,gin,gin,nowait
```

실제 파일은 `Assets/StreamingAssets/csv/story/steps.csv`, 작성 원본은 콘텐츠 Excel의 Steps다. 위 예시는 열 일부만 발췌한 것이므로 실제 행의 row_id·parent_id·dataset_id·순서는 기존 같은 유형의 행을 기준으로 맞춘다. `sync`는 **nowait 또는 빈 값**, wait/auto는 오류다. 팝업은 닫힘을 기다리지 않고 다음 스텝으로 간다. 상태 변경은 effects 등으로 따로 수행한다.

**CSV 신호 없이 인물 등록이 보일 수 있는 이유:** SceneAudioController가 실제 PlayerDataSO의 `*_met` 플래그 변화를 구독해 팝업을 요청한다. 호감도·재료 해금도 실제 상태 변화에 따라 알린다. 초기 로드·상태 복구는 변화 알림에서 제외하고 Chris 신규 인물 알림은 제외한다. 따라서 CSV popup 행과 자동 상태 변화 알림을 함께 확인한다.

### 폰트

작업한 TMP SDF 6종은 굵기가 앞에 오는 영어 이름으로 정리했다.

| 파일 | 용도 |
| --- | --- |
| Regular_PFStardust_3_0_SDF.asset | 팝업 작은 글씨 |
| Bold_PFStardust_3_0_SDF.asset | 팝업 큰 글씨 |
| Medium_Pretendard_SDF.asset / Bold_Pretendard_SDF.asset | Pretendard 중간·굵은 글꼴 |
| Medium_Paperlogy_SDF.asset / Bold_Paperlogy_SDF.asset | Paperlogy 중간·굵은 글꼴 |

UI에는 원본 OTF/TTF 대신 생성된 TMP SDF asset을 연결한다. 기존 Galmuri9 / NeoDunggeunmo 폰트도 유지하며 제조 시계는 NeoDunggeunmo를 사용한다. 테스트용 팝업 7개 버튼과 TemporaryPopupTestButtons 스크립트는 최종 정리에서 제거했다.

전체 CSV 규칙: [POPUP_CSV.md](ContentAuthoring/POPUP_CSV.md).

## 12. 사운드 연결과 재생 조건

SoundManager의 등록 수는 **24개 키 = BGM 5개 + SE 19개**다. 모든 키에 AudioClip이 연결되어 있다. 키 개수와 음원 파일 개수는 다르다. 예를 들어 SFX_typing은 기본음과 대체 샘플 5개를 교대로 사용한다.

### 등록한 종류와 실제 사용 구분

| 키 | 연결/재생 위치 |
| --- | --- |
| BGM_bar_01 / BGM_bar_02 / BGM_bar_03 | Play에서 01→02→03 순환. Home는 02 반복 |
| BGM_outside | OutSide 전용 음악, 장소 출력 배율 0.6 |
| BGM_title | 타이틀용 Clip 등록 |
| SFX_shaking | 기존 ShakingManagerNew의 셰이킹 재생 |
| SFX_typing | 공용 타이핑 서비스, 샘플 6개 교대 |
| SFX_ui_hover / SFX_ui_click | UI 호버·확정, 선택지·재료 선택 피드백 |
| SFX_money_change | 실제 재화 변화 |
| SFX_goal_completed | 목표 완료 팝업 진입 |
| SFX_notification | 나머지 6종 팝업 진입 |
| SFX_bottle_open / SFX_fizz | CapManager 개봉 성공·탄산 잔향 |
| SFX_pour | 실제 따르기 유량에 연결한 루프 |
| SFX_stir | 실제 젓기 애니메이션 동안 재생 |
| SFX_shaker_hit | ShakingRhythmManager 성공 타격 |
| SFX_street_loop / SFX_rain_loop | 도시 환경음·조건부 비 레이어 |
| SFX_door_open_close | 수락된 Home↔OutSide 이동 시작 |
| SFX_change_clothes | Clip 등록 유지. 새 재생 시점은 추가하지 않음 |
| SFX_incoming_call | Clip 등록 유지. 요청받은 꿈 전환 Timeline은 대상이 없어 추가 연결 생략 |
| SFX_stir_alternate / SFX_shaker_hit_alternate | 대체음으로 별도 등록. 기본 제조음의 자동 교대 목록에서는 제외 |

### 재생 규칙과 설정 위치

- ProjectLifeScope의 SoundManager에서 개별 Clip·gain을 관리한다. 사용자 BGM/SE 음량은 별도 믹서에서 적용한다. 저장값 없는 기본값은 **BGM 0.30 / SE 0.80**, 기존 사용자 저장값은 유지한다.
- 같은 장소 음악 그룹을 다시 호출해도 처음부터 재생하지 않는다. 장소 변경은 기존 곡 fade-out→교체→fade-in이다.
- 거리 비 환경음은 **0·1일차 퇴근, 2일차 출근**에 추가한다. 대사 중 환경음 0.65배, 옵션/일시정지 중 0.45배. 실내 진입·컷씬·포커스 이탈 때 정리한다.
- 팝업 효과음은 요청 시점이 아니라 패널이 화면에 진입한 Shown 시점에 한 번 재생한다. 짧은 중복 호출을 억제한다.
- 타이핑은 직접 대사/주변 대사를 구분하고 스킵 중에는 무음이다. 주변 대화는 거리 조건·직접 대사·일시정지 상태를 고려한다.
- 바·거리 BGM의 원본 M4A는 보관하고 실제 연결은 Streaming 설정의 OGG를 사용한다.

프로젝트에 대상이 없어 적용하지 않은 요청은 **꿈에서 깨는 전환 연락음, 단골 정보 슬롯 호버음, 0일차 집 문 앞 혼잣말 뒤 목표 연출**이다. 목표 팝업 자체의 소리는 공용 경로에 적용했다. 대상이 생기면 이후 작업으로 연결한다. 세부 gain·날씨·수명·검증: [SOUND_IMPLEMENTATION.md](ContentAuthoring/SOUND_IMPLEMENTATION.md).

## 13. 임시 리소스 최종 정리

과거 작업 흐름은 “최근 추가된 미사용 아트 복사 → 제조 관련만 남김 → 임시본으로 연결·편집 → 원본으로 이관”이었다. 제조 범위에는 재료 선택·기믹·완성·서빙을 포함했다. 오래된 항목·중복·제조 외 항목은 앞 단계에서 제외했다.

| 최종 처리 | 결과 |
| --- | ---: |
| 정리한 임시 PNG | 81개 |
| 재연결한 참조 | 338개 / 17파일 |
| 설정을 이관한 원본 메타 | 23개 |
| 원본 PNG 바이트 수정 | 0개 |
| Unity에서 확인한 Sprite/Texture ID | 462개 |
| 현재·과거 임시 GUID 검색 | 417개, 남은 에셋 참조 0개 |

메타 23개는 **선반 1 + 화살표 2 + 서빙 9 + 스터 11**이다. 자르기·피벗·Single/Multiple·테두리·필터·압축·해상도 변경을 옮겼다. 원본 GUID와 기존 Multiple Sprite ID는 유지했다. 임시본에만 있던 불필요한 Android 항목은 추가하지 않았다.

- `Assets/99.TempResources`와 폴더 메타·매핑 문서를 프로젝트에서 제거했다.
- 최종 경로/GUID/스프라이트 ID/삭제 목록은 루트 **RESOURCE_FINALIZATION.json**에 남겼다.
- _Recovery의 씬은 프로젝트 밖으로 옮겨 보존했다. 검증용 Editor 코드도 제거했다.
- **TempEnding, TempSofa는 실제 게임 흐름에서 사용하므로 유지했다.** 이름에 Temp가 있다고 일괄 삭제하면 안 된다.
- 앞으로 수정할 아트는 **원본 폴더**다. 사라진 임시 폴더를 다시 연결하지 않는다.

메타가 변경되면 Sprite 모드·잘라낸 영역·PPU·Max Size·압축·플랫폼 override 중 무엇이 바뀌었는지 확인한다. GUID를 새로 만들면 기존 연결이 깨질 수 있다. Android 플랫폼 항목이 있는 것만으로 Android 빌드가 진행되었다는 뜻은 아니다.

세부 결과: [TEMP_RESOURCE_FINALIZATION.md](TEMP_RESOURCE_FINALIZATION.md), [RESOURCE_FINALIZATION.json](RESOURCE_FINALIZATION.json).

## 14. Git 문제와 develop 병합

### Fork 파이프 오류 109

오류 메시지: `git add --force --verbose --pathspec-from-file=- --pathspec-file-nul --` 실행 중 “파이프가 끝났습니다”. 실제 Git 오류는 **.git/index.lock이 이미 존재함**이었다. Git이 먼저 종료되면서 Fork의 파일 목록 전송도 실패한 것으로 판단했다.

- 잠금은 2026-10-20 06:34 생성·06:35 최종 갱신된 0바이트 파일이었다.
- 실행 중인 git/git-lfs 프로세스가 없는 것을 확인하고 잠금을 외부로 백업해 해제했다. index 본문은 유지했다.
- Fork 번들 Git 2.50.1로 802개 경로의 add **사전검사(dry-run)**에 성공했다. 당시 스테이징·커밋은 변경하지 않았다.
- 재발하면 실행 중인 Git 작업·잠금 시각부터 확인한다. 실제 작업 중인 잠금을 지우거나 **.git/index 자체를 삭제하는 방식은 사용하지 않는다**.

Git 저장소 루트는 Unity 프로젝트의 한 단계 위 **Human-Bartender**다. 잠금 위치도 `HumanBartender/.git`이 아닌 `Human-Bartender/.git/index.lock`이다.

### develop 병합 최종값

`57-Gimmick-Upgrade`의 fa22f061과 최신 develop의 195bf653을 병합했다. 커밋 **f360d043**, 로컬 develop·origin/develop·원격 develop에 반영했다. 충돌은 아래 네 파일의 해당 값만 develop 기준으로 정리했다.

| 충돌 위치 | 최종값 |
| --- | --- |
| Play.unity | `skipTycoonForTest: 0`, `testDay: 1` |
| Hanger.png.meta | 기존 Android maxTextureSize 128 유지 |
| Room_Entrance.png.meta | 기존 Android maxTextureSize 128 유지 |
| Terrace_Entrance.png.meta | 기존 Android maxTextureSize 128 유지 |

위 홈 이미지들은 원본 `02.Environments/03.Home/01.Sprites/` 아래에 있다. 임시 아트 이관에서 “Android 항목을 추가하지 않음”과 이번 병합에서 “develop의 기존 Android 값을 유지함”은 서로 다른 작업이다.

과거 Play 백업 복원 커밋(fc6a2905)에 있던 테스트 설정은 최종 병합에서 정상 시작 설정으로 바뀌었다. **타이쿤을 건너뛰거나 99일차로 시작하는 상태가 현재 기본값이 아니다.**

## 15. 테스트 방법·검증 범위

### 다시 확인할 순서

| 대상 | 확인 방법 |
| --- | --- |
| 재료 선택 | Play 제조 준비에서 A/D, 호버·선택 색, 긴 설명 배경, 화면 모서리 추적 |
| 따르기 | Pour 독립 씬과 Play에서 잔 이미지·입자 범위, 95° 시작·110° 최대 유량·140° 기울기 |
| 병따기 | Cap / Cap 1에서 실패 입력 모션, 성공 탄산, 방향·크기·기본 자세 복귀 |
| 스터 | Stir 독립 씬에서 10칸·첫 W 시작·판정. Play에서는 로컬 시계가 숨고 공용 시계만 보이는지 |
| 공용 HUD | 기믹 전환 뒤 누적 시간 유지, 00:59→01:00, 수동 종료 가능 여부 |
| 서빙 | 8종 전용 클립·대체 연출, 고정 배경, 마지막 프레임, 제공·버리기·Reset |
| Home | 수면 후 Esc 차단, 화자별 말풍선, 다음 날 침대 시작, PC 시계 점멸 |
| 선택지·팝업 | 활성 개수별 중앙 정렬, 홀드 확정/감소/재개, 목표 종류 표시, 자동 알림 중복 여부 |
| 사운드 | 장소 음악 전환, 날씨별 환경음, 타이핑·팝업·제조 루프 종료 정리 |

CSV를 수정했다면 다음 메뉴를 사용한다.

- `Tools > Data > Validate CSV Content`
- `Tools > Craft > Validate Craft Data`
- `Tools > Story > Validate C Content`

게임은 **Assets/StreamingAssets/csv를 직접 읽는다**. Excel 변경만으로 반영되지 않는다. 내보내기는 프로젝트 폴더에서 다음 명령으로 수행한다.

```sh
python ContentAuthoring/export_content.py --out Assets/StreamingAssets/csv
```

CSV를 직접 수정한 뒤 오래된 Excel로 다시 내보내면 덮어쓸 수 있으므로 두 원본을 맞춘다. 설정·스텝 편집 상세는 [ContentAuthoring/README.md](ContentAuthoring/README.md)를 참고한다.

### 완료된 검증과 한계

- 각 기능 작업 때 Unity에서 스터·병따기·서빙의 격리 Play Mode 검증을 수행했다. 구체적인 성공/실패·취소·복구 항목은 해당 기능 문서에 있다.
- 스터는 16:9 / 16:10 / 21:9 배치, 리사이즈 후 판정 유지, 실제 임포트 너비 5820/3840을 확인했다.
- 타이머 표시·소유권·중복 제거, 9회 미완료/10회 완료와 목표 판정, CSV 구조를 확인했다.
- 임시 정리에서는 462개 ID 실제 로드, 서빙 8×30프레임과 6×5 영역·피벗, 임시 참조 0, 플랫폼 목록 유지, 최종 Unity 컴파일을 확인했다.
- develop 병합에서는 **런타임 434개 / 에디터 118개 C# 소스 컴파일**을 확인했다. 에러 0, 런타임 기존 경고 22개, 에디터 경고 0이었다. Home·OutSide·Play의 YAML 중복 ID와 로컬 참조, 병합 전 대비 새 미해결 GUID 여부를 검사했다.
- 정적 검사에서 OutSide에 병합 전부터 있던 미해결 GUID 4개가 남아 있었다. 이번 병합으로 늘어난 것은 없으며 실제 플레이 영향은 별도 확인 대상이다: `087ceb7100e03b94dbd838994bba8b39`, `21c3838866b64504baf7a308c387d205`, `66d309e05bc54a840a4a13d63c4f8c9e`, `b559aee4bdb35024194b3aa7c31249ad`.
- **전체 날짜·스토리·제조·서빙을 처음부터 끝까지 다시 플레이한 결과는 아니다.** 병합 때 모든 테스트 스위트를 새로 실행했다는 의미도 아니다.

## 16. 세부 문서·커밋·백업

### 세부 문서

| 문서 | 찾아볼 내용 |
| --- | --- |
| [CRAFT_PREPARATION_UI.md](CRAFT_PREPARATION_UI.md) | 아웃라인·설명창 API·삭제 필드·입력 |
| [HOME_TERRACE_UI.md](HOME_TERRACE_UI.md) | 테라스·말풍선·취침 콜백과 위치 |
| [CAP_FEEDBACK.md](CAP_FEEDBACK.md) | 병따기 크기·모션·VFX·씬 오버라이드 |
| [STIR_UI.md](STIR_UI.md) | 스터 배치·슬라이스·화질·10회·공용 시계 |
| [SERVING_ANIMATION.md](SERVING_ANIMATION.md) | 클립별 매핑·Timeline·재생 구조 |
| [TEMP_RESOURCE_FINALIZATION.md](TEMP_RESOURCE_FINALIZATION.md) | 임시 파일 정리 결과 |
| [RESOURCE_FINALIZATION.json](RESOURCE_FINALIZATION.json) | 81개 원본/임시 매핑·GUID·fileID·삭제 목록 |
| [POPUP_CSV.md](ContentAuthoring/POPUP_CSV.md) | 팝업 7종 CSV 필드·예시·검증 |
| [SOUND_IMPLEMENTATION.md](ContentAuthoring/SOUND_IMPLEMENTATION.md) | 사운드 적용 위치·정확한 gain·미적용 대상 |
| [CHOICE_DIALOGUE_UI_CHANGES_20261016.md](ContentAuthoring/CHOICE_DIALOGUE_UI_CHANGES_20261016.md) | 홀드·대기 아이콘 구현과 이전 프리팹 기록 |
| [ContentAuthoring/README.md](ContentAuthoring/README.md) | Excel/CSV 편집·내보내기 |

### 주요 커밋

| 커밋 | 내용 |
| --- | --- |
| 64091d24 / 9f65eaa5 | 팝업 연결·폰트·CSV 팝업 설명 |
| 8f7ed3a0 | 사운드 기획 적용 |
| 09681e28 / 4399f204 | 따르기 리소스·범위·공용 프리팹 반영 |
| 4820ce4f | 튜토리얼 점선 방향 |
| fc6a2905 | Play 백업 UI 배치 복원. 테스트 시작값은 이후 병합값이 우선 |
| 13c94009 | 실외 이동·카메라 |
| d9d3cbd0 | 재료 선택·설명창·A/D |
| 483690fe | 테라스·취침·다음 날 침대 시작 |
| 9dec400e | HomeClock |
| 23c7db12 | develop의 로고·엘리베이터 연출 |
| fa22f061 | 병따기·스터·공용 시계·서빙·원본 이관을 포함한 최종 기믹 작업 |
| f360d043 | develop 최종 병합·충돌 해결 |

커밋 원문은 저장소에서 `git show <커밋>`으로 확인한다. 단순 재적용·되돌리기 이력보다 현재 코드와 최종 병합 상태를 우선한다.

### 로컬 복구 자료

아래 자료는 Git에 포함된 작업 원본이 아니라 **이 PC의 임시 디렉터리에 보존한 복구용 사본**이다. 나중에 OS 임시 파일 정리로 사라질 수 있으므로 영구 보관이 필요하면 별도 보관 위치로 복사한다. 현재 에셋 정본은 Git에 있는 원본 폴더다.

공통 경로: `C:/Users/hyeon2-400/AppData/Local/Temp/` (`HYEON2~1`은 같은 사용자 경로의 짧은 표기).

| 하위 폴더 | 자료 |
| --- | --- |
| humanbartender-temp-finalize-w7kpoqkk | 임시 아트 전체·수정 전 파일·복구 씬·이관 검증 |
| humanbartender-common-timer-vtdkb14i | 공용 시계·10회 목표 이전 파일과 검증 화면 |
| humanbartender-stir-quality-mpjiimu2 | 스터 화질·반응형 배치 이전 파일과 화면 |
| humanbartender-stir-ui-383vo8qx | 스터 최초 교체·PSD 추출·검증 |
| humanbartender-serving-refresh-dfotc4lh | 서빙 클립 생성·Play/Timeline 검증 |
| humanbartender-cap-feedback-h1dxk5dl | 병따기 모션·탄산 최초 적용 |
| humanbartender-cap-scale-direction-3lndrhth | 병따기 크기·반전 방향 적용 |
| humanbartender-git-lock-20261020-073929 | 잠금 해제 전 index와 잠금 사본·Fork 사전검사 |
| humanbartender-merge-develop-3a7m_odw | 충돌 원문·해결 기록·컴파일 로그·씬 검사 결과 |

문서를 갱신할 때는 기준 커밋과 **현재 값 → 수정 위치 → 이전 구조 → 검증 결과**를 함께 고친다. 개별 작업 문서에 과거 값이 남으면 이 문서에서 현재값을 명확히 구분한다.
