# 바 좌석 표시(Seat Indicators) 소유권 정리

## 증상

- Play 씬의 좌석 표시(좌석 점 3개와 선택 링)가 `StoryTutorialOverlay(Clone)` 아래에 있었다.
- 튜토리얼 오버레이가 아직 없는 때(예: 99일차 영업)에는 좌석 표시가 나오지 않았다.

## 원인

- `BarSeatStatusView`는 일반 영업과 스토리가 함께 쓰는 상시 표시다. 그런데 `StoryTutorialView.CreateGuidance()`가 오버레이 자식으로 코드에서 만들었다(`fe4ef6d4` [#41]부터).
- 오버레이는 `StoryTutorialController.EnsureView()`가 만들고, 처음 불리는 곳은 `StoryFlow.RunPhaseAsync`다. 그래서 날마다 생기는 시점이 달랐다.
  - 1~3일차: 오프닝이 영업보다 먼저 돌아서 좌석 표시가 미리 생겼다.
  - 그 밖의 날: 영업(1부)이 끝나고 2부 대본이 시작될 때 생겼다.
  - 바 대본이 없는 날: 끝까지 생기지 않았다.

## 바꾼 것

- 좌석 표시를 Play 씬 `05.UI/Canvas_BarSeatHud/BarSeatHudRoot`에 저장했다. 이후 수정은 Inspector에서 한다(2026-11-07 정리 참고).
- `BarSeatStatusView`는 런타임 생성(`Create`, `Configure`)을 지우고 직렬화 참조만 쓴다.
- 튜토리얼 오버레이는 `UseSeatHud`로 씬의 좌석 표시를 빌려, 좌석 수업의 강조 대상·키 안내와 전면 안내 중 숨김에만 쓴다.

## 기존 규칙과 맞춘 점

| 규칙 | 이전 | 지금 |
|---|---|---|
| UI 배치·참조는 씬/프리팹에 저장(`UI_Visual_Update_2026-10-20.md`) | 플레이 중 코드 생성 | 씬 저장 |
| 바 UI 캔버스 960×540, match 0 | 오버레이 1280×720, match 0.5 | 960×540, match 0. 정수 값(테 78×28, 왼쪽 아래 기준 중심 50/19, 점 간격 24, 점 10, 링 22, 키 36×32 ±22, 키 글자 13) |
| 정렬: 바 UI 8~11, 기믹 HUD 100, 서빙 103, 일시정지 메뉴 105 | 오버레이 4000 안(일시정지 메뉴 위) | 12(대화·서비스·레시피 위, 기믹·서빙·메뉴·튜토리얼 아래) |
| 영업 중 선택 좌석은 `PlayCamera.SelectedSeat`(#95 되묻기, #97 서빙 자리) | 카메라 X에서 가장 가까운 좌석 | 영업 중 `PlayCamera.SelectedSeat`, 대화 중은 기존 계산 유지 |
| 영업 중 손님 상호작용 조건은 `ServicePanelController.CanUseGuestActions` | 자체 조건 | 같은 조건. 카메라 이동 중에는 숨기지 않는다 |
| 기믹·준비 화면 동안 바 UI 끄기(`GimmickRunner`·`CraftPrepStageHost`의 `barCanvases`) | 목록에 없음 | 두 목록에 등록 |

## 2026-11-07 정리 (1단계)

- Canvas Sort Order가 씬에 10(서비스 패널과 같음)으로 저장되어 있던 것을 문서대로 12로 맞췄다.
- 오브젝트 이름을 다른 바 UI처럼 띄어쓰기 없는 이름으로 바꿨다.

  | 이전 | 지금 |
  |---|---|
  | `Bar Seat HUD` | `BarSeatHudRoot` |
  | `Seat Indicators` / `HUD Border` | `SeatIndicators` / `SeatFrameBorder` |
  | `Selected Seat Left·Middle·Right` | `SeatLeftRing`·`SeatMiddleRing`·`SeatRightRing` |
  | `Seat Left·Middle·Right` | `SeatLeft`·`SeatMiddle`·`SeatRight` |
  | `Left Seat Key` / `Right Seat Key` | `PrevSeatKey` / `NextSeatKey` |
  | `Key` (둘 다) | `PrevSeatKeyText` / `NextSeatKeyText` |

- ×0.75 환산으로 생긴 소수 크기·위치·반지름(28.5, 10.5, 22.5, 49.5/18.75, 21.75, 13.5, 11.25, 5.25, 테두리 0.75·1.125, 글자 12.75)을 정수로 바꿨다.
- `Tools/Story/Set Up Bar Seat HUD`(`BarSeatHudSetup.cs`)를 지웠다. 실행할 때마다 HUD를 지우고 다시 만들어 Inspector 수정이 사라졌고, Undo 없이 씬을 저장했다. 배치는 이미 씬에 저장되어 있다.
## 2026-11-07 정리 (2단계)

- 좌석 표시 8개 오브젝트의 `StoryTutorialShape`(코드로 그리는 도형)를 `Image` + 스프라이트로 바꿨다. 같은 컴포넌트 자리(fileID)를 그대로 써서 `BarSeatStatusView`의 `dots`·`rings` 참조가 유지된다. 색(`m_Color`)과 `Raycast Target` 꺼짐도 그대로다.
- `BarSeatStatusView.dots`·`rings` 타입을 `StoryTutorialShape[]` → `Image[]`로 바꿨다. 상태 색은 지금처럼 코드가 `Image.color`에 넣는다.
- 스프라이트는 `03.Content/01.ArtResources/05.UI/03.Bar/SeatHud/`의 **임시 흰색 도형**이다(4배 해상도, PPU 400, Bilinear, 압축 없음). 아트가 나오면 같은 파일을 덮어쓰거나 Image의 Source Image만 바꾸면 된다.

  | 파일 | 크기(px) | 쓰는 곳 | Image Type |
  |---|---|---|---|
  | `SeatHud_FrameFill.png` | 128×112, 9-slice 56 | `SeatIndicators` 배경 | Sliced |
  | `SeatHud_FrameBorder.png` | 128×112, 9-slice 56, 선 4px | `SeatFrameBorder` | Sliced |
  | `SeatHud_Dot.png` | 64×64 원 | `SeatLeft·Middle·Right` | Simple |
  | `SeatHud_Ring.png` | 88×88 고리, 선 4px | `Seat…Ring` | Simple |

- `StoryTutorialShape`는 튜토리얼 오버레이(범례·강조 상자)가 아직 쓰므로 남겨 두었다.
- `BarSeatHudTests.SeatHudIsDrawnWithSpritesNotCode`: 좌석 표시 아래에 `StoryTutorialShape`가 없고, 모든 Image에 스프라이트가 있고 클릭을 막지 않으며, 프레임이 9-slice인지 검사한다.
## 2026-11-07 정리 (3단계)

177줄짜리 `BarSeatStatusView` 하나가 하던 일을 셋으로 나눴다.

| 파일 | 맡는 일 | 아는 것 |
|---|---|---|
| `03.Tycoon/BarSeatSignalRules.cs` | 좌석 상태 판정(대기 10초 이하 곧 떠남, 50% 이하 기다림, 손님이 바뀐 뒤 2.4초 도착). `EBarSeatSignal`도 여기로 옮김 | 숫자만 (순수 함수) |
| `03.Tycoon/UI/BarSeatHudPresenter.cs` | 지금 보일지, 선택 좌석, 좌석별 상태를 매 프레임 계산해 화면에 넘김 | 씬 시스템 8개 + `GuestSlot` 3개 |
| `03.Tycoon/UI/BarSeatStatusView.cs` (`04.Story`에서 이동) | 점 색·선택 링·깜빡임·키 안내 그리기. 튜토리얼용 `SetExploration`·`Suppressed`·`Indicators`·키 RectTransform은 그대로 | 자기 자식 UI만 |

- 씬 참조 8개(`phases`…`servicePanel`)를 View에서 Presenter로 옮겼다(같은 `BarSeatHudRoot` 오브젝트).
- `Awake`에서 씬 전체의 `GuestSlot`을 찾던 것을 Presenter의 `guestSlots` 배열(왼쪽·가운데·오른쪽) 연결로 바꿨다.
- 키 글자는 매 프레임 `GetComponentInChildren<TMP_Text>()`로 찾던 것을 `leftKeyLabel`·`rightKeyLabel` 직접 연결로 바꿨다. 흐리게 하는 처리는 `SetExploration`이 불릴 때만 한다.
- 화면에는 바뀐 값만 넘긴다(`SetActive`·`color`가 같으면 건드리지 않음).
- **매 프레임 계산은 남겼다.** 대기 시간이 계속 줄고 도착·퇴장은 깜빡여야 해서 원래 매 프레임 값이 필요하다. 보이기 여부까지 이벤트로 바꾸려면 `PlayPhaseController`(국면 변경)와 `CraftPrepStageHost`(열림)에 이벤트를 새로 만들어야 해서, 공용 클래스를 건드리는 별도 작업으로 남긴다.
- 테스트: `BarSeatHudTests`(Presenter 연결, 좌석 순서, 키 글자 연결 추가), `BarSeatSignalRulesTests`(기준 값 경계) 새로 추가.

## 2026-11-07 좌석 칸을 Horizontal Layout Group으로

좌석 점·링을 x = -24 / 0 / 24에 손으로 두던 것을 Layout Group이 줄 세우게 바꿨다.

```
SeatIndicators          왼쪽 아래 · 78×28 · Image(Sliced 프레임)   ← Layout Group 없음
├ SeatFrameBorder       가운데 · 78×28 · 테두리
└ Seats                 프레임을 꽉 채움 · HorizontalLayoutGroup
  │                     (Spacing 2, Middle Center, Control Child Size·Force Expand 끔)
  ├ SeatLeft   22×22    좌석 칸(빈 RectTransform)
  │ ├ SeatLeftRing      선택 링 (먼저 = 뒤)
  │ └ SeatLeftDot       점 (나중 = 앞)
  ├ SeatMiddle …
  └ SeatRight  …
```

- Layout Group은 **바로 아래 자식 하나하나를 한 칸**으로 줄 세운다. 그래서 링과 점을 좌석 칸 하나로 묶었다. 칸 22 + 간격 2 = 중심 간격 24라 예전 자리와 같다.
- 링·점은 기존 오브젝트를 옮겼다(점 이름만 `SeatLeft` → `SeatLeftDot`). `BarSeatStatusView`의 `dots`·`rings` 연결은 그대로다.
- `SeatIndicators`에 직접 붙였던 HorizontalLayoutGroup은 지웠다. 프레임 바로 아래에는 테두리와 `Seats` 줄이 있어서, 그 둘이 두 칸으로 나란히 밀렸다. 테두리 앵커도 가운데로 되돌렸다.
- 칸 위치는 Layout Group이 정하므로 씬 파일에는 0으로 저장되고, 열 때 다시 계산된다. 칸의 Pos를 손으로 고치지 말고 `Seats`의 Spacing·Padding을 고친다.
- 테스트 `SeatsAreLaidOutByOneHorizontalLayoutGroup`: 줄 설정, 칸 3개 순서, 칸 안의 링·점, 중심 간격 24, 프레임에 Layout Group이 없는지.

## 남은 예외

- 병따기 화면(`CapOpenView`)의 글자·버튼·수업 안내는 아직 런타임 생성·1280×720이다. 같은 규칙에 맞추는 일은 별도 작업으로 남긴다(`Web_Parity_Day1_Beer_2026-11-03.md`).
