# Asset Delete Guard

**0.2.0-preview.1 — Unity Editor reference inspection and reviewed deletion.**

Review what references a selected asset or folder before deleting it. The package is independent of game code, scene names, render pipelines and third-party frameworks. It contains no player assembly, network service, telemetry or automatic project-settings changes.

Use **Assets > Asset Delete Guard** for a direct review workflow. Native Delete is also guarded by default in the interactive Editor: Unity's file-deletion callback blocks the original request, then opens a new review. Unity can show its own confirmation and failure dialogs first. The package does not implement Unreal Redirectors, automatic reference replacement or restoration through Unity Undo.

## Install

Choose one installation method per project:

- **Local UPM:** open Package Manager, select **Install/Add package from disk**, and select this folder's `package.json`.
- **UPM archive:** use Package Manager's **Install/Add package from tarball** with the generated `.tgz`.
- **Asset Store style preview:** import the generated `.unitypackage`. It installs under `Assets/AssetDeleteGuard`.

Do not install both UPM and the `.unitypackage` in the same project: they contain the same assembly names and GUIDs. Keep `.meta` files when moving the package. To uninstall, remove the UPM dependency or the imported `Assets/AssetDeleteGuard` folder, then any sample copy you imported.

The development UPM identifier is `com.assetdeleteguard.editor`. A marketplace UPM release must use the identifier and publisher identity configured in the Publisher Portal; the development identifier makes no domain ownership claim. `Unity.AssetDeleteGuard` is only the source folder name.

## Use

1. Select main asset files or folders in the Project window.
2. Open **Assets > Asset Delete Guard > Review References** for inspection, or **Review and Delete** for the deletion workflow.
3. Review the summary cards and the **References**, **Indirect**, **Files** and **Coverage** tabs. References are grouped by source; selecting a row shows its target/property connections. **Locate** selects a source; search filters the list.
4. In the deletion window, acknowledge the report and press **Revalidate and delete**. A confirmation appears, followed by a complete rescan. A changed report requires another review.
5. Read the result for each root. Failure can be partial. No automatic retry or permanent-delete fallback runs.

Optional: assign a shortcut under **Edit > Shortcuts > Asset Delete Guard/Review and Delete**. No existing shortcut is replaced by default. The window offers Korean and English controls; detailed diagnostics are currently English. **Export report** writes a local JSON file only when requested.

**Native Delete:** toggle **Guard native Delete** in the review window or **Tools > Asset Delete Guard > Guard Native Delete**. Both use one setting, saved locally for this user and project. This also affects other tools calling Unity asset-deletion APIs in the interactive Editor; batch mode and filesystem deletion are outside the guard. Earlier tool actions and Unity's Material Variant reparenting cannot be rolled back; use the dedicated review menu for materials. See the manual for the full boundary.

## Coverage

- Saved direct file dependencies and indirect impact, including referencers in installed packages.
- Loaded scene and Prefab Stage object references, including inactive objects, modified persistent assets, scene settings and referenced transient materials/ScriptableObjects.
- Enabled and disabled build scenes, Unity 6 Build Profiles, registered build configuration objects and preloaded assets.
- A selected folder's actual disk contents, including hidden or unimported files and `.meta` files. Unimported contents appear with a scope warning.
- A read-only extension interface for project-specific sources; provider failures prevent deletion.

The scanner reports what Unity's dependency API and the stated providers can inspect. Zero reported references is not a guarantee that an asset is unused. Dynamic loading, arbitrary GUID/path strings, code/type dependencies, custom importers' undeclared references, other ProjectSettings fields, Addressables registration and `AssetReference` strings require additional coverage. See `Documentation~/manual.md` (or `Documentation/manual.md` in the imported preview).

## Compatibility and status

The source compatibility target is Unity **2019.4 LTS and later**, including Unity 6. The executed validation currently covers **Unity 6000.3.14f1 on Windows**. Earlier LTS versions, macOS/Linux, version-control providers and future Unity releases require their own validation. The package manifest's `unity` field expresses an intended minimum; it is not a tested-version matrix.

Only documented public Editor APIs are used, with compile-time version branches. Material Variant parent deletion is blocked in this preview when a child material is found, because it needs Unity's reparenting workflow.

The preview performs a full project scan and a synchronous rescan before deletion. Large projects may pause during revalidation. No incremental cache or large-project timing guarantee is claimed. No Addressables extension or reference replacement ships in this version.

## Demo, documentation and development

Import **Reference Demo** from the UPM package's Samples section, or use the sample included in the `.unitypackage`. Start with `Target.asset`.

- User manual: `Documentation~/manual.md`
- Korean guide: `Documentation~/manual.ko.md`
- Provider API: `Documentation~/extensions.md`
- Compatibility and test procedure: `Documentation~/validation.md`
- Publisher preparation: `Tools~/marketplace.md` in the source checkout

The source checkout includes PowerShell helpers in `Tools~`. `run-tests.ps1` creates a new disposable Unity project; destructive integration tests refuse to run in an ordinary project. `build-preview.ps1` creates independent UPM and `.unitypackage` previews in a new output folder, without including game assets, test code or developer scripts in the release content. It does not upload or publish anything.
