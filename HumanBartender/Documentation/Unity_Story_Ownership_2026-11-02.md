# Unity 사건 실행 구조의 이전 기록

이 문서는 월드 동작을 모두 Studio 컷신에 연결했던 중간 구현의 기록이다.
현재 구현과 검증 결과는 [CSV 고정 상태에서의 Unity 사건 실행](Unity_CSV_Runtime_Followup_2026-11-02.md)을 따른다.
16개 컷신 에셋 제작을 필수 작업으로 보지 않는다. 현재는 월드 C# 동작과 실제 Studio 컷신을 구분한다.

## 실행 경계

| 책임 | 소유자 | 수명과 금지 사항 |
|---|---|---|
| CSV 해석·명령 변환 | CsvContentCatalog / StoryCsvCompiler | 로드 시 `web_fields`를 강타입 명령·결제·화자·후속 컷신으로 변환한다. 실행용 스텝에서는 원시 사전을 제거한다. 플레이어 값을 변경하지 않는다. |
| 데이터 공개·조회 | NewDataLoadManager / IStoryContentSource | 검사 후 기존 캐시를 공개한다. 별도 사건 대본 저장소나 플레이어 상태를 만들지 않는다. |
| 대화 순서·선택 UI·실행 취소 | DialogueRunner | 대화마다 실행 scope와 CTS를 만든다. 종료 시 표현기·입력 대기·active 참조를 정리한다. |
| 사건 담당자 선택 | IOutsideStoryEvents / OutsideStoryEvents | 씬 scope에 등록된 처리기로 전달한다. 담당자 누락·중복은 오류다. |
| 상점 사건 규칙 | ShopStoryEvents | 구매·용돈·퀴즈만 담당한다. 실행 중인 UI·CTS·선택 결과를 필드에 저장하지 않는다. |
| 동네 사건 규칙 | NeighborhoodStoryEvents | 고양이·수리·갱단 규칙을 담당한다. 필수 연출 성공과 취소 여부 확인 후 효과를 적용한다. |
| 거리·공통 선택지 | StreetStoryEvents | 공통 선택지는 기존 로더에서 조회한다. 보상을 재생기에 넣지 않는다. |
| 사건 노출·시작 대본 | OutsideStorySceneRules | 기존 플레이어 플래그로 판단한다. 상태를 복제하거나 변경하지 않는다. |
| 배치·반경·직접 컷신 연결 | Home / OutSide의 WorldInteractionTarget | 씬에 저장된 오브젝트가 소유한다. 런타임 임시 대상 생성은 제거했다. |
| 상호작용 입력 잠금·UI·대상 | WorldInteractionController / InteractionStateLease | 세션 종료에서 정리한다. 씬 이동은 정리 후 전역 진행 서비스에 인계한다. |
| 연출 조회·실행 요청 | WorldInteractionCutscenePlayer | Inspector 연결 또는 기존 StudioCatalog를 사용한다. 중복 재생을 거부하고 종료 시 재생 host를 제거한다. |
| Timeline·화면·게임 정지·음원 복원 | 기존 StudioPlayer / StudioGameplayScope / StudioPlaybackService | 기존 플러그인 경로가 소유한다. 사건 처리기에 별도 컷신 플레이어나 지연 타이머를 만들지 않는다. |
| 플레이어 값 변경 | 기존 PlayerDataSO / IConditionUtil | 실행별 효과 scope를 사용한다. 사건 처리기는 플레이어 값 사본을 보유하지 않는다. |
| 캠페인 요구 조건 | CampaignStoryRules / GameProgressionService | 재료 획득 조건을 재사용하고 진행 서비스가 일차·씬 이동을 판단한다. 로더에서 조건 평가·효과 변경을 제거했다. |
| 바 종료 기록 | PlayPhaseController / IConditionUtil | 바 실행을 마친 소유자가 기록한다. 데이터 로더가 진행 플래그를 변경하지 않는다. |

## 변경한 실패·취소 규칙

- 필수 사건 연출이 없으면 오류로 종료한다. 짧은 대기만 수행하고 수리·발견·입양 완료로 기록하지 않는다.
- 연출 또는 선택 직후 취소되면 효과를 적용하지 않는다. 다음 실행은 별도 실행 scope에서 재시도한다.
- 이미 받은 용돈·재료, 이미 끝난 수리·발견은 완료 플래그로 중복 적용을 막는다.
- 입양은 성공한 연출 뒤 플래그를 기록하고 이동 의도를 반환한다. Controller가 대화·UI·입력 lease를
  정리한 뒤 GameProgressionService에 이동을 요청한다. 수락한 전환에는 이전 씬의 파괴 토큰을 전달하지 않는다.
- 후속 기억 컷신은 실제 재생 성공 시에만 관찰 플래그를 기록한다. 선택적 컷신을 건너뛴 것을 관찰 완료로 취급하지 않는다.
- 기존에 명시된 선택적 컷신 정책은 유지한다. 신규 사건 행동은 이 예외에 포함하지 않는다.

## Unity에서 편집하는 곳

- 사건 대상: Home / OutSide의 WorldInteractionTarget 오브젝트. 위치·반경과 `Timelines`를 Inspector에서 편집한다.
- 사건 규칙: ShopStoryEvents / NeighborhoodStoryEvents / StreetStoryEvents의 C# 구현.
- 컷신 원본: 기존 Cutscene Studio에서 StudioSequence와 Timeline을 작성한다.
- 연결: 대상의 `Timelines` ID에 `OutsideStoryCutscenes.Id(...)` 값을 연결하거나 기존 StudioCatalog에 등록한다.
- `Tools/Story/Set Up Native Outside Targets`는 최초 대상 배치 도구다. 기존 대상의 위치와 Inspector 연결은 덮어쓰지 않는다.
- `Tools/Story/Report Native Cutscene Bindings`는 정해진 사건 ID의 Studio 원본과 카탈로그 등록 현황을 기록한다.

## 현재 연출 자산 범위

실제 프로젝트에 있는 플러그인 경로는 확인했다. 현재 StudioCatalog에는 `CutsceneStudio_Test`만 등록되어 있고,
신규 사건 명령 16개에 해당하는 StudioSequence는 검색되지 않았다.
`OutsideStoryCutsceneBindings.validation.json`에 ID별 결과를 기록한다.

따라서 C# 실행·수명 구조와 플러그인 호출 연결은 구현했지만, 신규 사건 16개가 플레이 가능한 연출로
제작·연결되었다고 보지 않는다. 관련 분기는 필수 에셋이 연결될 때까지 명시적으로 실패한다.
없는 연출을 대신할 임의 컷신이나 무관한 테스트 에셋을 연결하지 않는다.

공통 CSV 48개는 그대로 유지한다. 반영 도구의 웹 JS 해시 검사는 후속 원본 변경을 알리는 작업 도구의 검사이며,
게임 실행에 웹 JS를 사용하는 의존성이 아니다. 웹 행동 변경을 채택할 때는 Unity C# 규칙과 연출을 검토한다.

## 확인 결과

- 실제 Unity 컴파일과 Home / OutSide 대상 배치 도구 실행 성공.
- 변경 관련 테스트 19개 통과. 로드 시 원본 보존·강타입 변환, 담당자 단일성, 무료 주문,
  두 방문의 테이블 분리, 보상 중복 방지, 취소 뒤 미적용과 재시도, 입양 이동 의도 분리를 검사했다.
- 실제 StudioPlayer 재생에서 중복 재생 거부, 취소·비활성화·재실행, 시간 배율·게임 상태·오디오 복원을 검사했다.
- 실제 Main → Home → OutSide → Play의 기존 DI·진행 경로 진입 성공.
- 전체 EditMode 48개 중 41개 통과·4개 건너뜀·3개 실패.
  실패는 앞선 39개 검사에서도 관측한 옵션 UI NullReference, 배치 입력 폴링, 레시피 그림 경로 기대값 검사다.
  이번 변경과 무관하다고 기준 버전에서 입증한 것은 아니며, 전체 회귀 통과로 보고하지 않는다.
- Python 반영 도구 9개 통과. CSV 48개의 정본 해시 유지와 38개 묶음 조립을 확인했다.
- 새 사건의 실제 연출 자산 제작·연결과 전체 분기 수동 플레이는 미완료다.

정확한 테스트 이름과 실패 메시지는 `Unity_Story_Ownership_2026-11-02.validation.json`에 기록했다.
