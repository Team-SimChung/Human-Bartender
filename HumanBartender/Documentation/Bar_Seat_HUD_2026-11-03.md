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

- 좌석 표시를 Play 씬 `05.UI/Canvas_BarSeatHud/Bar Seat HUD`에 저장했다. 배치와 참조는 `Tools/Story/Set Up Bar Seat HUD`가 저장한다.
- `BarSeatStatusView`는 런타임 생성(`Create`, `Configure`)을 지우고 직렬화 참조만 쓴다.
- 튜토리얼 오버레이는 `UseSeatHud`로 씬의 좌석 표시를 빌려, 좌석 수업의 강조 대상·키 안내와 전면 안내 중 숨김에만 쓴다.

## 기존 규칙과 맞춘 점

| 규칙 | 이전 | 지금 |
|---|---|---|
| UI 배치·참조는 씬/프리팹에 저장(`UI_Visual_Update_2026-10-20.md`) | 플레이 중 코드 생성 | 씬 저장 |
| 바 UI 캔버스 960×540, match 0 | 오버레이 1280×720, match 0.5 | 960×540, match 0. 예전 크기를 ×0.75로 환산(테 78×28.5, 왼쪽 아래 49.5/18.75, 점 간격 24, 점 10.5, 링 22.5, 키 36×33, 키 글자 12.75) |
| 정렬: 바 UI 8~11, 기믹 HUD 100, 서빙 103, 일시정지 메뉴 105 | 오버레이 4000 안(일시정지 메뉴 위) | 12(대화·서비스·레시피 위, 기믹·서빙·메뉴·튜토리얼 아래) |
| 영업 중 선택 좌석은 `PlayCamera.SelectedSeat`(#95 되묻기, #97 서빙 자리) | 카메라 X에서 가장 가까운 좌석 | 영업 중 `PlayCamera.SelectedSeat`, 대화 중은 기존 계산 유지 |
| 영업 중 손님 상호작용 조건은 `ServicePanelController.CanUseGuestActions` | 자체 조건 | 같은 조건. 카메라 이동 중에는 숨기지 않는다 |
| 기믹·준비 화면 동안 바 UI 끄기(`GimmickRunner`·`CraftPrepStageHost`의 `barCanvases`) | 목록에 없음 | 두 목록에 등록 |

## 남은 예외

- 병따기 화면(`CapOpenView`)의 글자·버튼·수업 안내는 아직 런타임 생성·1280×720이다. 같은 규칙에 맞추는 일은 별도 작업으로 남긴다(`Web_Parity_Day1_Beer_2026-11-03.md`).
