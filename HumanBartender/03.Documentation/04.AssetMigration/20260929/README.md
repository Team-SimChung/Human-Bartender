# 파일 구조 변경 및 연결 검증 — 2026-09-29

번호가 붙은 구조로 코드·씬·리소스·문서를 정리했다. 최신 그림으로 기존 그림을 교체하거나, 클래스 이름과 직렬화 필드 이름을 바꾸거나, 중복 자산을 병합하지 않았다. 위치 변경과 경로 갱신만 수행했다.

## 현재 위치

- `Assets/01.Game`: 게임 씬, 코드, 현재 연결된 자산, 데이터, 설정, Editor 도구, 테스트.
- `Assets/02.ArtStaging/01.GoogleDrive`: Google Drive 묶음의 아직 연결되지 않은 리소스와 검토할 이전 버전. 새 자산이라는 뜻과 적용 완료라는 뜻을 구분한다.
- `Assets/03.Dev`: 개발 씬, 실험용 자산과 이전 Stur 구현. 실제 게임에서 사용하는 LightTest 이미지 등은 Game으로 분리했다.
- `01.SourceAssets/02.Working`: Unity 의존성이 없음을 확인한 PSD/Aseprite/Spine/CLIP 원본 11개 및 기존 메타데이터.
- `02.ContentAuthoring`: 편집용 Excel, exporter, system 스키마.
- `03.Documentation`: 기획, 아키텍처, 이미지 참고, 이관 기록, 인수인계, 검증 자료.

비어 있는 예상 폴더는 생성하지 않았다. 사용 종료가 확인되지 않은 자산을 삭제하거나 Archive로 일괄 이동하지 않았다. Resources 내부 경로, StreamingAssets/csv, 외부 패키지 폴더는 유지했다. Spine의 Assets/Editor/SpineSettings.asset 및 VContainerSettings.asset도 원래 위치를 유지했다.

## 보존한 연결

- 모든 이동 자산은 원래 .meta와 함께 이동했다. GUID, 스프라이트 내부 ID, 임포트 설정을 유지했다.
- 씬, 프리팹, 애니메이션, 타임라인, 이미지, 머티리얼의 원래 내용을 유지했다.
- Build Settings의 씬 5개는 GUID와 순서를 유지하고 경로만 변경했다.
- 에디터 생성 도구와 테스트의 고정 경로, CSV 편집 안내, 이동한 문서의 일부 링크를 갱신했다.
- Resources 문자열 경로와 Addressables 주소는 그대로 유지했다. Addressables에 이전 Assets 경로처럼 보이는 주소 2개가 남아 있는 것은 의도적이며 파일 경로가 아니라 기존 로딩 키다.
- 구 폴더 메타데이터 중 연결되지 않고 통합된 112개는 original-folder-metadata.zip에 보존했다. 재사용 가능한 폴더 메타데이터 223개는 새 위치에 유지했다.

## 검증 결과

- 이동 기록: 파일 2,660개(.meta 별도).
- Unity 6000.3.14f1에서 변경 전/후 직접 열어 조사: 10,407개 오브젝트·컴포넌트 기록, 52,678개 직렬화 참조 필드가 동일.
- 원래 자산 2,755개에서 비참조 원본·메모 13개를 Assets 밖으로 옮겨 2,742개가 남았다. 나머지 자산의 GUID별 위치와 Unity 의존성은 계획과 일치했다. Assets 밖으로 옮긴 자산을 참조하는 Unity 의존성은 없었다.
- 이동 전후 컴포넌트 타입·순서·활성 상태·참조, 참조 GUID/fileID에 새 차이 0개.
- 바이너리 및 메타데이터 해시 비교와 중복 GUID 검사 통과. 고정 경로 갱신 20개 파일은 예외 목록으로 기록했다.
- CSV와 Excel 각각 내보내기 도구의 validate-only로 37개 데이터 묶음 검사 통과.
- 기존 EditMode 테스트: 총 88개, 통과 86, 실패 1, 건너뜀 1.
- 원본 백업 별도 프로젝트의 동일 카메라 테스트: Failed(Child) (실패 1).
  - DialogueCameraFeedbackTests.PulseChangesOnlyYAndReturnsToTheLiveCameraState: Unhandled log message: '[Assert] Assertion failed on expression: 'ShouldRunBehaviour()''. Use UnityEngine.TestTools.LogAssert.Expect

## 기존 문제와 검증 범위

Missing Script 46개 및 LightingData 호환 경고는 이동 전에도 존재했다. 이번 정리로 늘어나지 않았으며, 기존 누락 스크립트를 임의의 컴포넌트로 교체하지 않았다.

|자산|기존 Missing Script 수|
|---|---:|
|`Assets/01.Game/03.Content/03.Crafting/04.Minigames/02.Shake/04.Prefabs/ShakingMinigame.prefab`|6|
|`Assets/01.Game/03.Content/05.UI/01.Shared/04.Prefabs/Cell.prefab`|1|
|`Assets/01.Game/03.Content/05.UI/01.Shared/04.Prefabs/GridLine.prefab`|1|
|`Assets/03.Dev/01.Scenes/01.Minigames/Build.unity`|8|
|`Assets/03.Dev/01.Scenes/02.Lighting/LightTest.unity`|8|
|`Assets/03.Dev/01.Scenes/03.Vfx/EffectTest.unity`|8|
|`Assets/03.Dev/01.Scenes/03.Vfx/EffectTest02.unity`|6|
|`Assets/03.Dev/01.Scenes/05.Recovery/0 (3).unity`|6|
|`Assets/Resources/Cell.prefab`|1|
|`Assets/Resources/GridLine.prefab`|1|

카메라 테스트는 SendMessage 호출 시 `ShouldRunBehaviour()` Unity assertion을 보고한다. PlayMode 테스트 1개는 원래 Ignore로 지정되어 있다. 정적/Editor 연결 검증이 모든 게임 분기의 플레이 및 픽셀 단위 화면 검증을 대신하지는 않는다.

## 원래 설정 복구에 사용할 자료

- `file-map.csv`: 원래 경로 → 현재 경로, 원본 파일 해시, GUID.
- `migration-plan.json`: 파일·폴더 메타데이터 전체 이동 계획.
- `connection-snapshots.zip`: Unity에서 읽은 이동 전후 오브젝트/컴포넌트/참조 기록.
- `unity-comparison.json`: 변경 전후 비교 결과.
- `original-folder-metadata.zip`: 통합된 원래 폴더 메타데이터.
- `migration-static-verification.json`: 원본 해시와 GUID 검사 결과.
- `editmode-after.xml`: 기존 테스트 실행 결과.

전체 원본 백업은 아래 로컬 ZIP에 있다. 문서뿐 아니라 실제 파일 9,027개와 원래 .meta가 저장되어 있으므로 문제가 생긴 자산의 원본 바이트와 설정을 확인할 수 있다. 변경 후 추가 작업이 생기면 전체를 덮어쓰지 않고 해당 GUID/참조부터 비교해야 한다.

`C:\Users\hanrc\.codex\visualizations\2026\09\29\01a0ec51-0200-7eb3-aed3-bc70f9d66463\migration-backup\before.zip`
