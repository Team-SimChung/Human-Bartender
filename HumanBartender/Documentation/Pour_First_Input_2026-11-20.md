# 06. 따르기 종료 버튼과 제조시간

| 상태 | 제조시간 누적 | 종료 버튼 |
| --- | --- | --- |
| 첫 병 조작 전 | 멈춤 | 비활성·반투명 |
| 병을 잡고 조작 중 | 계속 | 비활성·반투명 |
| 놓은 뒤 원래 위치·각도로 복귀 중 | 계속 | 비활성·반투명 |
| 복귀 완료 후 대기 | 계속 | 활성·불투명 |
| 다시 병을 잡음 | 계속 | 비활성·반투명 |
| 종료 버튼을 누른 뒤 액체 정리·결과 확정 | 멈춤 | 비활성·반투명 |

첫 조작은 UI 버튼을 제외한 유효 작업 영역에서 병을 잡는 입력이다. 실제 액체가 나오기까지 기다리지 않는다. 마우스를 놓아도 이미 시작한 시계는 종료 요청까지 계속 누적한다. 따르기와 같은 구현을 쓰는 필업에도 동일하게 적용한다.

`BottleTiltController.HasBeenManipulated`는 첫 잡기를 기억한다. `IsAtRest`는 드래그가 끝나고 이동 진행률·회전 각도가 모두 0으로 복귀했는지 확인한다. 첫 조작을 받기 위한 `IsManualInputActive`는 그대로 두고, `ICraftGimmickTiming.IsTimeCounting`으로 제조시간만 별도 제어한다. `GimmickRunner`와 독립 따르기 화면 모두 같은 조건을 사용한다. 다른 기믹의 기존 시간 기준은 유지한다.

불투명도는 `NewPourGame.prefab`의 `Pour HUD`에 붙은 `CraftGimmickHud > Next > Disabled Next Button Alpha`에서 조절한다. 기본값은 0.5다. 버튼에 저장된 CanvasGroup이 배경과 자식 글자를 함께 조절하며 런타임에 UI 구조를 만들지 않는다. 새 연결은 기존 `Tools > Crafting > Save Pour HUD In Prefab` 메뉴로 복구할 수 있다.
