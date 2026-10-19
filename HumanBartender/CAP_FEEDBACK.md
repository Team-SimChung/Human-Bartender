# 병따기 오프너·성공 탄산 연출

## 적용 위치

- 공용 게임 프리팹: `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/04.Cap/NewCapGame.prefab`
- 처리 코드: `Assets/01.Game/02.Scripts/04.Features/02.Minigames/05.Cap/CapManager.cs`
- 현재 VFX 확인 씬: `Assets/03.Dev/01.Scenes/03.Vfx/Cap 1.unity`
- 일반 병따기 테스트 씬: `Assets/03.Dev/01.Scenes/01.Minigames/Cap.unity` (공용 프리팹 변경 상속)

## 동작

- 판정을 받을 수 있을 때 클릭·스페이스 입력이 들어오면 오프너가 병 입구를 축으로 들어 올려졌다가 복귀합니다. 실패한 입력도 움직이고, 입력 없이 고리를 놓친 경우는 움직이지 않습니다.
- 기본 동작 시간은 0.32초, 위로 0.24834, 기준 회전 -24도입니다. 자식 Transform의 음수 X 스케일 또는 SpriteRenderer.flipX를 확인해 회전 방향을 자동 반전합니다. 현재 좌우 반전된 병따개는 +24도로 움직여 오른쪽 손잡이가 올라갑니다. CapManager의 Opener 항목에서 조정할 수 있습니다.
- 성공 시 `FX_Carbonated_Gintonic 1`을 재생하고 기존 뚜껑 물리 발사와 사운드를 함께 실행합니다. 실패·시간 초과에는 탄산을 재생하지 않습니다.
- 탄산은 0.3초 동안 분출한 뒤 새 입자 생성을 멈춥니다. 파티클 반복도 실행 사본에서 해제합니다. 성공 후 진행 대기시간(0.9초)과 판정·난이도는 그대로입니다.
- 매니저가 비활성화되면 오프너 자세를 복원합니다. 생성한 이펙트는 제거하고, 씬에 원래 배치된 이펙트는 입자를 지우고 끕니다.
- 사운드가 주입되지 않는 독립 VFX 테스트 씬에서도 성공 처리를 실행할 수 있게 사운드 호출에 null 검사를 추가했습니다.

## 추가된 구조와 연결

```
NewCapGame
└─ Cap Root
   └─ Ring Center
      ├─ Bottle                 (기존)
      ├─ Cap Piece              (기존, 성공 시 날아감)
      ├─ Opener Pivot           (추가, 회전·상승 기준)
      │  └─ Opener              (추가, SpriteRenderer)
      └─ Bottle Mouth           (추가, 고정된 탄산 생성 위치)
```

- Opener는 사용자가 Cap 씬에서 선택한 `Assets/01.Game/03.Content/01.ArtResources/03.Crafting/01.Preparation/02.Tools/01.X1/bottle_opener.png`를 공용 프리팹에도 반영했습니다. 자식 X 스케일은 -1.61로 좌우 반전 상태를 유지합니다. 최종 정리에서 해당 단일 오프너의 원본으로 재연결했습니다.
- Opener의 기본 Sorting Order는 16입니다. 생성된 탄산은 병뚜껑과 같은 Sorting Layer에서 뚜껑보다 1 높은 순서로 그립니다.
- `openerPivot`에는 Opener Pivot을, `successEffectAnchor`에는 Bottle Mouth를 연결했습니다.
- `successEffectPrefab`에는 `Assets/01.Game/03.Content/06.Vfx/04.Prefabs/FX_Carbonated_Gintonic 1.prefab`을 연결했습니다. 일반 게임에서는 성공할 때만 런타임 사본을 생성해 병 입구에 배치하고 크기는 0.724325배입니다(기존 0.35 × 병 확대 비율 2.0695). 프리팹 원본의 파티클 설정은 수정하지 않습니다.
- `successEffectOverride`가 지정되면 런타임 생성 대신 그 씬 오브젝트를 재생합니다. 기존 위치·크기를 유지하며 시작 시에는 끕니다.

### 공용 크기와 씬 오버라이드 정리

- 병과 뚜껑의 스케일은 Cap 1 씬과 같은 **4.139**로 설정했습니다. 이전 2 대비 **2.0695배**입니다.
- Ring Center 기준 병 위치는 (0, -4.11, 0), 뚜껑 위치는 (0.015, -0.915, 0)입니다.
- Opener Pivot도 (0.015, -0.915, 0)에 놓고 스케일을 2.0695로 키웠습니다. 자식 Opener는 사용자가 선택한 이미지와 방향을 유지하고, 위치를 (0.25, -0.26, 0)으로 맞춰 병따개 머리가 병 입구에 걸리도록 했습니다.
- Target Ring, Incoming Ring, Bottle Mouth를 모두 뚜껑 중심 (0.015, -0.915, 0)에 맞췄습니다. 고리 반지름·판정 폭·접근 속도는 변경하지 않았습니다.
- 공용 프리팹으로 옮긴 병·뚜껑·오프너·입구 설정에 해당하는 Cap 1 씬 오버라이드 16개, Cap 씬의 병따개 설정 7개를 제거했습니다. 두 씬 모두 공용 크기와 방향을 상속합니다.
- 새 GameObject나 컴포넌트는 추가하지 않았습니다. 이전에 추가한 Opener Pivot/Opener/Bottle Mouth 구조를 계속 사용합니다.
- Cap 1 씬의 별도 `FX_Carbonated_Gintonic 1`은 계속 successEffectOverride로 연결되어 있습니다. 시작 시 비활성화하고 성공 시 기존 배치에서 재생합니다. 작업자의 파티클 배치·삭제·추가 설정은 유지했습니다.

## 검증

Unity Play Mode에서 Cap 1 씬의 기존 배치 연출과 공용 프리팹의 생성 연출을 각각 확인했습니다.

- 시작·실패 시 성공 이펙트 미재생
- 실패·성공 입력 시 오프너 이동 및 회전, 기본 자세 복귀
- 실패 쿨다운 후 재시도
- Cap 1/공용 프리팹의 병·뚜껑·병따개 크기 일치, 고리와 탄산 발생점의 입구 정렬
- 음수 X 스케일·일반 방향·음수 X 스케일과 flipX 동시 적용 시 손잡이가 위로 움직이는지 확인
- 성공 시 입자 생성, 반복 해제, 분출 종료
- 기존 뚜껑 물리 발사 및 성공 대기 후 게임 종료
- 비활성화 시 이펙트 정리

검증용 임시 Editor 스크립트는 제거했습니다. 결과·성공 장면·수정 전 파일 백업은 다음 위치에 있습니다.

`C:/Users/HYEON2~1/AppData/Local/Temp/humanbartender-cap-feedback-h1dxk5dl`

크기·방향 수정 전 백업 및 추가 검증 결과:

`C:/Users/HYEON2~1/AppData/Local/Temp/humanbartender-cap-scale-direction-3lndrhth`


## 임시 리소스 최종 정리 (2026-10-20)

임시 아트 참조를 원본으로 재연결했고 `Assets/99.TempResources`를 제거했다. 씬·프리팹의 배치와 재생 동작은 유지한다.
전체 이관·삭제 목록과 검증 결과는 `TEMP_RESOURCE_FINALIZATION.md` 및 `RESOURCE_FINALIZATION.json`을 참고한다.
