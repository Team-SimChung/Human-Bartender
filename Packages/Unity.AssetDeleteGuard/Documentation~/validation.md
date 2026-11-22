# Compatibility and validation

Status date: 2026-10-07. Version: 0.2.0-preview.1.

| Environment | Status |
|---|---|
| Windows / Unity 6000.3.14f1 | Isolated EditMode suite: 40 passed, 0 failed |
| Unity 2019.4 / 2020.3 / 2021.3 / 2022.3 LTS | Source target with API version branches; runtime qualification pending |
| Other Unity 6 versions and future releases | Pending; no forward-compatibility guarantee |
| macOS / Linux; VCS providers | Pending |
| Built-in / URP / HDRP | No pipeline API dependency; separate pipeline smoke tests pending |
| Large-project performance | Pending |
| Native Delete / redesigned review UI on Windows 6000.3.14f1 | Delete key and Project context-menu interception, reload approval reset and reviewed deletion with 13 direct referencers observed; broader version/VCS matrix pending |

Interactive smoke testing was also performed in a separate Unity 6000.3.14f1 Windows project: the real Assets menu opened the review window; direct and indirect sample references and file/metadata inventory appeared; Locate, resizing and Korean/English switching worked. Cancelling confirmation preserved the files. An external target-file change caused revalidation to stop deletion and return a new review. A subsequent reviewed deletion removed the sample and metadata, with Deleted shown and repeat deletion disabled. Restoring the original sample and `.meta`, then rescanning, restored the reference in the Inspector. General GUI/scale qualification beyond these cases remains pending.

The redesigned native review was additionally tested with 13 separate saved assets directly referencing one target and one indirect referencer. Adding 12 referencers after an earlier review stopped deletion and required a new review showing all 13 sources. The scrollable list exposed its last entries, and Locate selected the correct asset in the Inspector after correcting an overlapping row hit area. Confirming the fresh review removed the target and `.meta`, showed Deleted and disabled repeat deletion. The Inspector showed Missing. All 13 referring assets and their metadata remained present with unchanged SHA-256 hashes (26 files checked). This is reference inspection and approved deletion; it does not replace or repair broken references. The same real-deletion scenario is included in the EditMode suite.

## Reproduce tests

From the source checkout, in PowerShell:

```powershell
& './Tools~/run-tests.ps1' -UnityEditor 'C:/Program Files/Unity/Hub/Editor/6000.3.14f1/Editor/Unity.exe' -OutputDirectory 'C:/Temp/AssetDeleteGuard-Test-New'
```

The output directory must not exist. The script creates a minimal project, installs this local package with a compatible Unity Test Framework and runs its Editor tests. Results and Unity logs remain in the fixture. Pass `-TestFrameworkVersion` appropriate to the Unity version under test; the default is 1.6.0 for the validated Unity 6 environment. Integration tests require the fixture marker and never run deletion against an ordinary host project.

The suite checks graph updates/cycles, path boundaries, folder inventory including hidden files, saved direct/indirect references, internal deletion-set references, unsaved inactive scene references, transient material references, scene skybox references, dirty target rejection, disabled build scenes and preloaded assets, new referencers, new folder files, GUID replacement, provider failure, cancellation, real trash deletion, consumed approvals and partial/refused backend outcomes. Added native-integration checks cover the interactive/Assets boundary, exact inventory permits (not newly added children or unrelated paths), nested permit rejection, exception disposal, review-controller rescan/cancel/disposal and provider-qualified source identity. The headless suite tests the guard policy and executor integration; native callback routing is a separate interactive smoke test.

For native integration, test the Delete key and Project context-menu Delete on disposable samples. Unity's confirmation and Cannot Delete dialog are expected before the new review. Verify the asset and metadata still exist when review opens and after cancellation. Never treat a blocked-path batch as an atomic transaction. Include materials and third-party processors in release qualification because pre-callback and other-processor side effects remain outside the guard. Verify that reload resets acknowledgement and that a narrow dock exposes horizontal/vertical scrollbars rather than overlapping controls.

## Preview distribution

`Tools~/build-preview.ps1` builds from a new staging project and includes offline docs plus reference sample assets. The staging project has no game assets or project-specific package dependencies. It exports only `Assets/AssetDeleteGuard` without IncludeDependencies, and creates a local UPM tarball from an allowlist of package content. Test/developer scripts and publisher working notes are excluded.

The helper performs a read-only sample reference check in the staging project. Import the resulting `.unitypackage` or install the `.tgz` into a separate fresh project before qualification. A package build is not marketplace approval.

## Remaining release qualification

Run this suite on exact old-LTS/Unity 6 versions before adding them to the supported matrix. Also verify nested and variant Prefabs, Prefab Stage dirty/save/reload flows, sub-asset selections, binary serialization, Material Variant parents, read-only/VCS behavior, domain reload and disabled-domain-reload Play Mode, inaccessible package sources, import races, multiple review windows, drag resizing, Korean/English displays and scaled editor fonts. Test large folders and representative large projects with measured scan/revalidation times.

Record the exact editor patch, OS, dependencies, test results and package hash per release. Keep failed runs with their subsequent fixes. UI/manual checks and untested environments must not be reported as automated passes.
