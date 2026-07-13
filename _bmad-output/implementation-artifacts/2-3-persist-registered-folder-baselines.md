---
storyId: "2.3"
storyKey: "2-3-persist-registered-folder-baselines"
title: "Persist Registered Folder Baselines"
status: review
epic: "Epic 2: Register Trusted Folder Baselines"
created: 2026-07-13
updated: 2026-07-13
baseline_commit: "c2120e440f36f5221d14b45bdf61cb9958a484ca"
sprint: "Sprint 002 stretch"
source:
  - "../../docs/product-brief.md"
  - "../../docs/prd.md"
  - "../../docs/architecture.md"
  - "../../docs/epics-and-stories.md"
  - "../../docs/sprint-plan-002.md"
  - "sprint-status.yaml"
previousStories:
  - "1-3-load-an-empty-catalog-and-list-registered-folders.md"
  - "2-1-implement-domain-models-and-sha-256-hashing.md"
  - "2-2-scan-folders-recursively.md"
---

# Story 2.3: Persist Registered Folder Baselines

Status: review

## Story

As a user who registers a folder,  
I want the trusted baseline saved in a local JSON catalog,  
so that future CLI runs can verify against it.

## Context

Sprint 002 is active. Story 2.2 is done and passed code review, so its stable `FolderSnapshot` and `FileFingerprint` contracts make the stretch Story 2.3 eligible to start.

Story 1.3 already introduced `IntegrityCatalog`, `CatalogStore.Load()`, injectable catalog paths, missing-catalog-as-empty behavior, and malformed-JSON protection. Story 2.3 extends that existing Core catalog foundation with add-only baseline creation and JSON persistence. It must preserve every Story 1.3 read behavior and must not wire `register <folder>`, decide duplicate-registration policy, or implement refresh/update semantics.

## Scope

- Extend Core catalog behavior to create a registered-folder baseline from a completed `FolderSnapshot`.
- Accept stable identity input from the caller rather than inventing CLI registration or ID-generation policy.
- Add the new `RegisteredFolder` to an `IntegrityCatalog` without mutating scanner results.
- Persist an `IntegrityCatalog` to the injected `CatalogStore` path using `System.Text.Json`.
- Create a missing parent directory and valid `catalog.json` on first successful write.
- Round-trip registered-folder fields: `id`, `rootPath`, `createdAtUtc`, `lastVerifiedAtUtc`, and `files`.
- Round-trip fingerprint fields: `relativePath`, `sha256`, `size`, and `lastModifiedUtc`.
- Keep JSON property names camelCase and the catalog human-inspectable.
- Return a typed Core persistence outcome for expected write failures.
- Preserve missing-catalog load behavior and refuse to overwrite malformed existing JSON.
- Add focused xUnit tests using temporary catalog paths only.

## Out of Scope

- CLI `register <folder>` execution, dispatch, output, validation, or exit-code mapping; Story 2.4 owns it.
- ID generation, duplicate-root/duplicate-ID policy, overwrite registration, or registration's unreadable-file all-or-nothing rule.
- Replacing or refreshing an existing baseline, preserving refresh identity, or any Story 4.3 behavior.
- Updating `lastVerifiedAtUtc` after verification or refresh.
- Verification comparison, `VerificationService`, moved/renamed logic, or Story 3.1+ work.
- Duplicate detection or `DuplicateFinder`.
- Scanner changes beyond consuming the completed Story 2.2 snapshot.
- Non-empty `list` presentation, unregister behavior, report formatting, or new CLI behavior.
- Concurrency/locking policy, incremental persistence, database migration/versioning, or a custom atomic-write protocol not required by the architecture.
- GUI, SQLite or other databases, cloud sync, encryption, real-time monitoring, guaranteed network-share support, complex ignore rules, export reports, or V2 scope.

## Acceptance Criteria

1. **Create an add-only baseline record**  
   Given a completed `FolderSnapshot`, caller-supplied stable `id`, and `createdAtUtc`, when Core adds the baseline to an `IntegrityCatalog`, then the resulting `RegisteredFolder` preserves the supplied ID, snapshot root path, creation timestamp, and snapshot file fingerprints. Initial `LastVerifiedAtUtc` is `null`; this story does not invent verification or refresh semantics.

2. **First write creates valid catalog state**  
   Given the injected catalog path and a missing parent directory/catalog file, when a valid catalog is saved, then Core creates the directory and a valid JSON catalog at that exact path without touching real `%AppData%` in tests.

3. **Complete round trip**  
   Given a catalog containing a registered folder and fingerprints, when it is saved and loaded through separate `CatalogStore` operations, then all required folder and fingerprint values round-trip unchanged, including nullable `lastVerifiedAtUtc`, UTC `DateTimeOffset` values, hashes, sizes, relative paths, and collection contents.

4. **Stable camelCase schema**  
   Given a saved catalog, when its JSON is inspected, then required property names are exactly `registeredFolders`, `id`, `rootPath`, `createdAtUtc`, `lastVerifiedAtUtc`, `files`, `relativePath`, `sha256`, `size`, and `lastModifiedUtc`. No database, binary format, or serialization attributes that couple domain models to CLI concerns are introduced.

5. **Malformed JSON is never silently overwritten**  
   Given an existing catalog containing malformed JSON, when Core attempts a load/modify/save flow, then it returns a catalog/persistence error, leaves the original bytes unchanged, and does not replace the file with a new valid catalog.

6. **Expected write failures are typed and non-destructive**  
   Given the target directory or file cannot be created or written, when save is attempted, then Core returns a typed failure with a meaningful message rather than leaking an expected write exception or reporting success.

7. **Existing load behavior is preserved**  
   Given no catalog exists, `Load()` still succeeds with an empty catalog. Given malformed JSON, `Load()` still returns a catalog error without modifying the file.

8. **Empty collections remain persistable**  
   Given an empty catalog or a registered-folder record whose `Files` collection is empty, save/load produces valid round-trippable JSON. This does not decide whether CLI registration of an empty folder is allowed.

9. **Architecture and scope compliance**  
   Given the completed change, Core remains independent of CLI and `System.Console`, uses only .NET 8 platform libraries and existing test dependencies, and introduces no registration workflow, verification, duplicates, refresh, scanner expansion, or later/V2 behavior.

10. **Regression validation**  
    Given Story 2.3 is implemented, repository-root restore, build, and the full test suite succeed, including existing missing-catalog, malformed-JSON, scanner, parser, list, model, and hashing tests.

## Technical Notes

- Target .NET 8/C# and reuse `System.Text.Json`; no new package is required.
- Extend the existing `CatalogStore` rather than create a parallel store. Its constructor already supplies the injectable catalog path and its static `JsonSerializerOptions(JsonSerializerDefaults.Web)` already provides camelCase naming.
- Reuse one shared serializer-options instance for load and save. If pretty-printing is selected for human inspectability, keep the same options for all catalog writes and do not make whitespace an API contract.
- Preserve current `Load()` outcomes:
  - missing file => success with `IntegrityCatalog.Empty`;
  - malformed/unsupported JSON => typed catalog error;
  - read access/I/O failure => typed catalog error.
- Add a narrow `Save(IntegrityCatalog catalog)` path and a small typed result such as `CatalogSaveResult` if needed. Expected `IOException` and `UnauthorizedAccessException` outcomes must be represented without pulling CLI exit codes into Core.
- Before overwriting an existing catalog, validate that its current JSON is loadable. A malformed file must remain byte-for-byte unchanged. Do not call save after a failed load.
- First write should use `Directory.CreateDirectory` on the catalog's parent directory when one is present, then serialize the catalog to the configured path.
- Keep domain records simple. Do not add JSON attributes unless the shared naming policy cannot meet the required schema.
- A minimal add API may be placed on `IntegrityCatalog`, for example `AddRegisteredFolder(string id, FolderSnapshot snapshot, DateTimeOffset createdAtUtc)`, returning a new catalog. The caller supplies the ID; duplicate/overwrite policy remains later registration work.
- Preserve snapshot order and values as supplied. Do not rescan, rehash, normalize paths, or rewrite timestamps.
- The existing `RegisteredFolder.LastVerifiedAtUtc` is nullable. Initial persistence should keep it `null`; verification/refresh stories own later updates.
- Tests must parse JSON structurally (for example with `JsonDocument`) to assert exact field names without coupling to whitespace or property order.
- Use a path whose parent is a regular file or another deterministic seam to exercise write failure; avoid flaky OS-permission tests.
- Do not read or write the default `CatalogPathProvider` location in tests.

### Files to Update or Add

- Update `src/FolderPrint.Core/Catalog/CatalogStore.cs` for save behavior.
- Update `src/FolderPrint.Core/Catalog/IntegrityCatalog.cs` for a narrow add-only baseline operation.
- Add a small Core save-result type under `src/FolderPrint.Core/Catalog/` if required.
- Update `tests/FolderPrint.Tests/Catalog/CatalogStoreTests.cs`.
- Add focused catalog-domain tests only if keeping them separate improves clarity.
- Reuse unchanged unless integration proves necessary: `RegisteredFolder.cs`, `FileFingerprint.cs`, `FolderSnapshot.cs`, and Story 2.2 scanner files.
- Do not change `FolderPrint.Cli`.

### Previous Story Intelligence

- Story 1.3 deliberately implemented read-only catalog loading. Extend it; do not replace its typed load result or malformed-file protection.
- Story 2.2 is reviewed/done and returns deterministically ordered readable fingerprints plus unreadable paths. Persist only the supplied readable `Files`; Story 2.3 must not rescan or decide registration failure on `UnreadableFiles`.
- Core currently has no project references and no console dependency.
- Existing tests use xUnit, injected temporary paths, exact result assertions, and cleanup-safe temporary filesystem setup.

### References

- Product scope and inspectable JSON: `docs/product-brief.md#Product Principles`, `#V1 Scope`, `#Non-Goals`
- Catalog, records, fingerprints, and errors: `docs/prd.md#7.1 Catalog Management`, `#9 Data Requirements`, `#10 Non-Functional Requirements`, `#11 Error Handling Requirements`
- Catalog components/schema/architecture rules: `docs/architecture.md#Component Responsibilities`, `#JSON Catalog Schema`, `#Error Handling Strategy`, `#Testing Strategy`, `#Architecture Decisions`
- Story source: `docs/epics-and-stories.md#Story 2.3 Persist Registered Folder Baselines`
- Stretch eligibility and limits: `docs/sprint-plan-002.md#Story 2.3 Persist Registered Folder Baselines Stretch`, `#Dependencies`, `#Risks`, `#Definition of Done`
- Previous implementation: `_bmad-output/implementation-artifacts/2-2-scan-folders-recursively.md`
- Existing code: `src/FolderPrint.Core/Catalog/CatalogStore.cs`, `IntegrityCatalog.cs`, `CatalogLoadResult.cs`, `CatalogPathProvider.cs`
- Official .NET guidance: [System.Text.Json property naming](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties), [JsonSerializer options](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options), [JsonSerializer API](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer)

## Tasks

- [x] Add an immutable/add-only Core operation that maps a completed snapshot plus caller-supplied identity/creation time to a `RegisteredFolder` in `IntegrityCatalog`. (AC: 1, 8, 9)
- [x] Extend `CatalogStore` with JSON save behavior using the existing injected path and shared `System.Text.Json` options. (AC: 2-4)
- [x] Create missing parent directories and valid `catalog.json` on first successful save. (AC: 2)
- [x] Add typed save failure handling for expected path/access/I/O errors. (AC: 6)
- [x] Protect existing malformed JSON from overwrite during modification/save flows. (AC: 5, 7)
- [x] Add save/load round-trip tests for all registered-folder and fingerprint fields, including `null` and non-null `lastVerifiedAtUtc`, UTC timestamps, nested relative paths, and empty collections. (AC: 3, 8)
- [x] Add structural JSON tests for every required camelCase field name. (AC: 4)
- [x] Add tests for first-write directory creation and deterministic write failure without real `%AppData%`. (AC: 2, 6)
- [x] Re-run and preserve existing missing-catalog and malformed-JSON tests. (AC: 5, 7, 10)
- [x] Run validation and confirm no CLI, registration, verification, duplicate, refresh, scanner, dependency, or V2 scope was introduced. (AC: 9, 10)

## Validation Commands

Run from repository root:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
```

Optional scope check:

```powershell
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'VerificationService|DuplicateFinder|ReportFormatter|System.CommandLine|SQLite|Sqlite|export-report'
```

## Definition of Done

- All acceptance criteria pass.
- A completed snapshot can be represented as an add-only registered baseline with caller-supplied stable identity.
- First save creates the parent directory and a valid local JSON catalog.
- Save/load round trips every required registered-folder and fingerprint field with exact camelCase names.
- Missing-catalog and malformed-JSON protections remain intact; malformed content is never silently overwritten.
- Expected write failures return a typed Core result and do not report success.
- Tests use only injected temporary catalog paths and do not touch real `%AppData%`.
- Core remains free of CLI, `System.Console`, new project references, and external runtime dependencies.
- No register command, duplicate policy, verification, duplicate detection, refresh/replacement behavior, scanner change, or V2 scope is implemented.
- `dotnet restore`, `dotnet build`, and `dotnet test` pass from repository root.
- Story 2.3 is updated by the dev agent after implementation with validation evidence, completion notes, and changed files.

## Dev Agent Record

### Agent Model Used

OpenAI Codex (GPT-5)

### Implementation Plan

- Add an immutable catalog operation that maps an existing `FolderSnapshot` to a `RegisteredFolder` without scanning, hashing, duplicate policy, or CLI orchestration.
- Extend the existing JSON `CatalogStore` with typed save outcomes, first-write directory creation, shared camelCase serialization options, and malformed-catalog overwrite protection.
- Drive the implementation with focused catalog tests, then run the complete repository validation suite and scope checks.

### Debug Log References

- RED: `dotnet test --no-restore` failed to compile because `CatalogStore.Save` and `IntegrityCatalog.AddRegisteredFolder` did not exist.
- GREEN: focused catalog tests initially exposed two assertion defects (collection instance equality and expected wording), which were corrected before the focused suite passed 9/9.
- REGRESSION: restore, build, and the complete test suite passed; 43 tests passed with 0 failures.

### Completion Notes List

- Added immutable add-only baseline creation from caller-supplied identity, creation time, and an existing `FolderSnapshot`; initial verification time remains null.
- Added human-inspectable camelCase JSON catalog saving with parent-directory creation and full folder/fingerprint round trips.
- Added typed save results for expected serialization, I/O, and access failures.
- Existing malformed catalogs are validated before overwrite and remain byte-for-byte unchanged on failure.
- Added focused tests for baseline mapping, empty file sets, first write, schema names, complete round trip, malformed preservation, and deterministic write failure.
- No CLI behavior, verification, duplicate detection, refresh, scanner changes, external dependencies, or V2 features were added.

### File List

- `_bmad-output/implementation-artifacts/2-3-persist-registered-folder-baselines.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/stories/story-006.md`
- `src/FolderPrint.Core/Catalog/CatalogSaveResult.cs`
- `src/FolderPrint.Core/Catalog/CatalogStore.cs`
- `src/FolderPrint.Core/Catalog/IntegrityCatalog.cs`
- `tests/FolderPrint.Tests/Catalog/CatalogStoreTests.cs`
- `tests/FolderPrint.Tests/Catalog/IntegrityCatalogTests.cs`

## Change Log

- 2026-07-13: Created implementation-ready Story 2.3 as Sprint 002 stretch work after Story 2.2 completion and review.
- 2026-07-13: Implemented Core baseline persistence and catalog tests; moved story to review after all validations passed.
