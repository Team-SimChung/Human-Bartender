# Windows 데모 빌드

프로젝트를 열고 있는 Unity를 종료한 뒤 저장소 루트에서 실행합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\HumanBartender\Tools\Steam\Build-Windows.ps1
```

`ProjectVersion.txt`의 Unity 버전을 사용합니다. 설치 위치가 다르면 `-UnityPath`로 Unity.exe를 지정합니다.
활성 씬 목록과 Company/Product/Version은 기존 Unity 설정을 그대로 읽습니다.
Windows64, Development 옵션 없는 빌드를 만들고 Addressables는 Player 빌드와 함께 한 번 생성합니다.
Addressables 빌드 정책은 성공·실패에 관계없이 원래 값으로 복원합니다.

매번 `Builds/SteamDemo/<UTC 시각>-<고유값>/` 아래 새 폴더를 만듭니다. 기존 빌드를 지우거나 덮어쓰지 않습니다.

- `content/`: SteamPipe 업로드 원본. 실행파일과 런타임 파일, Data, StreamingAssets, 로컬 Addressables를 포함합니다.
- `build-info.json`: 빌드 성공 및 필수 파일 검사 이후 생성되는 기록입니다. Steam 설치본 검증을 의미하지 않습니다.
- `unity-build.log`: Unity 컴파일·콘텐츠·Player 빌드 로그입니다.
- `source-status.txt`: 빌드 시작 시 작업 트리 상태입니다. 커밋되지 않은 수정이 포함될 수 있습니다.
- Unity가 `DoNotShip`으로 표시한 디버그 폴더는 `content/` 밖에 보관합니다.
- StreamingAssets/json에 복사된 `.idea` 설정은 `ExcludedIDESettings/`로 옮겨 업로드에서 제외합니다.

별도 스테이징 복사는 필요하지 않습니다. 빌드 결과를 `content/`에 직접 생성합니다.
`steam_appid.txt`가 포함되면 검사를 실패시킵니다. 게임 코드에 Steam API를 추가하지 않습니다.

## Steam 업로드 전에 필요한 값

Steamworks SDK의 SteamCMD와 업로드 권한이 있는 계정, Demo App ID, Windows Depot ID가 필요합니다.
Demo App ID는 본편 Steamworks 페이지의 연결된 패키지/DLC/데모/도구에서 데모를 추가한 뒤 확인합니다.
Depot ID는 데모 앱의 SteamPipe → Depots에서 확인합니다.
본편 App ID를 데모의 업로드 대상으로 대신 사용하지 않습니다.

SteamPipe VDF의 ContentRoot는 검증된 `content/`를 가리키고, 로그·보고서·디버그 자료는 포함하지 않습니다.
Launch Option의 실행파일 경로는 `build-info.json`의 `executable`과 일치해야 합니다.
업로드 후 테스트 브랜치에서 Steam 설치·실행·저장·종료·재실행을 검증하고 공개 대상 빌드로 지정합니다.
처음 업로드한 앱은 `default`에 초기 빌드를 연결해야 다른 브랜치를 만들 수 있습니다.
미출시 앱의 초기 연결과 실제 공개 출시는 별도 절차입니다. 이후 빌드는 `steam-smoke`에서 검증한 뒤 `default`로 승격합니다.
Steam 클라이언트에서 데모 → 속성 → 베타 → `steam-smoke`를 선택하면 검증용 빌드를 받을 수 있습니다.
SteamCMD의 `app_update <App ID> -beta steam-smoke validate`로도 브랜치 설치를 확인할 수 있지만,
이는 Steam 라이브러리의 플레이 버튼, 실제 플레이·저장·종료·재실행 검증을 대신하지 않습니다.

App/Depot 번호의 원본은 `steam-demo.json` 하나입니다. Demo App ID `5215740`은 사용자가 제공한 Steamworks 화면에서 확인했습니다.
Windows Depot ID `5215741`도 사용자가 제공한 Steamworks Depot 화면에서 확인했습니다.
실제 번호를 입력한 뒤 `Prepare-Upload.ps1 -ReleasePath <빌드 폴더>`를 실행합니다.
기본값은 `Preview=1`이며 SteamCMD에서 실행해도 파일 목록·로그를 생성하는 미리보기입니다.
같은 입력에 `-ForUpload`를 추가하면 `Preview=0`인 업로드용 VDF를 생성합니다.
미리보기는 `app_build_preview.vdf`, 업로드는 `app_build_upload.vdf`로 별도 생성하며 서로 덮어쓰지 않습니다.
스크립트는 로그인·업로드를 실행하지 않습니다. SteamCMD에서 직접 로그인하고 표시된 `run_app_build` 명령을 실행합니다.
`SetLive`는 비워 두므로 업로드만으로 라이브 브랜치를 변경하지 않습니다.
App/Depot 번호를 추측해 채우지 않으며, 자격 증명을 파일에 저장하지 않습니다.

공식 문서: [SteamPipe 업로드](https://partner.steamgames.com/doc/sdk/uploading), [데모 설정](https://partner.steamgames.com/doc/store/application/demos).
