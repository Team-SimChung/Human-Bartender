# 웹 본편 1일차 맥주 병따기 이식

기준: `junseo874.github.io/bar-playtest/` 본편(웹 HEAD `24022cd`. 병따기 코드의 마지막 변경은 `19cc25b`).
웹 JS 구조는 들여오지 않고, 동작만 기존 Unity 구조(기믹 큐, 튜토리얼 문구 표) 위에 C#으로 옮겼다. 정본 CSV는 수정하지 않았다.

## 항목별 상태

| # | 웹 동작 | Unity 반영 | 상태 |
|---|---|---|---|
| 1 | 병따기 판정: 힘 게이지(초록 0.78±0.152, perfect ±0.08) → 0.45초 대기 → 링 타이밍(1.1초 ±0.14, 1.24초 자동 실패) | `CapOpenRules`(순수 C#)가 `gimmick-mechanics.js` OPEN을 그대로 옮겼다. 수치는 `CapOpenTuning` 기본값이다. | 완료 |
| 1-1 | 실패 3회(힘·링 합산)면 병이 깨지고 1.2초 뒤 힘 조절부터 다시 | 같다. 실패 수는 깨질 때만 0으로 돌아가고, 게이지 방향도 웹처럼 유지한다. | 완료 |
| 1-2 | 최종 perfect = 힘 perfect && 링 오차 ≤ 0.065초 | 같다. 예전 Unity 규칙(실패 수 ≤ 허용치 20%)은 지웠다. | 완료 |
| 2 | 열린 뒤 탄산 2.05초가 지나야 "다음 →"이 켜지고 눌러야 넘어감 | `CapManager`가 `ICraftGimmickManualEnd`·`ICraftGimmickOwnHud`(진행 표시·다음 버튼)를 구현한다. 예전처럼 0.9초 뒤 자동으로 넘어가지 않는다. | 완료 |
| 2-1 | 병따기 시간은 한 잔의 제조 시간에 넣지 않음 | `IsManualInputActive => false` | 완료 |
| 3 | 화면: 할로, 병 흔들림, 뚜껑 비행·비틀림(skew), 병따개 타격, 링·글로우, 게이지, perfect/성공 버스트, 금, 파편, 병 입구 | `CapOpenView`가 `open-view.js` 수식을 그대로 쓴다. 웹 캔버스 좌표(1280×720)를 기믹 카메라에 맞춘 무대에 `CapCanvasLayer`(캔버스 변환 행렬 그대로)로 그린다. | 완료 |
| 3-1 | 탄산 기포 44개, 15fps, 병 윤곽 클립·라벨 제외 | `CapCarbonation`이 같은 의사난수·수식으로 화면 해상도 텍스처에 그린다. | 완료 |
| 3-2 | 글자·버튼: 단계 라벨, PERFECT!/POP!/MISS/다시 시도, Space 힌트, 누르기 버튼, 다음 버튼 | 1280×720 기준 캔버스에 CSS 위치·크기·색 그대로 둔다. 글꼴은 웹 LunaPixel과 같은 Stardust(`Regular_PFStardust_3_0_SDF`). | 완료 |
| 4 | 소리: 뚜껑(피치 1.16/1.08), 0.45초 뒤 올라오는 탄산 꼬리, 합성 실패음 | `CapOpenAudio`. 같은 wav를 쓰고 탄산 곡선·실패음 합성을 옮겼다. 일시정지·종료 때 즉시 끊는다. | 완료 |
| 5 | 1일차 맥주 준비 수업: 레시피 → 맥주 선택 → 잔 → 도구 선반 이동 → 병따개 → 냉장고 이동 → 병맥주 → 제조 시작 | `StoryTutorialController` 맥주 단계를 웹 순서·문구로 맞췄다(`beer*` 키). 맥주 수업은 레시피 메모 확인 단계를 건너뛴다. | 완료 |
| 6 | 병따기 수업: 가림막(게이지 칸 또는 병 둘레 원 + 두 버튼 자리), SPACE 안내 상자, 단계별 문구 6종 | 병따기 화면이 직접 그린다(`CapOpenView.SetLesson`). 문구는 튜토리얼 문구 표(`open*` 키)에서 온다. | 완료 |
| 6-1 | 수업을 마치면 `flag.day1_open_taught = true` | 서빙 때 남긴다. 웹처럼 아직 읽는 곳은 없다. | 완료 |
| 7 | 병따개 경고 팝업 "병따개가 필요합니다" | 팝업 문구와 병따개 설명("맥주병이나 와인병을 열 때 쓰는 도구.")을 웹과 맞췄다. 조건은 데이터(`prep_action=open`) 기준으로 둔다. | 완료 |

## 의도적인 차이

- **점수**: 웹은 열기만 하면 100점이다. Unity는 실패 수를 결과에 담고 `CraftJudge`가 `open_penalty_per_failure`(15)만큼 깎는다(결정 사항).
- **튜토리얼 입력 차단**: 웹은 수업 중 잘못된 선택·뒤로가기를 막는다. Unity는 0일차와 같이 안내만 한다(결정 사항).
- **CSV의 `open_*` 값**: 웹은 `open_start_radius_px`·`open_target_radius_px`만 그리기에 쓰고, 나머지(`open_approach_sec` 1.6, `open_judge_window_px`, `open_transition_delay_sec`, `open_input_enabled_after_sec`)는 읽지 않는다. Unity도 같다.
- **근사**: 웹 뚜껑 소리의 highshelf(+1.5dB) 필터는 생략했다. 링 글로우(shadowBlur 12)는 옅은 고리를 겹쳐 흉내 낸다. 화면 축소 움직임 설정(reduced motion)은 Unity에 없어 항상 꺼진 것으로 본다.

## 에디터 도구 (다시 실행해도 같은 결과)

- `Tools/Crafting/Set Up Web Cap Gimmick`: `NewCapGame.prefab`의 예전 링 연출을 지우고 `CapOpenView`·`CapOpenAudio`를 연결한다. 웹 원본 그림(`04.Minigames/04.Open/`)의 가져오기 설정(Point, 무압축)도 맞춘다.
- `Tools/Story/Sync Tutorial Copy`: Play 씬 튜토리얼 문구 표를 코드 단계 목록에 맞춘다. 인스펙터에서 고친 문구는 보존한다.
  배치 모드로 씬을 저장하면 카메라 Viewport에 레터박스 값이 기록될 수 있다. 저장 뒤 `Play.unity` 변경이 문구 표뿐인지 확인한다.

## 그림

- `item_beer_open.png`: 웹 `assets/items/item-beer.png` 사본. 기존 `bottled_beer.png`와 4,431픽셀이 달라 웹 원본을 따로 들였다.
- `bottle_opener_open.png`: 웹 `item_opener`와 같은 그림을 Point 필터로 쓰려고 복사했다(준비 화면 원본의 필터는 그대로 둔다).
- 소리(`bottle_can_open.wav`, `fizz_tail.wav`)와 배경은 웹과 바이트가 같다.

## 검증

- `CapOpenRulesTests`: 판정 경계, 대기 시간, 실패 합산과 병 깨짐, 게이지 방향, perfect 조건, 다음 버튼 시점, 탄산 결정성·클립, 실패음 길이.
- 단계별 화면을 1280×720으로 찍어 배치를 확인했다(준비·힘 조절·링·빗나감·성공·탄산·깨짐·수업).
- 플레이 모드에서 1일차 오프닝을 처음부터 끝까지 진행하는 확인은 아직 하지 않았다.
