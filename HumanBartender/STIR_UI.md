# 스터 UI 교체 (2026-10-20)

적용 대상:

- 공용 프리팹: `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/02.Stir/NewStirGame.prefab`
- 독립 테스트 씬: `Assets/03.Dev/01.Scenes/01.Minigames/Stir.unity`
- Play 씬의 제조 실행기는 기존 공용 프리팹 참조를 그대로 사용한다. Play 씬 파일은 이번 작업에서 수정하지 않았다.

## 배치 기준과 리소스

`stirring_minigame_V3.png` 시안을 기준으로 960×540 좌표를 사용한다. CanvasScaler는 Expand로 설정해 화면 비율이 달라도 전체 UI가 잘리지 않게 했다. 아래 표는 최초 고정 배치의 기록이다. 이후 화면 크기·여백 수정으로 현재 배치는 아래 추가 항목의 StirLayoutView가 계산한다.

| 대상 | 위치 | 크기 | 리소스/표시 |
| --- | --- | --- | --- |
| 전체 배경 | (0, 0) | 960×540 | 원본 `BG_stir` |
| 왼쪽 연출 마스크 | (-310, 0) | 286×492 | 기존 바 배경과 최신 캐릭터 시트 |
| 캐릭터 | 왼쪽 마스크 안 (0, 30) | 362×440 | 원본 `stir_Sheet` 6프레임 |
| 확대창 | (-148, 148) | 139×243 | 같은 캐릭터 애니메이션 + 원본 `BG_stir_line` 테두리 |
| 원형 입력판 | (156, 11) | 310×310 | 타이머와 잔의 중심 |
| 탑뷰 잔 | 입력판 중심 | 255.75×264 | 신규 `IMG_stir_glass_top` |
| 스푼 | 입력판 중심에서 위로 | 9.5×135.75 | 신규 `IMG_stir_spoon`, 아래 끝이 회전 중심 |
| 타이머 링 | 입력판 중심 | 310×310 / 바탕 292×292 | 원본 `GAU_stir_round_A/B` |
| W/D/S/A | 중심에서 상/우/하/좌 174 | 원본 픽셀의 0.25배 | 대기 C, 현재 A, 다음 입력 B |
| 하단 결과 게이지 | (156, -214) | 531×22.25 | 원본 basic/success/fail |
| 경과 시간 | (397, 214) | 65×30 | mm:ss, 기존 NeoDunggeunmo 폰트 |
| 시간 아이콘 | (350, 214) | 22.3×25 | 원본 `ICN_sitr_time` |
| 시작 안내 | (156, -189) | 260×20 | 첫 W 입력 시 숨김 |

키 글자는 기존 Medium Pretendard 폰트를 사용한다. 원본 메모에 적힌 LAB디지털·Pretendard SemiBold 파일은 프로젝트에 없어서 새 폰트를 추가하지 않았다.

현재 UI 위치는 `Assets/01.Game/03.Content/01.ArtResources/05.UI/03.Crafting/02.Minigames/01.Stir/`다. 임시본에서 작업한 스프라이트·자르기·화질 설정을 원본으로 이관했고, `BTN_key_B`도 같은 원본 폴더를 사용한다.

## 구조와 연결 변경

이전에는 `Playfield/Diagonal Gauge`, 기본 도형으로 그린 잔·스푼·얼음, 캐릭터 무대가 있었다. 배경은 `Start Overlay`의 오래된 이미지에 들어 있었고, 경과 시간과 시작 안내는 StirHudView에 연결되지 않았다.

- `Diagonal Gauge`를 **Result Gauge**로 이름을 바꾸고 회전 없이 가로로 배치했다. 기존 6개 Segment를 재사용한다. 실제 목표 횟수가 다르면 런타임에 부족한 Image 칸을 만들고 남는 칸은 숨긴다. 성공은 파란 이미지, 실패는 빨간 이미지다.
- `Stir Background`, `Timer Ring Base`, `Top View Glass`, `Stir Spoon`, `Elapsed Icon`, `Elapsed Time`, `Start Hint`를 추가했다.
- 기존 `Stir Visuals`에 RectMask2D를 추가하고 왼쪽 연출 영역으로 재배치했다. 기존 배경 하나는 `Bar Background`, 애니메이션 Image는 `Stir Character`로 이름을 바꿨다.
- `Stir Closeup/Stir Closeup Character` 확대 영역과 `Stir Closeup Frame`을 추가했다. `StirAnimationView.linkedImages`에 연결해 기본 연출과 같은 프레임을 표시한다.
- 이전 Input Stage BG, Shell, Character Stage, Side Glass, Start Overlay, 중복 바 배경, Arena Face/Cross, Glass Shadow/Liquid Swirl, 기본 도형 잔, Ice, 스푼 Bowl/Handle/Shaft, 키 Fill, 게이지 Border는 삭제하지 않고 비활성화했다. StirManager의 ice 참조는 해제했다.
- 기존 버튼 캔버스·Serve/Retry 이벤트·입력 라우터·데이터 참조는 유지했다. 조작 순서 W → D → S → A → W, 제한 시간, 성공/실패 판정, 소리 재생 조건도 유지한다. 사운드 매니저가 없는 독립 실행에서는 소리 호출만 생략하도록 null 검사를 추가했다.
- 키 상태 이미지는 StirKeyBadge, 목표 칸 수는 StirHudView → StirGaugeView, 경과 시간·시작 안내는 기존 StirHudView가 관리한다.

## 신규 이미지와 메타 변경

원본 `Assets/01.Game/03.Content/01.ArtResources/05.UI/04.SourceFiles/01.PSD/stirring_minigame_V3.psd`에서 기존 레이어를 내보냈다. PSD와 기존 PNG는 수정하지 않았다.

- `Assets/01.Game/03.Content/01.ArtResources/03.Crafting/04.Minigames/02.Stir/01.Sprites/IMG_stir_glass_top.png`: 그룹 42의 레이어 5·타원 1·타원 2를 원래 합성 상태로 내보낸 탑뷰 잔. 1023×1056.
- 같은 폴더 `IMG_stir_spoon.png`: 레이어 10. 38×543. 두 신규 이미지는 Single Sprite, Point 필터다.
- 최초 교체에서 기존 메타 변경은 **임시 GAU_stir_success.png.meta와 GAU_stir_fail.png.meta 2개**였다. 전체 게이지 스프라이트/fileID는 유지하고 내부 그라데이션 영역 (x24, y18, w112, h52)을 `_fill` 스프라이트로 추가했다. 따라서 칸마다 전체 게이지 테두리를 반복하지 않는다.
- `TEMP_RESOURCE_MAP.json`에 이 두 항목의 원본 재연결 시 임포트 설정 동기화를 기록했다. 매핑 파일의 일부 스프라이트 이름에 메타 본문이 붙어 있던 기록도 실제 이름/fileID로 바로잡았다. 이 정리는 추가 이미지나 메타를 변경하지 않는다.

## 확인

Unity에서 코드 컴파일 및 공용 프리팹의 격리 실행을 확인했다. 준비 상태, 키 4방향, 성공/실패 이미지, 확대창 프레임 동기화, 목표 10칸/4칸 구성, 목표 도달 완료, 완료 시 타이머 비움이 통과했다. 전체 Play 제조 흐름의 CSV 로딩까지 검증한 것은 아니다.

검증 장면과 준비/성공·실패 화면 캡처는 작업 백업에 보관했다. 임시 Editor 코드와 검증 씬은 Assets에서 제거했다.

백업: `C:\Users\HYEON2~1\AppData\Local\Temp\humanbartender-stir-ui-383vo8qx`


## 화질과 화면 크기·여백 수정

원인:

- 캐릭터 시트는 원본이 5820×550인데 Max Size 2048로 가져와 각 프레임이 약 35% 크기로 축소된 뒤 화면에서 확대되고 있었다. 확대창에서 특히 흐려졌다.
- UI 이미지의 압축과 배경의 2048 제한도 가는 선을 손상시켰다.
- 화면은 CanvasScaler Expand를 사용하면서 내부 배치를 960×540에 고정해 넓은 화면에서 추가 좌우 공백이 생겼다.

현재 적용:

- 임시 stir_Sheet의 Max Size를 8192로 변경했다. 임시 UI 10개는 무압축으로 바꾸고 3840 배경과 2124 게이지는 Max Size 4096을 사용한다. 실제로 사용하는 원본 BTN_key_B도 무압축으로 바꿨다. PNG 파일 자체는 변경하지 않았다. 기존 플랫폼 항목의 값만 수정했으며 새 플랫폼을 추가하지 않았다.
- `Game Canvas`에 **StirLayoutView**를 추가했다. 좌우·위아래 18 UI 단위 여백, 패널 간격 16을 기준으로 실제 Canvas 크기에 맞춰 배치한다. 왼쪽 영역은 304를 기준으로 유지하고 추가 화면 폭은 오른쪽 영역과 게이지에 배분한다. 원형 입력판은 균등 배율을 사용한다.
- 고정 전체 배경 `Stir Background`는 **Left Panel Frame**으로 재사용하고 **Right Panel Frame / Right Panel Grid** 두 Image를 추가했다. 같은 BG_stir에 좌/우 프레임 2개와 68×68 격자 타일을 추가해 사용한다. 프레임은 9-slice, 격자는 Tiled로 표시하므로 화면 폭이 달라도 테두리 두께와 격자의 정사각 비율이 유지된다. 기존 전체 배경 스프라이트와 ID는 유지했다.
- 캐릭터·확대창·잔·스푼·시간 아이콘 등은 Image.preserveAspect로 원래 비율을 유지한다. 확대창도 테두리 원본의 1205:2029 비율을 사용하고 위 여백에 맞춰 배치한다.
- 게이지 배경에 9-slice 테두리를 지정했다. 창 크기가 바뀔 때 StirGaugeView.RefreshLayout은 칸의 위치만 조정하므로 이미 기록된 성공/실패 결과는 유지된다.
- 공용 프리팹과 현재 Stir 씬에 함께 반영했다. 열려 있던 Stir 씬의 미저장 상태는 먼저 복사해 백업하고 그 위에 UI 변경을 적용했다.

추가된 구조는 StirLayoutView 컴포넌트와 Right Panel Frame / Right Panel Grid 오브젝트 2개다. 기존 오브젝트는 삭제하지 않았다. 새 PNG는 만들지 않았다. 이번 메타 수정은 스터 임시 이미지 11개와 직접 사용하는 원본 키 이미지 1개에 한정한다. 원본 재연결 매핑에도 자르기·압축·해상도 설정 동기화를 기록했다.

검증: 16:9(960×540), 16:10(960×600), 21:9(1260×540)에서 패널 여백·높이와 크기 변경 후 판정 이미지 유지를 확인했다. 임포트된 캐릭터 텍스처 너비 5820, 배경 너비 3840도 확인했다.

화질/배치 수정 전 백업: `C:\Users\HYEON2~1\AppData\Local\Temp\humanbartender-stir-quality-mpjiimu2`


## 공용 타이머 적용과 스터 10회

- Play 씬 `Gimmick HUD/Panel/Time`의 기존 텍스트를 `Timer/Time`으로 옮기고, 스터의 `ICN_sitr_time`를 사용하는 `Timer Icon`을 추가했다. 공용 타이머가 모든 제조 기믹에서 시계 아이콘 + `mm:ss`로 표시한다. 가로가 넓은 화면에서도 스터와 같은 균등 배율을 사용하도록 시계 묶음만 크기·우측 상단 여백을 조정한다.
- 이전 `TIME 경과초 / 제한초` 표시를 분:초 표시로 교체했다. 실제 값은 기존 CraftTimer의 한 잔 누적 제조시간이다. 기믹이 바뀔 때 초기화하지 않는다. 제한시간에 따른 제조 판정은 그대로이며 화면에서 제한시간 숫자만 생략한다. 정답 재료가 없어 시간이 가려지는 경우는 `??:??`를 표시한다.
- 독립 Stir 씬에서는 기존 로컬 경과 시간과 아이콘을 표시한다. 공용 실행기로 시작한 스터는 StirHudView.elapsedText/elapsedIcon을 함께 숨겨 공용 타이머만 보이게 했다. 잔 주변 2초 링은 한 바퀴 제한시간이므로 계속 표시한다.
- CraftTimer.FormatElapsed가 공용 HUD와 독립 스터의 분:초 형식을 함께 처리한다. CraftGimmickHud.timeRoot는 아이콘·글자를 함께 감춘다. 기믹이 시간 표시를 직접 소유하는 경우에도 둘을 함께 숨기고 다음 기믹에서 복구한다.
- `Assets/StreamingAssets/csv/settings/balance_config.csv`의 `stir_target_stacks`를 **6 → 10**으로 변경했다. 원본 설정 엑셀 BalanceConfig!C92는 이미 10이어서 수정하지 않았다. 런타임이 CSV를 읽으므로 CSV와 원본 값이 이제 일치한다.
- StirManager의 코드 기본값, 공용 NewStirGame 프리팹과 독립 Stir 씬의 fallbackStackCount, 기존 StirSceneSetup의 생성 게이지 개수를 모두 10으로 맞췄다. 프리팹과 씬에는 기존 6칸을 유지하고 Segment 6~9를 추가해 편집 화면에서도 10칸으로 보이게 했다.

이번 구조 추가는 공용 HUD의 Timer/Timer Icon 두 오브젝트와 각 스터 게이지의 네 칸이다. 기존 Time 텍스트의 ID와 연결을 유지했고, 스터 타이머 오브젝트도 삭제하지 않았다. 이미지·메타는 수정하지 않았다.

검증: 00:00, 00:59 → 01:00, 61:05 표시, 기믹 전환 후 누적 시간 유지, 숨김 시간, 시간 소유권에 따른 아이콘/문자 동시 숨김·복구, 독립 스터 시계 유지, 공용 실행 시 중복 제거, 9회 뒤 계속 진행/10회 뒤 완료와 만점 판정, CSV 구조 검증을 통과했다. 전체 스토리 플레이 흐름을 재실행하지는 않았다.

백업과 검증 화면: `C:\Users\HYEON2~1\AppData\Local\Temp\humanbartender-common-timer-vtdkb14i`


## 임시 리소스 최종 정리 (2026-10-20)

임시 스터 이미지 11개의 자르기·테두리·무압축·Max Size 설정을 원본 메타로 이관했다. BG_stir의 프레임 2개·격자 타일과 게이지 fill 2개를 포함한다. NewStirGame 프리팹, 독립 Stir 씬, Play 공용 타이머는 모두 원본 아트를 사용한다. 10회 목표, 누적 시간, 배치와 기존 비활성 오브젝트는 유지한다. 위 임시본 작업 설명은 이전 변경 기록이다.
전체 이관·삭제 목록과 검증 결과는 `TEMP_RESOURCE_FINALIZATION.md` 및 `RESOURCE_FINALIZATION.json`을 참고한다.
