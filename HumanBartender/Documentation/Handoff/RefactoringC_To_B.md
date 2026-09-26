# B 담당자 인수인계 — C 제조·서빙 연동

2026-09-15 · 기준 커밋 `c861bc6e` + 현재 C 로컬 변경 · Unity 6000.3.14f1

대상은 제조·기믹·영업·서빙·정산을 맡은 **B 담당자**다. 아래 B 코드는 이번 작업에서 수정하지 않았다. 파일 링크는 조사한 로컬 경로이며, 다른 PC에서는 `HumanBartender` 아래 동일 파일을 찾으면 된다. 줄 번호는 이 문서 작성 시점 기준이다.

**가장 먼저 B-01과 B-02를 처리해야 C5의 실제 제조 취소를 완료할 수 있다.** 현재 C는 대본 대기와 구독을 정리하고 다른 세션의 이벤트를 거부하지만, B 자식 제조를 취소하거나 그 종료를 기다릴 API가 없다.

## 권장 순서

| 순서 | 항목 | 우선순위 | 완료 시 C가 받을 것 |
|---|---|---|---|
| 1 | B-01 시작·종료·취소 계약 | P1 | 수락된 작업 식별자와 최종 결과, 실제 CancelAsync |
| 2 | B-02 기믹 실패·정리 | P1 | 누락/예외를 성공으로 처리하지 않는 종료 결과 |
| 3 | B-03 늦은 완성 잔·UI 복원 | P1 | 작업별 1회 완료 발행, 이전 작업의 화면 복원 차단 |
| 4 | B-04 서빙 확정·일일 정산 | P1, 정산 화면 연결 전 | 최종 등급/정산 확정 지점과 A에 전달할 하루 기록 |

P1은 실행 정지 또는 결과 오염을 막기 위한 우선 항목이다. 아래 “재현/완료 기준”은 B 담당자가 수행할 검증이며, 이번에 모두 실제 플레이했다는 뜻은 아니다.

## B-01. 제조 요청의 수락·최종 결과·실제 취소

**근거 — 코드 확인 + C 이벤트 방어 테스트**

- [CraftFlowController.cs:180](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Craft/Loop/CraftFlowController.cs:180): BeginCraft는 void다. 진행 중/차단/칵테일 누락이면 반환만 하므로 호출자가 수락 여부를 직접 받지 못한다.
- [같은 파일:212](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Craft/Loop/CraftFlowController.cs:212): CraftBegan은 있지만, 실패/취소 종료 이벤트는 없다.
- [같은 파일:342](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Craft/Loop/CraftFlowController.cs:342): RunAsync는 UniTaskVoid다. 빈 큐는 354행에서 결과 없이 반환하고, 기믹에는 359행에서 컴포넌트 파괴 토큰만 전달한다. 공개 Cancel/CancelAsync API가 없다.
- C의 [StoryCraftGate.cs:110](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Story/StoryCraftGate.cs:110)는 CraftBegan에서 받은 세션과 같은 CraftCompleted만 수락한다. C 취소는 완료 대기만 끝내며, B 세션이 남으면 경고한다.
- C 테스트 `StoryCraftIgnoresOtherSessionsAndUnsubscribesAfterCancellation`은 구독 해제와 잘못된 세션 차단을 검증했다. 실제 기믹이 멈췄다는 증명은 아니다.

**현재 가능한 문제**

대본 제조 시작 → 준비/기믹 도중 대본 취소 → C UI와 구독은 종료 → B는 계속 진행한다. 반대로 수락 후 빈 큐/예외로 B가 완료 이벤트 없이 끝나면 C는 호출자가 취소할 때까지 결과를 기다릴 수 있다.

**필요한 계약 — 이름은 B가 결정**

1. 시작 결과에 수락/거절 이유와 실제 작업 식별자(OperationId 또는 안정적인 CraftSession 참조)를 반환한다.
2. 수락된 작업마다 Success/Failed/Cancelled 중 **최종 결과 하나**를 전달한다. 결과에는 같은 식별자와 실패 원인이 있어야 한다.
3. `CancelAsync(operation)`은 해당 작업의 준비·기믹·입력·타이머·UI 정리가 끝난 뒤 반환한다. C가 Phase를 직접 고치거나 B 오브젝트를 강제로 파괴하게 하지 않는다.
4. 취소/실패는 CraftCompleted 성공 이벤트와 완성 잔을 만들지 않는다. 정리 전에는 다음 시작을 거절하거나 대기시키는 정책을 명시한다.
5. 수동 메뉴 선택으로 시작되는 제조에도 같은 작업·종료 계약을 적용한다.

**완료 기준**

- [ ] 준비 중 취소, 첫/마지막 기믹 중 취소, 빈 큐, 기믹 예외, 시작 거절 모두 호출자가 무기한 대기하지 않는다.
- [ ] 취소 반환 시 자식 UI·입력·타이머가 끝나 있고, 바로 다음 정상 제조가 가능하다.
- [ ] 이전 작업의 늦은 이벤트가 다음 대본을 완료시키지 않는다.
- [ ] 기존 1부 수동 메뉴/튜토리얼 제조의 정상 완료 동작이 유지된다.

**C 후속:** B API 확정 후 StoryCraftGate를 연결하고, 위 시나리오를 실제 제조와 함께 검증한다. 이 연결은 C 담당자가 수행한다.

## B-02. 필수 기믹 누락을 성공으로 확정하지 않기

**근거 — 코드 확인**

- [GimmickRunner.cs:123](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Craft/Loop/GimmickRunner.cs:123): 프리팹이 없으면 null 반환. 137행의 ICraftGimmick 누락도 동일하다.
- [같은 파일:97](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Craft/Loop/GimmickRunner.cs:97): 결과가 null이어도 AdvanceGimmick을 호출하고 루프 뒤 114행에서 session.Complete를 호출한다. 이후 CraftFlowController는 판정/완료 이벤트를 발행한다.
- [같은 파일:75](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Craft/Loop/GimmickRunner.cs:75): 세션 시작·화면 전환·HUD 초기화가 try 앞이다. 131행 LiftAboveBarUi도 인스턴스 정리용 try 앞이다. 이 구간의 예외는 아래 finally로 정리되지 않는다.
- finally의 `runningTimer=null`은 Runner의 Tick 전달을 끊는다. **session.Timer.Stop 또는 취소/실패 Phase 전이는 아니다.** 경과 시간이 계속 증가한다고 단정하지는 않지만 세션의 실행 상태가 남을 수 있다.

**요청·완료 기준**

- [ ] 필수 프리팹/ICraftGimmick 누락은 B-01의 Failed로 전달하고, 판정·완성 잔을 생성하지 않는다.
- [ ] 초기화·카메라/화면 전환·HUD 연결·인스턴스 생성 후 작업을 정리 범위에 포함한다.
- [ ] 취소/실패 시 타이머와 세션 Phase가 실제 종료 상태가 된다. 성공 Completed로 덮어쓰지 않는다.
- [ ] 프리팹 누락, 구현 누락, HUD/화면 초기화 예외, 마지막 스텝 취소를 각각 검증한다. 오브젝트·바인딩·입력 잠금이 남지 않아야 한다.

## B-03. 완성 잔과 UI 복원도 작업 수명을 확인

**근거 — 코드 확인, 지연 이벤트/화면 경합 검증 필요**

- [ServeTrayUI.cs:120](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Tycoon/UI/ServeTrayUI.cs:120)는 CraftCompleted를 받으면 완성 잔을 추가한다. 134행 AddDrink에는 세션/작업별 중복 확인이 없다.
- C 게이트가 이벤트를 거부하더라도 다른 구독자인 트레이에는 동일 이벤트가 전달될 수 있다. C가 자신의 구독을 해제한 것만으로 늦은 잔 생성을 막지는 못한다.
- [GimmickRunner.cs:196](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Craft/Loop/GimmickRunner.cs:196)는 제조 자식 외의 활성 Overlay Canvas까지 찾아 끈다. 226행 RestoreBarCanvases는 해당 Canvas를 다시 enabled=true로 만든다. 그 사이 다른 흐름이 가시성을 변경했는지는 확인하지 않는다.

**요청·완료 기준**

- [ ] B-01에서 성공 결과를 작업별 한 번만 발행한다. 트레이의 중복 방어가 필요하면 작업 ID 기준으로 한다. 같은 칵테일을 두 번 정상 제조하는 것은 허용해야 한다.
- [ ] 취소된 작업의 늦은 완료 이벤트, 동일 완료 중복 전달 시 잔/정산이 추가되지 않는다.
- [ ] 바 UI 표시/복원을 소유한 작업을 확인한다. C 대화 시작·씬 전환 후 이전 제조 finally가 새 화면 상태를 덮어쓰지 않는다.
- [ ] 공통 입력/화면 잠금이 필요하면 A와 계약을 맞춘다. C의 InteractionStateLease는 Outside NPC·엘리베이터 입력용이며 제조 전체 잠금 API가 아니다.

## B-04. 최종 서빙·정산 확정 지점 — A 공동

**현재 유지해야 할 구조**

- [DailySales.cs:24](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Tycoon/DailySales.cs:24)는 SettlementId로 중복 반영을 막는다. 이 방어는 이미 있다.
- [GuestManager.cs:919](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Tycoon/GuestManager.cs:919)와 C [StoryCraftGate.cs:264](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Story/StoryCraftGate.cs:264)는 같은 ServeJudge/OrderSettlement 공식을 사용한다. C 전용 정산 공식을 추가하지 않았다.
- C도 같은 `guestManager.Sales.Apply`에 기록한다. 다만 B의 영업 주문과 C의 대본 주문에서 확정 호출은 각각 존재한다.
- [StoryScriptRunner.cs:458](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Story/StoryScriptRunner.cs:458)의 대본 주문 ID는 `scene.Id#step.Seq`다. 같은 장면 재실행을 같은 주문으로 볼지 새 주문으로 볼지는 요구사항 확인이 필요하다. 현재 ID가 반드시 잘못됐다고 단정하지 않는다.

**요청·완료 기준**

- [ ] 최종 서빙 등급 확정 → 정산 생성 → 장부 반영 → 주문 소비의 책임과 실패 처리 순서를 명시한다. 공통 commit API가 필요하면 B가 제공하고 C가 사용한다.
- [ ] 동일 주문의 중복 서빙은 한 번만 반영한다. 서로 다른 정상 주문은 같은 칵테일이어도 별도 반영한다.
- [ ] 재시도/장면 재실행/새 날짜의 주문 ID 범위를 C·A와 합의한다.
- [ ] A와 DailySales/PlayerSettlement의 역할을 정한다. 하루 종료 시 항목·합계를 A 세션/저장 계층으로 전달하는 지점을 하나로 둔다.
- [ ] 1부와 2부 매출을 합친 후 Play→Outside 이동해도 정산 화면의 합계가 유지되고, 다음 날 시작 때만 초기화된다.

## B가 건드릴 필요 없는 C 항목

대사/선택지/이동 대본의 CSV 편집, `when` 파서, StoryScriptRunner·StoryCraftGate 연결, CSV `action_ref`/priority 적용, 타이핑·컷신 표현은 C 담당자에게 남긴다. 조건식은 선택 사항이며 B 제조 결과를 C 조건 문법으로 직접 평가할 필요가 없다. B는 확정된 판정과 결과만 제공하면 된다.
