# C 실행·콘텐츠 검증 근거

2026-09-15 · Unity 6000.3.14f1 · `c861bc6e` 이후 C 변경

## 최신 추가 C 검증

**11 통과 / 0 실패.** 창 있는 별도 Unity 프로젝트 복사본에서 실행했다. 임시 테스트는 복사본에만 두었으며 원본 Assets/Editor에는 검증 장치를 추가하지 않았다.

| 검증 | 확인한 범위 |
|---|---|
| 실제 Play 씬 | Day 0 스토리 시작, 대사 3초 진행 후 Play Mode 종료 |
| 실제 OutSide 씬 | CSV 준비 완료, 본체/라디오 ID·동작·부모 연결, 중복 라디오 제거 |
| 엘리베이터·로고 | 이동 취소, 비활성화, 다음 정상 탑승, 플레이어 부모·입력·위치·벽·화면 알파/차단 복원 |
| 실제 삼호 Timeline | 씬 자산 재생, CSV Signal 대사 호출, 취소 후 대사 정리, 재실행 |
| 실제 씬 전환 | CSV enter_home → 기존 A SceneTransitionManager → Home 준비 완료. commute_in 조건에서 exit_home 연결 확인 |
| 카메라 | 취소 시 PPC 복원, 이전 줌 완료가 새 줌을 덮어쓰지 않음, offset 교체, 비활성화 |
| Spine | 예제 SkeletonDataAsset의 실제 반복 트랙, 취소/비활성화 후 트랙 제거, 재재생 |
| UI Timeline | 실제 PlayableDirector/Resolution 트랙의 취소와 자연 종료 복원, 이전 전환의 늦은 쓰기 차단 |
| CSV 우선순위 | 정방향/역방향 후보 배열에서 동일 선택, phase/조건 필터, 엔티티가 CSV Definition을 직접 사용 |
| 사운드 | 중복 키 거부, SE 풀 0 보정 |
| C 검증기 | CSV·씬 파일 비변경, 삼호 씬 바인딩 범위 구분, 이관 대사 내용·화자·주소 확인 |

근거: **CRemaining.EditMode.xml**, **Unity.CRemaining.Excerpt.txt**. XML의 상위 suite에는 프로젝트가 발견한 다른 테스트 수도 나오지만 선택 실행한 test-case는 11개다. 과거 26개 이력과 합산하지 않는다.

### 실제 재현하여 수정한 오류

- 첫 실행: 10개 중 9개 통과. 라디오와 엘리베이터 종료가 같은 UniTask를 동시에 대기하면서 continuation 중복 등록 오류 발생. 공통 완료 신호로 수정.
- 두 번째 실행: 11개 중 9개 통과. 승강기 비활성화 도중 Unity가 플레이어 부모 변경을 거부했다. 부모를 유지하고 이동량을 전달하도록 수정.
- 같은 실행에서 Timeline 자연 종료 직후 Canvas 해상도 복원이 아직 끝나지 않은 상태가 관찰됐다. stopped 콜백 뒤 Unity 그래프 정리까지 기다리도록 수정.
- 세 번째 실행: **11/11 통과**. 마지막 수정 후 실제 씬 검증 전체를 다시 실행했다.

원래 실패 메시지와 스택 근거: **CRemaining.FixedFailures.txt**. 수정 전 실패를 A/B 문제로 넘기지 않았다.

## 컴파일·데이터·변경 범위

- 런타임/에디터 Roslyn 컴파일: 각각 exit 0. 기존 경고는 남는다. Assembly-CSharp.Current.Compile.txt 및 Assembly-CSharp-Editor.Current.Compile.txt.
- C 정적 콘텐츠 검사: **오류 38 / 경고 12**. 전체 항목은 CContentReport.Current.txt.
- 컷신 주소 10개를 수정했다. 삼호 Timeline 3개는 씬에 바인딩된 자산으로 분류한다. 경고는 해당 씬의 director가 필요하다는 범위 안내이며 전역 Addressables 등록을 뜻하지 않는다.
- 예약/QA의 미구현 Fx/Sfx·reputation/give/Gif, 미등록 컷신·포스터·초상화는 그대로 진단한다. 이 숫자는 Day 0 실행 오류 개수가 아니다.
- Excel의 기존 값 변경은 컷신 resource_key 10셀뿐이다. 기존 수식·스타일·시트·표·병합·틀 고정을 보존하고 장면 1행/대사 3행을 추가했다. 기존 Index 수식은 Scenes=65, Steps=668로 재계산됐다.
- Excel → CSV 재내보내기 → 재읽기의 37개 데이터 묶음이 현재 CSV와 값·타입·순서·null까지 일치한다. Workbook.Current.txt.
- 작업 시작 시 스냅샷과 비교해 A/B 코드, 공유 Main/Play 씬, CSV schema 및 Game Settings 엑셀 변경 0건. 공유 프리팹·Addressables·설정 변경도 없다. ScopeAudit.Current.txt.
- CSV의 기존 UTF-8 BOM/CRLF를 유지했다. `git -c core.whitespace=cr-at-eol diff --check` 통과.

## 해석과 한계

- 날짜/phase는 테스트 조건으로 지정했다. 승강기·로고 시간은 복사본의 런타임에서 단축했다. 원본 씬을 테스트 설정으로 저장하지 않았다.
- 삼호 검증은 실제 자산과 CSV에 Signal 대사 진입을 호출한 범위다. 모든 Signal 시각·모든 Timeline 연출을 눈으로 확인한 것은 아니다.
- UI Timeline 전환 검증에는 임시 Timeline과 실제 트랙 구현을 사용했다. Spine은 프로젝트에 있는 예제 자산을 사용했다.
- Home에 도착하고 CSV 출입구 연결을 확인했다. 저장·취침·자동 다음 날 설정·일일 정산은 A/B 계약 대기 항목이며 구현/검증 완료로 분류하지 않는다.
- 전체 제조→서빙→하루 종료, B 제조 자식 작업의 실제 취소, 모든 날짜/선택지 분기, 모바일/IL2CPP 빌드는 이번 범위 밖이다.
- 기존 LightingData/SRP 머티리얼 등의 경고가 남아 있으므로 콘솔 전체 경고 0이라고 말하지 않는다.

## 이전 검증 이력

CExecution.EditMode.xml의 **26/26 통과**는 이전 핵심 실행기 변경 당시 결과다. CContentReport.txt의 **51 오류 / 8 경고**도 그 시점의 정적 검사다. 이전 로그·컴파일·ScopeAudit.txt를 보존했다.

그 후 사용자 요청으로 원본 Editor의 RefactoringCExecutionTests.cs, 해당 meta, WriteBatchReport, 빈 CutsceneTestTool을 삭제했다. 최신 검증을 위해 이 파일들을 원본에 복구하지 않았다.

현재 상시 CSV 검사는 **Tools → Story → Validate C Content**와 읽기 전용 C Inspector에서 실행한다. 실행기 임시 테스트는 배포용 Editor 도구가 아니다.
