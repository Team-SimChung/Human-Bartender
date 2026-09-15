# 대본·선택지·컷신

대사 수정은 steps.csv의 text.ko / text.en, 장면 조건은 scenes.csv, 바 선택 문구는 bar_choices.csv를 수정합니다.

| CSV 파일 | 엑셀 시트 | 설정하는 내용 |
|---|---|---|
| `cutscenes.csv` | Cutscenes | 컷신 ID·재생 종류·리소스 주소 |
| `script_files.csv` | ScriptFiles | 대본 파일의 장소·기본 날짜. 하위 Scene 표와 연결 |
| `scenes.csv` | Scenes | 장면의 실행 날짜·단계·조건·순서 |
| `steps.csv` | Steps | 대사·등장·퇴장·주문·효과 등 실행 단위. 결과 대사도 포함 |
| `bar_choice_groups.csv` | BarChoiceGroups | 바·공통 대본의 선택지 묶음 ID |
| `bar_choices.csv` | BarChoices | 바·공통 대본의 선택 문구·조건·효과·이동 장면 |
| `street_choices.csv` | StreetChoices | 거리 대본의 선택 문구·조건. 결과 Steps의 부모 |

[전체 폴더 안내](../README.md)

## 대사·선택지·장면을 추가할 때

1. 편집 원본으로 쓰는 Excel 또는 CSV에서 같은 유형의 행을 복사합니다.
2. 새 `row_id`와 필요한 게임 ID를 부여합니다. 대사는 `dialogue_id`, 장면은 `id`가 중복되지 않게 합니다.
3. `parent_id`는 실제 부모 행을, `source_order`는 그 부모 안에서의 저장 순서를 가리킵니다. `seq`는 실행 순서입니다. 기존 거리 선택지의 결과 스텝처럼 `seq`가 없는 목록은 저장 순서를 유지합니다.
4. 선택지의 `goto`/스텝의 `scene_id`에 적은 장면을 함께 추가합니다. 바 선택지는 같은 바 대본 안에서, 거리 goto는 거리 대본 안에서 찾습니다.
5. `Tools → Data → Validate CSV Content`로 표 구조를, `Tools → Story → Validate C Content`로 C 실행 규칙과 참조를 검사한 뒤 Play를 다시 시작합니다. 현재 실행 중인 대본에 실시간 반영하는 도구는 아닙니다.

검증기는 실행할 때마다 CSV에서 ID 목록을 다시 만듭니다. 새 대사 ID마다 검증 코드에 상수를 추가하지 않습니다. 새 `type`, 새 스키마/표, 새 날짜 데이터 묶음, 새 Unity 씬 이동 동작은 소비 코드와 등록도 필요합니다.

### 조건식은 선택 사항

- `when`이 비어 있으면 조건 없이 실행합니다. 직접 CSV를 편집할 때 null 표기는 `\N`이며 Excel에서는 빈 셀입니다.
- `when`은 **실행해도 되는지**, `effects`는 **성공한 뒤 무엇을 바꿀지**입니다. 대사와 선택지 모두 항상 둘을 채우는 방식이 아닙니다.
- 예: `grade >= excellent`는 잘 만든 경우에만 해당 대사를 선택하고, `!flag.shiba_met`는 아직 시바를 만나지 않았을 때만 장면을 엽니다.
- 조건을 전용 열(필요 플래그·최소 등급 등)로 구조화하는 것은 가능합니다. 현재는 기존 문자열 문법을 유지하며, 조건을 코드와 CSV 양쪽에 복제하지 않습니다.
- 검사기는 게임과 같은 ConditionUtil 문법을 사용합니다. 검사를 위해 실제 플래그·돈·호감도를 변경하지 않습니다. 문법 검사가 모든 플레이 상태에서의 분기 도달 가능성을 보장하지는 않습니다.

### 현재 확장 제한

- 바/거리 대본의 `goto`는 CSV 장면 ID 연결입니다.
- 문의 `action_ref`는 Unity 씬 이동 동작을 뜻합니다. 현재 InteractEntrance는 CSV의 `action_ref` 대신 Inspector의 목적지를 사용하므로, CSV만 추가해 새 출입 동작이 연결되지는 않습니다. C의 추가 정리 대상입니다.
- 새 날짜 대본은 A 담당 NewDataLoadManager의 `barDayNumbers` 등록도 필요합니다. 현재 기본 목록은 0·1·2·3·99입니다.
- 컷신 리소스 파일·Addressables 등록은 데이터 행과 별도로 실제 에셋이 필요합니다. 공통 Addressables 그룹은 A 담당이며 이번 작업에서 수정하지 않았습니다.
