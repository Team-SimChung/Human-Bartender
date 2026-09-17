# 통합 제조 관리자

`CraftManager`의 작업 관리 로직을 `CraftFlowController : MonoBehaviour`에 합쳤다.
별도 `CraftManager` 클래스와 `.Manager` 전달 계층은 제거했다. 기존 스크립트 이름과 `.meta` GUID,
Inspector 필드를 유지하므로 씬/프리팹/UnityEvent 연결을 옮길 필요가 없다.

## 책임

- `CraftFlowController`: UI 연결, 요청 수락/거절, 준비, 큐 생성, 단계 실행 요청, 결과 기록, 판정, 취소/종료.
- `GimmickRunner : ICraftExecutor`: 화면 준비, 기믹 한 단계의 생성/실행/정리, 입력 가능한 시간 갱신, 화면 복원.
- `CraftSession`, `ActualCraft`, `GimmickQueueBuilder`, `CraftJudge`: 상태·데이터·규칙 코드 유지.

## UI 버튼 연결

기존 컴포넌트의 `BeginCraft(string)`, `StartGimmicks()`, `CancelCurrentCraft()`를 연결한다.
선택용 `ToggleGlass/ToggleTool/ToggleIngredient`, `SelectGlass/SelectTool`, `MarkRecipeNoteRead`도 유지한다.
`BeginCraft`는 테스트 자동 준비 옵션을 적용한다. `StartGimmicks`는 비동기 실행을 시작하는 버튼용 void 메서드다.

## 코드에서 직접 호출

```csharp
if (!craftFlow.TryBegin(cocktailId, out var session, out var reason))
    return;

var completion = craftFlow.Completion;
craftFlow.SelectGlass(glassId);
craftFlow.ToggleIngredient(ingredientId);
await craftFlow.StartGimmicksAsync();
var result = await completion;
// result.JobId / result.Phase / result.FailureReason / result.Actual
```

다른 경로에서 취소하려면 `await craftFlow.CancelCraftAsync(session.JobId)`를 호출한다.
동기 `CancelCraft`는 취소만 요청하고, 비동기 API는 기믹·화면 정리가 끝날 때까지 기다린다.
지난 작업 ID나 잘못된 ID는 현재 작업을 취소하지 않는다. 비활성 컴포넌트의 새 요청은 거절한다.

## 이벤트와 보존 규칙

- `CraftBegan`, `PreparationChanged`, `CraftCompleted`, `CraftEnded`, `CraftFlowActiveChanged`를 같은 컴포넌트에서 구독한다.
- `CraftCompleted`는 성공에만 발생하며 `CraftEnded`는 성공/실패/취소 정리 후 한 번 발생한다.
- `Completion`은 종료 알림 및 제조 가능 상태 갱신 후 완료된다. 정리 중 새 요청은 거절한다.
- 비활성화/파괴 시 진행 중 작업을 취소한다. 전역 싱글톤은 도입하지 않았다.
- 기믹 순서·오선택·자동 재료·점수 계산은 유지한다. 빈 큐와 실행 설정 누락/예외는 실패 종료한다.
- `StoryCraftGate`와 대본은 수정하지 않았다. C의 실제 취소 연결은 후속 작업이다.

## 검증

`Assets/Editor/CraftManagerTests.cs`는 GameObject에 통합 컨트롤러를 붙여 작업 관리와 수명을 검사한다.
실제 기믹 대신 `ICraftExecutor` 테스트 구현을 사용한다. 실제 씬의 시각적·입력 복원 확인은 별도다.
