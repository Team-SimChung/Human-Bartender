# A 담당자 인수인계 — C 연동 추가 점검

2026-09-15 · 기준 커밋 `c861bc6e` + 현재 C 로컬 변경 · Unity 6000.3.14f1

대상은 공통 상태·로딩·씬 전환·LifetimeScope·공유 리소스를 맡은 **A 담당자**다. 아래 A 코드는 이번 작업에서 수정하지 않았다. 파일 링크는 조사한 로컬 경로이며, 다른 PC에서는 `HumanBartender` 아래 동일 파일을 찾으면 된다. 줄 번호는 이 문서 작성 시점 기준이다.

**P1:** 실행 정지·상태 오염을 막기 위해 먼저 처리. **P2:** 콘텐츠 확장 또는 운영 방식 확정 시 처리. “코드 확인”은 소스에서 확인한 경로이며, 아래 재현 절차를 모두 실제 실행했다는 뜻은 아니다.

## 권장 순서

| 순서 | 항목 | 우선순위 | 완료 시 C가 받을 것 |
|---|---|---|---|
| 1 | A-01 씬 전환 결과·수명 | P1 | 시작 수락/거절과 실제 종료 결과를 기다리는 API |
| 2 | A-02 상태 변경의 독립성·이벤트 | P1 | 호감도/카르마 독립 변경, 확정된 돈 상태 알림 |
| 3 | A-03 새 게임·재진입 초기화 | P1 | DI 등록과 분리된 세션 초기화 규칙 |
| 4 | A-04 새 날짜 CSV 등록 | P2 | 등록 절차 또는 동적 목록, 누락 진단 |
| 5 | A-05 하루 정산 수명 — B 공동 | P1, 일일 정산 연결 전 | 씬을 넘어 보존되는 단일 정산 원본 |
| 6 | A-06 리소스·배치 검증 환경 | 활성 콘텐츠는 P1, 나머지 P2 | 합의한 주소/타입과 CI 실행 방식 |

## A-01. 씬 전환 전체를 하나의 작업으로 관리

**근거 — 코드 확인**

- [SceneTransitionManager.cs:37](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Tool/SceneTransitionManager.cs:37): `LoadScene`은 void이고, `isFading`이면 결과 없이 반환한다.
- [같은 파일:69](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Tool/SceneTransitionManager.cs:69): 바깥 로드 함수가 `isFading=true`를 설정하지만, 내부 `FadeAsync`가 101행에서 false로 만든다. 따라서 첫 페이드 후 실제 씬 로드 중에는 두 번째 요청을 차단하지 못하는 구간이 있다.
- [같은 파일:91](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Tool/SceneTransitionManager.cs:91): 페이드에 호출자 취소 토큰·예외 시 복원을 보장하는 finally가 없다.
- C 출입구는 이제 CSV action_ref/phase를 사용하며, void 요청 때문에 입력 상태를 잠그지 않는다. 조건 불일치로 입력이 잠기는 C 문제는 수정했다. A의 거절·실패·완료 결과를 전달받을 수 없는 경계는 남아 있다.

**요청**

- 작업 하나가 페이드 시작부터 씬 로드·후속 페이드·정리까지 잠금과 결과를 소유하도록 한다.
- 기존 void 호출의 호환성을 유지하면서, 수락/거절 및 성공/실패/취소를 기다릴 수 있는 API를 제공한다. 함수 이름은 A가 결정한다.
- Unity 네이티브 씬 로드가 이미 시작된 뒤 취소의 의미를 명시한다. 대기만 끝내고 실제 로드가 뒤늦게 진행되는 상태를 “취소 완료”로 보고하지 않는다.
- 입력 잠금은 현재 작업의 소유자만 반환하도록 공통 계약을 제시한다. C는 출입구·엘리베이터에서 이 계약을 사용한다.

**완료 기준**

- [ ] 첫 페이드가 끝난 직후 두 번째 요청을 넣어도 수락 정책이 일관되고 중복 로드가 발생하지 않는다.
- [ ] 잘못된 씬 이름·파괴된 페이드 오브젝트·중도 취소가 호출자에게 결과로 전달된다.
- [ ] 성공/실패 후 입력 차단이 남지 않으며, 이전 작업의 정리가 새 작업의 잠금을 풀지 않는다.

## A-02. CSV 효과가 한 상태만 바꾸도록 보장

**근거 — 코드 확인**

- [PlayerDataSO.cs:79](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Player/PlayerDataSO.cs:79): `AddNewCharacter(id, val)`은 호감도와 카르마를 모두 val로 초기화한다. 미등록 캐릭터의 호감도/카르마 Add·Set 함수가 모두 이 함수를 사용한다(88·100·113·125행).
- 예: 초기화 직후 `AddCharacterAffinityAmount("new_actor", 3)`은 호감도뿐 아니라 카르마도 3으로 만든다. C의 호감도 효과가 의도하지 않은 다른 상태까지 바꾸는 경로다.
- [같은 파일:44](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Player/PlayerDataSO.cs:44): `AddMoney`는 음수 보정 전에 요청 delta로 이벤트를 발행한다. 잔액 5에서 -10이면 구독자가 읽는 순간 잔액이 -5이며, 실제 최종 변동 -5와 이벤트 -10도 다르다.

**요청**

- 캐릭터 기본 항목 생성과 개별 능력치 변경을 분리한다. 호감도 변경은 기존 카르마를 유지하고 반대도 같아야 한다.
- 돈 이벤트가 “요청 delta”, “실제 반영 delta”, “최종 잔액” 중 무엇인지 확정한다. 상태 보정과 확정을 마친 뒤 알린다.
- 이벤트 구독자 예외가 발생했을 때 상태 변경 성공 여부를 정의한다. C가 실패로 받은 효과를 재실행해 이중 반영하지 않도록 한다.

**완료 기준**

- [ ] 미등록/기등록 캐릭터 각각에 호감도·카르마 Add/Set을 호출해 다른 능력치가 유지된다.
- [ ] 잔액 5에서 -10, 충분한 잔액의 TrySpend, 구독자 예외에서 최종 상태와 반환 결과가 일치한다.
- [ ] C의 조건/효과 문법을 A 쪽에 다시 구현하지 않는다. A는 상태 읽기·변경 계약을 담당한다.

## A-03. DI 등록과 새 게임 초기화를 분리

**근거 — 코드 확인, 재진입 시나리오 검증 필요**

- [ProjectLifetimeScope.cs:15](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Tool/ProjectLifetimeScope.cs:15): Configure가 PlayerData와 PlayerSettlement를 초기화한다.
- [GameStateManager.cs:51](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/GameStateManager.cs:51): 정적 인스턴스 최초 생성 때만 Init이 실행되고, Init은 private이다. 스코프 재생성 때 플레이어 상태와 일차/흐름의 초기화 수명이 달라질 수 있다.

**요청·완료 기준**

- [ ] 새 게임 / 이어하기 / 씬 재진입에 대한 명시적인 초기화 진입점을 둔다. 의존성 등록만으로 플레이어 데이터가 지워지지 않게 한다.
- [ ] Domain Reload 켜짐·꺼짐에서 두 번 Play, Main→Play→Outside→Main 재진입 후 일차·플래그·재화·정산의 기대값을 확인한다.
- [ ] Outside의 once 기록 범위(현재 매니저 인스턴스 수명)를 유지할지, 날짜/세이브로 확장할지 정한다. 영구 저장은 이번 C 구현에 포함되어 있지 않다.

## A-04. 새 날짜 CSV가 검증만 되고 로딩되지 않는 상황 방지

**근거 — 코드 확인**

- [NewDataLoadManager.cs:21](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/DataNew/NewDataLoadManager.cs:21): `barDayNumbers = { 0, 1, 2, 3, 99 }`.
- [같은 파일:173](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/DataNew/NewDataLoadManager.cs:173): 이 목록에 있는 날짜만 읽는다. `LoadOptionalAsync`는 없는 source를 null로 처리한다(276행).
- C 검증기는 CSV source를 수집해서 검사한다. 검증 통과와 런타임 로더 등록은 별개다.

**요청·완료 기준**

- [ ] 기존 날짜에 대사/선택지 행 추가는 코드 변경 없이 반영된다.
- [ ] 새 `script/bar/day4`를 추가할 때 명시적 등록을 유지할지 카탈로그로 자동 수집할지 정하고 편집 절차에 적는다. day4는 예시이며 실제 콘텐츠 추가 요청은 아니다.
- [ ] 누락된 필수 날짜, 의도적으로 비어 있는 날짜, 미등록 날짜를 구별해서 알린다.
- [ ] 기존 `WaitUntilLoadedAsync`와 전체 로딩 성공 후 공개 계약은 유지한다. 이 부분은 이미 적용되어 있다.
- [ ] 런타임 재로딩을 지원하려면 `LoadVersion` 변경 통지/소비 계약을 C와 맞춘다. 현재 C 스팟 등록은 최초 준비 후 한 번이며, 실시간 재로딩은 지원 약속이 없다.

## A-05. 일일 매출의 보존 범위 — B와 공동 결정

**근거 — 코드 확인, UI/저장 연결 여부 확인 필요**

- B의 [GuestManager.cs:72](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Tycoon/GuestManager.cs:72)가 `DailySales` 인스턴스를 가지고, 초기화 시 188행에서 Reset한다. C 매출도 같은 Sales에 반영한다.
- A의 [PlayerSettlement.cs:18](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/03.Scripts/Player/PlayerSettlement.cs:18)는 판매수량·팁·유지비를 별도로 보관하고 루트에 등록된다. 현재 Assets의 C# 검색에서 이 AddSalesQty/AddTip/SetCost의 외부 호출은 발견되지 않았다. 이 SO가 실제 화면용 원본인지 레거시인지 확인이 필요하다.

**요청·완료 기준**

- [ ] B와 함께 정산 원본을 하나로 정하고, 나머지는 조회용 변환 또는 폐기 대상임을 명시한다. C에 새 장부를 만들지 않는다.
- [ ] Play 씬을 떠나기 전에 합계와 항목이 어디로 전달되고, 어떤 시점에 다음 날 기록이 초기화되는지 정한다.
- [ ] 1부 1건 + 2부 1건 정산 후 Outside 이동·정산 화면 재진입에도 두 건이 유지된다. 중복 완료 알림은 한 번만 반영되고 다음 날만 비워진다.

## A-06. 공유 Addressables와 검증 실행 환경

**리소스 근거 — C 정적 검증에서 검출**

| C CSV 설정 | 현재 공유 그룹의 주소 | 처리 주체 |
|---|---|---|
| `Intro_lab` | `Intro_lab` | C에서 자산 대조 후 수정 완료 |
| `Finished_GinFizz` | `Finished_GinFizz` | C에서 자산 대조 후 수정 완료 |

- [cutscenes.csv:3](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/StreamingAssets/csv/story/cutscenes.csv:3), [같은 CSV:12](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/StreamingAssets/csv/story/cutscenes.csv:12).
- [Default Local Group.asset:29](</Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset:29>), [같은 그룹:94](</Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset:94>).
- 주소를 CSV의 오타에 맞춰 일괄 변경하지 않는다. A는 실제 자산·주소·타입을 확인하고, 정말 필요한 추가 등록/별칭만 공유 그룹에 반영한다. C가 활성/예약 콘텐츠를 구분해 목록을 전달한다.
- 이번에 CSV 주소 10개를 수정했고 편집용 Excel도 동기화했다. 공유 그룹 변경은 없다. 삼호 3개는 씬에 직접 연결한 Timeline이며 전역 Addressables가 아니다. 남은 INTRO·꿈/Day13 컷신·포스터·초상화 주소는 `Documentation/Validation/RefactoringC/CContentReport.Current.txt`에 있다. 자산을 준비한 뒤 활성 범위와 기대 타입을 C와 확인한다.

**배치 환경 근거 — 이전 배치 실행에서 재현**

- [ToolbarSystem.cs:141](/Users/yongseokpark/Documents/Github/Human-Bartender/HumanBartender/Assets/Plugins/CustomToolbar/Editor/ToolbarRegister/Unity6000_3/ToolbarSystem.cs:141)가 창 없는 환경에서 `toolbars[0]`을 접근한다.
- 창 있는 별도 Unity 복사본에서는 C 테스트 **26/26 통과**했다. 무창 CI가 필요할 경우 A 또는 공통 에디터 도구 담당자가 빈 창 목록/batch 실행을 처리한다.
- 이 플러그인 문제와 미등록 콘텐츠를 “C 실행기 컴파일 실패”로 묶지 않는다.

## C 담당자가 계속 맡는 것

CSV action_ref→출입구 연결, priority 전달·후보 선택, 승강기 취소·입력 복원, Signal 대사 CSV 이관은 C에서 반영했다. Home 진입은 기존 씬 전환 API로 확인하며, 저장·취침·다음 날 전환까지 완료했다고 판정하지 않는다. Timeline·대화 연출과 콘텐츠 문법/주소 편집은 계속 C 소유다.

**Home 후속 계약:** A가 home_context와 출입 허용 여부, 수동 저장/저장 후 취침 결과, 다음 날 전환 시점을 제공해야 한다. B와 정산 보존 범위를 결정한 뒤 C가 `home_sofa_interaction` 및 다음 날 출입구에 연결한다. C에는 두 번째 날짜 상태나 별도 저장 상태를 만들지 않았다. 일반 대사 추가마다 A의 분기 코드를 추가해 달라는 요청은 아니다.

`when`은 조건부 콘텐츠에만 사용한다. 비워 둔 행도 실행할 수 있다. 조건 문법과 평가기는 C, 조건이 읽는 실제 게임 상태는 A가 담당한다.
