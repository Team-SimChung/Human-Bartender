# 웹 CSV 반입: `b2f8ca7` → `24022cd`

기준: `junseo874/junseo874.github.io` `24022cdec49a23200d5500a3e8deef931267206d`("용석이를 위한 데이터 수정").
절차는 `ContentAuthoring/README.md`와 같다(읽기 전용 검사 → Unity `CsvContentValidator.ValidateBatch` → `--apply`).

## 바뀐 데이터

ID 열을 빼고 값만 비교한 결과다. 반입 도구 기준으로 바뀌는 파일은 6개다.

| 파일 | 내용 |
|---|---|
| `story/steps.csv` | 대사 문구 수정(3일차 바, 조니, 밤 장면, 엔딩, 동네·상점·거지 이벤트, 0일차 크리스 등). 신문 남자 선택지가 `result_steps` 구조로 들어왔다. 웹이 넣었다가 뺀 `expression` 스텝은 이번 커밋에 없다. |
| `story/scenes.csv` | 거리 장면 9개 추가(신문 읽는 남자 7개, 기신교 신도 2개), 테스트 장면 `t99_fxsfx` 삭제 |
| `story/street_choices.csv` | 소지금 부족 문구 6개를 "돈이 부족합니다."로 통일, 신문 남자 선택지 2개 추가 |
| `story/bar_choices.csv`, `bar_choice_groups.csv` | 오타 1개("어린아이"), `t99_fxsfx` 관련 3행 삭제 |
| `presentation/values.csv` | 강조 표시 3행의 부모 행 수정(`kihyun_night1_13` → `_12`), 원본 BOM |

- 맥주·병따기를 포함한 `craft/`, `settings/`, `characters/`, `interaction/` 값은 그대로다.
- 손님 외형(`guests/guest_parts.csv`, `guest_exclusions.csv`, `guest_settings.csv`)과 `system/data_sets.csv`는 웹이 Unity 판을 정본으로 받아들여 지금 Unity 파일과 같다. 외형 덮어쓰기 충돌이 없다.

## 웹 실행 계약(`web_runtime_contract.json`)

`b2f8ca7` 이후 바뀐 계약 파일 5개를 검토하고 `reviewed_commit`과 blob을 `24022cd`로 갱신했다.

| 웹 파일 | 변경 | Unity 반영 |
|---|---|---|
| `story-flow.js` | 걸어 들어온 손님은 등장 연출 뒤 1초 쉬고 다음 대본으로(기존 0.8초 고정) | `StoryScriptRunner.EnterAsync`: 등장 묶음에 `entrance != seated`가 있으면 페이드인 뒤 1초 대기 |
| `story-flow.js` | 대사 효과(`text_tags`) 컴파일 | Unity `DialogueTextCompiler`가 이미 처리한다. 변경 없음 |
| `story-flow.js` | `expression` 스텝 | 데이터에서 빠졌다. 지원하지 않는다 |
| `outside-beggar.js` | 등장을 0일차 퇴근길로 한정, 지불 선택지 "네, 믿을게요 · 200원" | 선택지 문구 반영(`StreetStoryEvents`). Unity에는 거지를 거리에 배치하는 로직이 원래 없어 등장 조건은 바꿀 곳이 없다 |
| `outside-neighborhood.js` | 보스 선택지 "( 대화 그만두기 )", "승강기는 이미 고쳤습니다." / "승강기 고쳤습니다.", "돈으로 살게요 · 500원" | `NeighborhoodStoryEvents` 반영 |
| `outside-shop.js` | 웹 내장 예비 대사 문구 | Unity는 CSV `shop/*` 장면을 읽어 반입으로 맞춰진다. 하드코딩 문구 변경 없음 |
| `tools/lib/shared-data-views.cjs` | 내용을 `runtime-projection.js`로 옮기고 다시 내보냄 | 웹 전용 투영이라 변경 없음. 옮겨 간 `runtime-projection.js`를 계약에 추가했다 |

## 범위 밖(후속)

- 신문 읽는 남자·기신교 신도 거리 이벤트(웹 `outside-newspaper.js`, `outside-believer.js`)의 등장 조건·상호작용. 이번에는 데이터만 들어오고 Unity에서는 실행되지 않는다.

## 반영 상태

- 읽기 전용 검사: 통과(38개 묶음, CSV 48개, 바뀐 파일 6개).
- Unity `ValidateBatch`와 `--apply`: Unity 에디터가 프로젝트를 열고 있어 배치 모드를 실행하지 못했다. 에디터를 닫은 뒤 진행한다.
