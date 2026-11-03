# 웹 본편 0일차 흐름 이식 체크리스트

기준: `junseo874.github.io/bar-playtest/` 본편(웹 HEAD `aa201e9`; 이전 반영 `b2f8ca7`과의 차이는 텍스트박스 스킨 경로뿐).
웹 JS 구조는 들여오지 않고, 동작만 기존 Unity 구조 위에 C#으로 옮겼다. 정본 CSV는 수정하지 않았다.

## 항목별 상태

| # | 웹 동작 | Unity 반영 | 상태 |
|---|---|---|---|
| 1 | 새 게임 → 플레이 안내 → 검은 화면 도입 → 집 | `NewGameOpening`(Home)이 Day 0·출근·`flag.opening_seen` 없음일 때 실행. 도입 컷신은 `cutscenes.csv`의 `tl_intro`를 `OptionalStoryCutscene`으로 재생하고, 리소스가 없으면 건너뛴다. Play 직전의 `luna-dream` 인트로와 클릭 대기 안내는 제거했다. | 완료 (컷신 리소스 대기) |
| 2 | 이동·달리기 / 상호작용 안내(1/2, 2/2) | 같은 오프닝에서 기존 튜토리얼 오버레이(`StoryTutorialView.ShowPageAsync`)로 표시한다. 문구·입력 잠금 시간은 `NewGameOpening` 인스펙터에서 편집한다. 닫으면 "첫 출근하기!" 목표 팝업을 띄운다. | 완료 (글자 위주, 웹의 캐릭터 그림·키캡 아트는 없음) |
| 2-1 | 전화 선택지 튜토리얼 패널(W/S/휠, E) | 웹에서는 도입 컷신 안의 크리스 전화 선택지에 붙는다. 전화 대사는 CSV에 없고 도입 컷신에 속하므로 컷신 제작 때 함께 넣는다. | 보류 |
| 3 | 0일차 첫 출근: 1층 바 문 왼쪽에서 걸어 들어옴 | `OutsideStorySceneRules.ResolveSpawn`이 시작 자리를 정한다. OutSide 씬의 `FirstCommuteSpawn`(-1.55, -0.7)에서 시작해 `FirstCommuteArrival`이 `WalkTarget`까지 걷고, 바에 들어가기 전까지 `Bounds` 벽을 켠다. 엘리베이터는 이 날 1층에서 대기한다. | 완료 |
| 3-1 | 도착 혼잣말 "바 '언노운' 여기인가보네." | CSV에 없다(웹 JS 하드코딩). | 데이터 공백 |
| 4 | 바 문 로딩 화면(최소 3초, 문 열림, 인사 문구) | `BarDoorTransitionView`를 `SceneTransitionManager`에 연결했다. OutSide→Play는 `BarEntrance`, Play→OutSide는 `BarExit`. 그림은 이미 들여온 `Web_GRD_bar_door_*` 레이어를 `Tools/Story/Build Bar Door Transition`으로 조립했다. | 완료 (배치는 수동 확인 필요) |
| 5 | 제조 튜토리얼 시작 | `craft tutorial_recipe` / `tutorial_beer` 인자를 해석하지 못해 시작되지 않던 문제를 고쳤다. 별칭 표는 `CsvGameContentRules` 한 곳에 있고, 실행기와 검증기가 같은 함수를 쓴다. | 완료 |
| 5-1 | 바 튜토리얼 단계 | 문구를 `StoryTutorialController`의 직렬화 표(`copy`) 하나로 모았다. 레시피 열기 → 확인 → 닫기, 붓기의 목표량 → 현재량 단계를 추가했다. 키 안내는 현재 키 설정을 따른다(좌석 이동은 Unity 기본 Q/E). | 완료 |
| 5-2 | 바 선택지 "E키나 마우스 좌클릭으로 꾹 눌러 선택" 안내 | `ChoiceHoldButton`은 있지만 안내 문구가 없다. | 남음 |
| 5-3 | 셰이크·스터 2쪽 가이드, Day 1 병따기 수업 | 기존 한 줄 안내만 있다. | 남음 |
| 6 | 코스터 위 잔 위치 | `StoryServeZone.prefab`을 120×140(피벗은 책상 표면 선)으로 바꾸고 Coaster·Drink·EmptyGlass 앵커를 넣었다. 코드에서 이미지를 만들던 부분은 `StoryServeZoneLayout`으로 대체했다. 서빙 캔버스(960×540)와 바 카메라가 웹 스토리 카메라(폭 960px)와 1:1이라 웹 수치를 그대로 썼다. | 완료 (수동 확인 필요) |
| 6-1 | 빈 잔 그림(맥주·칵테일·롱드링크) | Unity에 테이블용 빈 잔 그림이 없어 선반 잔 그림을 쓴다. | 리소스 공백 |

## CSV 연결 ID

웹 원본(팀장 웹 데모 포함)은 `row_id`·`parent_id`·`template_id`가 `shared_<해시>`이고 `context`가 비어 있다.
Unity 사본은 반입 때 `expressions_tom_default`처럼 읽을 수 있는 이름으로 바뀌고 `context`가 채워진다.
게임 데이터는 원본과 같다(조립 결과 비교로 확인). 장기적으로는 웹 내보내기에서 처음부터 읽을 수 있는 ID를 쓰는 편이 좋다.

## 에디터 도구 (다시 실행해도 기존 배치를 보존한다)

- `Tools/Story/Set Up First Commute Arrival`: OutSide 첫 출근 시작 위치·도착 지점·경계
- `Tools/Story/Set Up New Game Opening`: Home 오프닝 오브젝트와 연결
- 서빙 자리 앵커는 공용 월드 프리팹 `TableServeSpot`의 `GlassAnchor`에서 조정한다.
- `Tools/Story/Build Bar Door Transition`: 문 화면 프리팹을 다시 조립하고 연결한다(프리팹을 새로 만든다)

## 다른 작업자 변경과의 충돌 (`443211d` 이후)

| 파일 | 내용 | 조치 |
|---|---|---|
| `characters/expressions.csv`, `expression_parts.csv` | LeeDongHun `93f93878`의 톰 파츠 애니메이션 행이 웹 CSV로 교체되면서 사라졌다(톰이 `tom_default` 정지 그림으로 정의됨). | 정본(웹) 쪽에 톰 파츠 행을 추가하거나 Unity 반영 규칙을 정해야 한다. 미조치. |
| `story/cutscenes.csv` | LeeDongHun `735e01a0`이 정리한 컷신 행(`tl_intro` 등)이 웹 CSV 반입으로 다시 생겼고, `tl_raid_1` 메모도 원래대로 돌아갔다. | 오프닝이 `tl_intro`를 쓴다. 어느 쪽을 정본으로 할지 결정이 필요하다. 미조치. |
| `OutSide.unity` 카메라 Viewport | batchmode 저장에서 4:3 레터박스 값이 기록되었다. | 커밋된 값으로 되돌렸다. |
| 그 밖의 씬·프리팹 | 다른 작업자가 바꾼 오브젝트는 모두 보존되어 있다. 삭제된 에셋을 참조하거나 되살린 파일은 없다. | — |
