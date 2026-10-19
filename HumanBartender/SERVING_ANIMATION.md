# 서빙 애니메이션 리소스 교체

## 적용 결과

- 사용 이미지: `Assets/01.Game/03.Content/01.ArtResources/04.Cutscenes/04.ServingAnime`
- 배경과 음료 시트 총 9개를 **가로 6 × 세로 5**로 자릅니다. 왼쪽에서 오른쪽, 위에서 아래로 0~29 순서입니다.
- 원본 시트 크기는 5738×2707입니다. Count 분할의 내림 계산으로 한 칸은 956×541이며, 나머지 가장자리 2픽셀은 제외합니다. 프레임을 개별 그림 영역에 맞춰 자동으로 자르지 않습니다.
- 스프라이트 GUID·이름·fileID는 유지하고 영역과 중앙 피벗만 갱신했습니다. PNG 이미지 자체는 수정하지 않았습니다. 최종 정리에서 원본 아트 메타에 격자·피벗·필터·압축 설정을 반영했습니다.
- 배경은 **`background_0` 한 장을 고정**해 사용합니다. 배경 시트의 다른 29개 스프라이트는 재생하지 않습니다.
- 서빙 화면은 960×540 기준이며, 음료도 전체 프레임 크기로 표시합니다. 원래 시트에 그려진 이동과 확대가 그대로 표시됩니다.

## 생성된 애니메이션 8종

폴더: `Assets/01.Game/03.Content/07.Animations/02.Crafting/02.Serving/01.Clips`

| 칵테일 ID | 클립 | 사용 시트 |
| --- | --- | --- |
| bottle_beer | Serve_bottle_beer.anim | give_drink_beer |
| cosmopolitan | Serve_cosmopolitan.anim | give_drink_cosmopolitan |
| dry_martini | Serve_dry_martini.anim | give_drink_drymartini |
| gin_fizz | Serve_gin_fizz.anim | give_drink_ginfizz |
| gin_tonic | Serve_gin_tonic.anim | give_drink_gin_tonic |
| kahlua_milk | Serve_kahlua_milk.anim | give_drink_kahluaMilk |
| screwdriver | Serve_screwdriver.anim | give_drink_screwdriver |
| tequila_sunrise | Serve_tequila_sunrise.anim | give_drink_tequila_sunrise |

각 클립은 기존 서빙 화면과 같은 21fps, 30프레임, 약 1.43초이며 한 번 재생한 뒤 마지막 프레임을 유지합니다. 전용 리소스가 없는 다른 음료는 기존 규칙처럼 코스모폴리탄 연출을 사용합니다.

## Play 씬 구조와 재생 방식 변경

위치: `Play.unity → 05.UI → serve Canvas → BG → drink`

| 대상 | 이전 | 현재 |
| --- | --- | --- |
| BG의 Image | 이전 배경 | 새 background_0 고정 |
| drink의 Image | 이전 음료 이미지, 375×528 | 새 프레임, 960×540, 중앙 배치 |
| ServeScreen 목록 | 5개 항목, 진토닉·진피즈 같은 클립 공유 | 8개 전용 클립 연결 |
| drink의 Animation | Legacy 재생 컴포넌트 | 제거 |
| drink의 Animator | 이전 controller 연결 | 기존 컴포넌트 재사용, controller 연결 해제 |
| ServeScreen의 Ani 필드 | Animation 참조 | Animator 참조로 변경 |
| 재생 종료 처리 | 런타임 Legacy 복사본·AnimationEvent | PlayableGraph로 재생, 종료 시 Apply 호출 |

`ServeScreen.cs`는 Animator에 AnimationClipPlayable을 연결합니다. 종료 시 마지막 프레임에서 멈추고 등급·제공·버리기 버튼을 표시합니다. 재시작·Reset·비활성화·파괴 시 그래프를 정리합니다. Play/TryPlay/Apply/Discard/Provide/Reset과 제조 흐름의 연결은 유지했습니다. 이전 애니메이션 에셋과 컨트롤러 파일은 삭제하지 않았습니다.

열려 있던 Play 씬의 최신 수정 내용을 먼저 복사한 후 연결했습니다. 그 복사본과 비교해 변경된 오브젝트 블록은 서빙 UI의 7개 블록뿐임을 확인했습니다.

## 기존 서빙 Timeline 4종

다음 에셋도 같은 새 배경과 애니메이션 클립을 직접 참조하도록 갱신했습니다.

- `Serve_Cosmopolitan.playable`
- `Serve_Dry.playable`
- `Serve_Ginfizz.playable`
- `Serve_kalua.playable`

기존 Timeline 구간(2.5초)과 신호 타이밍은 유지합니다. 새 클립을 해당 구간 길이에 맞춰 재생하므로 30프레임이 전체 구간에 걸쳐 표시됩니다. 새 이미지가 없는 보관용 `Serve_Unknown`은 변경하지 않았습니다.

`UI Timeline` 스크립트의 추가 항목:

- `CutSceneBGBehaviour.backgroundSprite`: 배경을 직접 연결합니다. 없으면 기존 Resources 경로를 사용합니다.
- `CutSceneImageBehaviour.sourceSprite`: 최초 스프라이트를 직접 연결합니다. 없으면 기존 경로를 사용합니다.
- `CutSceneImageMixerBehaviour`: 위 직접 참조를 우선 사용합니다.
- `CutSceneSpriteAnimBehaviour.animationClip`: UI 클립의 프레임을 Timeline 시간에 맞춰 샘플링합니다. 없으면 기존 시트 경로 방식을 사용합니다.
- UI 스프라이트 바인딩에 Animator가 필요한 경우 Timeline 이미지에 실행 중에만 추가하고, 정지·종료 때 자신이 추가한 컴포넌트만 제거합니다.

## 메타 변경과 원본 환원

최초 메타 변경 대상은 임시 서빙 PNG 9개였으며, 최종 정리에서 원본 메타 9개로 이관했습니다. 6×5 슬라이스와 중앙 피벗을 설정한 의도적인 변경이며 GUID와 스프라이트 fileID는 유지했습니다.

`TEMP_RESOURCE_MAP.json`의 9개 경로를 이동한 임시 폴더에 맞추고, `spriteGrid` 및 `requiresImportSettingsSyncOnFinalize`를 갱신했습니다. 최종 정리에서 원본에 격자 설정을 옮기고 새 클립 8종·Play 씬·Timeline 4종을 원본 GUID로 재연결했습니다. 임시 매핑의 최종 결과는 루트 `RESOURCE_FINALIZATION.json`에 기록했습니다.

## 검증과 백업

- 8종 × 30프레임의 스프라이트·크기·중앙 위치 샘플 검사
- Unity Play Mode에서 8종과 전용 리소스가 없는 음료의 대체 연출 재생
- 배경 고정, 마지막 프레임 유지, 결과 버튼 활성화
- Reset·중간 비활성화 시 그래프 정리
- Timeline 4종의 처음·중간·마지막 프레임 재생
- 9개 시트의 격자 영역과 GUID/fileID 유지 검사

검증용 Editor 스크립트·씬·프리팹은 제거했습니다. 수정 전 파일, 열려 있던 씬 복사본, 검증 코드·결과·이미지는 아래에 보관했습니다.

`C:/Users/HYEON2~1/AppData/Local/Temp/humanbartender-serving-refresh-dfotc4lh`


## 임시 리소스 최종 정리 (2026-10-20)

임시 아트 참조를 원본으로 재연결했고 `Assets/99.TempResources`를 제거했다. 씬·프리팹의 배치와 재생 동작은 유지한다.
전체 이관·삭제 목록과 검증 결과는 `TEMP_RESOURCE_FINALIZATION.md` 및 `RESOURCE_FINALIZATION.json`을 참고한다.
