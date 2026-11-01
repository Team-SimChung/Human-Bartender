# 웹 CSV 정본 반영과 Unity 책임 경계

> 2026-11-02 후속 구조 변경: `Unity_Story_Ownership_2026-11-02.md`가 현재 사건 실행 구조를 설명한다.
> 초기 웹 호환 처리기는 제거했고, Unity C# 사건 처리와 기존 Cutscene Studio 경로로 교체했다.

## 대조에서 확인한 문제와 교체 시 주의점

| 확인한 차이 | 단순 교체 시 문제 | 반영한 대응 |
|---|---|---|
| `story/steps.csv`가 1,416행·20열에서 1,610행·36열로 확장됨 | 새 메타데이터를 버리면 무료 주문·화자·외부 사건 동작이 달라진다. | 추가 필드를 DTO에 보존하고 기존 실행기로 연결한다. |
| 웹 스키마의 `source_order` 선언과 실행 순서에 필요한 정수 표현 차이 | 배열 조립 순서와 바 대본 `seq`가 달라질 수 있다. | 구조 정렬용 정수 변환과 공개 전 순서 검사를 둔다. |
| 코스터·빈잔이 주문 이후에도 방문 중 유지되는 웹 대본 | 주문 UI 종료와 함께 테이블을 지우거나 제조 결과를 빈잔으로 바꾸면 상태 소유권이 섞인다. | 방문 표시 상태만 별도로 보유하고 주문·제조·정산 소유자는 유지한다. |
| 집 대본이 바의 공통 선택지 그룹을 참조함 | 집 실행기에 없는 선택지로 중단될 수 있다. | 이미 로드된 선택지 배열을 조회한다. |
| 사건의 일부 선택·효과는 웹 JS가 구성함 | CSV 열만 맞춰도 실행 의미가 같다고 보장할 수 없다. | 기존 조건·진행 경계로 연결하고 관련 JS 14개 변경 시 다음 수입을 차단한다. |
| 웹 리소스 키와 Unity Sprite 주소가 다름 | 정본에 Unity 주소를 덧쓰면 웹과 Unity의 데이터 원본이 갈라진다. | 엔진별 리소스 주소표를 공통 CSV 밖에 둔다. |
| 과거 Excel 역수입 경로가 남아 있음 | 오래된 문서가 최신 웹 CSV를 덮어쓸 수 있다. | 웹 정본 기록이 있는 기본 흐름에서 Excel 역수입을 거부한다. |

후속 변경에서도 웹 명령 처리기에 별도 돈·재고·진행 상태를 만들지 않는다.
새 행동이 현재 소유자의 계약을 넘으면 그 계약의 확장 여부를 먼저 검토한다.
특히 수량 재고, 새 저장 커서, 전용 연출은 CSV를 읽었다는 이유만으로 구현 완료로 처리하지 않는다.

## 정본과 반영

- 정본: `junseo874/junseo874.github.io`, `bar-playtest/data/shared-csv`.
- 기준 커밋: `b2f8ca7a524f19363d0f8c8af6fa075e5b5661b4`.
- 공통 데이터: 38개 묶음, 편집 CSV 43개와 구조 CSV 5개. 값·순서·서식은 원본과 같다.
  2026-11-02부터 연결 ID(`shared_<해시>`)만 읽기 쉬운 이름으로 바꿔 게시한다(`ContentAuthoring/readable_ids.py`, 대응표 `web_id_map.json`).
  웹 원본 해시는 `web_content_source.json`의 `web_files`에 남고, 바꾼 뒤 조립한 게임 데이터가 원본과 같아야 게시된다.
- `ContentAuthoring/web_content_source.json`은 반영 커밋과 파일별 SHA-256을 기록한다.
- `web_runtime_contract.json`은 네이티브로 연결한 웹 실행 모듈 14개의 Git blob을 기록한다.
  새 커밋에서 사건·선택·주문 실행 규칙이 바뀌면 자동 반영을 거부한다. 대응 구현과 이 계약을 함께 검토해야 한다.
- `.gitattributes`에서 공통 CSV의 줄바꿈 자동 변환을 차단한다. BOM과 셀 안 줄바꿈도 원본이다.
- `import_web_content.py`는 커밋의 Git blob과 입력을 대조하고 구조·게임 ID·바 대본 순서·선택지 참조를 검사한다.
- 기본 실행은 읽기 전용이다. `--apply`에서만 반영한다. 직전 사본의 로컬 수정은 충돌이며 덮어쓰지 않는다.
- `--apply`는 성공한 Unity 검사 결과 JSON을 요구한다. 검사 후 CSV가 바뀌면 해시 불일치로 반영을 거부한다.
- 쓰기 실패 시 이번 실행이 교체한 파일과 manifest를 되돌린다. 타깃 외부의 파일을 삭제하지 않는다.
- Excel 역수입은 차단한다. 기존 XLSX, `ContentAuthoring/system/`, `csv_owned_datasets.json`은 이전 편집 흐름의 기록이다.

## 유지하는 SSOT와 소유권

| 영역 | 소유자 | 반영 원칙 |
|---|---|---|
| CSV 구조·변환 | CsvContentCatalog / CsvDataReader | 게임 행동과 효과를 실행하지 않는다. |
| 데이터 공개 | NewDataLoadManager | 전체 타입 매핑과 게임 참조 검사 후 SO와 파생 캐시를 갱신한다. |
| 일차·출퇴근·씬 전환 | GameProgressionService / ISceneTransitionService | `start_phase`와 사건 이동도 이 경계를 거친다. |
| 플레이어 값 | PlayerDataSO / IPlayerDataReader·Writer | 웹 호환용 돈·호감도·플래그 복사본을 만들지 않는다. |
| 조건·효과 | IConditionUtil | 실행별 scope, 실패 처리와 중복 효과 방지를 유지한다. |
| 주문 | OrderRequestController / OrderRequest | 대본은 요청·결과 대기만 담당한다. |
| 제조 결과 | CraftSession / CraftedDrink | 주문한 술과 실제 만든 술을 구분한다. 빈잔으로 제조 결과를 수정하지 않는다. |
| 판정·정산 | ServeProcessor / DailySales | 웹 명령에서 서빙 금액을 직접 지급하지 않는다. |
| 방문별 테이블 | CraftServingView 내부 StoryTableState | 두 손님의 코스터·잔은 분리한다. 주문이 끝나도 방문 상태는 유지하고 퇴장·실행 정리에서 제거한다. |
| 외부 사건 | OutsideStoryEvents / 도메인별 처리기 / 기존 WorldInteraction | CSV 명령을 기존 선택 UI·효과·표현·진행 요청으로 연결한다. 영속 상태를 보유하지 않는다. |
| 리소스 | ResourceLoader / unity/resource_bindings.csv | 에셋 타입과 주소만 보정한다. 대사·가격·조건·보상을 넣지 않는다. |

## 대본 호환

- 바와 거리 실행기는 분리된 현재 계약을 유지한다. 바의 배열 순서와 `seq` 불일치는 공개 전에 오류로 처리한다.
- `empty_glass`는 해당 방문의 표시 상태만 변경한다. 잔이 없으면 만들지 않는다. 다음 제공은 다시 채워진 상태다.
- 일반 `coaster`는 실제 드롭을 기다린다. 코스터 공급은 드롭 완료 후 트레이로 복귀하고 테이블에는 표시용 사본을 유지한다.
- 튜토리얼 안내와 일반 코스터는 같은 방문 상태를 사용한다. 튜토리얼 종료로 방문 코스터를 삭제하지 않는다.
- `web_fields.payment`, `who`, 외부 명령과 `cinemaAfter`를 보존해 실행한다. 무료 주문은 유료로 바뀌지 않는다.
- 집의 `choice`가 공통 바 선택지 그룹을 참조하는 경우 같은 로드된 선택지 배열을 사용한다.
- `neighborhood-*` 신규 사건의 Unity 위치·진입 연결은 OutsideStorySceneRules와 Unity 씬의 WorldInteractionTarget에 둔다. 대사와 결과 행은 웹 CSV에서 조회한다.
- 미지원 명령은 예외로 보고한다. 필수 행동을 처리한 것처럼 성공시키지 않는다.
- 99일차·`script/qa/`·`qa/`는 구조·ID·타입을 검사하되 본편 조건식 지원 범위와 분리한다.
  평판 등 미구현 기능을 시험하는 QA 효과를 맞추려고 현재 평판 소유권이나 저장 스키마를 확장하지 않는다.
  해당 QA 효과를 실제 실행하면 기존 조건 처리기가 명시적으로 거부한다.
  본편 문법 검사는 연결된 바·집·거리·상호작용 묶음을 대상으로 한다. 현재 실행 소비자가 없는
  `script/common`의 `give(...)`와 보관용 컷신/ambient 묶음 때문에 본편 로드를 차단하지 않는다.
- 코스터·대본·주문 콜백은 실행 취소와 객체 파괴 시 기존 정리 경로를 거친다.

## 검증 명령

```powershell
$csvStage = Join-Path $env:TEMP 'HumanBartenderWebCsv-b2f8ca7'
$csvReport = Join-Path $env:TEMP 'HumanBartenderCsvValidation.json'
$csvLog = Join-Path $env:TEMP 'HumanBartenderCsvValidation.log'
python -B ContentAuthoring/import_web_content.py --commit b2f8ca7a524f19363d0f8c8af6fa075e5b5661b4 --stage $csvStage
& 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe' -batchmode -nographics -projectPath $PWD.Path -executeMethod CsvContentValidator.ValidateBatch -csvContentPath $csvStage -csvValidationReport $csvReport -logFile $csvLog | Out-Null
python -B ContentAuthoring/import_web_content.py --input $csvStage --compatibility-report $csvReport --apply
python -B ContentAuthoring/export_content.py --validate-only
python -B -m unittest discover -s ContentAuthoring -p test_import_web_content.py -v
```

Unity 검사: `CsvContentValidator.ValidateBatch`, `StoryTableStateTests`, `WebCsvCompatibilityTests`, `WebContentRuntimeSmokeTests`, `OutsideStoryEventTests`, `OutsideStoryCutsceneLifetimeTests`.
실제 씬 검사는 Main → Home → OutSide → Play의 기존 진행·DI 경로를 사용하며 종료 후 에디터 씬 구성을 복원한다.

## 리소스와 검수 범위

현재 월드 행동은 씬 소유 C# 담당자가 실행하고, 실제 컷신만 기존 Studio 재생 경로를 사용한다.
16개 Studio 에셋이 필요하다는 중간 판단은 철회했다. 기억 대본은 정본 CSV에서 일시적인 Studio 표시 데이터로 투영한다.
현재 책임·수명·검증은 `Unity_CSV_Runtime_Followup_2026-11-02.md`를 참고한다.
최종 시각 검수는 0~3일차 전체 선택 분기, 실제 제조·서빙, 새 상호작용 위치, 컷신·연출 자산을 포함해야 한다.

저장 스키마는 일차·재화·스킬·호감도·플래그를 유지한다. 웹의 정수 재고는 이 프로젝트의 기존 단일 획득 플래그로 연결하므로
수량형 인벤토리 확장은 별도 작업이다. CSV 행 ID를 저장 진행 커서로 새로 사용하지 않는다.

## 최초 CSV 반영 시 확인 결과

아래 수치는 2026-11-01 CSV 반영 검사의 기록이다. 후속 Unity 구조 검사는 `Unity_Story_Ownership_2026-11-02.validation.json`에 별도로 남긴다.

- 공통 CSV 48개: 고정 커밋의 Git blob과 일치하며 재반영 검사에서 변경 파일 0개.
- 구조: 38개 데이터 묶음 조립 성공. Unity의 기존 로더 타입 32개 매핑 성공.
- Python 반영 도구: 테스트 9개 통과. 로컬 수정 충돌, CSV·기록 파일의 I/O 오류 복구, 원본 경로 제한,
  웹 실행 모듈 변경 차단, 검사 이후 CSV 변경 차단을 포함한다.
- 새 Unity 테스트 10개 통과: Main → Home → OutSide → Play 실제 씬 진입,
  방문별 코스터·빈잔 분리와 초기화, 무료 주문, 웹 필드 보존, 중복 용돈·재료 지급 방지.
  최종 집중 재검사에서도 10개 모두 통과했다. 용돈은 수락 대사 이후 지급하며,
  갱단 보상은 갱단 획득 플래그만 기록하고 상점 구매 플래그를 만들지 않는다.
- 제조 데이터: 칵테일 29종과 선반 아이템 44개, 오류 0건·경고 2건.
- 전체 EditMode: 39개 중 32개 통과·4개 건너뜀·3개 실패.
  실패는 `VisualUIReviewTests.CommonOptionsKeepsItsWidgetsAppliesDisplayExplicitlyAndRestoresFocus`,
  `RuntimeMenuWiringAndGlitchRestoreUseTheActualUnityLifecycle`, `ServiceAndRecipeControlsKeepTheirOwnersAndUseCurrentArt`.
  각각 옵션 UI NullReference, 배치 입력 폴링, 기존 레시피 그림 경로 기대값에서 실패했다.
  이 검사들의 옵션/입력 코드·테스트·Play 씬·레시피 행 프리팹은 이번 변경 대상이 아니다.
  CSV 작업의 회귀라고 단정하거나 전체 프로젝트 검사가 통과했다고 보지 않는다.
- Unity 배치 검사와 회귀 검사 후 프로젝트 설정·루트 프리팹의 정규화된 내용이 기존과 동일함을 확인했다.

전체 선택 분기의 수동 플레이와 전용 사건 애니메이션·컷신의 시각 검수는 별도로 남는다.
