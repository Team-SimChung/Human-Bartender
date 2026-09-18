# 주문·제조·서빙 연결

## 책임

| 스크립트 | 책임 | 호출자 |
|---|---|---|
| `OrderRequestController` | 주문 등록·취소, 서빙 접수, 정산 실행, 정리 후 콜백 | `StoryScriptRunner`, `GuestManager` |
| `OrderRequest` | 주문 정보와 상태의 단일 원본 | 주문 컨트롤러 |
| `CraftFlowController` | 제조 준비·기믹 실행·제조 판정·취소 | 제조 메뉴·준비 UI 또는 외부 코드 |
| `CraftSession` | 제조 작업 ID, 실제 선택·결과, 종료 상태 | 제조 컨트롤러 |
| `CraftServingView` | 2부 메뉴 표시와 서빙 위치 생성·해제 | 주문 컨트롤러 |
| `ServeProcessor` | 서빙 판정·정산 계산과 장부 반영을 분리 | 주문 컨트롤러 |
| `DailySales` | 정산 ID별 중복 반영 방지와 매출 보관 | `ServeProcessor.Apply` |

주문 상태는 `Waiting → Serving → Completed`이며 취소·실패 시 `Cancelled` 또는 `Failed`로 종료한다. `Guest.hasOrdered`도 이 상태를 조회한다. 제조의 `JobId`와 주문 ID는 별개다. 제조를 취소하고 다시 만들어도 열린 주문에 서빙할 수 있다. 다른 칵테일도 접수하고 `ServeJudge`에서 주문 일치를 평가한다.

## 요청 API

```csharp
var request = orderController.Request(
    new OrderDetails(orderId, receiverId, cocktailId),
    result =>
    {
        if (result.Completed)
        {
            // result.Serve: 제조 등급, 최종 등급, 주문 일치, 정산 결과
            // 다음 대화나 손님 진행을 처리한다.
        }
        else
        {
            // result.State: Cancelled 또는 Failed
            // result.Error: 실패 사유
        }
    });

orderController.CancelOrder(request.Id); // 제조와 트레이는 유지
```

잘못된 인자·중복 ID·필수 연결 누락은 등록 시 예외로 거절한다. 등록된 요청의 성공·취소·실패는 콜백으로 한 번 전달한다. 콜백은 `null`로 생략할 수 있다. 서로 다른 주문은 동시에 등록할 수 있지만 제조는 한 작업씩 수행한다.

서빙 접수 후 다음 프레임까지 기다려 드래그를 끝낸다. 선택적인 손님 반응을 기다린 뒤 장부에 반영하고, 수신 연결 해제와 표시 정리 후 콜백을 전달한다. 정리가 끝나기 전까지 주문 ID는 예약된다. 예전 수신 함수는 같은 ID의 새 주문을 완료할 수 없다.

마지막 주문이 제조 중 취소되면 서빙 연결을 해제하고 제조 화면 닫기는 제조 종료까지 미룬다. 표시 정리에서 예외가 발생하면 로그를 남기고 요청 제거와 콜백은 계속 수행한다. 이미 반영한 정산은 되돌리지 않는다.

## 1부 호출 흐름

1. `GuestManager.WatchOrderAsync` / `StartNextOrderAsync`가 `RegisterGuestOrder`를 호출한다.
2. 주문 정보·팁 배율·손님 반응·완료 콜백을 주문 컨트롤러에 등록한다. 기존 슬롯과 제조 메뉴를 사용한다.
3. 메뉴 선택이 `CraftFlowController.BeginCraft → TryBegin`으로 이어진다. 준비 UI의 버튼이 `StartGimmicks → StartGimmicksAsync`를 호출한다.
4. `CoasterDropZone → GuestManager.TryServeDrink`에서 손님·대기시간 검사 후 `OrderRequestController.TryServe`로 전달한다.
5. 접수되면 손님 대기 타이머를 멈춘다. 주문 컨트롤러가 `ReactToDrinkAsync`의 반응을 기다린 후 기존 장부에 반영한다.
6. 완료 콜백의 `ContinueAfterServeAsync`가 재주문 또는 퇴장을 진행한다. 퇴장·비활성화는 해당 주문을 취소한다.

## 2부 호출 흐름

1. `StoryScriptRunner`의 대본 처리에서 `OrderAsync`가 주문 컨트롤러에 등록한다.
2. 컨트롤러가 `CraftServingView`에 주문자 앞 서빙 위치와 제조 메뉴를 열도록 요청한다.
3. 실제 제조는 플레이어의 칵테일 선택·준비 UI로 진행한다. 주문이 특정 제조 세션을 기다리거나 제조를 자동 시작하지 않는다.
4. `StoryServeDropTarget`이 등록된 수신 함수를 호출한다. 컨트롤러가 판정·정산·수신 해제·표시 정리를 수행한다.
5. 콜백이 해당 주문의 결과 신호를 완료한다. `StoryScriptRunner.ServeAsync`가 받아 스토리 전용 `StoryResultContext`로 변환하고 다음 대화를 진행한다. 먼저 서빙해도 결과는 보관된다.
6. 스토리 종료 시 `CancelOrder`를 호출한다. 제조 취소와 독립적이다.

기존 `craft` 대본 스텝은 유지한다. 주문 없는 tutorial 스텝은 메뉴만 열고 제조 완료를 기다리지 않는다. 공통 계약에는 스토리 타입이 없으며 `StoryOrder`, `StoryCraftGate`, `IStoryCraftGate`는 제거했다.

## 씬과 검증

`Play.unity`의 표시 담당 오브젝트에 `OrderRequestController`를 추가했다. `StoryFlow`와 `GuestManager`가 이를 참조한다. 표시 담당·칵테일/밸런스 데이터·기존 장부 소유자인 `GuestManager`를 연결했다. 새 장부를 만들지 않는다.

전체 런타임 컴파일 및 격리 Unity 검증에서 제조 13개, 주문 8개가 통과했다. 주문 검증은 콜백 순서, 서빙 중 취소, 제조·주문 취소 독립성, 콜백 없는 요청, 중복/잘못된 서빙, 표시 시작 실패, 늦은 수신, 실제 판정·음수 매출·반올림·중복 반영을 포함한다. 실제 씬의 드래그·애니메이션·대본 실행은 별도 Play Mode 확인 대상이다.
