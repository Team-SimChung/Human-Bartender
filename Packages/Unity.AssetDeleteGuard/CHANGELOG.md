# Changelog

## 0.2.0-preview.1 — 2026-10-06

- Rebuilt the review UI with summary cards, source grouping, connection details, tabbed coverage/inventory and a fixed action bar; dark/light theme-aware English/Korean controls.
- Added default-on interactive native Delete interception, deferred review of blocked paths and a shared per-user/project setting. Unity may display its own confirmation/failure dialogs. Batch/filesystem deletion and pre-callback Material Variant mutations remain outside protection.
- Separated review-session ownership from rendering and report export. Closing, reload and Play Mode discard approval; native request queues are cancelled at lifecycle boundaries.
- Limited approved backend execution to an exact disposable disk-inventory permit, including exception cleanup.
- Added provider-qualified source identity to prevent unrelated extension sources merging in the UI.

## 0.1.0-preview.1 — 2026-10-06

- Initial standalone Editor package and explicit review/delete menus.
- Saved dependency graph, open object references and common build-setting references.
- Complete folder inventory, stable snapshot checks and full revalidation before trash deletion.
- Conservative rejection of dirty targets, read-only files, unsafe paths, sub-asset selections, Material Variant parents and incomplete scans.
- Per-root deletion results without automatic retries; local JSON reports.
- Korean/English window controls, read-only provider interface and direct/indirect reference sample.
- Disposable-project tests and preview packaging tools.

This is a development preview. Supported release versions and marketplace publisher metadata are not yet finalized.
