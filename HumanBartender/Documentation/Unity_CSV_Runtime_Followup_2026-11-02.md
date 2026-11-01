# CSV 고정 상태에서의 Unity 사건 실행

CSV 구조·명령 표기·스키마와 Cutscene Studio 본체를 유지하고 Unity 실행·연결부를 수정했다.
웹 정본 커밋은 기존 b2f8ca7a524f19363d0f8c8af6fa075e5b5661b4 그대로다.

## 책임과 원본

| 영역 | 담당 | 유지하는 경계 |
|---|---|---|
| 대사·선택지·결과 스텝 | 기존 CSV와 NewDataLoadManager / IStoryContentSource | 실행용 투영을 만들 수 있지만 원본 배열과 CSV를 수정하지 않는다. |
| 웹 표현의 변환 | StoryCsvCompiler | 로드 경계에서만 해석하며 memoryKey도 보존한다. |
| 사건 규칙·효과 요청 | ShopStoryEvents / NeighborhoodStoryEvents / StreetStoryEvents | 기존 IConditionUtil을 사용하며 돈·진행 상태 사본을 보유하지 않는다. |
| 월드 표시·동작 | 씬 대상의 WorldStoryActors | 자신의 하위 Transform·SpriteRenderer만 제어한다. 게임 플래그와 씬 이동을 처리하지 않는다. |
| 화자 앵커 | 대상의 WorldDialogueSpeakers | 발 위치와 말풍선 머리 기준점을 분리해 씬에 저장한다. |
| 대화·입력·세션 | DialogueRunner / WorldInteractionController / InteractionStateLease | 실행별 scope와 CTS를 사용하고 종료에서 UI·참조·자세·입력 소유권을 정리한다. |
| 실제 컷신 | 기존 StudioPlayer / StudioGameplayScope / StudioPlaybackService | 플러그인 내부 구현을 수정하거나 별도 재생 시계를 만들지 않는다. |
| 기억의 표시 데이터 | CsvStoryCutsceneProjection | 현재 CSV에서 일시적인 StudioSequence / Timeline / Clip을 만들고 재생 종료 뒤 제거한다. 영속 대사 복제본을 저장하지 않는다. |
| 일차·씬 이동 | GameProgressionService | 대화 정리 후 이동 의도를 인계한다. 이전 씬의 파괴 토큰을 전역 이동에 전달하지 않는다. |
| 씬 시작 자리 | OutsideStorySceneRules.ResolveSpawn / Player | 규칙은 순수 함수가, 위치는 씬 Transform이 소유한다. 엘리베이터 대기 층도 같은 규칙을 따른다. |
| 첫 출근 도착·새 게임 오프닝 | FirstCommuteArrival / NewGameOpening | 씬 컨텍스트가 준비를 마친 뒤 실행한다. 오프닝 완료는 `flag.opening_seen`으로 기록한다. |
| 바 출입 전환 화면 | BarDoorTransitionView (ISceneTransitionOverlay) | SceneTransitionManager가 페이드 위에 띄운다. 문구·시간은 컴포넌트, 배치는 프리팹이 소유한다. |
| 튜토리얼 문구 | StoryTutorialController.copy | 단계 진행 규칙과 분리한 직렬화 표. 빠진 키는 기본 문구를 쓰고 경고한다. |
| 서빙 자리 그림 배치 | StoryServeZone.prefab / StoryServeZoneLayout | 코스터·잔 위치는 프리팹 앵커가, 표시 여부는 컴포넌트가 정한다. |

웹 본편 0일차 흐름의 반영 상태는 [Web_Parity_Day0_2026-11-02.md](Web_Parity_Day0_2026-11-02.md)에 있다.

## 월드 행동과 컷신

접근·퇴장·고양이 발견·먹이·쓰다듬기·입양·수리·크리스 등장·불량배 타격 반응은 C# 월드 동작으로 처리한다.
씬에 배치된 SpriteRenderer와 취소 가능한 DOTween을 사용하며, 동작 대기만 수행하고 성공시키는 경로는 없다.
이전에 모든 동작에 붙였던 outside/neighborhood/... 등 16개 Studio ID와 매핑 클래스는 제거했다.
16개 컷신 에셋 제작은 필수 조건이 아니다.

CSV의 timeline과 cinemaAfter는 계속 기존 Studio 재생 경로를 사용한다.
대상의 명시적 StudioSequence 또는 StudioCatalog를 먼저 조회한다. 기억 연출의 등록 에셋이 없는 경우에는
CSV의 johnny / terraceMemory / terraceMemory3 대사를 그대로 일시적인 Studio 표시 데이터로 투영한다.
각 줄의 입력 대기는 기존 StudioClock이 처리한다. 필수 기억을 완료하기 전에 스킵해 관찰 완료로 기록하지 않는다.
이는 대사 기반 재생 경로이며 전용 컷신 그림·카메라 연출의 제작 완료를 의미하지 않는다.

## 데이터 중복과 진행

- 불량배의 선택지·결제 결과와 용돈 수락/거절 대사·결과는 기존 CSV에서 조회한다.
- 용돈 금액도 원본 CSV 효과를 사용한다. Unity는 기존 획득 플래그로 중복 지급만 방지한다.
- 갱단의 지불·수리 약속 대기 중에는 같은 거래를 다시 수락하지 않는다. 돈이 남아 있어도 중복 차감하지 않는다.
- 기존 stock goto는 Unity 상점 처리기로 투영한다. 과거 stock 분기를 실행하려고 정본 CSV를 변경하지 않는다.
- 기억의 memoryKey를 보존해 ending의 terraceMemory3가 다른 기억으로 바뀌지 않게 한다.
- 조니 기억은 실제 재생 성공 후 campaign_observation을 기록한다. 취소·실패 시 기록하지 않는다.
- 입양 후 첫 취침의 neighborhood-rest는 현재 입양/시청 플래그로 실행한다. 웹 장면의 보관용 day 값으로 누락시키지 않는다.
- 입양을 완료한 실제 동작 뒤에는 이동 의도를 반환하고 Controller가 UI·입력 lease를 정리한 뒤 Home 이동을 인계한다.

## 수명과 복원

WorldStoryActors는 세션 중에만 위치·배율·색·표시·좌우 반전의 원래 값을 보관한다.
중복 실행과 비활성 소유자의 실행을 거부하며, 자신의 대상 밖 오브젝트를 연결하면 오류로 처리한다.
실패·취소·비활성화와 세션 종료에서 임시 자세를 복원하고 활성 작업 참조를 해제한다.
영속 효과는 행동 담당자에서 성공과 취소 여부를 확인한 다음 기존 효과 처리기에 요청한다.

부비와 취침 대화는 같은 집 안 부비 오브젝트를 사용한다. 별도 부비 사본이나 중복 표시 소유자를 만들지 않는다.
씬 대상·Sprite·화자 앵커는 OutsideStoryUnitySetup으로 작성하며, 실행 중 임시 대상 생성은 하지 않는다.

## 확인 결과

- 변경 관련 자동 검사 26개 모두 통과.
- 실제 월드 동작 15종의 실행과 취소·비활성화·재실행·세션 종료 자세 복원 확인. 문 효과음은 기존 음원 서비스에 위임한다.
- 실제 Studio 재생기로 원본 조니 기억 대사 39개를 끝까지 진행하고 관찰 완료/진행 조건을 확인했다.
  중간 취소 시 미완료 유지와 이후 재시도도 확인했다.
- Main → Home → OutSide → Play 실제 진행·DI 경로, 씬의 화자 연결, 현재 로더의 기억 대본 조회 확인.
- 전체 EditMode 55개 중 48개 통과·4개 건너뜀·3개 실패. 실패는 앞서 관측한 VisualUIReviewTests의
  옵션 UI NullReference, 배치 입력 폴링, 레시피 그림 경로 기대값이다. 전체 회귀 통과로 보고하지 않는다.
- 마지막 월드 소유권 가드 변경 뒤 관련 월드 수명·실제 씬 진입 2개를 다시 실행해 모두 통과했다.
- Python 반영 도구 9개 통과, CSV 38개 데이터 묶음 조립 성공.
- 공통 CSV 48개·기존 스키마 5개와 Studio 본체/에디터·Assets/Plugins의 고정 범위 해시가 작업 전후 일치한다.
- 씬 YAML의 기존 오브젝트 ID가 제거되지 않았음을 확인했다. 추가 대상·표현·화자 연결이 변경 범위다.

0~3일차 전체 선택 분기와 실제 제조·서빙을 포함한 수동 플레이, 카메라·화자 위치·표시 크기의 최종 시각 검수는 남아 있다.
월드 동작에는 제공된 Sprite와 기본 이동·반응을 사용한다. 전용 타격·수리 등의 미술/애니메이션 완성도를 보장하지 않는다.
검사 기록은 Unity_CSV_Runtime_Followup_2026-11-02.validation.json에 남겼다.
