# #128 P3-2 수면 후 명언·일차 전환 / P3-3 시바견 상점 남은 상품·판매 종료

기준: 웹 `junseo874.github.io/bar-playtest` — `src/features/campaign/day-transition.js`, `src/features/outside/events/outside-shop.js`,
`docs/gameplay/events.md`("일차 전환 · 명언과 Day 숫자", "시바견 상점").

## 2. 수면 후 명언·일차 전환

### 동작

집에서 취침하면 `GameProgressionService.SleepAsync`가 전환 화면(`DayTransitionView`)을 띄운다.
검은 막 → 명언·저자 → 명언 사라짐 → 잠깐 빈 화면 → `Day N` 등장 → 숫자 N→N+1 굴림 → **준비 시점**에 일차 변경(집 기상) 또는 OutSide 로드(2→3) → Day 사라짐 → 0.9초 공개.
그 뒤 기존 기상 연출(`HomeSleepAnimation.WakeUpAsync`)이 이어진다. 3일차 취침은 기존대로 엔딩이다.

| 대상 | 맡는 일 |
| --- | --- |
| `DayTransitionView`(`05.Presentation/03.UI`) | 명언 목록·시간표·화면. `PlayToPrepareAsync`, `FinishAsync`, `HideImmediate`. 실시간(DOTween `SetUpdate(true)`) |
| `SceneTransitionManager` | 전역(DontDestroyOnLoad) 오브젝트로 `dayTransition` 칸을 갖고 `IDayTransitionSource`로 노출. 비어 있으면 예전 0.5초 페이드 |
| `GameProgressionService.SleepAsync` | 준비 시점에 일차·흐름 변경, 씬 로드. 거절·취소·실패 시 화면을 바로 닫고 기존 복구 |

- 명언은 대사 CSV가 아닌 화면 설정이라 `DayTransitionView` Inspector 목록(웹 원문 3개)에 있다. 문구가 없는 일차 조합은 숫자 전환만 한다.
- 입력으로 건너뛰지 않는다. 검은 막(`ShadeImage`)이 Raycast를 받아 아래 화면 입력을 막는다.

### 시간표 (초, Inspector 기본값 = 웹)

| 구간 | 값 |
| --- | --- |
| 검게 덮임 / 빈 대기 | 0.65 / 1.5 |
| 명언 등장(9px 위로) / 저자 간격·등장(8px 위로) | 0.5 / 0.3·0.7 |
| 명언·저자 유지 | 4 |
| 명언 사라짐 | 0.85 (웹과 같은 시각 7.15초에 시작) |
| **빈 화면 간격** → Day 등장 | **0.6** → 0.3 (웹은 명언이 사라지는 동안 Day가 겹쳐 나타남. 사용자 요청으로 간격을 둠, `dayGap`=0이면 웹과 같음) |
| 숫자 굴림 시작 / 굴림 시간 | Day+0.85 / 0.65 (칸 높이의 110%) |
| 다음 날 준비 | Day+1.55 |
| Day 사라짐 | Day+2.3부터 0.3 |
| 공개(준비가 늦으면 더 기다림) | Day+2.7부터 0.9 |
| 명언 있는 전환 전체 | 약 12.2 (간격 포함; 웹 10.75) |

### 화면 (`ProjectLifeScope.prefab` › `Canvas_DayTransition`)

- Screen Space Overlay, 960×540 Match 0, **Sort Order 30000**(전역 Cash 205 위, 바 출입 문 32000 아래).
- `DayTransitionRoot`(CanvasGroup, 처음엔 꺼짐) › `ShadeImage` / `QuoteGroup` 840×200 › `QuoteText`·`AuthorText` / `DayTitleGroup` › `DayLabelText` + `DigitsMask`(RectMask2D 61×55) › `OldDigitText`·`NewDigitText`.
- 글자: PFStardust. **AGENTS 크기 예외** — 웹(1280) ×0.75로 명언 17, 저자 13, Day 42. 전체 화면 연출이라 웹 비율을 따랐다.
- 셋업: `Tools/Human Bartender/UI/Setup Day Transition`(프리팹 원본을 열어 없을 때만 만들고 빈 연결만 채움). 2026-11-15 배치 모드로 실행·저장했다(프리팹 변경은 추가만).

## 3. 시바견 상점 남은 상품·판매 종료

- 재고 규칙을 `ShopStoryEvents`에 모았다: `HasStock`(비터스 미구매 또는 참치캔 가능), `TunaAvailable`(2일차 이후·부비 미입양·미구매), `NeedsQuiz`(비터스·와일드 독 보유, 미학습), `IsSoldOut`.
- **판매 종료:** 팔 물건이 없으면 첫 만남·재방문 인사 대신 `이제 팔 거 없어. 꺼져, 시바.` 한 줄로 끝난다(`OutsideStorySceneRules.ResolveEntry` → `ShopSoldOut` 명령).
  퀴즈가 남아 있으면 판매 종료보다 퀴즈가 먼저다. 대사는 웹처럼 상점 규칙 코드가 갖고, 웹 반입 보호 대상인 `steps.csv`는 고치지 않았다.
- **상품 열기 순서**를 웹 `openStock`과 같게: 퀴즈 → 판매 종료 → 용돈 → 목록.
- **용돈 조건**을 웹과 같게: `!받음 && 2일차 이하 && 소지금 ≤ 300`(이전 Unity의 "비터스 미구매" 조건 제거). 비터스를 산 뒤에도 참치캔이 남고 돈이 없으면 용돈을 제안한다.
- 구매한 물건 목록 제외·재구매 차단·돈 부족 비활성은 이미 있었다.
- 범위 밖: 구매 시 획득 알림·인벤토리 수량(웹 `notifyItem`, `inventory[item]=1`)은 P3-5에서 다룬다.

## 검증 (2026-11-15)

배치 EditMode: `DayTransitionTests`, `OutsideStoryEventTests`(상점 테스트 9개 추가), 집·거리·서빙·해금·CSV 관련 묶음 — **119개 중 115개 통과**.

| 실패 | 원인 |
| --- | --- |
| `DailyUnlocksTests.PlaySceneHasDailyUnlocksScreen` | 기존: 저장된 Play 씬의 `DailyUnlocksRoot`가 켜져 있음 |
| `DayOneThugInteractionTests.AuthoredDayOneThugUsesOneNpcOwnerAndPreservesItsSpeakerAndOtherThugs` | 기존(이번 변경 전 커밋에서도 실패 확인) |
| `HomeTVActionTests.PowerResetsOnDayChangeWithoutReloadingTheScene` | 기존(같음, #125 TV 테스트) |
| `WebCsvCompatibilityTests.PublishedFilesMatchRecordedSnapshotsAndProtectedOverrides` | 기존(같음, `story/cutscenes.csv`가 #119에서 바뀌고 반입 기록 미갱신) |

세 개는 변경을 빼 둔 상태(마지막 커밋)에서도 같은 테스트가 실패하는 것을 확인했다.

명언 뒤 간격(`dayGap` 0.6초) 추가 후 배치 재실행: `DayTransitionTests` + `OutsideStoryEventTests` 30개 모두 통과.

수동 확인(아직):
1. Test Day 0·1 집에서 취침 → 명언·저자·Day 굴림·기상 순서와 시간, 명언 줄바꿈·줄 간격(`QuoteText` lineSpacing 30은 화면에서 조절).
2. Test Day 2 취침 → OutSide 퇴근길로 넘어가는 동안 화면이 덮여 있는지. 3일차 취침은 엔딩 그대로.
3. 2일차 시바: 비터스·참치캔을 모두 산 뒤 다시 말 걸기 → 판매 종료 한 줄. 와일드 독이 있으면 퀴즈 먼저.
4. 1280×720 / 1920×1080 / 2560×1440.
