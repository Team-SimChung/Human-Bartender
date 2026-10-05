# Changelog

Changelog format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

#### Types of changes

- **Added** for new features.
- **Changed** for changes in existing functionality.
- **Deprecated** for soon-to-be removed features.
- **Removed** for now removed features.
- **Fixed** for any bug fixes.
- **Security** in case of vulnerabilities

_Please, always remove previous plugin version before updating!_

## [2.3.4] - 2026-07-10

### Fixed

- Fix Find References, Find Issues, and other selection-based menu items and hotkeys staying permanently disabled on Unity 6000.4/6000.5, caused by the 2.3.3 EntityId fix silently truncating object ids to 32 bits
- Fix References Finder reveal/select using a mismatched id space on Unity 6000.4/6000.5, causing it to resolve the wrong (or no) object

## [2.3.3] - 2026-06-30

### Fixed

- Add Unity 6000.5 support: fix compile errors from the removed `EntityId`↔`int` cast (CS0619)
- Add map data versioning to automatically trigger full Assets Map rebuild when map processing logic changes between updates
- Speed up missing component removal by using Unity's built-in API as fast path and skipping already-processed prefabs during batch fix

## [2.3.2] - 2026-02-19

### Fixed

- Fix spurious "Failed to clear session cache" warning on Windows when session cache version mismatches (e.g. after switching git branches)
- Prevent auto-generate lighting from rebaking lightmaps and reflection probes during scene scanning
- Fix excessive memory consumption on large projects causing system freeze during scanning
- Fix SerializedObject native memory leak during property traversal
- Fix O(n²) performance in recursive reference graph walks
- Fix UX issues after no results operations (i.e. no issues found)

## [2.3.1] - 2026-02-13

### Added

- Add Rate us widget

### Fixed

- References Finder: invalidate cached asset object IDs after asset move to prevent false `0` references.

## [2.3.0] - 2026-02-03

### Added

- Assets Map: add VFX Graph dependency parsing support

### Changed

- Improve Unity 6.4 compatibility

## [2.2.0] - 2025-12-30 🎅 HO-HO-HO 🎄

### Added

- Issues Finder: detect deleted scenes referenced in Build Settings

### Changed

- Add progress bar support to SaveIntArray and LoadIntArray methods
- Reduce memory usage in some scenarios
- Improve exception handling in assets map serialization

### Fixed

- Fix UI briefly breaks when clicking Reveal button in any module
- Prevent NullReferenceException when checking asset references list
- Add type compatibility validation before deserialization to prevent silent data loss
- Batch GUID cache invalidation to improve performance when processing many assets
- Fix UI lists breaking with errors after code recompilation
- Fix script icon references not detected in Unity 6
- Fix revealing in prefabs with UI didn't work correctly in some edge cases

## [2.1.1] - 2025-12-05

### Changed

- Improve Unity 6.3 compatibility

## [2.1.0] - 2025-12-04

### Added

- Issues Finder:
  - Add `StartSearchInPath()` API for scanning specific files or folders
  - Add `StartSearchInPaths()` API for scanning multiple files/folders efficiently
  - Add **🐛 Find Issues** context menu item for project assets and folders

### Changed

- Implement incremental Asset Map caching, making repeated Maintainer scans ~90% faster on average (up to 95% faster).
- References Finder: apply sorting to nested items when expanding assets

## [2.0.1] - 2025-10-10

### Fixed

- Fix Issues Finder now properly scans embedded packages

## [2.0.0] - 2025-08-22

### Added

- Issues Finder: 
  - Add extension API allowing custom issues detection, see docs for details 
  - Add **Invalid Sorting Layer** issue detection 
  - Add **Invalid Renderer Materials** issue detection 
  - Add **Invalid Renderer Batching** issue detection
- References Finder: 
  - Add **Find Dependencies References** menu item
- UX: Add Highlighter for better results navigation in Unity 2021.3 or newer
- UX: Add collapse / expand left panel feature

### Changed

- Improve Assets Map creation performance dramatically (one phase got 400x speedup with 40% mem usage reduction)
- Improve Assets Map update performance
- Improve Unity 6 / 6.1 / 6.2 compatibility
- Update minimum supported Unity version to 2020.3.0f1
- Improve Issues Finder settings UX
- Skip hidden components when revealing a reference

### Removed

- Remove legacy .NET 3.5 support

### Fixed

- Fix Inspector might not show up while navigating results in some scenarios
- Fix Discord icons might not show up in some scenarios
- Fix deleted prefab / scene reveal might break UI (thx Paul Dyatlov)
- Fix console errors while revealing references (thx Pishi)
- Fix object-level reveal lead to Can't show it properly message
- Fix UI exception when clearing results in some cases
- Project Cleaner:
  - Fix BuildProfile assets now properly auto-ignored to prevent false unused asset detection
  - Fix asset size calculation inconsistency across multiple runs that could show incorrect total sizes
  - Fix assets referenced from project settings now properly treated
  - Fix HDRP default resources folder now automatically excluded
- Issues Finder:
  - Fix rare console exception when switching to module tab
  - Fix missing reference reset didn't work in few edge cases (thx Alex)

## [1.17.1] - 2024-11-22

### Fixed
- Fix lightmapper warning from Demo scene on Mac
- Fix disabled filters could break search in some cases

## [1.17.0] - 2024-11-20

### Added
- Add demo scene at Maintainer/Demo/Maintainer Demo.unity

### Fixed
- Fix deprecated API warnings in Unity 6 LTS (thx Makaka Games)

## [1.16.3] - 2024-09-04

### Changed
- Improve Unity 6 compatibility
- Dramatically improve Assets Map update time in some scenarios by magnitude of 5

### Fixed
- Fix records tabs layout
- Fix GUI Layout errors in Console when Maintainer is opened first time

## [1.16.2] - 2023-06-07

### Changed
- Improve exceptions handling at the Issues Fixer
- Improve Issues Fixer performance (thx Mattis)

## [1.16.1] - 2023-04-14

### Changed
- Multiple managed references to the same instance in one SerializedObject are now traversed only once in Unity 2021.2 or newer

### Fixed
- Fix infinite [SerializeReference] recursion could lead to freeze (thx Makaka Games)
- Fix null ref when dealing with files without asset importer (thx CDF)
- Fix few harmless errors in Console when revealing Issue

## [1.16.0] - 2022-09-18

### Added
- Add Deep search option to the References Finder

### Changed
- Improve Assets Map generation performance (thx Adam Kane)
- Reduce Assets Map file size
- Reduce References Finder UI CPU overhead
- Nicify property path at the Hierarchy Reference Finder results

### Removed
- Remove few obsolete APIs

### Fixed
- Fix reveal in prefabs could work incorrectly

## [1.15.0] - 2022-08-05

### Added
- Add support for SerializeReference attribute (thx CanBaycay)

### Changed
- Improve Duplicate component search accuracy

## [1.14.3] - 2022-06-24

### Fixed
- Fix scripts references might not appear in References Finder (thx CanBaycay)

## [1.14.2] - 2022-06-21

### Fixed
- Fix Addressable folders dependencies processing (thx sebas77)

## [1.14.1] - 2022-04-11

### Changed
- Improve Unity 2022.2 compatibility

## [1.14.0] - 2022-02-05

### Added
- Add "Ignore Editor assets" option (on by default) to Project Cleaner
  - In addition to Editor-only special folders, it now ignores assets under editor-only Assembly Definitions

### Removed
- Remove 'Editor Resources' and 'EditorResources' folders from the Project Cleaner builtin ignores

### Fixed
- Fix redundant .asmdef ignore added to the Project Cleaner while migrating from < 1.4.1 version
- Fix icons rendering performance degradation regression

## [1.13.2] - 2022-01-19
### Changed
- Improve few built-in icons rendering

## [1.13.1] - 2022-01-17
### Fixed
- Fix possible exception while scanning for missing references

## [1.13.0] - 2021-10-04
### Added
- Add automatic IDependenciesParser implementations integration in Unity 2019.2+
- Add Filters count to the filtering tabs titles
- Add Tools > Code Stage > Maintainer > Find Issues In Opened Scenes along with new StartSearchInOpenedScenes API
- Add Game Object > Maintainer > Find Issues In Opened Scenes menu
- Add MaintainerExtension base class for extensions
- Add DependenciesParser base class for dependency parsing extensions
- Add items count to the Issues Finder and Project Cleaner reports
- Add new support contact, let's chat at [Discord](https://discord.gg/FRK5HRzZvq)!

### Changed
- Improve Project Cleaner ignores context menu UX
- Allow searching issues at the unsaved Untitled scene
- Previous search results do not clear up anymore if new search was cancelled
- Improve Unity 2022.1 compatibility
- Make AssetInfo class public
- Change IDependenciesParser.GetDependenciesGUIDs signature to allow more flexibility:
  - accept AssetInfo instance
  - return IList
- Utilize IDependenciesParser for settings dependencies processing

### Deprecated
- Starting from Unity 2019.2+, deprecate redundant AssetDependenciesSearcher.AddExternalDependenciesParsers API
- Deprecate legacy .NET 3.5 (to be removed in next major upgrade in favor of newer .NET version)

### Fixed
- Issues Finder:
  - Fix some Game Objects still could be analyzed with Game Objects option disabled
  - Fix missing components reveal didn't properly fold inspectors
  - Fix Game Object context menus were active for non-GameObject selection
  - Fix checking scenes and Game Objects with disabled corresponding options regression
  - Fix rare case with duplicate issues in prefab variants without overrides
- References Finder:
  - Fix search terms were not reset after Unity restart causing a confusion
  - Fix rendering settings path in Exact References at Unity 2020.1+
- Fix possibility of rare filtering issues when having "Assets" in the project path
- Fix fallback fonts were not tracked properly leading to inclusion at the Project Cleaner
- Fix reveal in Prefab Variants didn't set proper Selection
- Fix window notification could not show up after window activation
- Fix minor UI issues
- Fix other minor issues

## [1.12.2] - 2021-08-18
### Fixed
- Fix possible NullReferenceException at scene settings processor (thx thygrrr)

## [1.12.1] - 2021-07-31
### Fixed
- Fix possible NullReferenceException at Issues Finder (thx NIkoloz)

## [1.12.0] - 2021-06-28
### Added
- Project Cleaner: 
  - Add new quick way to treat all scenes in project as used (thx Makaka Games)
  - Add new context menu action for found unused scene assets to treat them as used
### Changed
- Improve errors reporting and exceptions handling
- Project Cleaner: improve used scenes filtering UX
### Fixed
- Issues Finder: fix minor issues at missing references algorithm

## [1.11.0] - 2021-06-23
### Added
#### Issues Finder
  - improve generic missing references lookup in file assets
  - add MonoScript missing default properties lookup support
  - add Materials missing texture references lookup support (thx Makaka Games)
### Removed
- Remove additional path restrictions when dropping assets to path filters
### Fixed
- Issues Finder: fix minor UI issues

## [1.10.2] - 2021-06-17
### Changed
- Make References Finder take embedded packages into account (thx Mr. Pink)
### Fixed
- Fix subfolders processing at References Finder

## [1.10.1] - 2021-06-16
### Changed
- Switch from plain text changelog to markdown-driven format
### Fixed
- Fix References Finder picked more source assets then looking in folders in some cases
- Fix asset selection on reveal when using two-column Project Browser

## [1.10.0] and older

See older versions changelog in legacy text format [here](https://codestage.net/uas_files/maintainer/changelog-legacy.txt)
