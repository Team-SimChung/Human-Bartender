# Human Bartender CSV 편집

## 편집 파일

- `HumanBartender_C_Content.xlsx`: 대사·장면·선택지·캐릭터·상호작용·컷신. 편집 표 18개.
- `HumanBartender_Game_Settings.xlsx`: 날짜·밸런스·칵테일·레시피·손님·UI 설정. 편집 표 24개.
- `system/`: 행 형식과 연결 규칙을 담은 CSV 5개. 스키마를 변경할 때만 수정합니다.
- `export_content.py`: Excel을 CSV로 내보내는 도구. Python 3.9 이상, 추가 패키지 없음.

게임은 `Assets/StreamingAssets/csv`의 CSV를 직접 읽습니다. JSON 파일을 읽거나 생성하는 단계는 없습니다.
기존 데이터 묶음 37개의 값·타입·순서·누락 필드를 보존했습니다.
`Index`에서 표를 찾고 `FieldGuide`에서 열의 의미를 확인하세요. C 엑셀의 `StepTypes`에는 스텝별 arg 설명도 있습니다.

## CSV 폴더

게임 폴더와 내보내기 결과는 동일한 구조입니다.

| 폴더 | 편집 내용 |
|---|---|
| `story/` | 대본·선택지·컷신 |
| `characters/` | 캐릭터·표정·동작·정보 |
| `interaction/` | 거리·집 상호작용·대화 연결·장소 |
| `craft/` | 칵테일·레시피·재료·주문 판정 |
| `guests/` | 손님 등장·성격·대사·외형 |
| `settings/` | 날짜·밸런스·UI·전환 |
| `system/` | 표 연결·형식 규칙, 일반 편집 때 유지 |

`BalanceRoot_d3bb059b7` 같은 해시 ID는 읽을 수 있는 `balance_root`로 바꿨습니다.
이는 밸런스 하위 표의 부모 ID이며 실제 설정값은 `settings/balance_config.csv`에 있습니다.
`steps_format_01` 등의 번호는 행 형식 종류이며 실행 순서가 아닙니다.
엑셀 **Index**의 CSV 경로 열에서 각 시트가 어떤 파일로 내보내지는지 확인할 수 있습니다.

## 편집 방법

1. 데이터 시트의 **6행부터** 수정합니다. 5행 헤더의 이름과 순서는 유지합니다.
2. 노란 영역은 콘텐츠, 회색 영역은 연결·형식 정보입니다. 잠금이나 매크로는 없습니다.
3. `id`와 `dialogue_id`는 게임의 기존 참조 ID입니다. `row_id`는 표의 행을 연결하는 고정 ID입니다.
4. `dataset_id`는 데이터 묶음입니다. `source_id`는 상호작용 오브젝트 ID이므로 둘을 구별합니다.
5. `parent_id`는 부모 표의 `row_id`입니다. `@데이터묶음`은 최상위 목록을 가리킵니다.
6. 새 행은 **같은 유형의 기존 행 전체를 복사**합니다. `row_id`에 새 고유값을 입력하고 부모와 `source_order`를 맞춥니다. `template_id`는 복사한 값을 유지합니다.
7. `source_order`는 같은 부모·필드 안에서 중복 없는 0 이상의 정수입니다. 실행 순서인 `seq`와 목록의 저장 순서인 `source_order`를 구별합니다.
8. 부모를 삭제할 때 하위 행도 정리합니다. 연결이 끊어진 행은 내보내기 오류가 납니다.

### 대사 수정 예

- `Steps`에서 `context = d2_bar_open`으로 필터합니다.
- `text.ko`와 `text.en`을 수정하고 기존 `dialogue_id`와 `row_id`는 유지합니다.
- 바 선택지 문구는 `BarChoices`에 있습니다. `context`에 선택지 묶음 이름이 표시됩니다.
- 거리 선택지 결과 대사는 `Steps`에 있습니다. `parent_id`가 해당 `StreetChoices.row_id`를 가리킵니다.

### 빈 값과 타입

- 빈 셀 또는 `\N`: 값 없음(null).
- `\E`: 빈 문자열.
- 실제 문자열이 `\`로 시작하면 첫 `\`를 한 번 더 씁니다. 실제 `\N` 문자는 엑셀에 `\\N`으로 입력합니다.
- 숫자는 숫자로, ID는 텍스트로 입력합니다. `0`과 `false`는 빈 값과 다릅니다.
- 데이터 표의 수식은 지원하지 않습니다. 쉼표·따옴표·실제 줄바꿈·텍스트 태그는 그대로 보존합니다.
- 원래 없던 필드는 CSV 스키마가 따로 구별합니다. 선택한 `template_id`에 없는 열을 채우면 오류가 납니다.

## 내보내기

두 엑셀과 `system` 폴더, Python 도구를 함께 둡니다. 이 폴더의 터미널에서 실행합니다.

```sh
python3 export_content.py
```

같은 위치의 `csv` 폴더에 **편집 표 42개 + 구조용 CSV 5개**가 생성됩니다.
Excel의 “다른 이름으로 저장 → CSV”는 현재 시트만 저장하므로 위 도구를 사용하세요.
CSV는 UTF-8 BOM과 표준 따옴표 규칙을 사용합니다.

프로젝트 루트에서 편집한 엑셀을 게임에 반영하려면 다음 명령을 실행합니다.

```sh
python3 outputs/20260915-content-editing/export_content.py --out Assets/StreamingAssets/csv
```

Unity 메뉴 **Tools → Data → Validate CSV Content**에서 데이터 구조와 로더 타입을 검사합니다.
제조 규칙은 기존 **Tools → Craft → Validate Craft Data**에서 검사하며, 이 도구도 CSV를 읽습니다.

CSV를 직접 편집했다면 다음 명령으로 구조를 검사할 수 있습니다.

```sh
python3 outputs/20260915-content-editing/export_content.py --source csv --input Assets/StreamingAssets/csv --validate-only
```

CSV 직접 수정과 Excel 편집을 섞으면 오래된 엑셀에서 다시 내보낼 때 변경이 덮어써질 수 있습니다.
팀에서는 편집 경로를 하나로 정하고 CSV 변경 내용을 Git으로 검토하세요.

## 구조용 CSV

| 파일 | 역할 |
|---|---|
| `table_index.csv` | 표 이름·상대 파일 경로·편집 영역 |
| `columns.csv` | 열 순서와 값 타입 |
| `row_formats.csv` | 중첩 필드·목록·사전의 연결 규칙 |
| `data_sets.csv` | 데이터 묶음의 루트 연결 |
| `balance_root.csv` | 밸런스 하위 표를 연결하는 루트 행 |

이 파일들은 일반 콘텐츠 편집 때 유지합니다. 새 필드·새 묶음·새 객체 구조를 만들 때는 스키마를 확장합니다.
`ScriptFiles`와 `GuestSettings`의 기존 루트 `row_id`도 유지합니다.
`quests`는 현재 비어 있으며 새로운 퀘스트 레코드는 스키마 확장이 필요합니다.

## 검증 범위

표 헤더·행 ID 중복·부모 연결·저장 순서·타입·행 형식을 검사합니다.
조건식 문법과 실제 실행 시점, 리소스 주소·타입, 게임 내 장면 도달 여부는 별도 실행 검증 대상입니다.
`SourceInventory`의 “로더 등록”은 기능 전체가 구현됐다는 의미가 아닙니다.

런타임은 기존 C# 데이터 타입을 그대로 사용합니다. 기존 `JsonProperty`의 필드 이름만 매핑에 재사용하며 JSON 역직렬화는 실행하지 않습니다.
