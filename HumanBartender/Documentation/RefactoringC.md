# C 리팩토링 진행 기록

현재 작업 기준: `c861bc6e` 이후 C 로컬 변경. 2026-09-15, Unity 6000.3.14f1.
PDF의 지시문은 제안으로 검토하며, 사용자가 확정한 CSV 전용 데이터 방식이 우선합니다.
씬 GUID, 기존 게임 ID와 enum 값, 레시피 계산·입력 순서를 유지합니다.

## 1–7 진행 상태

| 순서 | 항목 | 현재 상태 |
|---|---|---|
| 1 | 씬 연결 복구 | Outside의 누락 컴포넌트 5개를 현행 NPC 컴포넌트로 연결. 폐기된 트리거·히스토리 참조와 날짜 강제 설정 정리 |
| 2 | 실행·조건·효과·데이터 로딩 계약 | 조건 불충족과 오류 구분, 실행별 조건 상태, 효과 실패 전달, 데이터 전체 로딩 후 공개 처리 반영 |
| 3 | 바 스토리 실행 | 타이핑 스킵/실행 취소 분리, 실패·취소 정리, 실행별 조건 상태 반영 |
| 4 | Outside 실행 | 준비→좌표 등록→초기화, CSV action/priority 적용, 출입구 연결, 엘리베이터·라디오·로고의 취소/복원 반영. A의 공통 입력·전환 결과 계약 연결은 후속 |
| 5 | 제조 연결 | 동일 제조 세션만 완료 수락, 취소 시 구독·C UI 정리 반영. **B의 실제 제조 CancelAsync와 실패 결과가 없어 자식 종료 대기는 미완료** |
| 6 | 컷신 | 실제 Timeline 정지 후 그래프 정리 대기, 취소·핸들·Signal 대사 CSV 이관, 하위 트랙 전환 수명 반영. 예약/미지원 콘텐츠는 별도 |
| 7 | 편집 데이터와 도구 | 참조·문법·리소스 검사, 캐시 Inspector, 캐릭터/레이아웃 도구 유지. 주소 10개 수정 및 Excel 동기화. 미등록/예약 콘텐츠 오류 목록은 유지 |

이후 요청에 따라 A/B API 완료 후 연결할 부분을 제외하고 C의 독립적인 SSOT 정리를 추가 반영했습니다. 카메라 교체/취소, 사운드 중복·빈 풀 방어, Spine 반복 재생 취소도 포함합니다. 미구현 Home 저장·다음 날 흐름, 예약 리소스·QA 콘텐츠까지 전체 완료한 것은 아닙니다.

## 다른 담당자에게 전달할 문서

- [A 인수인계: 상태·씬 전환·로더·공유 리소스](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Documentation/Handoff/RefactoringC_To_A.md)
- [B 인수인계: 제조 종료·취소·서빙·정산](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Documentation/Handoff/RefactoringC_To_B.md)
- [C 전체 구조·남은 SSOT 문제](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Documentation/RefactoringC_SSOT.md)
- [이번 검증 근거와 한계](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Documentation/Validation/RefactoringC/README.md)

A/B 코드는 이번 C 변경에서 수정하지 않았습니다. 인수인계에는 구현 우선순위·소스 근거·재현 조건·완료 기준을 적었습니다. C 코드에는 실행 소유권과 취소 이유를 설명하는 짧은 주석을 추가했습니다.

## 확정된 Excel → CSV 흐름

- 편집 엑셀 2개와 `export_content.py`, `system` CSV 규칙은 `outputs/20260915-content-editing`에 있습니다.
- 게임·에디터 검사는 `Assets/StreamingAssets/csv`의 CSV를 직접 읽습니다. 데이터 JSON의 생성·읽기 단계는 제거했습니다.
- 기존 게임 데이터 JSON 37개를 삭제했습니다. Unity 패키지·도구의 JSON 설정은 이 전환 대상이 아닙니다.
- `story`, `characters`, `interaction`, `craft`, `guests`, `settings`에 편집 표 42개를 배치하고, 내부 연결 표 5개를 `system`으로 분리했습니다.
- `BalanceRoot_d3bb059b7` 등 변환용 해시 ID를 읽을 수 있는 고정 ID로 교체했습니다. 밸런스 부모는 `balance_root`이며 게임의 기존 `id`·`dialogue_id`는 유지했습니다.
- 필드 별칭은 기존 C# 데이터 선언에서 읽지만 JSON 역직렬화는 실행하지 않습니다.
- CSV 폴더와 엑셀 `Index`에 파일 경로와 편집 안내를 포함했습니다.

## 편집 규칙

- Excel의 헤더는 5행, 데이터는 6행부터입니다. ID는 문자열이며 순서와 별개입니다.
- 빈 Excel 셀은 null, `\E`는 빈 문자열입니다. CSV null은 `\N`입니다. 0과 false는 실제 값으로 보존합니다.
- UTF-8 BOM과 CSV 따옴표 규칙으로 한글·쉼표·따옴표·실제 줄바꿈을 보존합니다.
- 행 형식 규칙이 원래 없는 필드와 null을 구별합니다. 같은 종류의 기존 행을 복사하고 새 고유 `row_id`와 부모·순서를 지정합니다.
- 조건부 대체 장면은 같은 seq를 사용할 수 있습니다. 목록 저장 순서는 `source_order`로 관리합니다.
- 위치·Transform은 Unity 씬에서 편집합니다. CSV 전환이 미등록 Home·Quest·Ending 콘텐츠의 실행 구현을 의미하지는 않습니다.

## 이전 핵심 C 실행 검증 — 이력

- 런타임·에디터 어셈블리 전체 Roslyn 컴파일 통과. 기존 경고는 남아 있습니다.
- 별도 Unity 프로젝트 복사본에서 `RefactoringCExecutionTests` **26/26 통과**. 실제 Outside 콜드 시작, Play 씬의 Day 0 대사 시작 후 Play Mode 종료, Timeline 일시정지·재개·자연 종료를 포함합니다.
- 단위 검증으로 취소/실패 정리, 이전 선택지·제조 세션 이벤트 차단, once, 조건/효과 문법과 검증 도구의 CSV/SO 비변경을 확인했습니다.
- C 정적 콘텐츠 검사는 **오류 51 / 경고 8**입니다. 예약·QA·미구현 콘텐츠까지 포함하며 현재 Day 0 실행 실패 개수가 아닙니다. 검증 도구 테스트 통과가 콘텐츠 오류 0을 의미하지 않습니다.
- 26개 통과는 정리 전 검증 기록입니다. 이후 사용자 요청으로 Editor의 리팩토링 확인용 테스트와 전용 보고서 저장 함수를 삭제했습니다. 런타임 실행 로직과 CSV 검사 규칙은 유지했습니다.
- 전체 제조→서빙→하루 종료 재플레이, 모든 CSV 분기, Home, 모바일/IL2CPP 빌드, 모든 Signal 연출은 이번 검증 범위 밖입니다.

## Editor 정리

- 삭제: `RefactoringCExecutionTests.cs`와 `.meta`, 테스트 전용 `WriteBatchReport` 함수, 내용이 없던 `CutsceneTestTool.cs`와 `.meta`.
- 유지: CSV 콘텐츠 검사 메뉴, C 캐시 읽기 전용 Inspector, 캐릭터 애니메이션·컷신 레이아웃/제작 도구.
- A/B 소유의 CSV 파서 테스트·공통 CSV 검사·제조 검증/설정 도구는 변경하지 않았습니다.
- 과거 XML·검증 보고서는 이력으로 보존했습니다. 현재 Test Runner에서 삭제된 C 테스트를 실행할 수 있다는 의미는 아닙니다.
- 당시 정리 후 삭제 코드의 잔여 참조가 없고 컴파일을 통과했습니다. 이후 추가 C 작업은 별도 Unity 복사본에만 임시 검증 코드를 두고 Play를 재실행했습니다. 원본 Editor에는 테스트 장치를 다시 추가하지 않았습니다.

## 추가 C 정리와 최신 근거

- [SSOT 구조와 후속 경계](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Documentation/RefactoringC_SSOT.md)에 편집 원본·런타임 캐시·실행 소유권을 정리했습니다.
- 최신 Unity 결과는 **11/11 통과**입니다. `Documentation/Validation/RefactoringC/CRemaining.EditMode.xml`과 같은 폴더의 README에 실제 검증 범위를 기록했습니다. 위 26개 이력과 실행 건수를 합치지 않습니다.
- 현재 C 정적 검사는 **오류 38 / 경고 12**입니다. 주소 수정 10개, 씬 전용 Timeline 3개의 범위 구분을 반영했습니다. 지원되지 않는 QA·예약 콘텐츠는 검사에서 숨기지 않았습니다.
- Excel의 기존 값·수식·서식을 보존하고 삼호 대사 장면 1행/대사 3행을 추가했습니다. 재내보내기한 37개 데이터 묶음이 현재 CSV와 값·타입·순서·null까지 일치합니다.

## 이전 CSV 전환에서 확인한 범위

- 기존 데이터 37개와 CSV 복원 결과의 값·타입·순서·누락 필드 일치.
- 실제 로더 대상 30개에서 기존 역직렬화 결과와 CSV 직접 매핑 결과 일치.
- 저장된 Excel에서 대사 편집·행 추가·CSV 내보내기·재읽기 검증.
- 중복 ID, 끊긴 부모 연결, 잘못된 정수·행 형식, 허용되지 않은 열 값과 CSV 따옴표 오류 거부 검증.
- 별도 프로젝트 복사본의 Unity EditMode 테스트 7개 통과(실패 0). 테스트 결과: `Documentation/Validation/CsvContent.EditMode.xml`.
- 배치 실행 초기화에서 기존 CustomToolbar 플러그인의 `FindMainToolBarWindow()`가 창 배열 접근 오류를 기록했습니다. CSV 테스트는 모두 통과했으며, 이 플러그인의 창 없는 실행 대응은 별도 에디터 개선 항목입니다.
- 이후 이번 C 작업에서 확인한 Play 범위는 위에 별도로 기재했습니다. 이전 CSV 전환 검증과 이번 테스트를 합쳐 실행 건수를 부풀리지 않습니다.

커밋·푸시·배포는 하지 않았습니다.
