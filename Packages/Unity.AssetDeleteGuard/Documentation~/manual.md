# Asset Delete Guard manual

## Review workflow

Use the Project window to select one or more main assets or folders, then **Assets > Asset Delete Guard > Review References**. Inspection never deletes assets. **Review and Delete** opens the same report with an explicit deletion step. Selecting a sprite, mesh or other sub-asset is rejected; select its parent file deliberately if you intend to remove the whole file. Targets outside `Assets`, the `Assets` root and `.meta` files selected directly are rejected.

The summary cards show the asset count, direct reference-source count and indirect impact count. Click a card to switch tabs. **Files** lists imported assets, subfolders, metadata and files that Unity does not import. This is the real filesystem scope of deleting the folder. Paths beneath an already selected parent are merged into that parent root. References whose source is also being deleted are removed from the remaining-source list.

**References** groups direct dependencies and serialized object/property references by provider and source identity. Select a source to inspect its connections below the list. File dependencies do not identify a particular Inspector property. Multiple relationships can belong to the same source. **Indirect** follows the reverse dependency graph, with cycles and duplicates removed. Indirect impact is contextual; it is not proof of a particular runtime failure. **Coverage** contains errors and limitations; the footer keeps analysis status, acknowledgement and actions visible.

Search filters the visible direct rows. **Locate** selects a loaded scene object or the source asset when available; build-setting-only references can have no selectable asset. **Export report** writes relative asset paths and scene object labels to a user-selected local JSON file. The report contains no reusable approval token and cannot be imported to authorize deletion.

## Analysis states and deletion

- **Ready:** the included providers completed against a stable snapshot. Scope warnings still apply.
- **Partial:** at least one source failed or changed, or a target condition prevents deletion. Read the errors, resolve the cause and rescan.
- **Cancelled:** no deletion can use the unfinished report.

Review the scope warnings and acknowledge the report before pressing **Revalidate and delete**. The final dialog explains irreversibility and trash limitations. The executor refreshes external changes, reconstructs the inventory and all included references, compares the new signature and checks for editor changes immediately before dispatch. A change returns a fresh report without calling the deletion backend. Each used approval is consumed even if deletion fails; it cannot trigger a second deletion.

Dirty selected assets, dirty selected scenes/stages, read-only target files or metadata, symlinks/junctions in the deletion path, missing metadata and provider failures prevent deletion. Imports, compilation and Play Mode also prevent execution. A script/domain reload or closing the review window discards approval. Missing scripts found in loaded scene objects produce an incomplete report that prevents deletion.

Deletion calls Unity's trash API. Newer versions support a grouped request; the legacy branch stops after the first failed root. Every root is checked against the filesystem afterward, including its metadata. Results can be **Deleted**, **Failed or partially deleted**, or **Unknown**. There is no rollback, automatic retry, background replay or permanent-delete fallback. Unity may remove some entries before failing. Read the per-root results before any manual recovery.

## Restore and version control

Unity Undo does not restore these deletions. Restore both the asset and its original `.meta` from version control or the operating system's trash, then refresh Unity. A different `.meta` creates a different identity and will not repair the original references. Trash availability and retention depend on the OS and the active version-control provider. Never infer that the complete operation failed from one failed root or that Unity's return value alone proves success.

## Included reference sources

| Source | Current behavior |
|---|---|
| Saved assets in Assets and installed Packages | Public `AssetDatabase.GetDependencies(path, false)` and dependency hashes; no text-only YAML assumption |
| Loaded scenes / Prefab Stage | Serialized object references in active and inactive objects; nested nonpersistent objects are followed |
| Loaded scene settings | Loaded RenderSettings/LightmapSettings serialized references; labels may not identify the owning scene |
| Modified persistent assets | Serialized object references in loaded dirty objects; modified targets block deletion |
| Build settings | Enabled and disabled scene entries; Unity 6 global scene list and Build Profile scene entries |
| Build configuration objects / preloaded assets | Registered objects and serialized object references |
| Extension providers | Registered synchronous read-only implementations; errors block deletion |

## Native Delete integration

Enabled by default in the interactive Editor. The window's **Guard native Delete** toggle and **Tools > Asset Delete Guard > Guard Native Delete** share one local user/project setting. Disable it before uninstalling an Assets-based copy of the plugin or when a third-party asset-management workflow needs its normal deletion behavior.

When Unity reaches `OnWillDeleteAsset`, the guard returns `FailedDelete` for eligible paths beneath `Assets`. After Unity finishes its call, blocked paths are handed to a new review window. Unity's normal confirmation and **Cannot Delete** dialog can appear first. Dismiss the latter to continue to the review. Cancelling or closing the review preserves the files; it never retries the original command. The callback does not scan, load assets, create windows or call AssetDatabase APIs.

The callback does not identify the initiating caller or the native batch. The guard therefore also applies to interactive `AssetDatabase.DeleteAsset`/trash calls from other tools. Paths arriving before the deferred review are coalesced for display only; no transaction or original-operation replay is inferred. Batch mode, filesystem deletion, `Packages` paths and directly deleted metadata are outside this boundary. Sub-assets cannot be distinguished in this path-only callback: review shows the actual main-file path Unity requested. Use the dedicated menu for explicit main-asset selection validation.

**Material Variant caution:** Unity can reparent child materials before the file-deletion callback. This guard cannot undo those prior mutations. Use **Review and Delete** for materials; its scanner blocks detected parent deletion before the native material workflow begins. Third-party tool side effects before their deletion call are likewise outside this guard. Unity also calls other registered deletion processors; direct file changes performed by those processors cannot be prevented or rolled back by this package.

Approved deletion uses the same full rescan and executor as the dedicated menu. A synchronous, disposable permit contains only the reviewed disk inventory; other files are still blocked even during that execution. Exceptions release the permit. Queued paths are discarded on assembly reload, Play Mode transitions, editor shutdown or disabling native integration. Window closure/reload/Play Mode discards its analysis and approval. No queue or permit is persisted.

## Known limits of the preview

File explorer deletion, batch-mode APIs and arbitrary filesystem scripts bypass this workflow. Native integration has the boundaries described above. The package does not leave Redirector assets or rewrite references after deletion.

`Resources.Load`, Addressables registration/AssetReference strings, AssetBundle membership semantics, string paths/GUIDs, reflection and code/type usage cannot be completely inferred from serialized dependencies. Runtime-loading locations and installed Addressables produce warnings; an Addressables provider is not included. No claim of a complete project-wide reference proof is made. ProjectSettings fields other than the explicitly included sources are outside coverage. Custom importers can omit dependencies from Unity's graph. Package cache read failures prevent a Ready result.

The scanner works at the main-file level, so a sub-asset reference counts as use of the containing file. It does not offer selective sub-asset deletion, automatic reference replacement, material reparenting or Prefab override editing. Detected Material Variant parent deletion is blocked.

Scanning uses Unity's main thread. The window yields between assets/objects, but a single dependency query, folder enumeration, target content hash or final synchronous rescan can pause the Editor. Large-project performance, UI sizing, non-Windows trash semantics and VCS integrations still need release qualification. Cancellation cannot interrupt a single in-progress Unity API call.

A final filesystem race is possible if an external process modifies files during Unity's deletion call; the filesystem and Unity API do not supply a transaction covering analysis and deletion. Filesystem postchecks describe the observed result. Stop external asset writers while performing deletion.

## Installation troubleshooting

If duplicate assembly/type or GUID messages occur, remove one installation route; keep either UPM or `Assets/AssetDeleteGuard`. When switching routes, preserve any sample/custom provider work before removing its folder. The package neither installs optional packages nor changes serialization mode, render pipeline, build settings or the user's shortcut bindings.

On a Partial report, save/revert dirty targets, finish imports and resolve the specific failed source before rescanning. A new report after confirmation means the project changed; review it and acknowledge again. If an old report reappears after a domain reload, it must be rescanned and cannot be used to delete.
