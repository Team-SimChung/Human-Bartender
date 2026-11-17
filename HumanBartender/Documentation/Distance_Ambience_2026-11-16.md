# 거리 환경음 파츠

공용 원본은 `Assets/01.Game/03.Content/08.Prefabs/04.Shared/03.Audio/DistanceAmbientAudio.prefab`이다. AudioSource와 DistanceAmbientAudio를 미리 저장하며 실행 중 오브젝트를 생성하지 않는다. 다른 환경음은 원본을 씬에 배치한 뒤 클립·플레이어·거리·날짜를 연결한다.

Home의 `04.Gameplay/IndoorAmbience/WindowRainAudio`가 실내 창문 중앙에서 기존 `rain_loop.ogg`를 재생한다. 플레이어와 음원 사이의 월드 XY 거리로 계산하므로 카메라 이동과 Z 깊이에 영향을 받지 않는다. 현재 배경 그림의 창문 중앙에 배치했으며 Transform으로 위치를 조절할 수 있다.

Inspector 설정:

- 음원 연결: 전용 AudioSource, 반복할 Clip, 거리 기준 Listener(플레이어 Transform).
- 거리별 음량: Base Volume, Full Volume Distance, Silent Distance, Distance Curve, Fade Seconds.
- 날짜별 재생: Days 배열의 Day와 Play. 배열 순서 대신 Day 값으로 찾는다. 빈 배열·미등록 날짜·중복 날짜에는 재생하지 않는다.

기본 거리는 0.75 이내 최대 음량, 4 이상 무음이며 사이에서는 곡선에 맞춰 줄어든다. 기본 음량은 0.3, 변화 시간은 0.5초다. Home 창문은 우선 0·1·2일차 재생, 3일차 정지로 연결한다. 실제 날짜에 맞춰 배열의 체크를 바꾸면 된다.

출력은 기존 효과음 믹서로 보내므로 설정창 효과음 음량이 적용된다. 대화·일시정지 중에는 기존 환경음 음량 규칙을 사용한다. 포커스 상실과 비활성화 시 즉시 정지하며 다시 활성화되면 조건에 따라 페이드인한다.

HomeTerracePresentation의 Indoor Ambience 배열에 등록된 환경음은 테라스 진입 시 페이드아웃하고 실내 복귀 시 다시 거리·날짜 조건을 확인한다. 공용 파츠는 이 홈 전환 담당자에 의존하지 않으며 다른 씬에서는 `SetPlaybackAllowed`로 장소 조건을 연결할 수 있다.

설치 메뉴는 `Tools/Home/Connect Window Rain Audio`이다. 기존 소리의 클립·날짜·음량·위치 설정은 덮어쓰지 않는다. Home 씬에 저장하지 않은 변경이 있으면 자동 저장하지 않고 경고한다.

검증은 DistanceAmbientAudioTests에서 거리·날짜·중복 일정·페이드·효과음 믹서·실내 전환 연결을 확인한다.

2026-11-16 검증: 거리 환경음 15개, 홈 전환 4개가 통과했다. 연결 도구를 두 번 실행한 뒤 Home 씬과 공용 프리팹의 해시가 동일한 것도 확인했다. 기존 따르기 검사를 포함한 전체 78개 중 편집용 팝업 미리보기 상태를 고정하던 검사 1개를 실행 초기화 기준으로 정리했으며, 해당 검사와 홈 전환 5개를 재실행해 모두 통과했다. 최종 고유 검사 78개가 통과했고 결과는 `Logs/HomeDistanceAmbience/validation-summary.json`에 있다.

씬 저장 시 Pixel Perfect Camera가 자동 변경한 뷰포트 값은 기존 값으로 복원했다. 실제 스피커로 최종 음량 밸런스를 조절하는 청감 확인은 수행하지 않았다.
