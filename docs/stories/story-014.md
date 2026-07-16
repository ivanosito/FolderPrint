---
storyId: '4.3'
storyKey: '4-3-refresh-a-registered-folder-baseline'
title: 'Refresh a Registered Folder Baseline'
status: done
baseline_commit: 3c4cc0505fd8dc5f7920c0b20d3084d465812e64
epic: 'Epic 4: Manage Registered Folders and Baselines'
created: 2026-07-15
updated: 2026-07-15
sprint: 'Sprint 007 committed'
previousStory: '4-2-unregister-a-folder.md'
source:
  - '../../docs/epics-and-stories.md'
  - '../../docs/sprint-plan-007.md'
  - '../../docs/architecture.md'
  - '../planning-artifacts/prds/prd-FolderPrint-2026-07-07/prd.md'
  - 'sprint-status.yaml'
  - '4-2-unregister-a-folder.md'
  - '../../docs/retrospectives/sprint-006-retrospective.md'
---

# Story 4.3: Refresh a Registered Folder Baseline

Status: done

## Story

As a CLI user, I want to refresh a registered folder baseline after intentional changes, so that future verifications compare against the newly trusted state.

## Context

Story 4.3 is Sprint 007's sole committed story and completes Epic 4. The parser already recognizes refresh, but CliRunner lacks a refresh arm and currently returns silent success without scanning or saving.

Story 4.2 established the required persistence protocol: carry the initial version into one filesystem-coordinated SaveIfUnchanged, durably flush a same-directory temporary file, recheck immediately before replacement, and preserve newer state on conflict. Refresh must reuse it.

Refresh accepts intentional state as trusted, so it follows registration's reliable-scan rule: unreadable findings or scan failures prevent replacement. Success changes one baseline and timestamp while preserving identity, survivors, and target files.

## Scope

- Dispatch refresh <folder> and return deterministic V1 output and exit codes.
- Load and validate the catalog, identify one registration through established identity, and scan the target exactly once.
- Reject incomplete snapshots and immutably replace only the matched baseline.
- Preserve Id, stored RootPath, CreatedAtUtc, every survivor, metadata field, baseline, and position.
- Set LastVerifiedAtUtc from one injected UTC completion clock value.
- Persist through exactly one shared guarded save and add automated tests.

## Out of Scope

Standalone duplicates, DuplicateFinder, verification comparison or drift reporting, retries or merging, parser/exit/schema changes, target-file writes, GUI, database, cloud, encryption, realtime monitoring, dependencies, and V2.

## Acceptance Criteria

1. A complete readable scan is persisted through one guarded save. Then stdout is exactly Refreshed folder: <stored-root-path>{NL}Files: <count>{NL}, stderr is empty, and exit is 0. Never print success first; NL is the injected writer's newline.
2. Reuse RegistrationService.NormalizeRootPath, ordinal-ignore-case identity, full CatalogValidator, and the exact entry/index from RegisteredFolderLookup. Case, dot, or trailing-separator aliases may match, but persist and report stored RootPath; prefixes, siblings, nested paths, IDs, raw equality, and record equality do not match.
3. Replacement is immutable and index-based. Source stays unchanged; result owns a new collection; target Id, stored root, and creation time stay unchanged; files become an owned snapshot copy; LastVerifiedAtUtc becomes one injected utcNow().ToUniversalTime() read after the reliable-scan gate. Survivor objects, order, and data stay unchanged. Empty reliable snapshots are valid.
4. Validate all catalog data before target access. Missing or empty catalog, no match, or invalid request returns NotFound (3) with no scan, clock, save, lock or catalog creation, or rewrite. Duplicate normalized roots or invalid registrations and fingerprints return CatalogError (4) and preserve raw bytes.
5. At preflight, a missing or file-valued matched root returns 3, while unauthorized or I/O failure returns ScanError (5). Directory disappearance, enumeration, I/O, unauthorized access, or hashing failure after scan invocation also returns 5; partial state is never trusted.
6. Any unreadable path returns 5. Stderr is exactly Folder refresh failed because one or more files could not be read. followed by ordinally sorted Unreadable: <relative-path> lines, each with NL. Catalog bytes remain unchanged. Duplicate readable hashes are valid fingerprints and never invoke DuplicateFinder.
7. Refresh itself performs no target mutation: it may enumerate, read metadata, and hash, but never creates, deletes, moves, renames, or writes target files or directories. Controlled tests assert FolderPrint-caused effects preserve content, tree, existence, last-write timestamps, and attributes; do not assert last-access time or promise protection from external actors.
8. If the active catalog or mutation artifacts are inside the target, return 4 before scan. Reuse or narrowly expose registration's containment rule so FolderPrint state under the target is never scanned or modified.
9. Carry the initial version through scanning into exactly one SaveIfUnchanged. Concurrent register, verify, unregister, refresh, or visible external edits return 4; never retry, merge, resurrect, or call unconditional Save. Preserve latest or prior bytes, suppress success, and leave no temp artifact.
10. Mapping is fixed: parser shape 2; invalid, unregistered, missing, or file roots 3; catalog, validation, unsafe placement, conflict, or save failures 4; scan or unreadable failures 5; success 0. Refresh never returns DifferencesFound (1).
11. Core owns transformation, scan orchestration, typed result, and persistence without console, writers, CLI exits, or CLI references. CLI owns dispatch, output, and exit mapping. Existing register, list, verify, unregister, parser, scanner, catalog, and reporting regressions pass.
12. Real register -> add/change/delete drift -> refresh -> verify returns clean/0; later drift returns differences/1. Identity and unrelated registrations remain usable and JSON schema stays unchanged.

## Refresh Timestamp Contract

- RefreshService injects Func<DateTimeOffset>, defaulting to DateTimeOffset.UtcNow.
- Read once after directory validation, successful scan, and unreadable rejection.
- Persist ToUniversalTime() in existing LastVerifiedAtUtc; add no field.
- Conflict or save failure discards the candidate timestamp.

## Conflict-Aware Mutation Contract

- Reuse one CatalogStore.SaveIfUnchanged with the initial version.
- Reuse sidecar locking, durable temp writing, final version checking, replacement, and cleanup.
- Add no alternate save, unconditional save, retry, or merge.
- A narrow internal beforeSave test seam is allowed; production remains one attempt.

## Output and Exit Contract

| Outcome | Stdout | Stderr | Exit |
|---|---|---|---:|
| Persisted | two pinned success lines | empty | 0 |
| Invalid/unregistered/missing/file root | empty | stable identity or root error | 3 |
| Catalog/unsafe placement/conflict/save failure | empty | typed catalog error | 4 |
| Scan failure/unreadables | empty | scan error and sorted unreadables | 5 |

## Tasks / Subtasks

- [x] Add immutable baseline replacement to IntegrityCatalog. (AC: 3, 11)
  - [x] Preserve identity and survivors, own files, store UTC refresh time.
  - [x] Test positions, empty transitions, source immutability, duplicate IDs, invalid indices, UTC conversion, and owned files.
- [x] Add RefreshStatus, RefreshResult, and RefreshService under Core Registration. (AC: 2-9, 11)
  - [x] Sequence normalize -> load/version -> lookup -> catalog-inside-root guard -> directory validation -> one scan -> unreadable rejection -> one clock -> immutable replacement -> one guarded save.
  - [x] Return stored root and file count only after save; return typed sorted unreadables on scan failure.
  - [x] Make RegistrationService.IsPathInsideRoot internal static, or extract one internal Core helper; never make it public or duplicate it. Preserve registration behavior.
- [x] Wire explicit refresh dispatch and typed CLI mapping. (AC: 1, 4-6, 10-11)
  - [x] Parser, enum, usage, and exit values already exist; do not change them.
  - [x] Append refresh scan and clock injection parameters so positional verification delegates remain compatible.
  - [x] Emit exact output and never use verification reporting.
- [x] Add reliability, concurrency, safety, and regression tests. (AC: 1-12)
  - [x] Cover aliases and nonmatches, missing/empty/malformed/ambiguous catalogs, invalid unrelated data, root failures, scan exceptions, unreadables, and empty snapshots.
  - [x] Prove zero pre-lookup target access, one scan/clock/save, raw-byte failure preservation, and no not-found lock or catalog creation.
  - [x] Prove writer and external conflicts preserve latest state without retry, resurrection, output, or temp residue.
  - [x] Prove target tree, content, last-write data, and attributes remain unchanged, including catalog-inside-root.
  - [x] Add refresh and verify integration plus survivor command regressions.
- [x] Run full validation and scope checks. (AC: 7, 9-12)

### Review Findings

- [x] [Review][Patch] Filesystem-link aliases can bypass catalog containment [src/FolderPrint.Core/Registration/RefreshService.cs:94]
- [x] [Review][Patch] Hash-provider exceptions escape the typed scan-error contract [src/FolderPrint.Core/Registration/RefreshService.cs:116]
- [x] [Review][Patch] Project story mirror remains stale at ready-for-dev [docs/stories/story-014.md:5]

## Dev Notes

### Existing Seams and Guardrails

- Fix CliRunner's missing arm; parser and enum already recognize refresh.
- RegisteredFolderLookup.Find owns validation, normalized identity, duplicate rejection, stored folder, and index.
- Reuse RegistrationService.NormalizeRootPath and its active-catalog-inside-root policy.
- FolderScanner.Scan supplies sorted fingerprints and unreadables. Reject unreadables; never call VerificationService.
- Add immutable refresh beside IntegrityCatalog timestamp and remove methods; do not transform catalog in CLI.
- Reuse reviewed CatalogStore.SaveIfUnchanged unless a direct failing test proves a defect.
- Scan normalized request but persist and report stored root, not alias or snapshot root.
- Duplicate hashes are accepted; do no comparison, grouping, or verification formatting.

### Architecture and File Scope

- Use .NET 8, platform libraries, and xUnit only. Keep CLI -> Core and keep console and exit concepts out of Core.
- Preserve camelCase JSON, model constructors, manual parser, numeric exits, and project/package files.
- Update CliRunner.cs, IntegrityCatalog.cs, IntegrityCatalogTests.cs, and only if needed RegistrationService.cs for containment reuse.
- Add RefreshStatus.cs, RefreshResult.cs, RefreshService.cs, RefreshServiceTests.cs, CliRefreshTests.cs, and optionally a focused integration test file.
- Do not change parser/kind/exits, CatalogStore, scanner/hasher, verification/reporting, models/schema/projects/packages, or DuplicateFinder without a failing direct requirement.

### Testing Requirements

- Use xUnit naming, temporary injected paths, fixed valid hashes and timestamps, deterministic callbacks/barriers, never real AppData or sleeps.
- Compare raw failure bytes and survivor identity/order; count scan, clock, and save attempts.
- Target tests compare tree, content, existence, last-write timestamps, and attributes, not last-access.
- Pin the current silent-success dispatch defect with a red test.
- Reviewed baseline is 186 tests; retain it plus focused refresh coverage.

### Previous Story and Git Intelligence

Story 4.2 introduced immutable index mutation, typed lifecycle results, explicit dispatch, and guarded persistence shared by register, verify, and unregister. Review added final pre-replacement checking, filesystem sidecar coordination, correct inaccessible-catalog classification, and survivor list and verify coverage. Refresh builds on those corrections.

Baseline 3c4cc05 contains Sprint 007 planning. Commits 4498577 and d7a8ee4 contain persistence hardening and implementation patterns. No dependency or project change is indicated.

### References

- [Source: docs/epics-and-stories.md#Story-43-Refresh-a-Registered-Folder-Baseline]
- [Source: docs/sprint-plan-007.md#Story-43-Refresh-a-Registered-Folder-Baseline]
- [Source: docs/architecture.md#Refresh]
- [Source: docs/architecture.md#Error-Handling-Strategy]
- [Source: _bmad-output/planning-artifacts/prds/prd-FolderPrint-2026-07-07/prd.md#FR-18-Refresh-a-registered-folder]
- [Source: _bmad-output/implementation-artifacts/4-2-unregister-a-folder.md#Conflict-Aware-Mutation-Contract]
- [Source: docs/retrospectives/sprint-006-retrospective.md#Recommended-Next-Story]
- [Source: src/FolderPrint.Cli/CliRunner.cs]
- [Source: src/FolderPrint.Core/Catalog/IntegrityCatalog.cs]
- [Source: src/FolderPrint.Core/Catalog/CatalogStore.cs]
- [Source: src/FolderPrint.Core/Catalog/RegisteredFolderLookup.cs]
- [Source: src/FolderPrint.Core/Registration/RegistrationService.cs]
- [Source: src/FolderPrint.Core/Scanning/FolderScanner.cs]

## Validation Commands

```powershell
dotnet restore
dotnet build FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release --no-build --filter 'FullyQualifiedName~Refresh|FullyQualifiedName~CatalogStore|FullyQualifiedName~IntegrityCatalog|FullyQualifiedName~RegistrationService|FullyQualifiedName~Unregistration|FullyQualifiedName~Verify'
dotnet format FolderPrint.sln --verify-no-changes --no-restore
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Inspect Core boundaries, target-write APIs reachable from refresh, excluded scope, and the final diff.

## Definition of Done

- Every AC passes and tasks are checked only with evidence.
- One reliable snapshot replaces one baseline; identity, survivors, and targets stay unchanged.
- Timestamp, output, and exit contracts are tested.
- Guarded persistence preserves stale and latest state without retry or temp residue.
- Refresh then verify is clean; later drift is detected.
- Boundaries, exclusions, and full/focused quality checks pass.
- Move to review only after implementation validation; Epic 4 remains in-progress until reviewed and done.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Implementation Plan

- Add an immutable index-based catalog transformation before introducing orchestration.
- Keep refresh sequencing and typed failures in Core, reusing lookup, validation, scanning, containment, and SaveIfUnchanged.
- Append CLI injection seams, add explicit dispatch and deterministic output, then prove behavior through unit, conflict, safety, and real filesystem integration tests.

### Debug Log References

- Task 1 red: focused catalog tests failed with CS1061 for the missing WithRefreshedBaseline API.
- Task 1 green: 15 focused catalog tests and 192 full regression tests passed.
- Task 2 red: focused tests failed for missing RefreshService and RefreshStatus production types.
- Task 2 green: 8 focused refresh service tests and 200 full regression tests passed.
- Task 3 red: focused CLI tests failed because CliRunner had no refresh injection or dispatch.
- Task 3 green: 6 focused CLI refresh tests and 206 full regression tests passed.
- Task 4: 13 focused concurrency/integration tests and 211 full regression tests passed.
- Task 5: Release build passed with zero warnings/errors; 211 full and 94 focused Release tests passed; formatting, dependency, scope, boundary, and diff checks passed.
- Code review patches: 2 focused regressions, 213 full Release tests, 96 focused Release tests, and formatting verification passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Implemented immutable index-based baseline replacement with owned files, UTC refresh time, survivor preservation, and source immutability.
- Implemented typed Core refresh orchestration with catalog validation, exact lookup, reliable scan gating, deterministic unreadables, one completion clock, containment protection, and one conflict-aware guarded save.
- Wired explicit refresh CLI dispatch with exact two-line success output, sorted unreadable reporting, and V1 exit-code mapping while preserving existing constructor positions.
- Added deterministic cross-writer conflict coverage for register, verify, unregister, refresh, and external edits plus real filesystem non-mutation and refresh-to-verify regression flows.
- Completed all acceptance criteria without parser, schema, project, package, scanner, verification, DuplicateFinder, duplicates command, or target-file mutation changes.
- Resolved all code-review findings: physical link containment, cryptographic scan-error mapping, and canonical story mirror synchronization.

### File List

- _bmad-output/implementation-artifacts/4-3-refresh-a-registered-folder-baseline.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- docs/stories/story-014.md
- src/FolderPrint.Core/Catalog/IntegrityCatalog.cs
- tests/FolderPrint.Tests/Catalog/IntegrityCatalogTests.cs
- src/FolderPrint.Core/Registration/RefreshResult.cs
- src/FolderPrint.Core/Registration/RefreshService.cs
- src/FolderPrint.Core/Registration/RefreshStatus.cs
- src/FolderPrint.Core/Registration/RegistrationService.cs
- tests/FolderPrint.Tests/Registration/RefreshServiceTests.cs
- src/FolderPrint.Cli/CliRunner.cs
- tests/FolderPrint.Tests/Cli/CliRefreshTests.cs
- tests/FolderPrint.Tests/Cli/CliRefreshIntegrationTests.cs

## Change Log

- 2026-07-15: Implemented Story 4.3 refresh baseline support with conflict-aware persistence, deterministic CLI reporting, safety guarantees, and comprehensive automated coverage; moved to review.
- 2026-07-15: Addressed all three code-review findings and moved Story 4.3 to done.
