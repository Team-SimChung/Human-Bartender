# CSV 팝업 사용법

바/공통 스토리의 StoryScriptRunner와 거리·집 대화의 DialogueRunner에서 `popup` 스텝을 실행할 수 있습니다.
Timeline의 대사 전용 신호(CutsceneDialogueHandler)는 `say`만 지원하므로 그 대사 목록에는 넣지 않습니다.

## Unity 연결

`ProjectLifeScope.prefab`의 자식 `PopupService`에서 `Bindings`의 7개 슬롯에 UI 컴포넌트를 직접 연결합니다. 이벤트 함수 선택 방식이 아닙니다.

| 종류 | arg | UI 컴포넌트 | text 데이터 |
|---|---|---|---|
| 재료 획득 | ingredient_acquired | UIIngredientPopupView | shelf_items.csv의 재료 ID |
| 신규 인물 등록 | character_registered | UIPopupView | 비워도 됨 |
| 호감도 상승 | affinity_increased | UIAffinityPopupView | 표시할 인물 이름 |
| 호감도 하락 | affinity_decreased | UIAffinityPopupView | 표시할 인물 이름 |
| 새로운 목표 | new_objective | UIPopupView | 비워도 됨 |
| 목표 달성 | objective_completed | UIPopupView | 비워도 됨 |
| 이벤트 발생 | event_occurred | UIPopupView | 비워도 됨 |

- 재료 UI: Shelf Data에 NewShelfItemDataSO, Visuals에 CocktailRecipeVisualCatalog, Ingredient Name과 Ingredient Icon에 텍스트·이미지를 연결합니다. 이름은 현재 한국어 데이터이며, 이미지는 기존 카탈로그의 재료 ID 매핑을 사용합니다. 이름·이미지 조회 실패 시 실행 실패입니다.
- 호감도 UI: Character Name에 이름 텍스트를 연결합니다. CSV 문자열을 그대로 표시합니다.
- 나머지 UI: 기존에 배치한 고정 내용을 표시하며 전달 문자열은 사용하지 않습니다.
- 공통 Panel의 편집 위치가 표시 위치입니다. Enter From Right=true면 오른쪽에서 왼쪽으로 진입하고 false면 반대입니다. 화면 밖으로 완전히 나가는 위치를 Canvas 기준으로 계산합니다.
- 팝업 컨트롤러 오브젝트는 활성 상태로 유지합니다. 외부 신호는 UI의 `Hide()`에 연결합니다. 들어온 쪽으로 퇴장합니다. Auto Hide Seconds=0은 신호 대기, 양수는 진입 완료 후 자동 닫힘 시간입니다.
- Duration은 이동 시간입니다. 시간 정지 중에도 애니메이션이 실행됩니다. 같은 UI를 다시 호출하면 기존 이동·타이머를 중단하고 새 데이터로 진입을 다시 시작합니다. 대기열은 없습니다.

## CSV 열 규칙

게임이 읽는 파일은 `Assets/StreamingAssets/csv/story/steps.csv`입니다. Excel을 편집하는 경우 Steps 시트의 같은 열에 작성한 뒤 내보냅니다. CSV 정본 데이터 묶음은 ContentAuthoring/csv_owned_datasets.json 규칙을 따릅니다.

| 열 | 작성 규칙 |
|---|---|
| type | popup |
| arg | 위 표의 종류 문자열. 대소문자 무시, 앞뒤 공백 제거. 숫자와 미등록 명칭은 오류 |
| text.ko | 한국어 전달 데이터. 재료는 ID, 호감도는 표시 이름 |
| text.en | 영어 전달 데이터. 재료 ID는 한국어와 동일하게 작성 |
| sync | nowait. 비워도 동일하게 작동. wait/auto 등은 오류 |
| actor / dialogue_id | 사용하지 않음. 직접 CSV 편집 시 \N |
| when | 기존 조건식. 거짓이면 팝업 스텝을 건너뜀 |
| effects | 표시 시작 성공 후 적용할 기존 효과식. 실패하면 적용하지 않음 |
| seq | 장면 내 실행 순서. 기존 값과 중복되지 않게 지정 |

영어 설정이면 text.en을 사용하고, 영어가 없으면 text.ko로 대체합니다. `text.ko`는 검증 기준이므로 재료·호감도는 반드시 작성합니다. 신규 인물·목표·이벤트 팝업은 현재 고정 UI만 표시합니다. 데이터 조회나 게임 상태 변경은 수행하지 않습니다.

`await Popup(...)`은 표시 시작 성공 여부만 기다립니다. 진입/유지/퇴장 완료를 기다리지 않습니다. 따라서 연속 팝업 스텝은 동시에 표시될 수 있습니다. 종류 오류, UI 미연결, 재료 조회 실패 등으로 false가 반환되면 해당 대본 실행이 실패하고 후속 스텝과 그 행의 effects는 실행하지 않습니다.

## 작성 예시 (설명용으로 관련 열만 발췌)

아래 CSV는 전체 파일을 대체하는 파일이 아닙니다. 기존 steps.csv의 전체 헤더와 메타데이터 열을 유지하세요.

```csv
context,seq,type,arg,text.ko,text.en,when,effects,sync
intro,10,popup,ingredient_acquired,sugar_cube,sugar_cube,\N,\N,nowait
intro,11,popup,character_registered,\N,\N,\N,\N,nowait
intro,12,popup,affinity_increased,포트,Port,\N,\N,nowait
intro,13,popup,affinity_decreased,포트,Port,\N,\N,nowait
intro,14,popup,new_objective,\N,\N,\N,\N,nowait
intro,15,popup,objective_completed,\N,\N,\N,\N,nowait
intro,16,popup,event_occurred,\N,\N,\N,\N,nowait
```

이전 대본 행에서 `flag.johnny_recipe_unlocked = true`를 적용하고 다음 행에서 `ingredient_acquired`/`sugar_cube`를 실행하면 해금 알림으로 사용할 수 있습니다. 팝업 자체는 재료 해금·인물 등록·호감도·목표 상태를 변경하지 않습니다.

### 전체 열 예시 한 행

기존 `t99_dialogue` 테스트 장면에 추가하는 예입니다. 실제 추가 시 해당 장면의 seq/source_order/row_id 중복을 먼저 확인합니다.

```csv
context,seq,type,actor,arg,text.ko,text.en,dialogue_id,when,effects,sync,scene_id,dataset_id,row_id,parent_id,parent_field,source_order,template_id,text,payment
t99_dialogue,101,popup,\N,ingredient_acquired,sugar_cube,sugar_cube,\N,\N,\N,nowait,\N,script/bar/day99,popup_example_ingredient,scene_t99_dialogue,steps,100,steps_format_02,\N,\N
```

`steps_format_02`는 type/arg/text/when/effects/sync를 모두 지원하는 기존 형식입니다. 팝업 전용 형식을 새로 만들 필요는 없습니다. parent_id는 실제 대상 Scenes의 row_id, dataset_id는 부모와 동일한 데이터 묶음, parent_field는 부모가 사용하는 스텝 목록 필드입니다. 거리 선택지의 결과 팝업은 기존 결과 스텝 행을 복사하여 그 부모와 목록 필드를 유지합니다. context만 바꾸는 것으로 장면 연결이 바뀌지는 않습니다.

문자열에 쉼표·줄바꿈이 있으면 셀 전체를 큰따옴표로 감싸고, 문자열 내부 큰따옴표는 두 번 씁니다. 빈 값은 직접 CSV에서 `\N`, Excel에서는 빈 셀입니다.

## 확인

1. Tools → Data → Validate CSV Content로 구조와 부모 연결을 검사합니다.
2. Tools → Story → Validate C Content로 popup 종류/sync/필수 문자열을 검사합니다.
3. 재료 이미지 카탈로그 매핑과 UI 연결은 Inspector에서 확인합니다.
4. Play를 다시 시작하여 표시와 Hide 신호를 확인합니다. 실행 중 CSV 자동 반영은 하지 않습니다.

코드에서 직접 호출할 때는 다음과 같습니다.

```csharp
[Inject] IPopupService popupService;

bool started = await popupService.Popup(PopupType.IngredientAcquired, "sugar_cube");
```
