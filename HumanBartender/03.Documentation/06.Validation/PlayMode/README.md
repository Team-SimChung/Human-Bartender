# CSV 전환 후 Unity Play 검증 — 2026-09-15

Unity 6000.3.14f1의 현재 프로젝트를 그래픽 에디터에서 실행했습니다.
임시 에디터 검사 스크립트로 실제 상태·UI 문자열·로그를 읽었고, Input System의 Space 입력과 UI 버튼 콜백으로 메인 → 대사 → 제조 준비까지 진행했습니다.
15:35:10 이후에는 검사 스크립트가 조작 명령을 보내지 않았습니다. 이후 에디터에서 이어진 플레이는 런타임 로그로 확인했으며, 해당 제조·서빙을 에이전트가 직접 조작했다고 주장하지 않습니다.

## 확인 결과

| 경로 | 확인한 내용 | 근거 |
|---|---|---|
| Main 시작 | CSV loaded=True, error=null | main-loaded.txt |
| Play 진입 | 0일차 대사, 크리스 이름·초상·한글 표시 | day0-start.txt, day0-start.png |
| 대사 진행 | Space 입력·타이핑 건너뛰기 후 다음 대사 진행, 색 태그 치환 | events.log, dialogue-progression.txt |
| 주문 → 제조 | 진토닉 주문 후 잔·도구·재료 준비 UI 진입 | craft-preparation.png, runtime.log 96행 |
| 이어진 플레이 | 진토닉 제조 2회, 진피즈 제조와 실제 서빙·정산 기록 | runtime.log의 CraftFlow / StoryCraft 기록 |
| 0일차 종료 | 15:39:20에 end_part와 Day 0 2부 종료 | runtime.log 610–612행 |
| Outside 전환 | 15:39:25에 Spot 5개·Entity 8개 등록, CommuteOut | runtime.log 616–622행 |

Play 씬의 기존 설정은 skipTycoonForTest=1, testDay=0입니다. 따라서 1부 손님 운영 전체를 검증한 결과가 아닙니다.
선택지 선택 분기·Outside 대화 전체·Home·다른 일차·모바일 빌드는 이번 기록만으로 완료 판정하지 않습니다.

## 확인된 C파트 미완료 문제: 플레이 정지 중 UI 정리 예외

- 시각: 15:35:23, 플레이 종료 → Edit Mode 전환 시점.
- 상황: 0일차 진토닉 제조 준비 대기 중 플레이가 정지됐습니다.
- 오류: `MissingReferenceException: The object of type UnityEngine.GameObject has been destroyed but you are still trying to access it.`
- 실제 호출 경로:
  1. `StoryScriptRunner.RunAsync()` 108행: finally에서 `presenter.Clear()` 호출.
  2. `BarStoryPresenter.Clear()` 196행: `choiceView?.CloseChoices()` 호출.
  3. `UIDialogueChoiceView.CloseChoices()` 142행: 이미 파괴된 `choicesPanel`에 `SetActive(false)` 호출.
- 증거: runtime.log 103행부터의 원문 스택과 events.log의 플레이 모드 전환 기록.
- 판정: CSV 데이터 로딩 오류가 아니라 **C파트 3번의 취소·종료 시 UI 수명 처리와 관련된 미완료 문제**입니다. 데이터 로드는 이미 완료됐고, 실패 위치도 CSV 파서가 아닌 파괴된 UI 접근입니다.
- 영향 범위: 중간 정지 시 예외가 발생합니다. 이어진 별도 플레이에서는 제조·서빙 후 0일차가 정상 종료됐으므로, 이 오류를 정상 진행 전체가 불가능하다는 근거로 확대하지 않습니다.
- 필요한 보완: 파괴된 UI에 대한 정리를 건너뛰고, 실행 종료의 finally 정리에서 발생한 예외가 취소 결과를 덮어쓰지 않도록 처리해야 합니다.

게임 코드는 이 검증 단계에서 추가 수정하지 않았습니다. 임시 검사 스크립트는 제거했으며, 검사 시작 전 백업한 ScriptableObject 69개와 디스크 파일이 동일한 것을 확인했습니다.
