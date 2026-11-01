# CSV 팝업 사용법

스토리 대본과 거리·집 대화의 `Steps` 행에서 `popup`을 실행합니다.
게임이 읽는 CSV는 `Assets/StreamingAssets/csv/story/steps.csv`입니다. Excel에서는 `Steps` 시트를 편집한 뒤 내보냅니다.

## 작성 순서

기존 Steps 행을 복사한 뒤 실행 순서와 부모 연결을 맞추고, 아래 열을 설정합니다.

| 열 | 넣을 값 |
|---|---|
| `type` | `popup` |
| `arg` | 아래 표의 팝업 종류 |
| `text.ko` | 재료 ID, 인물 이름 또는 목표 종류 |
| `text.en` | 영어 표시값. 재료 ID는 한국어와 동일하게 작성 |
| `sync` | `nowait`. 빈 값도 같은 동작 |
| `actor`, `dialogue_id` | 직접 CSV에서는 `\N` |
| `when`, `effects` | 기존 조건·효과 규칙을 그대로 사용 |

`seq`와 `source_order`는 실행 순서에 맞춰 중복 없이 지정합니다.
`row_id`는 고유하게 작성하고, `parent_id`·`parent_field`·`dataset_id`는 같은 목록의 기존 행을 기준으로 맞춥니다.

## 팝업 7종

| 표시 | `arg` | `text.ko` 예시 | 데이터 필수 |
|---|---|---|---|
| 재료 획득 | `ingredient_acquired` | `gin` | 필수 |
| 신규 인물 등록 | `character_registered` | `\N` | 생략 가능 |
| 호감도 상승 | `affinity_increased` | `톰 거너` | 필수 |
| 호감도 하락 | `affinity_decreased` | `톰 거너` | 필수 |
| 새로운 목표 | `new_objective` | `메인 목표` | 필수 |
| 목표 완료 | `objective_completed` | `메인 목표` | 필수 |
| 이벤트 발생 | `event_occurred` | `\N` | 생략 가능 |

- 재료 획득: `shelf_items.csv`의 재료 ID를 전달합니다. 해당 재료의 이름과 아이콘이 표시됩니다. `CocktailRecipeVisualCatalog`에도 같은 ID의 이미지가 연결되어 있어야 합니다. `gin`은 현재 데이터와 이미지가 모두 연결된 예시입니다.
- 호감도 상승·하락: 전달한 인물 이름을 표시합니다. 예를 들어 `톰 거너`를 전달하면 이름 옆에 상승·하락 문구가 표시됩니다.
- 새로운 목표·목표 완료: 전달한 목표 종류를 큰 글씨에 그대로 표시합니다. `메인 목표`, `서브 목표`, `일일 목표`처럼 원하는 문자열을 넣습니다.
- 신규 인물·이벤트: 프리팹에 설정된 고정 문구를 표시합니다.

`arg`는 대소문자를 구분하지 않으며 앞뒤 공백을 제거합니다. 표의 종류 문자열을 사용합니다.

## CSV 예시

아래는 필요한 열만 발췌한 예시입니다. 실제 Steps 행에는 기존 헤더의 다른 열과 부모 연결 정보도 함께 작성합니다.

```csv
type,arg,text.ko,text.en,sync
popup,ingredient_acquired,gin,gin,nowait
popup,character_registered,\N,\N,nowait
popup,affinity_increased,톰 거너,Tom Gunner,nowait
popup,affinity_decreased,톰 거너,Tom Gunner,nowait
popup,new_objective,메인 목표,Main Objective,nowait
popup,objective_completed,메인 목표,Main Objective,nowait
popup,event_occurred,\N,\N,nowait
```

한국어 설정에서는 `text.ko`, 영어 설정에서는 `text.en`을 표시합니다. 영어가 비어 있으면 한국어로 대체합니다.
검증 기준인 `text.ko`는 필수 데이터가 있는 팝업에서 반드시 작성합니다.
직접 CSV의 빈 값은 `\N`, Excel의 빈 값은 빈 셀로 작성합니다. 쉼표나 줄바꿈이 들어간 값은 큰따옴표로 감싸고, 내부 큰따옴표는 두 번 씁니다.

## 실행 동작

- `sync`는 `nowait` 또는 빈 값입니다. `wait`, `auto`는 오류입니다.
- 팝업 표시를 시작하면 다음 스텝으로 바로 넘어갑니다. 닫힐 때까지 기다리지 않습니다.
- 자동 닫힘 시간은 각 팝업 컴포넌트의 `Auto Hide Seconds`에서 설정합니다. 0이면 `Hide()` 호출을 기다립니다.
- 여러 팝업을 연속 실행하면 동시에 표시될 수 있습니다. 같은 종류를 다시 실행하면 새 데이터로 다시 진입합니다.
- 팝업은 알림을 표시합니다. 재료 해금·호감도·목표 진행 상태는 기존 `effects` 등으로 별도 변경합니다.
- 필수 텍스트 누락, 팝업 UI 미연결, 재료 조회·이미지 조회 실패 시 해당 스텝은 실패합니다.

## Unity 연결과 확인

`ProjectLifeScope.prefab`의 `PopupService > Bindings`에 7종 UI가 연결되어 있습니다.

| 팝업 | 컴포넌트 | 텍스트·이미지 연결 |
|---|---|---|
| 재료 획득 | `UIIngredientPopupView` | Shelf Data, Visuals, Ingredient Name, Ingredient Icon |
| 호감도 상승·하락 | `UIAffinityPopupView` | Character Name |
| 새로운 목표·목표 완료 | `UIObjectivePopupView` | Objective Type: 큰 글씨 텍스트 |
| 신규 인물·이벤트 | `UIPopupView` | 고정 문구와 Panel |

1. `Tools > Data > Validate CSV Content`로 CSV 구조와 부모 연결을 검사합니다.
2. `Tools > Story > Validate C Content`로 팝업 종류·필수 텍스트·sync를 검사합니다.
3. Play를 다시 시작하여 팝업 표시를 확인합니다.

Timeline의 대사 전용 신호(`CutsceneDialogueHandler`)에는 `say` 대사만 작성합니다.
