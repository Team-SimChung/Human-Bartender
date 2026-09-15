# CSV 폴더 안내

게임은 이 폴더의 CSV를 직접 읽습니다. 데이터 JSON을 생성하거나 읽는 단계는 없습니다.
총 **편집 표 42개 + 구조용 표 5개**이며, 파일은 용도별로 나누었습니다.

## 어디에서 편집하나요?

| 폴더 | 내용 | 자주 여는 파일 |
|---|---|---|
| `story/` | 대본·선택지·컷신 | `steps.csv`, `scenes.csv`, `bar_choices.csv` |
| `characters/` | 캐릭터·표정·동작 | `characters.csv`, `expressions.csv` |
| `interaction/` | 거리·집 상호작용 | `interact_points.csv`, `dialogue_flows.csv` |
| `craft/` | 칵테일·레시피·재료 | `cocktails.csv`, `recipes.csv`, `shelf_items.csv` |
| `guests/` | 손님 등장·성격·대사·외형 | `random_waves.csv`, `barks.csv` |
| `settings/` | 날짜·밸런스·UI·전환 | `balance_config.csv`, `days.csv`, `ui_strings.csv` |
| `system/` | 표 연결·형식 규칙 | 일반 콘텐츠 편집 때 유지 |

각 폴더의 README에 모든 파일의 역할과 엑셀 시트 이름을 적었습니다.
편집용 엑셀과 내보내기 도구는 프로젝트의 `outputs/20260915-content-editing`에 있습니다.
엑셀의 **Index**에서 표와 CSV 경로를, **FieldGuide**에서 열 설명을 찾을 수 있습니다.

## BalanceRoot 뒤에 붙던 해시는?

`BalanceRoot_d3bb059b7`은 변환 과정에서 만든 **부모 행의 내부 ID**였습니다.
뒤의 문자열은 연결 ID가 겹치지 않도록 붙인 해시이며, 밸런스 수치나 게임 기능을 뜻하지 않습니다.
현재는 **`balance_root`**로 정리했습니다. 관련 하위 표의 `parent_id`도 함께 변경했습니다.
시작 금액 같은 실제 값은 `settings/balance_config.csv`에서 편집합니다.

- `row_id`: 행을 연결하는 고정 ID. 이름에 있는 번호는 실행 순서가 아닙니다.
- `parent_id`: 부모 행의 `row_id`. `@데이터묶음`은 최상위 목록입니다.
- `template_id`: 행이 사용하는 필드 구성. `steps_format_01` 등의 번호는 형식의 종류입니다.
- `id`, `dialogue_id`: 게임이 참조하는 기존 ID. 이번 정리에서 변경하지 않았습니다.

기존 연결 ID는 유지하고, 새 행은 같은 종류의 기존 행을 복사한 뒤 고유한 `row_id`를 지정합니다.
조건에 따라 필드 구성이 달라지므로 `template_id`는 복사한 값을 유지합니다.

## 게임에 반영하기

엑셀을 저장한 뒤 프로젝트 루트에서 실행합니다.

```sh
python3 outputs/20260915-content-editing/export_content.py --out Assets/StreamingAssets/csv
```

Unity의 **Tools → Data → Validate CSV Content**로 구조와 데이터 타입을 검사합니다.
CSV를 직접 수정했다면 아래 명령으로도 검사할 수 있습니다.

```sh
python3 outputs/20260915-content-editing/export_content.py --source csv --input Assets/StreamingAssets/csv --validate-only
```

Excel과 CSV를 동시에 따로 수정하면 다음 내보내기에서 CSV 변경을 덮어쓸 수 있으므로 팀의 편집 원본을 하나로 정하세요.
CSV의 빈 값/null은 `\N`, 빈 문자열은 `\E`이며, 쉼표·따옴표·줄바꿈은 표준 CSV 규칙을 따릅니다.
