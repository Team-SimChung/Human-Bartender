# 수리공 NPC 설정

수리공은 전용 RepairmanInteraction 대신 공통 NpcInteraction 목록을 사용한다. 대사·지급·완료 플래그·저장은 기존 CSV 경로다. 전체 메뉴는 [NPC Inspector 안내](Npc_Interaction_Inspector_2026-11-07.md)를 참조한다.

## 진행과 저장

- 0일차에는 숨고 1일차부터 수리 완료 전까지 출근·퇴근 모두 남는다.
- 400원 또는 기어 부품을 전달하면 elevator_repair_paid를 기록한다.
- 전달 후 중단되면 기존 Paid 대본으로 재개하며 다시 소비하지 않는다.
- 수리 연출 성공 뒤 기존 사건 처리기가 elevator_repaired를 기록한다.
- 완료 대사 종료 후 기존 목표 알림을 띄우고 퇴장한다.
- 완료 후 중단·재방문·진행 상태 복원을 해도 수리 완료가 취소되거나 수리공이 재등장하지 않는다.

별도 저장 데이터는 없다. 빠른 이동·후속 갱단은 같은 elevator_repaired를 조건으로 읽을 수 있다. 이번 연출 조정에서 빠른 이동 자체를 추가한 것은 아니다.

## Inspector에서 찾기

OutSide의 `04.Gameplay/InteractionObjects/StoryEvents/neighborhood-repairGreeting`을 선택한다.

| 필드 | 설정 |
|---|---|
| World Interaction Target > Start Mode | 직접 상호작용 |
| Interaction Radius | 기존1.4. 대상 루트와 루나 사이 XY 거리 |
| Npc Interaction > Actor | 같은 루트의 NpcActor |
| Use Story Presence | 끔 |
| Minimum Day / Maximum Day | 1 / -1(마지막 일차 제한 없음) |
| Presence Phase | Any(출근·퇴근 모두) |
| Presence Condition | `!flag.elevator_repaired` |
| Committed Condition | `flag.elevator_repaired` |
| Npc Actor > Idle Facing | 플레이어 바라보기 |

대화 가능 여부는 기존 CSV가 별도로 판단한다. 위 값은 표시와 중단 시 복원을 관리한다.

## 수리 암전과 퇴장 조절

`Npc Interaction > Sequences`의 두 목록을 펼친다.

| 연출 ID | 실행 방식·연결 | 동작 |
|---|---|---|
| repair | CSV · 등록된 명령으로 호출 → 수리공 · 엘리베이터 수리(RepairElevator), 조건 flag.elevator_repair_paid | 화면 암전 .35초 → 대기1.35초 → 화면 복귀 .45초 |
| repair-complete | 대화 정상 종료 후 실행, 조건 flag.elevator_repaired | 기존 목표 완료 알림 → 이동하며 페이드 퇴장 |

repair의 `등록된 CSV 명령`은 `수리공 · 엘리베이터 수리`다. CSV의 수리 요청이 코드에 등록된 `RepairElevator` 명령으로 전달되면 이 선택 값과 일치하는 목록을 실행한다. `연출 ID`인 repair를 CSV 문자열과 비교하는 방식은 아니다. 별도의 `CSV · 연출 ID로 호출` 방식은 CSV에 `type=npc-sequence`, `arg=연출 ID`를 작성해 연결한다. 두 방식 모두 암전·이동·페이드 수치는 Inspector의 `동작 순서`를 사용한다.

수리 화면 전체는2.15초다. 어두워지는 시간은 화면 암전, 어두운 상태 유지 시간은 대기, 다시 보이는 시간은 화면 복귀에서 조절한다.

퇴장은 `repair-complete > 동작 순서 > 이동하며 페이드 퇴장`에서 조절한다.

| 필드 | 현재 설정 |
|---|---|
| 전체 월드 X/Y 이동량 | 기존 사용자 값 보존. 적용 전 씬은(0.525,0) |
| X 이동 방향 기준 | 작성한 X/Y 그대로 |
| 페이드 전 이동 시간 (초) | 1.1초 |
| 이동 중 페이드 시간 (초) | .5초 |
| 페이드 변화 | 일정한 변화(Linear) |

전체1.6초 동안 일정한 속도로 이동하며 마지막 .5초에 사라진다. 속도는 전체 거리÷1.6초이며 NpcMovement2D.Move Speed는 쓰지 않는다. 더 빨리 퇴장시키려면 두 시간의 합을 줄인다. 페이드 시작 시점만 바꾸려면 합은 유지하며 두 구간을 조절한다.

Exit Point나 목적지 오브젝트는 없다. X=4이면 명령 당시 X=-11.675에서 -7.675까지 이동한다. Y도 현재 위치에 더하며 Z는 유지한다. 부모 크기·회전과 무관한 월드 축 기준의 직선 이동이다.

수리 화면 암전과 NPC 퇴장 페이드는 별도 목록의 값이므로 독립적으로 바꾼다. NpcActor에는 수리공 전용 거리·시간을 보관하지 않는다. Interaction이 값을 전달하면 Actor가 공통 동작을 실행한다.

## 책임과 리소스

WorldInteractionTarget은 시작·범위, NpcInteraction은 등장·순서·값, NpcActor는 이동·시선·Animator·페이드, 기존 사건 처리기는 지급·완료 플래그를 담당한다. 루트의 Sprite·Speech anchor 계층과 기존 화자 repair 연결을 유지한다.

웹 대응 대기는 수리공 시트9프레임을12fps로 재생한다. 별도 걷기 원본·수리 전용 애니메이션을 만든 것은 아니다. Paid 대본의 금액 문구도 원문 그대로이며 부품 경로의 문구 변경은 별도 콘텐츠 작업이다.

`Tools/Story/Apply Web NPC Presentation`을 다시 적용하면 암전·퇴장 시간은 위 값으로 맞추지만 기존 퇴장 X/Y와 목표 알림은 보존한다. 기존 목록의 연출 ID와 실행 조건도 유지한다. 평소에는 Inspector에서 조절한다. `Migrate NPC Interaction Sequences`는 최초 공통 구조 이전 도구다. 코드 리팩토링을 반영하는 것만으로 이 도구가 자동 실행되거나 현재 씬의 수치가 바뀌지는 않는다.

## 검사 이력

초기 수리공 구현42개 통과, 상대 이동·페이드 책임 변경29개 통과는 이전 단계 기록이며 이번 웹 시간·이동 중 페이드 조정 결과가 아니다.

기존 RepairmanStoryTests·RepairmanInteractionTests·RepairmanRuntimeSmokeTests 및 공통 Actor·목록 검사를 사용한다. 새 결과는 최신 검증 로그를 기준으로 확인한다. 별도 NPC 저장 형식을 도입하지 않고 기존 플래그 수집·복원을 검사한다.
