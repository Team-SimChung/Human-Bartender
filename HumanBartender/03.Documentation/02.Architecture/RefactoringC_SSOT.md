# C 구조와 SSOT 정리

2026-09-15 · `c861bc6e` 이후 C 로컬 변경 · Unity 6000.3.14f1

## 현재 상태

C 내부의 실행 설정·대사·상호작용·연출에서 발견한 중복 원본과 실행 경합을 정리했다. A/B의 상태·씬 전환·제조 취소 계약은 후속 연결로 남긴다. 미등록 리소스와 예약 콘텐츠까지 전부 구현됐다는 뜻은 아니다.

PDF는 과거 코드에 대한 제안이다. 실제 코드·CSV·씬을 기준으로 수정했고, 사용자가 정한 CSV 전용 흐름을 유지했다. A/B 구현, Main/Play 씬, 공유 프리팹·Addressables·패키지 설정은 수정하지 않았다. C 소유 OutSide/Home 씬의 연결은 수정했다.

## 어느 곳에서 무엇을 편집하는가

| 정보 | 편집/확정 위치 | C의 사용 방식 |
|---|---|---|
| 대사·선택지·조건·효과·진행 순서 | csv/story | 준비된 캐시를 실행기와 Timeline Signal이 조회 |
| 상호작용 종류·우선순위·조건·동작 ID | csv/interaction/interact_points.csv | 선택된 NewInteractPointData 한 행에서 직접 읽음 |
| 날짜별 대화·반복/once·선택 순서 | csv/interaction/dialogue_flows.csv | 조건에 맞는 첫 flow. Completed에만 once 기록 |
| 위치·카메라/엘리베이터 앵커·화자 오브젝트 | Unity 씬 | source_id·spot_id로 실제 오브젝트와 연결 |
| 컷신 ID·종류·리소스 키 | csv/story/cutscenes.csv | ID → resource_key → 씬 Timeline 또는 Addressables |
| 동작 ID에 대응하는 C 실행 | OutsideActions | 실행기와 검증기가 같은 이동 표 사용 |
| 게임 상태·날짜·저장 | A | C는 상태 API와 ConditionUtil 사용 |
| 제조·판정·서빙·매출 확정 | B | StoryCraftGate로 연결. 별도 정산 공식/장부 없음 |
| 실행 취소·종료·복원 | 해당 C 실행 작업 | 현재 소유자만 복원하며 종료까지 대기 |

SO와 딕셔너리는 CSV를 읽어 만든 **런타임 캐시**다. 복사본 자체가 SSOT 위반은 아니다. 캐시를 별도 편집 원본으로 사용하거나 서로 다른 작업이 같은 상태를 독립적으로 확정하는 것을 막는다. C 캐시 Inspector는 읽기 전용이다.

Excel은 편집 도구이고 게임이 읽는 입력은 CSV다. Excel 편집 후에는 내보내기를 수행한다. CSV를 직접 고쳤다면 다음 Excel 내보내기 전에 같은 내용을 반영한다. 두 파일을 다른 내용으로 동시에 편집하지 않는다. 이번 변경은 Excel→CSV 재내보내기 후 37개 데이터 묶음의 값·타입·순서·null까지 비교했다.

## 제거한 중복과 경합

### 1. CSV 설정과 Inspector의 불일치

- InteractiveEntity의 priority/kind/activation/action/steps 별도 Inspector 설정을 제거했다. Definition과 선택된 대사 캐시에서 읽는다.
- 같은 source_id의 후보는 높은 priority, 동률이면 고정 ID 순으로 선택한다. CSV 행 순서에 의존하지 않는다.
- 출입구는 Inspector의 targetScene/eGameFlow 대신 CSV action_ref/phase를 사용한다.
- 엔티티/앵커 중복 ID, 없는 스팟, 컴포넌트가 지원하지 않는 동작은 진단하고 해당 상호작용을 차단한다.
- 테스트 날짜·플로우·플래그와 오래된 씬 직렬화 필드를 제거했다.

### 2. 엘리베이터·라디오·NPC

- 본체에 street_elevator, 자식 라디오에 elevator_radio를 연결하고 중복 라디오 컴포넌트를 제거했다.
- 엘리베이터 한 작업이 이동, 벽, 입력, 카메라, 로고와 라디오 종료를 관리한다.
- 플레이어 부모는 유지하고 승강기의 프레임 이동량만 전달한다. 비활성화/파괴가 플레이어에게 전파되지 않는다. 도착 전 취소는 시작 위치를 복원하고 정상 도착은 도착 위치를 유지한다.
- StopAsync는 자식 정리와 입력 복원이 끝날 때까지 기다린다. 여러 종료 요청은 공통 완료 신호를 기다린다. 첫 Play 검증에서 발견된 UniTask 동시 대기 오류를 수정했다.
- InteractionStateLease는 C의 현재 소유자만 복원하고 외부에서 바뀐 입력 상태를 덮어쓰지 않는다. A의 전역 입력 잠금 계약은 별도다.
- 빈 NPC_Samho.PlayDialogueForCutscene과 미사용 트리거 대사 필드를 제거했다.

### 3. 컷신·Signal·Timeline 하위 전환

- 씬의 별도 컷신 ID 목록을 없애고 실제 Timeline 자산 참조만 남겼다. 실행 ID는 CSV에서 결정한다.
- 삼호 Signal 대사 3줄을 tl_samho_arrive의 CSV steps로 이관했다. 씬에는 화자 ID와 실제 오브젝트 연결만 둔다.
- ID 호출과 직접 자산 호출 모두 같은 실행 소유권 검사를 통과한다. 다음 실행 전에 대사 정리가 끝나는 것을 기다린다.
- Timeline은 실제 director 정지를 기다린다. 대사 오류를 실패로 전달하고 일시정지 상태에 방치하지 않는다.
- 배경·이미지·말풍선·Canvas 해상도 전환도 이전 작업 교체와 Timeline 종료 때 취소한다. 늦은 퇴장/전환이 새 화면을 지우거나 덮어쓰지 않는다.
- 종료 후 바인딩과 리소스 핸들을 반환한다.

### 4. 카메라·로고·사운드·Spine

- offset/zoom은 각각 한 작업이 소유한다. 이전 줌 복원 후 새 줌을 적용하며 오래된 finally가 새 해상도를 덮어쓰지 않는다. 취소·비활성화 시 Pixel Perfect Camera 설정도 복원한다.
- 실외 카메라는 부모를 정한 뒤 로컬 오프셋으로 이동한다. isChangeResolution을 실제 적용하고 중복 모드/슬롯을 거부한다.
- 로고의 원래 알파·위치·활성 상태·레이캐스트 차단을 복원한다. 완료 전 취소는 이미 본 것으로 기록하지 않는다. Tween 종료 콜백에서 재차 Kill하지 않는다.
- 사운드 중복 이름은 마지막 값으로 덮어쓰지 않는다. SE 풀 크기 0은 최소 1개로 보정한다.
- 독립 Spine API의 취소를 전달하고 반복 애니메이션도 취소/비활성화 시 트랙을 비운다. 예약 Gif 콘텐츠 지원 완료와는 별개다.

### 5. 이전 핵심 실행기 정리 유지

DialogueRunner/StoryScriptRunner의 실행별 조건 상태, Completed/Cancelled/Failed 구분, StopAsync 종료 대기, 취소된 선택지 차단, 타이핑 스킵/취소 구분, 캐릭터 슬롯의 로드·핸들 소유권, StoryCraftGate의 동일 세션만 수락하는 구조를 유지했다.

## 콘텐츠 정합성과 남은 경계

자산 GUID와 등록 주소를 대조해 컷신 resource_key 10개를 수정했다. 삼호 Timeline 3개는 씬에 연결된 자산으로 판별한다. 이를 Play 씬에서 전역 Addressables처럼 사용할 수 있다고 판단하지 않으며 검증기는 씬 바인딩 범위를 경고로 표시한다.

남은 정적 오류는 콘텐츠 작업이다. INTRO, 꿈/Day13 컷신, 포스터·예약 초상화의 주소/자산 등록이 필요하다. QA의 glitch_in, hard_cut, alarm_distant, reputation, 공통 대본의 give(...), 예약 Gif는 현재 지원 범위 밖이다. 없는 자산이나 의미를 임의의 연출/상태 변경으로 대체하지 않았다. 상세 목록은 Validation/RefactoringC/CContentReport.Current.txt에 있다.

## A/B 완료 후 C가 연결할 부분

1. **B 제조:** 수락/거절과 성공/실패/취소 결과, 작업별 CancelAsync를 StoryCraftGate에 연결. 현재 C 취소가 B 준비·기믹·타이머 종료를 의미하지 않는다.
2. **A 씬 전환/입력:** await 가능한 전환 결과와 전역 입력 소유권을 출입구·엘리베이터에서 소비. 현재 void 호출은 요청 전달까지만 알 수 있다.
3. **Home/하루 경계:** 저장·취침·다음 날 context·정산 보존 계약을 받아 연결. Home 진입과 기존 CSV 출입구 연결은 구현했으나 저장/다음 날은 임의 구현하지 않았다.
4. **새 날짜·리소스:** A의 로더 등록 및 자산 담당자의 Addressables 등록 후 콘텐츠 활성화·재검증.

근거와 완료 기준은 Handoff/RefactoringC_To_A.md, RefactoringC_To_B.md에 있다. 최신 실행 결과와 실제 검증 범위는 Validation/RefactoringC/README.md를 따른다.
