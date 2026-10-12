# 새 쉐이킹 씬 연결과 데이터 흐름

## 씬과 프리팹

- 테스트 씬: `HumanBartender/Assets/03.Dev/01.Scenes/01.Minigames/NewShaking.unity`
- 실행 프리팹: `HumanBartender/Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/01.Shake/RhythmShakingMinigame.prefab`
- 제어 컴포넌트: 루트의 `ShakingRhythmManager`

씬을 다른 씬에 로드하는 방식보다, 기존 제조 실행기에서 프리팹을 생성하는 방식이 프로젝트의 실제 실행 경로다. `NewShaking`은 카메라와 EventSystem을 갖춘 독립 테스트 씬이다. 테스트 씬에서는 `isTest = true`라 종료 후 R로 다시 시작할 수 있다. 프리팹 기본값은 false다.

현재 `Play.unity`의 `GimmickRunner.prefabs`에서 `Shake` 항목은 기존 `NewShakingMinigame`을 가리킨다. 실제 제조에 새 버전을 사용하려면 해당 항목에 `RhythmShakingMinigame`을 연결한다. 이번 정리는 새 버전의 씬과 프리팹만 수정했다.

```text
RhythmShakingMinigame
├─ Character             캐릭터, Animator, AnimSpeedController
├─ Background
├─ Panel Background
├─ Background Audio
└─ Rhythm Canvas
   └─ Rhythm Board
      ├─ Header          콤보 / 단계 / 판정 / 처리 노트 수
      ├─ Lanes           A/D 레인 / 생성점 / 판정점 / 노트 샘플
      ├─ Effects         가장자리 빛 / 속도선
      ├─ Progress        20칸 판정 결과
      ├─ Controls        A/D 버튼과 안내
      └─ State           시작 팝업 / 카운트다운 / 종료 표시
```

노트 샘플은 편집 중 보이고, 실행 시 숨긴 뒤 생성할 때 복제한다. 두 레인의 생성점과 판정점은 각 레인 바로 아래에 있고, 이동 거리가 같아야 한다. 그룹 오브젝트는 좌표 원점과 배율을 유지한다.

## 외부에서 들어오는 정보

호출 흐름:

```text
CraftFlowController
  → GimmickRunner.ExecuteAsync(step, context, session.Timer, token)
  → 프리팹 생성 및 HUD 연결
  → ShakingRhythmManager.PlayAsync(step, context, timer, token)
```

| 전달값 | 전달하는 곳 | 새 쉐이킹에서의 역할 |
|---|---|---|
| `GimmickStep step` | 제조 기믹 큐 → 실행기 | 공통 계약으로 받는다. 쉐이킹의 노트 수나 속도에는 사용하지 않는다. |
| `CraftContext context` | `CraftContext.From(session)` → 실행기 | 칵테일 ID, 잔, 도구, 믹스 방식, 선택 재료 목록을 받지만 현재 UI와 난이도에는 반영하지 않는다. |
| `CraftTimer timer` | 제조 세션 → 실행기 | 제조 전체 누적 조작 시간. 종료 시 `CompletedAtSec`에 기록한다. |
| `CancellationToken token` | 제조 흐름 → 실행기 | 취소 시 대기를 해제한다. 실행기는 finally에서 프리팹과 HUD 연결을 정리한다. |

`PlayAsync`는 시작 버튼을 대신 누르지 않는다. 시작은 Space/A/D 또는 시작 팝업 클릭으로 이루어진다. 외부가 조작을 직접 전달해야 한다면 `StartGame()`, `LeftInput()`, `RightInput()`을 호출할 수 있다. 현재 키보드 입력은 매니저가 `Keyboard.current`로 읽으며, 옛 PlayerInput/이벤트 브리지는 사용하지 않는다.

노트 수, 도착 시간, 생성 간격, 판정 시간 범위는 매니저 Inspector에 저장되어 있다. 콤보 5/10의 배속과 생성 패턴은 `ShakingRhythmRules`에 있다. 입력받은 잔·재료·레시피 데이터가 이 값을 바꾸지는 않는다.

## 진행 중 외부에서 읽는 정보

| 접근 경로 | 정보 / 용도 |
|---|---|
| `ICraftGimmick.IsManualInputActive` | 입력 가능한 동안 실행기가 제조 타이머에 deltaTime을 더한다. |
| `ICraftGimmickProgress.ProgressText` | 처리 노트 수 / 목표 노트 수. |
| `ICraftGimmickProgress.ShowOkMark` | false. 쉐이킹에는 공통 O.D 표시를 띄우지 않는다. |
| `ICraftGimmickOwnHud.OwnedParts` | Progress. 리듬 보드가 진행 표시를 맡으므로 공통 HUD의 중복 진행 표시를 숨긴다. |
| `ICraftGimmickManualEnd.CanEndNow` / `EndNow()` | 공통 다음 버튼에서 현재 결과로 조기 종료한다. |
| `SpeedMultiplier` / `CurrentSpeed` | 현재 배속 / UI 좌표 기준 이동 속도. |

## 종료 결과가 전달되는 곳

20개 노트를 처리하거나 `EndNow()`를 호출하면 완료 신호를 보낸다. `PlayAsync`가 아래 값을 담은 `GimmickResult`를 반환한다.

| 결과 필드 | 값 |
|---|---|
| `Type` | `ECraftGimmick.Shake` |
| `SuccessCount` | Perfect + Good 횟수 |
| `FailureCount` | 놓친 노트 + 빈 타이밍 입력 횟수 |
| `TargetStackCount` | 목표 노트 수 + 빈 입력 횟수. 점수 분모로 사용한다. |
| `EndType` | 목표 처리 완료: AutoTarget / 외부 다음 버튼: ManualNext |
| `CompletedAtSec` | 제조 전체 타이머의 누적 시간 |
| `CompletionScore` | 성공 수 ÷ 점수 분모, 0~1 |

```text
PlayAsync의 GimmickResult
  → GimmickRunner.ExecuteAsync 반환
  → CraftFlowController의 session.Actual.Record(result)
  → ActualCraft.GimmickResults
  → CraftJudge가 CompletionScore × 100으로 쉐이킹 점수 계산
```

예: 20개 모두 성공하고 빈 입력이 2회면 성공 20, 실패 2, 점수 분모 22이므로 약 90.9점이다. 빈 입력은 노트 진행 수를 늘리지 않는다. 최대 콤보는 현재 종료 UI에 표시하며 `GimmickResult`에는 포함되지 않는다.

옛 `IMiniGameController`, `InitGame`, `Serve`, `Retry`, `OnNextButton`, `CraftStationData` 결과 쓰기는 새 버전에서 제거했다. 종료 결과를 저장하는 곳은 제조 흐름의 `session.Actual.Record`다.

## 정리 내용과 검증

이전 경로 노드, 생성기, 풀, 15칸 게이지, 서빙/재시도 캔버스, 비활성 입력 컴포넌트와 반복 격자/스캔라인 장식을 제거했다. 프리팹 오브젝트는 183개에서 121개로 줄었으며 이 중 6개는 역할별 그룹이다. 씬에 남아 있던 삭제된 프리팹 오브젝트의 override도 정리했다.

코드 컴파일과 씬/프리팹 참조 검사를 수행한다. 실제 Unity 화면 및 플레이 검증은 별도로 필요하다.
