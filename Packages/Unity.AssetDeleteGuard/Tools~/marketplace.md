# Marketplace preparation — publisher working draft

This is a development preview. There has been no upload, agreement acceptance, submission or public release.

## Naming and package identity

- Source folder: `Unity.AssetDeleteGuard`, as requested.
- Product display name: `Asset Delete Guard`.
- Assemblies/namespaces: `AssetDeleteGuard.*` (no personal nickname or official Unity namespace).
- Development UPM identifier: `com.assetdeleteguard.editor`. Before a store UPM submission, replace this with the Portal-approved identity throughout the package, fixture manifest, docs and consuming manifests; migration must be tested.
- Publisher author, support email, website, organization identity and price are not invented. Fill them from the real publisher account. UPM submission metadata also needs an accurate tested `unity`/`unityRelease` pair.

## Submission preparation

Prepare the following before publishing: exact-version/platform validation, completed license/publisher information, readable offline manuals, reference sample, clean-install console check, current Asset Store Tools validation, real UI screenshots/video, and a publisher-edited English description. The included `.unitypackage` is a local preview archive; use the supported Asset Store Tools upload workflow from its clean staging project when actually submitting.

Current official policy calls for new submissions built with Unity 2022.3 or later. Legacy compatibility can be retained in source but must be separately tested and described. Tool samples, namespace isolation, accurate limitations, license notices and disclosure of AI assistance are relevant. Check the current [submission guidelines](https://assetstore.unity.com/publishing/submission-guidelines) before submission (reviewed 2026-10-06, policy updated 2026-05-20).

The source compatibility target is 2019.4+, but the current executed version is 6000.3.14f1 only. Do not list all Unity versions, all platforms, transactional native-Delete protection, Addressables support, redirectors, replacement, instant scanning, guaranteed restoration or guaranteed safe deletion as existing features. Describe native interception as an interactive file-deletion guard with Unity's own dialogs, API-tool impact and the Material Variant pre-callback limitation.

Traditional `.unitypackage` distribution and Asset Store UPM publishing have different enrollment/identity requirements. The generated local `.tgz` is not an enrolled marketplace product. See [Unity's UPM publishing information](https://marketplace.unity.com/publishing/upm-publishing) and use the Portal-provided identifiers if enrolling.

## English listing draft — edit after qualification

Asset Delete Guard helps you review references before deleting assets and folders in the Unity Editor. Inspect saved asset dependencies, loaded scene and Prefab Stage references, common build settings, and indirect dependency impact. Browse the deletion inventory, locate reference sources and export a local report. An explicit review-and-delete command rescans the project immediately before using Unity's trash API and asks for a new review when the report changes.

Editor-only. Includes a reference sample and offline English/Korean documentation. No render-pipeline or third-party framework dependency. Native Delete is guarded in the interactive Editor at the file-deletion callback; a new review follows Unity's confirmation/failure dialogs. This also affects other tools' asset-deletion APIs. Batch/filesystem deletion and pre-callback side effects such as Material Variant reparenting are outside protection. Dynamic string/code references and Addressables-specific registration/AssetReference strings are outside the included coverage. No automatic reference replacement or Redirectors. Scanning uses a full-project pass; large projects can pause during revalidation. Unity Undo restoration and operating-system trash retention are not guaranteed. Supported Unity versions and platforms: **replace this sentence with the tested release matrix**.

## AI description draft — publisher must verify

OpenAI Codex assisted with C# implementation, tests, package tooling, sample serialization and documentation. The development process included source/API review and automated testing in Unity 6000.3.14f1. No AI service runs inside the delivered plugin. Add the publisher's actual manual review, testing and changes before submitting this field; do not claim those steps occurred until performed.

## Release checklist

- [ ] Final publisher identity, product naming/trademark check and support contacts.
- [ ] Final distribution license and Third-Party Notices review.
- [ ] Source compatibility branches tested on every advertised editor version.
- [ ] Clean install and sample import tested for each offered distribution route.
- [ ] GUI/manual safety and scale/performance matrix completed.
- [ ] Windows/macOS/Linux and intended VCS support decisions documented.
- [ ] Optional integration claims agree with included providers.
- [ ] Real screenshots, demo and publisher-edited listing completed.
- [ ] Version moved out of preview only after qualification; changelog/matrix updated.
- [ ] Current Asset Store validation passes; submitted archive contains only intended content.

The package intentionally lacks fabricated publisher metadata. This is an outstanding release task, not a reason to place a personal nickname in technical identifiers.
