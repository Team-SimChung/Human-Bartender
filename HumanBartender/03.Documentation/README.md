# HumanBartender 프로젝트 안내

- 실제 게임 씬: `Assets/01.Game/01.Scenes`
- 코드: `Assets/01.Game/02.Scripts`
- 게임 리소스: `Assets/01.Game/03.Content`
- SO 인스턴스: `Assets/01.Game/04.Data`
- 미적용 Google Drive 리소스: `Assets/02.ArtStaging/01.GoogleDrive`
- 개발용 씬·자산: `Assets/03.Dev`
- 게임 CSV: `Assets/StreamingAssets/csv`
- 편집용 Excel·내보내기 도구: `02.ContentAuthoring`

[현재 전체 폴더 트리](04.AssetMigration/FILE_TREE.md)

[2026-09-29 구조 변경·참조 보존 검증 및 복구 자료](04.AssetMigration/20260929/README.md)

`Content`에는 현재 게임에 연결된 기존 자산과 이미 적용된 최신 자산이 함께 있다. `ArtStaging`에는 아직 적용하지 않은 후보와 검토할 이전 버전이 있다. 원래 납품 경로와 현재 경로는 이관 문서의 `file-map.csv`에서 찾는다.

Unity 자산의 이동은 `.meta`와 함께 처리한다. 같은 이미지라도 GUID, 스프라이트 분할, 피벗, 필터, 내부 ID가 다를 수 있으므로 이름이 같다는 이유로 교체하거나 병합하지 않는다.
