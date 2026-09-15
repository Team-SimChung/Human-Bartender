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
