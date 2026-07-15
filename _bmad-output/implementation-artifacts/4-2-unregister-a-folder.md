---
storyId: "4.2"
storyKey: "4-2-unregister-a-folder"
title: "Unregister a Folder"
status: review
baseline_commit: d34f419f9a5779afd4ba25ba848cf8e633fb2694
epic: "Epic 4: Manage Registered Folders and Baselines"
created: 2026-07-15
updated: 2026-07-15
sprint: "Sprint 006 gated stretch"
previousStory: "4-1-display-registered-folder-metadata.md"
source:
  - "../../docs/epics-and-stories.md"
  - "../../docs/sprint-plan-006.md"
  - "../../docs/architecture.md"
  - "sprint-status.yaml"
  - "4-1-display-registered-folder-metadata.md"
  - "epic-3-retro-2026-07-15.md"
---

# Story 4.2: Unregister a Folder

Status: review

## Story

As a CLI user, I want to unregister a folder, so that FolderPrint stops tracking it without deleting or modifying my files.

## Context

Story 4.2 is Sprint 006 gated stretch. Its gate is satisfied: Story 4.1 is done and reviewed.

The parser already recognizes `unregister <folder>`, but `CliRunner` does not dispatch it. `RegisteredFolderLookup` already supplies V1 normalization, ordinal-ignore-case identity, whole-catalog validation, duplicate-root rejection, and an unambiguous match index.

This story closes the carried-forward conflict-awareness action. Current `SaveIfUnchanged` compares and then calls a separate save, leaving a check-to-replace race; registration also saves a snapshot unconditionally. Establish one shared guarded mutation protocol so participating register, verify, and unregister operations cannot overwrite a committed mutation.

## Scope

- Dispatch V1 `unregister <folder>`.
- Remove exactly one validated match through immutable catalog transformation.
- Preserve every survivor, nested baseline, metadata field, and catalog order.
- Use guarded persistence, deterministic reporting, and existing exit codes.
- Add Core, store, CLI, concurrency, filesystem-safety, and regression tests.

## Out of Scope

Refresh, standalone duplicates, `DuplicateFinder`, scan, hash, verify, target-root checks or changes, parser/exit/schema changes, conflict retries, GUI, database, cloud sync, encryption, realtime monitoring, dependencies, and V2 features.

## Acceptance Criteria

1. A valid unambiguous match is removed and guarded-saved. The command writes exactly `Unregistered folder: <stored-root-path>` plus one newline to stdout, nothing to stderr, and exits `0`. Use the persisted matched `RootPath`, so case/separator/dot aliases report identically.
2. Identity reuses `RegistrationService.NormalizeRootPath`, `StringComparer.OrdinalIgnoreCase`, full `CatalogValidator` validation, and the index returned by `RegisteredFolderLookup`. Never remove by raw string, prefix, nested relation, ID, or record equality.
3. Removal is immutable: source catalog/collection are unchanged; the result owns a new collection; survivor order, objects, IDs, roots, registration/verification timestamps, and every fingerprint field/order are preserved. Sole-entry removal persists a valid empty catalog rather than deleting it.
4. Missing catalog, valid empty catalog, or no match returns `NotFound` (`3`), stderr only, with no save or catalog creation/rewrite. Invalid requested paths follow existing identity behavior and return `3`; parser-shape errors remain `UsageError` (`2`).
5. Missing, inaccessible, or file-valued registered roots can be unregistered. No target `File`/`Directory`, scanner, hasher, or verifier API is called; target bytes, existence, attributes, and timestamps remain unchanged.
6. Malformed JSON/structure, invalid registrations/fingerprints, unsafe or duplicate fingerprint paths, invalid stored roots, or duplicate normalized registered roots returns `CatalogError` (`4`), no stdout, and exact catalog-byte preservation.
7. Unregister carries its initial load version into exactly one guarded mutation. Catalog change or lock/write/flush/replace failure returns `4`, suppresses success, preserves latest committed/prior bytes, leaves no temp artifact, and never retries or calls unconditional `Save`.
8. Compare and replacement form one exclusive store operation for participating FolderPrint writers. Register, verify, and unregister share it, so interleavings cannot lose committed mutations. Conflict text is command-neutral. External edits are detected whenever visible before replacement; do not claim guarantees beyond filesystem coordination.
9. Output is deterministic: success stdout-only/0; invalid or unregistered identity stderr-only/3; load, validation, conflict, and save failure stderr-only/4. Unregister never returns Differences or ScanError and reports success only after persistence.
10. Core owns typed mutation/results without console, writers, CLI exit codes, or CLI references. CLI owns dispatch/output/exit mapping. Existing register/list/verify behavior remains passing and exclusions hold.

## Conflict-Aware Mutation Contract

- Snapshot mutations carry `CatalogStore.Load().Version` into `SaveIfUnchanged` or an equivalent narrow API.
- The guarded API owns one exclusive compare-and-replace critical section; it never releases coordination between compare and replacement.
- All snapshot writers participate: registration changes from unconditional save, verification remains guarded, and unregister guards once.
- Under coordination, reload/compare, write a same-directory temp, close/flush per store policy, and replace only on version match. Clean temp files on every failure.
- Stale versions are typed failures. Never merge, retry, or overwrite newer state.
- A sidecar/process lock is acceptable only if all FolderPrint writers share it. Tests use deterministic barriers or narrow internal seams, never sleeps.

## Output and Exit Contract

| Outcome | Stdout | Stderr | Exit |
|---|---|---|---:|
| Persisted removal | `Unregistered folder: <stored-root-path>` | empty | `0` |
| Invalid/not registered | empty | stable identity diagnostic | `3` |
| Invalid/ambiguous catalog | empty | typed catalog diagnostic | `4` |
| Conflict/save failure | empty | command-neutral typed diagnostic | `4` |

## Tasks / Subtasks

- [x] Add immutable removal to `IntegrityCatalog`. (AC: 2-3, 10)
  - [x] Remove by validated index into an independent collection with explicit bounds behavior.
  - [x] Test first/middle/last/sole removal, source immutability, survivor deep equality/order, duplicate IDs, and invalid indices.
- [x] Establish shared guarded mutation in `CatalogStore`. (AC: 7-8, 10)
  - [x] Make compare plus replacement one coordinated operation with temp cleanup and neutral errors.
  - [x] Route registration through the guard using its initial version; keep verification guarded.
  - [x] Test matching/stale/missing-to-created/write failures and deterministic final-check/cross-writer interleavings.
- [x] Add typed Core unregistration. (AC: 1-8, 10)
  - [x] Sequence normalize/load -> validated lookup -> immutable remove -> guarded save.
  - [x] Return typed success/invalid/not-found/catalog-error plus stored root only after saving.
  - [x] Never access targets, scan, hash, verify, retry, or call unconditional save.
- [x] Wire CLI dispatch and exact output/exit mapping. (AC: 1, 4-6, 9-10)
  - [x] Add the missing `CommandKind.Unregister` arm; parser/enum/usage/exit values already exist.
  - [x] Withhold success output until save succeeds.
- [x] Add safety/regression tests. (AC: 1-10)
  - [x] Cover aliases, sibling/prefix/nested non-matches, duplicates, malformed requests/state, and missing/empty catalogs.
  - [x] Prove survivor deep preservation and raw-byte preservation on failures.
  - [x] Prove missing/file-valued/inaccessible roots cause no target or verification work.
  - [x] Cover concurrent add/update/remove, catalog appearance, store failure, no premature output, and no temp residue.
  - [x] Catch current silent-success fall-through; prove re-registration and survivor list/verify behavior.
- [x] Run all validation and scope checks. (AC: 7-10)

## Dev Notes

### Existing Seams and Guardrails

- `CliRunner.cs`: add explicit unregister dispatch/adapter.
- `RegisteredFolderLookup.cs`: authoritative identity/full-validation/index seam.
- `IntegrityCatalog.cs`: add immutable index removal.
- `CatalogStore.cs`: strengthen guarded compare-and-replace and neutralize verification-only conflict wording.
- `RegistrationService.cs`: use initial load version with guarded save.
- Story 3.4 verify already guards timestamp persistence; preserve its contract.
- Story 4.1 supplies validation and byte-preservation patterns.
- Normalize without probing the root; validate every entry; omit only the selected object.
- Never print success before saving, retry conflicts, repair malformed data, or create a catalog for not-found.
- Sole-entry success writes normal empty JSON; failures preserve prior/latest bytes and remove temp artifacts.

### Architecture and Expected File Scope

Use .NET 8/platform libraries only. Keep `CLI -> Core`, manual parsing, JSON schema, and injected test catalog paths.

Expected updates: `CliRunner.cs`, `IntegrityCatalog.cs`, `CatalogStore.cs`, `RegistrationService.cs`; add a narrow typed unregistration service/result/status under Core `Registration/` or `Catalog/`; add `CliUnregisterTests.cs` and focused Catalog/Registration tests. Do not modify scanner, hasher, verification comparison/report formatting, models/schema, project files, or packages without direct tested necessity.

### Testing Requirements

Use xUnit conventions, fixed data, exact output/newlines, raw-byte and deep-data comparisons, and deterministic concurrency barriers. No `Thread.Sleep`, clock/timezone/random/filesystem-order dependency, or real AppData catalog. Prove register/unregister and verify/unregister cannot lose committed changes. Reviewed baseline: 151 tests; retain them plus Story 4.2 coverage.

### Prior Intelligence

Story 4.1 is done/reviewed and established full validation, deterministic stored-root reporting, and byte preservation. Its review hardened IDs, timestamps, sizes, and fingerprint paths. Epic 3 carried forward concurrent-change, invalid-catalog, metadata, and failure-state preservation; this artifact closes that action. Git baseline is `d34f419`.

### References

- [Source: docs/epics-and-stories.md#Story-42-Unregister-a-Folder]
- [Source: docs/sprint-plan-006.md#Story-42-Unregister-a-Folder]
- [Source: docs/architecture.md#Unregister]
- [Source: docs/architecture.md#Error-Handling-Strategy]
- [Source: _bmad-output/implementation-artifacts/4-1-display-registered-folder-metadata.md]
- [Source: _bmad-output/implementation-artifacts/epic-3-retro-2026-07-15.md]
- [Source: src/FolderPrint.Cli/CliRunner.cs]
- [Source: src/FolderPrint.Core/Catalog/CatalogStore.cs]
- [Source: src/FolderPrint.Core/Catalog/RegisteredFolderLookup.cs]
- [Source: src/FolderPrint.Core/Registration/RegistrationService.cs]

## Validation Commands

```powershell
dotnet restore
dotnet build FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release --no-build --filter "FullyQualifiedName~Unregister|FullyQualifiedName~CatalogStore|FullyQualifiedName~IntegrityCatalog|FullyQualifiedName~RegistrationService|FullyQualifiedName~Verify"
dotnet format FolderPrint.sln --verify-no-changes --no-restore
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Inspect Core boundaries, target-filesystem calls reachable from unregister, excluded-scope symbols, and final source/test diff.

## Definition of Done

- Every AC passes and tasks are checked only with evidence.
- Exactly one validated entry is removed; reporting follows guarded persistence.
- Survivors and targets remain unchanged; all failures follow the pinned contract.
- Shared mutation prevents participating writers losing committed changes.
- Boundaries/exclusions hold; full/focused tests, build, format, dependency, scope, and diff checks pass.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Implementation Plan

- Add index-based immutable catalog removal and prove source/survivor preservation.
- Strengthen catalog persistence into one path-scoped guarded compare-and-replace operation, then route registration, verification, and unregister through that shared protocol.
- Add typed Core unregistration using existing normalization, catalog validation, and lookup seams.
- Wire deterministic CLI output/exit mapping without target filesystem access.
- Add focused Core/store/CLI/concurrency tests, then run the full BMAD validation suite.

### Debug Log References

- RED (immutable removal): focused tests failed because `IntegrityCatalog.RemoveRegisteredFolderAt` did not exist.
- GREEN (immutable removal): 9 focused catalog tests passed for first/middle/last/sole removal, bounds, source immutability, and duplicate IDs.
- RED (guarded mutation): focused tests failed because the coordinated store seam did not exist; registration's stale snapshot test exposed unconditional overwrite behavior.
- GREEN (guarded mutation): 41 store/register/verify tests passed after atomic guarded replacement, neutral conflict diagnostics, and guarded registration persistence.
- RED (Core unregister): focused tests failed because typed unregistration service/result/status did not exist.
- GREEN (Core unregister): 11 tests passed for identity, preservation, malformed/ambiguous state, target independence, and register/verify interleavings.
- RED (CLI unregister): all 7 tests exposed the existing silent-success fall-through and missing catalog mutation/output mapping.
- GREEN (CLI unregister): all 7 end-to-end tests passed after explicit dispatch and typed mapping.
- Final validation: restore succeeded; Release build completed with 0 warnings and 0 errors; 71 focused tests and all 181 tests passed; formatting, dependency, boundary, excluded-scope, and diff checks passed.

### Completion Notes List

- Added immutable, index-based registered-folder removal that preserves survivor objects, order, metadata, and baselines.
- Added one path-scoped mutex around guarded version comparison and same-directory replacement, with durable flush, temp cleanup, typed conflicts, and operation-neutral diagnostics.
- Converted registration to guarded saving while preserving Story 3.4 verification behavior.
- Added typed Core unregistration that validates the whole catalog, reuses V1 path identity, removes exactly one match, and never accesses the target root.
- Wired `unregister <folder>` with exact success output and V1 exit-code mapping; success is withheld until persistence completes.
- Added 30 Story 4.2 regression cases, increasing the reviewed baseline from 151 to 181 tests without dependencies or excluded-scope features.

### File List

- `_bmad-output/implementation-artifacts/4-2-unregister-a-folder.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/stories/story-013.md`
- `src/FolderPrint.Cli/CliRunner.cs`
- `src/FolderPrint.Core/Catalog/CatalogStore.cs`
- `src/FolderPrint.Core/Catalog/IntegrityCatalog.cs`
- `src/FolderPrint.Core/Properties/AssemblyInfo.cs`
- `src/FolderPrint.Core/Registration/RegistrationService.cs`
- `src/FolderPrint.Core/Registration/UnregistrationResult.cs`
- `src/FolderPrint.Core/Registration/UnregistrationService.cs`
- `src/FolderPrint.Core/Registration/UnregistrationStatus.cs`
- `tests/FolderPrint.Tests/Catalog/CatalogStoreTests.cs`
- `tests/FolderPrint.Tests/Catalog/IntegrityCatalogTests.cs`
- `tests/FolderPrint.Tests/Cli/CliUnregisterTests.cs`
- `tests/FolderPrint.Tests/Registration/RegistrationServiceTests.cs`
- `tests/FolderPrint.Tests/Registration/UnregistrationServiceTests.cs`

## Change Log

- 2026-07-15: Created implementation-ready Story 4.2, defined shared guarded mutation, and moved it to ready-for-dev.
- 2026-07-15: Implemented conflict-safe unregister, shared guarded persistence, deterministic CLI reporting, and 30 automated tests; moved Story 4.2 to review.
