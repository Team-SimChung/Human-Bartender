# Home 테라스 전환과 대화창

## 실행

- 기존 소파 상호작용 → `수면` → 해당 날짜의 취침 대화를 실행한다.
- 테라스 대화(`night0`, `night1`, `night2`, `ending`)는 **0.35초 페이드 아웃 → 테라스 카메라로 즉시 전환 → 0.35초 페이드 인** 순서로 시작한다.
- `수면`을 선택하면 취침 대화와 다음 날 전환이 끝날 때까지 **Esc·게임패드 취소를 무시**한다. 이때 Esc로 옵션 창도 열리지 않는다. 수면 선택 전 소파 메뉴는 기존처럼 취소할 수 있다.
- 대화 전체가 끝나면 테라스 화면을 유지하다가 **취침 페이드 아웃(0.5초) → 날짜 증가·침대 앞으로 이동·실내 카메라 복원 → 페이드 인(0.5초)** 순서로 다음 날을 시작한다. 대화 직후 실내 화면이 잠깐 나타나는 전환은 제거했다.
- 0→1·1→2·2→3일차는 침대 앞에서 시작한다. 최종 3일차 종료 시 `TempEnding`으로 이동하는 기존 엔딩 분기는 유지한다.
- 오브젝트 비활성화·씬 언로드 같은 수명 종료 취소와 오류 정리는 계속 작동한다. 취침 실패 시에는 실내 화면으로 복귀하고 메뉴를 다시 표시한다.
- `workshop`은 테라스 대상 목록에서 제외하여 기존 표현을 유지한다. CSV의 대사, 조건, 완료 플래그, 미제작 회상 Timeline 건너뛰기 정책은 기존 실행기를 사용한다.

## Home씬에 추가한 구조

```text
02.Cameras
  CM_Terrace                         [CinemachineCamera, CinemachinePixelPerfect]
Terrace_BG_City                       [HomeTerracePresentation, WorldDialogueSpeakers]
  Terrace                            (기존 배치)
  balustrade                         (기존 배치)
Terrace_0                            (기존 캐릭터 애니메이션)
  LunaSpeechAnchor                   local (-0.15, -0.35, 0)
  ChrisSpeechAnchor                  local ( 0.14,  0.16, 0)
04.Gameplay
  HomeWakeUpSpawn                     world (-0.82, -0.7, -1)
```

- `CM_Terrace`는 기존 Main Camera를 제어하는 Cinemachine 카메라이며, 추가 렌더링용 Camera 컴포넌트는 만들지 않았다. 시작 시 비활성, Priority 200, Orthographic Size 1.35이다.
- 카메라 위치는 배치된 `Terrace_BG_City` 중심을 사용하고 Z는 기존 Main Camera와 같게 연결했다. 기존 배경·난간·캐릭터의 위치, 크기, Sprite, Animator 연결은 유지했다.
- `HomeTerracePresentation`에 카메라, 기존 Main Camera의 CinemachineBrain, 화자 목록, 루나 기준점을 연결했다. `Fade Duration`과 `Story Ids`를 Inspector에서 조절할 수 있다.
- 기존 `InteractionSystem/WorldInteractionController`의 새 `Home Terrace` 필드에 이 컴포넌트를 연결했다.
- 실제 이동은 화면을 보여주는 카메라 전환으로 처리한다. 실내 플레이어의 위치와 이동 카메라의 추적 관계는 유지한다.
- 다음 날에는 `HomeTerracePresentation`의 새 `Wake Up Spawn`에 연결한 `04.Gameplay/HomeWakeUpSpawn`으로 실내 플레이어를 옮긴다. 침대 위치를 바꾸면 이 기준점을 이동하면 된다. 입장 시 사용하는 기존 `HomeSafeSpawn`과는 별도다.
- `IWorldInteractionCancellationPolicy`를 추가하고 `HomeSofaAction.CanCancel`로 수면 중 사용자 취소를 차단한다. 컴포넌트 종료에 필요한 강제 취소는 유지한다.
- `WorldInteractionActionContext`에 `PrepareMorning`과 `FinishNight` 콜백을 추가했다. 취침 서비스가 검은 화면에서 날짜를 바꾼 뒤 아침 위치와 카메라를 준비하며, 정상 종료 전까지 테라스 카메라를 유지한다. 취침 실패로 날짜가 복구되면 플레이어 위치도 취침 전 위치로 복구한다.

## 화자별 위치

| CSV actor | 기준점 | 말풍선 배치 | 꼬리 |
|---|---|---|---|
| `luna` | `Terrace_0/LunaSpeechAnchor` | 기준점 아래, 추가 Y 오프셋 -8 | 말풍선 위에서 화자 방향으로 향함 |
| `chris` | `Terrace_0/ChrisSpeechAnchor` | 기준점 위, 추가 Y 오프셋 +8 | 말풍선 아래에서 화자 방향으로 향함 |

- 기준점은 캐릭터의 자식이므로 `Terrace_0`을 옮기면 함께 이동한다.
- 기존 `OutsideDialogueTextBox` 프리팹의 글꼴·크기·외형·타이핑을 재사용한다. 말풍선 전체 높이를 고려해 위/아래에 붙이고 화면 내부 제한도 유지한다.
- `WorldDialogueSpeakers`의 화자 항목에 `Placement`와 `Bubble Offset`을 추가했다. 기존 연결은 기본값인 위쪽 배치를 사용한다.
- `WorldInteractionView.Begin`의 마지막 선택 인수로 화자 목록을 전달할 수 있다. 테라스에서는 플레이어 대신 배치된 화자 기준점을 사용하며, 종료 후 일반 대화 위치로 복원한다.
- 회상 Timeline 이후에도 테라스가 활성 상태라면 같은 화자 목록으로 복귀한다.

## 이전 UI 구조 변경 기록

재료 선택 화면의 아웃라인, A/D 이동, 마우스 추적 설명창, 삭제한 Info Anchor/Direction 연결, 런타임 Sprite 복사, 설명 배경 자동 크기 조절 기록은 [CRAFT_PREPARATION_UI.md](CRAFT_PREPARATION_UI.md)에 정리되어 있다.
