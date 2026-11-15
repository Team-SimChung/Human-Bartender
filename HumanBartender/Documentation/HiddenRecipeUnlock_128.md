# #128 히든 레시피(올드 패션드) 해금 창

## 동작과 소유권

3일차 바 대본에서 아일리의 각설탕 대사("... 감사해요.", `flag.day3_sugar_received = true`)가 확정되면
`StoryScriptRunner.CheckRecipeUnlockAsync`가 `CampaignStoryRules.TryUnlockJohnnyRecipe`를 부른다.
와일드 독(갱단 또는 시바)·비터스·조니 기억(`campaign_observation`)·각설탕 수령이 모두 있으면
`flag.johnny_recipe_unlocked = true`를 한 번 기록하고 `HiddenRecipeUnlockScreen`을 띄운다.
확인 버튼을 누를 때까지 다음 스텝(톰 입장)을 진행하지 않는다. 웹 `story-flow.js` `checkJohnnyUnlock`과 같은 순서다.

| 대상 | 맡는 일 |
| --- | --- |
| `CampaignStoryRules` | 해금 조건 판정과 플래그 기록. 칵테일 ID 상수 `JohnnyCocktailId` |
| `StoryScriptRunner` | 해금 직후 창을 띄우고 확인까지 대본 대기. 창이 연결되지 않았으면 `[Story]` 오류 로그 후 기존 `EventOccurred` 알림으로 대신한다 |
| `HiddenRecipeUnlockScreen` | 표시·확인 대기만. 진행 플래그와 입력 맵은 바꾸지 않는다 |
| `PlayInputHandler` | 창이 열려 있는 동안 대화 진행·빨리 넘기기·좌석 이동·서비스 패널·되묻기 입력을 무시 |

- 바뀌는 내용은 CSV에서 채운다: 칵테일 이름(`cocktails.name.ko`), 그림(`CocktailRecipeVisualCatalog`),
  재료 줄(`RecipeNote.Build`로 고를 수 있는 레시피 줄만, 이름은 `shelf_items.name.ko`, 양은 `qty` + 단위).
- 고정 문구(배지, 제목, 설명, 잔·도구, 선반 안내, 버튼)는 씬의 TMP에 있다. 원문은 웹 `app.js` `johnnyUnlockHTML`.
- 재료 줄은 씬에 3줄을 직접 배치했다. 레시피 줄이 더 많으면 경고하고 넘치는 줄은 표시하지 않는다.
- 설정창이 열려 있으면 확인을 무시한다. Esc 설정창은 막지 않는다.
- 해금 판정 자체(조건, 중복 방지, 선반·레시피 노출)는 #116에서 구현했고 `WebCraftingAlignmentTests`가 검사한다.

## 캔버스

`05.UI/Canvas_HiddenRecipeUnlock` — Screen Space Overlay, 960×540 Match 0, **Sort Order 12**.
DailyUnlocks와 12 사이에 빈 번호가 없어 같은 번호를 쓰고 Hierarchy에서 `Canvas_DailyUnlocks` 바로 뒤에 둔다(위에 그려짐).
두 창은 동시에 뜨지 않는다(DailyUnlocks는 개점 대사 전, 이 창은 대본 중). AGENTS.md Sort Order 목록에 추가했다.

```
Canvas_HiddenRecipeUnlock (HiddenRecipeUnlockScreen)
└ HiddenRecipeRoot (CanvasGroup, 처음엔 꺼짐)
  ├ BackgroundDimOverlay (Raycast 켬)
  ├ PanelShadow 687×468
  └ HiddenRecipePanel 615×396 (웹 820×528 × 0.75)
    ├ PanelBorder
    ├ HeaderGroup (24,-24) — BadgeImage/BadgeText, TitleText 20, DetailText
    ├ HeaderDivider
    ├ DrinkGroup (24,-112) 253×200 — DrinkImage 120×126, DrinkNameText, DrinkToolText
    ├ IngredientGroup (299,-112) 292×200 — StockTitleText, IngredientList(VerticalLayoutGroup) › IngredientRow0~2 292×47, HintText
    ├ FooterDivider
    └ ConfirmButton 172×30 — ConfirmText
```

## 아트

- `Old_fashioned_final.png`를 웹 `bar-playtest/assets/01.ArtResources/03.Crafting/03.Cocktails/01.RatingReveal/01.Finished/`에서
  Unity의 같은 경로로 가져왔다. 임포트 설정은 `kahlua_milk_final.png`와 같다(PPU 100, Point, 무압축, Multiple).
  카탈로그는 다른 칵테일처럼 투명 여백을 뺀 스프라이트(`Game_Old_fashioned_final_*`, 220×266)를 쓴다.
- `CocktailRecipeVisualCatalog`의 `johnny_old_fashioned`가 갓파더(Legacy) 임시 그림 대신 이 그림을 가리킨다.
  레시피 목록·미리보기·해금 창에 쓰이고, 테이블 전용 그림이 없어서 테이블 잔·드래그 잔도 이 그림으로 대체된다.
- 창 배경은 기존 `WebGenerated/Notifications`의 `Web_GRD_daily_unlocks_johnny_unlock_*`, `Web_GRD_johnny_unlock_*`를 늘려 쓴다(웹도 100% 늘림).

### 남은 아트 (이번 범위 밖)

- 비터스: 카탈로그가 칼루아 그림을 임시로 쓴다. 각설탕: 카탈로그에 그림이 없어 해금 창에서는 대체 그림을 쓰거나 아이콘을 숨긴다.
  웹에는 `assets/backgrounds/campaign/johnny-bitters.png`, `johnny-sugar_cube.png`가 있다.

## 사용자 셋업

1. Unity 6000.3.14f1에서 `Assets/01.Game/01.Scenes/Play.unity`를 연다.
2. `Tools/Human Bartender/UI/Setup Hidden Recipe Unlock`을 실행한다.
3. `05.UI/Canvas_HiddenRecipeUnlock`의 Inspector와 화면을 확인한다. 필요하면 Fallback Sprite를 지정한다.
4. `StoryScriptRunner`와 `PlayInputHandler`의 Hidden Recipe Screen 칸이 연결됐는지 확인한다.
5. Play 모드를 끈 상태에서 씬을 저장한다.

메뉴는 Play 모드와 다른 활성 씬에서 실행을 거부한다. 캔버스가 이미 있으면 배치와 사람이 연결한 값을 덮어쓰지 않고 비어 있는 연결만 채운다.
씬을 자동 저장하지 않는다. 이미지·폰트·카탈로그가 없으면 경고하고 비워 둔다.

## 검증

자동 테스트: `Assets/01.Game/06.Tests/Editor/HiddenRecipeUnlockTests.cs`.

- CSV 그대로 이름·그림·재료 3줄("와일드 독 45ml", "비터스 5ml", "각설탕 1개") 표시, 각설탕 그림 대체, 확인 후 닫힘.
- 남는 줄 숨김, 넘치는 줄 경고, 선반 데이터가 없으면 ID 표시.
- 취소·비활성화 후 재진입, 중복 호출 거절, 설정창 열림 중 확인 무시, 참조 누락 보고.
- 카탈로그가 `Old_fashioned_final.png`를 가리킴.
- 저장된 Play 씬의 캔버스·Sort Order·순서·연결·Raycast·Scale·이름. 사용자 셋업·저장 전에는 실패한다.

### 2026-11-15 실행 결과

- 셋업 메뉴를 배치 모드에서 일회성 래퍼로 실행하고 Play 씬을 저장했다(래퍼는 삭제). 씬 변경은 추가만 있다(새 캔버스와 두 연결).
  저장 때 배치 화면 비율 때문에 Main Camera의 Viewport Rect가 바뀐 것은 원래 값(y 0, height 1)으로 되돌렸다.
- Overlay 캔버스 루트의 Scale은 Canvas가 정하므로 Scale 검사에서 루트만 뺐다.
- `HiddenRecipeUnlockTests` 8개 모두 통과. `WebCraftingAlignmentTests`, `BarSeatHudTests` 통과.
- `DailyUnlocksTests.PlaySceneHasDailyUnlocksScreen` 실패(기존): 저장된 씬에서 `DailyUnlocksRoot`가 켜진 채라 `IsOpen`이 true다. 이번 작업 전부터 실패했고 런타임에서는 Awake가 끈다.
- 실제 플레이와 세 해상도 확인은 아직 하지 않았다.

### Test Day 3 직접 실행 (2026-11-15 추가)

- 원인: PlayPhaseController의 Test Day는 일차만 바꾸고 전날 조건(와일드 독·비터스·조니 기억)을 채우지 않았다.
  각설탕 대사 뒤 해금 판정이 조용히 실패해 창 없이 톰이 들어왔다(Editor.log의 `[Condition] flag.day3_sugar_received = true` 뒤 해금 기록 없음).
- 수정: Test Day가 3 이상이면 `CampaignStoryRules.SeedJohnnyPrerequisites`로 빠진 조건만 채우고 `[PlayPhase]` 경고에 채운 플래그를 남긴다.
  웹 `seedJohnnyPrerequisites`와 같은 역할이다. 에디터 전용 분기라 빌드와 정상 진행에는 영향이 없다.
  바 대사 테스트 창도 같은 플래그 목록(`JohnnyPrerequisiteFlags`)을 쓴다.
- 3일차에 각설탕을 받았는데 조건이 부족하면 `[Story]` 경고로 빠진 항목(wild_dog / bitters / johnny_memory)을 한 번 알린다.

### 일차별 신규 재료·레시피(DailyUnlocks) 대조

웹 규칙(`story-flow.js` `dailyUnlocks`: 그날 `unlock_day`, 조건 통과, 조니 재료·올드 패션드 제외)을 웹 CSV와 Unity CSV에 각각 적용해 비교했다. 두 결과가 같다.

| 일차 | 재료 | 레시피 |
| --- | --- | --- |
| 0 | (표시 안 함) | (표시 안 함) |
| 1 | beer, tequila, vodka, rum, orange_juice, cola, dry_vermouth | bottle_beer, screwdriver, tequila_sunrise, dry_martini |
| 2 | red_wine, champagne, whiskey, grenadine, sour_mix, cranberry_juice, triple_sec, kahlua, milk, amaretto | red_wine, champagne, whiskey_sour, cosmopolitan, kahlua_milk, godfather |
| 3 | 없음(창이 뜨지 않음) | 없음 — 올드 패션드는 대본 중 히든 레시피 창으로 |

- 두 CSV의 `unlock_day`·`unlock_when`·`status`·`kind`는 모든 행에서 같다. 조건부 항목은 조니 4종뿐이다.
- 카탈로그 이미지: 2일차의 cranberry_juice·triple_sec·kahlua·milk·amaretto(재료)와 godfather(레시피), 그리고 bitters는
  카탈로그 ID는 있지만 가리키는 파일이 735e01a0(2026-11-01, Legacy 리소스 정리)에서 삭제돼 이미지가 비어 있다.
  웹도 이 재료 5종은 전용 그림 없이 `item_dummy`(gin.png)로 표시한다. godfather는 웹에 새 `godfather_final.png`가 있다(Unity Legacy 그림과 다름).
- 처리(2026-11-15, 사용자 결정 "웹처럼 더미"):
  - 웹 `godfather_final.png`를 `01.Finished/`로 가져오고(올드 패션드와 같은 임포트 설정, 투명 여백 뺀 198×241 스프라이트) 카탈로그 godfather를 교체했다.
  - Play 씬의 `DailyUnlocksScreen`·`HiddenRecipeUnlockScreen` Fallback Sprite를 웹 `item_dummy`와 같은 파일인
    `00.Dummy_Assets/02.Ingredient_Images/gin.png`로 지정했다. 그림 없는 재료는 빈칸 대신 진 병 더미로 보이고, 경고 로그는 그대로 남는다.
  - 히든 레시피 창의 비터스·각설탕은 아래 "웹 그림 맞춤"에서 웹 그림으로 바꿨다.
  - 레시피 목록 미리보기(`CocktailRecipePreview`)는 Fallback이 없어 이 재료들의 아이콘이 여전히 비어 있다. 이번 범위 밖.
  - `DailyUnlocksTests.EveryDailyUnlockHasACatalogImageOrTheWebDummy`, `UnlockScreensFallBackToTheWebDummy`가 이 상태를 고정한다.
- `DailyUnlocksTests.EachDayListsExactlyTheWebUnlocksInCsvOrder`가 이 목록과 순서를, `EveryDailyUnlockHasACatalogImage`가 이미지를 고정한다.
  CSV가 바뀌면 웹 결과를 다시 확인하고 목록을 고친다.

### 웹 그림 맞춤 (2026-11-15)

웹 `runtime-assets.json`의 `recipe_cocktail_*`, `recipe_item_*`(없으면 `item_*`, 그것도 없으면 `item_dummy`)와 카탈로그를 픽셀 단위로 비교했다.

| 대상 | 이전 | 지금 |
| --- | --- | --- |
| 칵테일 6종(gin_tonic, gin_fizz, bottle_beer, screwdriver, tequila_sunrise, dry_martini) | `00.Dummy_Assets/01.Cocktail_Preview_Images` 옛 미리보기 | Unity에 이미 있던 `01.Finished/*_final.png`(웹과 같은 파일). 10-19에 Single 스프라이트로 바뀐 파일이라 fileID 21300000(220×411 전체)을 가리킨다. 임포트 설정은 건드리지 않았다 |
| 칵테일 3종(red_wine, champagne, whiskey_sour) | 옛 미리보기 | 웹 `wine_final.png`, `Champagne_final.png`, `whisky_sour_final.png`를 `01.Finished/`로 가져옴 |
| 와일드 독 | 위스키 병 임시 그림 | 웹 `johnny-wild_dog.png` |
| 비터스 | 삭제된 파일(빈칸) | 웹 `johnny-bitters.png` |
| 각설탕 | 카탈로그 항목 없음 | 웹 `johnny-sugar_cube.png`(항목 추가) |

- 재료 3종은 `03.Crafting/02.Ingredients/02.Shelf/`에 웹 파일 이름 그대로 두었다. 임포트 설정은 같은 폴더 `gin.png`와 같다(PPU 100, Point, 무압축, 투명 여백 뺀 스프라이트 하나).
  칵테일 3종은 `kahlua_milk_final.png`와 같은 설정이다. 모든 파일은 웹 커밋의 원본 바이트와 같다.
- 나머지 재료 14종은 웹이 같은 병 그림을 180×180 정사각형에 여백을 붙인 판을 쓸 뿐 그림은 같다(투명 여백을 뺀 픽셀 비교 일치). 바꾸지 않았다.
- 2일차 재료 5종(cranberry_juice 등)은 웹도 더미라 Fallback(gin.png)과 같다.
- `00.Dummy_Assets/01.Cocktail_Preview_Images`는 카탈로그에서 더 쓰지 않지만 다른 참조가 있을 수 있어 지우지 않았다.
- 테스트: `DailyUnlocksTests.EveryConfirmedCocktailUsesTheWebFinishedArt`, `HiddenRecipeUnlockTests.CatalogUsesTheOldFashionedArtInsteadOfGodfather`(조니 재료 3종 포함).

### 최종 실행 결과 (2026-11-15 20시대)

- 배치 EditMode `DailyUnlocksTests`·`HiddenRecipeUnlockTests`·`WebCraftingAlignmentTests`·`BarSeatHudTests`·`BarDialogueTestEnvironmentTests`: 49개 중 48 통과.
- 실패 1개는 기존 `DailyUnlocksTests.PlaySceneHasDailyUnlocksScreen`(저장된 씬에서 `DailyUnlocksRoot`가 켜져 있음).
- 누락 참조는 NUnit `Is.Null`/`Is.Not.Null`로 판별되지 않아(Unity의 가짜 null) 이미지 검사는 `!= null` 비교를 쓴다.
- 에디터 종료 때 저장된 Play 씬에 PlayPhaseController `testDay` 1→2 변경이 함께 들어갔다. 사용자 결정으로 2를 유지한다(에디터 직접 실행 전용 값).

### 제조·서빙 화면 그림 (2026-11-15)

**테이블 잔·잔 드래그** — 웹 `02.Table/01.Finished`의 `Wine`, `Champagne`, `whisky_sour`, `godfather`, `Old_Fashioned`를
Unity 같은 폴더로 가져오고(`cosmopolitan.png`와 같은 Single 설정) 카탈로그 `tableCocktailSprites`에 추가했다. 칵테일 13종 모두 웹과 픽셀 일치
(kahlua_milk는 웹도 테이블 전용 키가 없어 완성 그림을 쓴다).

**재료 선반** — `CraftPrepStage.prefab`의 `wild_dogSlot`·`bittersSlot`·`sugar_cubeSlot` 그림을 `johnny-*.png`로 바꿨다.
병 바닥 높이가 그대로이도록 슬롯 Y, 이름표(`ItemNameText`)·`InfoAnchor` 위치, 충돌체 크기(그림 + 0.18 × 0.25, 오프셋 유지)를 함께 맞췄다.
크기는 PPU 100 원본 그대로다. 실제 선반 모양은 Unity에서 확인한 뒤 프리팹 원본에서 조절한다.

**선반 높이 보정** — 피벗은 모든 병이 가운데라 문제없었고, #116 셋업 도구(`WebCraftingShelfSetup`)가 처음 넣은 Y가 선반 면과 달랐다.
냉장고 위칸의 크랜베리주스·우유는 바닥이 0.76(리큐어 위칸 값을 그대로 씀)인데 캔의 바닥은 1.08이라 0.32 아래로 처졌고,
리큐어 아래칸 5종은 바닥이 −1.24로 그레나딘·사워믹스(−1.31)보다 0.07 떠 있었다.

| 슬롯 | Y 이전 → 이후 | 바닥 |
| --- | --- | --- |
| cranberry_juice, milk | 1.5 → 1.82 | 1.08(캔과 같음) |
| triple_sec, kahlua, amaretto | −0.5 → −0.57 | −1.31 |
| bitters | −0.485 → −0.555 | −1.31 |
| sugar_cube | −0.96 → −1.03 | −1.31 |

셋업 도구의 처음 배치 값도 같은 기준으로 고쳤다(재실행 시 기존 배치는 덮어쓰지 않음).

**서빙(최종 결과) 화면** — `ServeScreen`의 칵테일별 클립은 8종뿐이라 red_wine·champagne·whiskey_sour·godfather·johnny_old_fashioned는
코스모폴리탄 클립이 재생됐다. 웹 `serve-view.js`의 `.serve-fallback`과 같게, 전용 클립이 없으면 배경 위에 그 칵테일의 완성 그림을 띄운다.

| 웹(1280×720) | Unity(960×540, `ServeScreen` Inspector) |
| --- | --- |
| 260×370 | `FallbackDrinkImage` 195×278, 가운데 앵커·피벗 |
| 1.5초 동안 `top` +80px | `fallbackDuration` 1.5, `fallbackDropY` 60 |
| scale .72 → 1 | `fallbackStartScale` .72 |
| opacity .2 → 1 | `fallbackStartAlpha` .2 |

- 연출은 DOTween(게임 시간)이고 끝나면 등급·버튼을 보인다. 닫거나 다음 잔으로 넘어가면 저장된 자리·크기·투명도로 되돌린다.
- 정지 그림 칸이 비어 있으면 예전처럼 코스모폴리탄 클립을 쓰고 `[ServeScreen]` 오류로 셋업을 안내한다.
- 셋업: Play 씬에서 `Tools/Human Bartender/UI/Setup Serve Fallback Drink` → `BG` 아래 `drink` 바로 뒤에 `FallbackDrinkImage`를 만들고 세 칸을 연결한다. 다시 실행해도 기존 배치는 바꾸지 않는다.
- 테스트: `ServeFallbackTests`(클립 경로, 정지 그림 경로, 되돌리기, 그림 없음 경고, 셋업 전 대체 클립, 저장된 씬 구조).
- 셋업은 배치 모드에서 일회성 래퍼로 실행·저장했다(래퍼 삭제). 씬 변경은 새 오브젝트와 `ServeScreen` 칸 추가뿐이며, 배치 저장 때 바뀐 Main Camera Viewport Rect는 원래 값으로 되돌렸다.
- 배치 EditMode(ServeFallback·CraftGuestClock·CraftDiscard·CurrencyPresentationRuntime·HiddenRecipeUnlock·WebCraftingAlignment·DailyUnlocks·BarSeatHud·BarDialogueTestEnvironment·GimmickPlayTestEnvironment): 100개 중 99 통과. 실패 1개는 기존 `PlaySceneHasDailyUnlocksScreen`.
- 실제 플레이 화면(선반 병 크기, 서빙 연출 위치, 세 해상도)은 아직 확인하지 않았다.
- 전용 서빙 시트가 생기면 `_animations`에 클립을 등록하면 그쪽을 쓴다. 웹의 탄산 효과는 Unity에 없어 다루지 않았다.

### 일반 손님 주문 점검 (2026-11-15)

- 일차별 무작위 주문 후보(해금된 확정 레시피)는 웹 `cocktailsAvailable`과 Unity `CocktailUnlockPolicy.IsUnlocked`가 같은 규칙이고 `craft/` CSV도 같아 결과가 같다.
  0일차 2개, 1일차 6개, 2·3일차 12개(3일차는 대본 해금 뒤 올드 패션드 추가).
- 무작위 후보·고정 주문·단골 대본 주문 모두 그날 재료·잔·도구가 선반에 있어 만들 수 없는 주문은 없다.
- `guests/random_waves.csv`만 웹 11-08(d9a3474)보다 오래돼 고정 주문이 달랐다(1일차 bottle_beer/gin_fizz, 2일차 screwdriver/godfather).
  웹 d9a3474 파일을 그대로(LF, BOM, 읽기 쉬운 ID 동일) 가져와 1일차 tequila_sunrise/screwdriver, 2일차 whiskey_sour/champagne으로 맞췄다.
  `ContentAuthoring/web_content_source.json`의 `files`·`web_files` 해시를 갱신하고 `partial_imports`에 기록했다. Python 구조 검사와 반입 도구 테스트 통과.
- `WebCraftingAlignmentTests.GeneralGuestFixedOrdersMatchWeb`, `EveryGuestOrderableCocktailIsCraftableThatDay`가 이 상태를 고정한다.

수동 검증:

1. 바 대사 테스트 창(BarDialogueTestWindow)에서 3일차, 아일리의 각설탕 대사 앞부터 시작한다.
2. "... 감사해요." 뒤에 창이 뜨고, 확인 전에는 톰이 들어오지 않는지, Tab·좌석 이동·대화 진행이 막히는지 확인한다.
3. 확인 후 톰 주문 → 레시피 목록의 올드 패션드 → 선반 맨 끝의 각설탕 → 제조 → 서빙까지 진행한다.
4. Game 창 1280×720 / 1920×1080 / 2560×1440에서 배치와 글자를 확인한다.
