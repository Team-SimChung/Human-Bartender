# 1일차 복도 불량배 NPC

`day1-corridor-thug`는 공통 NpcInteraction 목록으로 대사 전 카메라·타격 동선·퇴장을 실행한다. 대사·선택·500원 결제·중복 결제 방지·완료 저장은 기존 CSV 경로를 유지한다. 전용 DayOneThugInteraction은 사용하지 않는다. 공통 작성법은 [NPC Inspector 안내](Npc_Interaction_Inspector_2026-11-07.md)에 있다.

## Inspector와 실행 순서

OutSide의 `04.Gameplay/InteractionObjects/StoryEvents/day1-corridor-thug`를 선택한다.

| 필드 | 설정 |
|---|---|
| World Interaction Target > Start Mode | 범위 진입 시 자동 시작 |
| Interaction Radius | 기존2. 대상 루트와 루나 사이 XY 거리 |
| Npc Interaction > Actor | 같은 루트의 NpcActor |
| Use Story Presence | 켬 |
| Minimum Day | 0. 실제 등장 일차는 기존 CSV 조건 사용 |
| Presence Condition / Committed Condition | 빈 값. CSV 등장·완료 기준 사용 |
| Npc Actor > Idle Facing | 플레이어 바라보기 |

Sequences의 `실행 방식`은 자동 시작 설정과 다르다. 대화가 시작된 뒤 목록을 무엇으로 호출할지 정한다. `CSV · 연출 ID로 호출`은 CSV의 `npc-sequence` 행에서 `arg`와 `연출 ID`를 비교하고, `CSV · 등록된 명령으로 호출`은 코드에 등록된 명령과 Inspector의 `등록된 CSV 명령` 선택 값을 비교한다. 두 방식 모두 실제 이동 거리·시간은 Inspector의 `동작 순서`를 사용한다.

| 연출 ID | 실행 방식·등록된 CSV 명령 | 내용 |
|---|---|---|
| web-dialogue-start | 대화 시작 전 실행 | 루나가 불량배를 봄 → HomeSpawn+(.4,.65) 카메라, 가로 범위4.8, 최소 .65초와 정착 |
| punch | CSV · 등록된 명령으로 호출 → 불량배 · 타격(ThugPunch) | 아래 타격 동선 |
| depart | CSV · 등록된 명령으로 호출 → 불량배 · 퇴장(ThugDepart) | 오른쪽 이동과 마지막 구간 페이드 |
| web-dialogue-finish | 대화 정상 종료 후 실행 | 카메라 추적 복귀, 최소 .4초 |

대사 전 루나 위치를 강제로 옮기지는 않는다. 타격·퇴장을 어느 대사 사이에서 호출할지는 기존 CSV가 결정하고, 실제 이동 위치·거리·시간은 Inspector에서 정한다. 돈을 주면 타격을 건너뛰고 퇴장하며 퇴장 후 루나 마지막 대사도 유지한다.

## punch 조절

`Sequences > punch > 동작 순서`를 연다. 대상은 모두 비움=루나, 실행 NPC는 불량배다.

| 순서 | 동작 | 값 |
|---|---|---|
| 1 | 대상 바라보기 | 루나를 봄 |
| 2 | 기준 위치로 이동 | 대상 위치+X .33, X만 이동, .65초, 일정한 이동, 이동 중 바라보는 방향 유지 켬 |
| 3 | 기준 위치에 배치 | 대상 위치+X .23, X만 이동 |
| 4 | 기준 위치로 이동 | 대상 위치+X .63, X만 이동, .8초, 일정한 이동, 이동 중 바라보는 방향 유지 켬 |
| 5 | 대기 | .2초 |

접근·물러남을 웹 동선대로 표현하며 동작 시간 합은1.65초다. 최초 배치 대신 실행 당시 루나 X가 기준이고 불량배 Y/Z는 유지한다. Move Speed 대신 각 동작의 시간을 사용한다.

예전 접근→Punch 상태 재생 목록은 이 동선으로 교체했다. 임시 움츠림 Punch 클립이 Controller에 남아 있어도 현재 목록은 호출하지 않는다. 별도 주먹 그림·체력 피해·루나 넉백은 추가하지 않았다. 대기·이동 중에는 기존 Neighbor 시트의 웹 대응 대기 프레임을 사용한다.

## depart 퇴장 조절

`Sequences > depart > 동작 순서 > 이동하며 페이드 퇴장`을 연다.

| 필드 | 값 |
|---|---|
| 전체 월드 X/Y 이동량 | (.845,0) |
| X 이동 방향 기준 | 작성한 X/Y 그대로 |
| 페이드 전 이동 시간 (초) | 1초 |
| 이동 중 페이드 시간 (초) | .3초 |
| 페이드 변화 | 부드럽게 시작·끝(SmoothStep) |

총1.3초 동안 .845를 일정한 속도(.65/초)로 이동하고 마지막 .3초에 사라진다. 페이드 곡선은 투명도에만 적용된다. Exit Point는 없고 명령 당시 현재 위치가 기준이다. X=4로 바꾸면 그 위치에서 오른쪽4로 퇴장한다.

## 역할·취소·저장

NpcInteraction은 순서·값·등장 조건, NpcActor는 공통 이동·시선·Animator·퇴장·자세 복원, NpcSceneStaging은 루나 방향·카메라를 맡는다. WorldInteractionController가 대화 세션·입력 잠금과 전/후 목록 호출을 관리한다.

등장은 기존1일차 출근·!day1_thug_done 조건이다. 퇴장만 끝났다고 완료하지 않고 대화와 종료 연출이 성공한 뒤 기존 실행기가 완료 플래그를 기록한다.

중단하면 접근·퇴장 중이거나 이미 숨었더라도 미완료 사건의 시작 자세·표시 조건과 카메라를 복원한다. 이미 지급한 돈과 day1_thug_choice는 기존 규칙대로 유지해 재시도 중복 결제를 막는다. 정상 완료 후 재방문·진행 상태 복원에서는 재등장하지 않는다. 별도 NPC 저장 형식은 없다.

루트 아래 Sprite·Speech anchor는 유지하며 말풍선은 이동을 따라간다. 사용자 배치·크기·시작 방식·범위는 보존했다. Tools/Story/Apply Web NPC Presentation은 웹 동작·시간을 다시 적용하므로 평소 편집은 Inspector에서 한다.

## 검사 이력

초기 전용 불량배 구현49개 통과는 이전 단계 기록이다. 당시 임시 Punch 애니메이션 검사를 이번 웹 동선 결과로 해석하지 않는다.

기존 DayOneThugInteractionTests·DayOneThugRuntimeSmokeTests를 새 목록에 맞춰 검증한다. NpcSceneStagingTests·NpcPreludePresenceTests는 카메라·루나 복원과 대화 전 취소도 다룬다. 이번 결과·숫자는 최신 검증 로그를 기준으로 확인한다.
