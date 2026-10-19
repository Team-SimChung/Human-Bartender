# 재료 선택 화면 변경 기록

2026-10-20. 재료 선택 화면의 아웃라인, A/D 이동, 마우스 설명창 작업에 대한 구조 기록입니다.

## 마우스를 따라다니는 설명창

- 기존 `CraftPrepStage.prefab`의 `World Item Info` / `Frame` / `Name` / `Description`을 그대로 사용합니다. 새 Canvas나 설명창 프리팹을 만들지 않았습니다.
- `CraftPrepItemTooltip`이 마우스의 화면 좌표를 제조 준비 전용 카메라로 월드 좌표에 변환합니다. 표시 중에는 매 프레임 마우스를 따라갑니다.
- Inspector의 `Cursor Offset`은 마우스와 설명창 왼쪽 위 모서리 사이의 픽셀 간격입니다. 기본값은 `(18, -18)`로, 마우스 오른쪽 아래에 표시합니다.
- `Screen Padding`은 카메라 화면 테두리와의 최소 간격이며 기본값은 `12`픽셀입니다. 오른쪽이나 아래 공간이 부족하면 반대쪽으로 옮기고, 카메라 화면 안으로 보정합니다.
- 카메라는 `CraftPrepStageScreen`과 `CraftPrepInfoHotspot`에서 전달합니다. Inspector에서 새 카메라를 수동 연결할 필요가 없습니다.
- `CraftPrepShelfSlot`과 병따개 설명 영역 모두 포인터 진입 시의 화면 좌표를 전달합니다.
- 재료를 선택하거나 선택 해제해 트레이를 갱신해도 마우스가 해당 재료 위에 있으면 설명창을 유지합니다. 마우스를 벗어나거나 선반을 이동하거나 준비 화면을 닫으면 숨깁니다.
- 설명 배경이 고정 크기여서 글자가 삐져나오던 문제는 `CraftPrepItemTooltip.ResizeToContent`에서 제목·설명의 실제 필요 크기를 측정해 해결했습니다. 글씨 크기를 줄이지 않고 기존 `Frame`의 가로·세로를 늘립니다. 기존 배경 크기를 최소값으로 사용하며, 다음 재료를 표시할 때 이전 확대 비율이 누적되지 않도록 원래 크기에서 다시 계산합니다.
- 새 Inspector 설정은 `Content Padding=(16,12)`픽셀, 제목과 본문 사이의 `Text Spacing=6`픽셀, `Max Width=480`픽셀입니다. 최대 너비나 카메라 화면 너비를 넘는 긴 문장은 줄바꿈하고 배경 높이를 늘립니다. 기존 배경이 480픽셀보다 넓으면 그 크기를 우선합니다.
- 실행 중 `Name` / `Description`의 RectTransform 너비·높이·피벗·위치를 내용에 맞춰 갱신합니다. 폰트·글씨 크기·글자 수는 유지하고, 자동 글씨 축소·말줄임은 사용하지 않습니다. 기존 Frame의 뒤집힌 스케일도 유지하며 Sprite 리소스를 교체하지 않습니다.
- 이 크기 수정에서 변경한 파일은 `CraftPrepItemTooltip.cs`, Inspector 기본값을 추가한 `CraftPrepStage.prefab`, 이 기록입니다. GameObject 추가·삭제나 새 컴포넌트·셰이더·머티리얼 추가는 없습니다. 화면 가장자리 보정은 커진 배경 크기를 사용합니다.

### 없어진 설정과 API

- `CraftPrepShelfSlot` / `CraftPrepInfoHotspot`의 `infoAnchor`, `infoDirection` 필드 및 슬롯의 `InfoAnchor`, `InfoDirection` 접근자를 제거했습니다.
- `CraftPrepItemTooltip.Direction` 열거형을 제거했습니다. 위/아래 배치는 화면 여유 공간에 맞춰 자동으로 정합니다.
- 프리팹의 위 필드 연결 46개를 제거했습니다. 기존 앵커 Transform 자식들은 유지하지만 설명창 위치 계산에는 사용하지 않습니다.
- `Show(item, anchor, direction)`은 `Show(item, camera, pointerPosition)`으로 바뀌었습니다. 문자열을 받는 `Show`도 같은 방식입니다.
- `CraftPrepStageScreen.ShowItemInfo`에 `Vector2 pointerPosition` 인자가 추가됐습니다.
- `UpdatePointerPosition(Vector2)`으로 현재 화면 좌표를 직접 전달할 수도 있습니다.

## 앞선 아웃라인 작업에서 추가된 구조

| 파일 | 역할 |
|---|---|
| `Assets/01.Game/02.Scripts/04.Features/01.Crafting/01.Preparation/CraftPrepItemOutline.cs` | 슬롯과 병따개가 함께 사용하는 일반 C# 보조 클래스입니다. GameObject에 추가하는 컴포넌트는 아닙니다. |
| `Assets/01.Game/03.Content/06.Shared/03.Shaders/CraftPrepSpriteOutline.shader` | 준비 화면 전용 셰이더입니다. 마우스의 하늘색 외곽선과 선택 시의 부드러운 금빛 번짐을 그립니다. |
| `Assets/01.Game/03.Content/06.Shared/02.Materials/m_CraftPrepOutline.mat` | 위 셰이더를 사용하는 공유 머티리얼입니다. 준비 화면의 선택 가능한 SpriteRenderer 23개에 연결했습니다. |

- 최초에는 물건 전체 색상을 바꾸던 코드에서 파란색/노란색의 단단한 아웃라인으로 변경했습니다. 이후 참고 사이트의 실제 선택 화면에 맞춰 하늘색 `#62ECFF`, 금빛 `#FFDA7B`로 다듬었습니다.
- 마우스 외곽선은 화면 기준 1.35픽셀, 선택 번짐은 3.6픽셀입니다. 처음 적용한 1픽셀 / 3픽셀에서 바깥으로 조금 더 퍼지도록 조정했습니다. `m_CraftPrepOutline.mat`의 `Hover Width` / `Selection Softness`에서 조절합니다. 셰이더 단위는 원본 이미지 픽셀이 아닌 화면 픽셀입니다.
- 너비를 넓힌 미세 조정은 공유 머티리얼의 두 수치만 변경했습니다. 스크립트, 프리팹 계층, 연결 구조와 색상·0.1초 전환은 그대로입니다.
- 이후 진하기 조정에서는 셰이더와 공유 머티리얼에 `Highlight Strength` 설정을 추가하고 `1.15`로 지정했습니다. 기존 강도 `1`보다 15% 진하게 표시하며, 불투명도는 최대 1로 제한합니다. 외곽선 너비와 번짐 반경은 바꾸지 않았습니다. 새 스크립트나 프리팹 연결은 없습니다.
- 색상은 각 슬롯 및 병따개의 `Hover Color` / `Selected Color`에서 바꿉니다. 선택 상태가 마우스 상태보다 우선합니다.
- 전환은 `CraftPrepItemOutline.TransitionDuration`의 `0.1`초를 사용하고 일시정지와 무관하게 진행됩니다.
- `MaterialPropertyBlock`으로 항목별 상태를 전달합니다. 공유 머티리얼 자체의 색상이나 강도를 매번 바꾸지 않습니다.
- Tight 메시에서 테두리나 번짐이 잘리지 않도록 실행 중에만 `Sprite.Create(..., FullRect)`로 표시용 Sprite를 만듭니다. 이름에 `(Selection)`이 붙습니다. 이 복사본은 저장하지 않고 비활성화할 때 제거하며 원래 Sprite를 복원합니다.
- 하단 트레이의 `Icon`은 표시용 Sprite 복사본이 아닌 원본 에셋을 반환합니다. 원본 PNG, 스프라이트 슬라이스, 임포트 설정은 이 기능을 위해 수정하지 않았습니다.
- 레시피를 본 뒤의 기존 정답 가이드 색상은 별도로 유지됩니다.
- 기존 월드 상호작용용 `OutlineHighlight`와 `m_SpriteOutLine.mat`은 이 준비 화면용 구현과 별개입니다.
- 선택 항목의 콜라이더, 위치, 크기, 부모/자식 구조는 변경하지 않았습니다. 추가된 것은 코드·셰이더·머티리얼과 실행 중의 표시용 Sprite입니다.

## 앞선 A/D 입력 변경

- `Assets/01.Game/05.Settings/01.Input/PlayerInput.inputactions`의 `CraftPrep` 맵에서 `PrepPrevious`를 A, `PrepNext`를 D로 변경했습니다.
- `PlayInputHandler.OnPrepPrevious` / `OnPrepNext`는 키를 누를 때만 처리합니다. 키를 뗄 때의 중복 이동을 막았습니다.
- 바 좌석 이동의 Q/E 입력과 화면의 화살표 버튼은 별도로 동작합니다.

## 수정 위치

- 설명창의 표시·위치·경계 보정: `CraftPrepItemTooltip.cs`
- 재료 포인터 입력: `CraftPrepShelfSlot.cs`
- 도구 및 병따개의 포인터 입력: `CraftPrepInfoHotspot.cs`
- 선택 갱신 시 설명 유지·선반 이동 시 숨김: `CraftPrepStageScreen.cs`
- 실제 연결과 Inspector 기본값: `CraftPrepStage.prefab`

위 스크립트는 모두 `Assets/01.Game/02.Scripts/04.Features/01.Crafting/01.Preparation/`에 있으며,
프리팹은 `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/01.Preparation/`에 있습니다.
