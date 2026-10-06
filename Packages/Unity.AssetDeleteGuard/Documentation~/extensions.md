# Read-only reference providers

An optional integration references `AssetDeleteGuard.Core` and `AssetDeleteGuard.Editor` from an Editor-only assembly. It implements `IReferenceProvider` and registers an instance through `ReferenceProviders.Register`. Unregister it by ID when the integration is disabled. Use a stable, unique ID and update its Version when reference semantics change.

`Collect(ReferenceQuery)` runs synchronously on Unity's main thread, including immediately before deletion. `TargetPaths` and `TargetGuids` correspond by index. A provider returns `ProviderResult` with zero or more `ReferenceEntry` objects, warnings and errors. Each entry needs a stable nonempty SourceId and a TargetPath from the query. SourcePath should be a project-relative path when one exists, SourceLabel a readable source description and PropertyPath the exact setting/key when known. The host assigns ProviderId and Extension kind. A source inside the deletion set is excluded.

Providers must be read-only, deterministic and free of save, import, refresh, network or delayed mutation side effects. Do not use an incomplete cache without reporting an error. Throwing, returning null, an invalid entry or an error prevents deletion. Warnings describe bounded omissions while still allowing review; they must not disguise provider failures. Return `Applicable = false` only when the provider is irrelevant to this project/query.

Do not retain or mutate another provider's query/report. Queries contain copies of the host arrays, and result entries are copied before the host keeps them. Snapshot state and the final report are owned by the analyzer; extension providers cannot issue a deletion approval.

The public `ReferenceAnalyzer.Analyze(paths)` is a synchronous read-only entry for custom inspection tools. Call it after compilation/imports settle, on the main thread in Edit Mode. Errors appear in the returned report. The deletion executor is internal; importing JSON or registering a provider does not expose a public unattended-delete API.

Addressables and other optional integrations should be separate packages with declared, versioned dependencies and their own fixture tests. This preview does not ship an Addressables provider.
