---
storyId: "2.4"
storyKey: "2-4-wire-register-folder-command"
title: "Wire register <folder> Command"
status: review
epic: "Epic 2: Register Trusted Folder Baselines"
created: 2026-07-13
updated: 2026-07-13
baseline_commit: "d405b65a4f3030a3131516bfabbcd2571774ed6b"
sprint: "Sprint 003 committed"
source:
  - "../../docs/product-brief.md"
  - "../../docs/prd.md"
  - "../../docs/architecture.md"
  - "../../docs/epics-and-stories.md"
  - "../../docs/sprint-plan-003.md"
  - "sprint-status.yaml"
previousStories:
  - "1-2-parse-v1-commands-and-define-exit-codes.md"
  - "1-3-load-an-empty-catalog-and-list-registered-folders.md"
  - "2-1-implement-domain-models-and-sha-256-hashing.md"
  - "2-2-scan-folders-recursively.md"
  - "2-3-persist-registered-folder-baselines.md"
---

# Story 2.4: Wire `register <folder>` Command

Status: review

## Story

As a CLI user,  
I want to register a trusted folder from the command line,  
so that FolderPrint can establish the baseline I will verify later.

## Context

Sprint 003 is active and Story 2.4 is its committed story. Stories 1.2, 2.2, and 2.3 already provide the required parser, scanner/hasher, and safe JSON catalog persistence contracts. This story composes those completed pieces into the first end-to-end trusted-baseline command.

The command must remain an adapter over reusable Core behavior. Registration is all-or-nothing: invalid roots, duplicate roots, unreadable files, malformed catalogs, and save failures must never create a partial baseline or damage the last valid catalog. Story 3.1 is gated stretch work and must not begin here.

## Scope

- Dispatch the existing parsed `register <folder>` command through `CliRunner` or an equivalent existing CLI boundary.
- Normalize the supplied root to one absolute Windows V1 identity before duplicate comparison, scanning, and persistence.
- Load the catalog before scanning and stop immediately on catalog load failure.
- Reject a folder already registered under the documented path-identity rule without overwriting or duplicating its baseline.
- Scan through the existing `FolderScanner`, which already delegates hashing to `FileHasher`.
- Reject the entire registration if the snapshot contains any unreadable file.
- Use the existing immutable `IntegrityCatalog.AddRegisteredFolder` and `CatalogStore.Save` behavior to persist one complete baseline.
- Generate one nonempty stable ID and capture one UTC creation timestamp only after the registration has passed validation and scan completeness checks.
- Keep initial `LastVerifiedAtUtc` as `null`.
- Return deterministic existing exit codes and concise output/error messages.
- Add focused Core policy tests and CLI/integration-style tests using temporary folders and injected catalog paths only.
- Preserve every existing parser, `list`, scanner, hashing, catalog-load, and catalog-save behavior.

## Out of Scope

- Story 3.1 or any verification comparison logic.
- `verify <folder>` dispatch, reporting, or `lastVerifiedAtUtc` updates.
- Moved/renamed detection or ambiguity handling.
- Duplicate-file/content detection; rejecting an already registered root is registration policy, not `DuplicateFinder` work.
- Refresh, overwrite registration, baseline replacement, unregister, or removal behavior.
- Non-empty `list` formatting beyond preserving existing behavior.
- Changes to recursive scanning, hashing, fingerprint metadata, or relative-path separator rules.
- Symlink resolution, physical-volume identity, cross-platform path policy, network-share guarantees, catalog locking/concurrent-writer coordination, or complex canonicalization.
- GUI, SQLite or another database, cloud sync, encryption, real-time monitoring, complex ignore rules, export reports, external runtime dependencies, or other V2 scope.

## Acceptance Criteria

1. **Successful end-to-end registration**  
   Given an existing unregistered folder whose files are readable, when the user runs `folderprint register <folder>`, then FolderPrint loads the catalog, recursively scans the normalized root, persists exactly one registered-folder baseline, reports the registered root and readable file count, writes no error output, and returns `ExitCodes.Success`.

2. **Existing scanner and hasher are reused**  
   Given root and nested files, when registration succeeds, then persisted fingerprints come from the existing `FolderScanner`/`FileHasher` path and retain the scanner-produced relative paths, lowercase SHA-256 hashes, sizes, and UTC last-modified metadata. CLI or registration policy code does not enumerate or hash files independently.

3. **Registered record is complete and stable**  
   Given a successful registration, when the catalog is reloaded through a separate `CatalogStore`, then the new record has a nonempty stable ID, the normalized absolute root path, a UTC `CreatedAtUtc` captured once, `LastVerifiedAtUtc == null`, and the complete readable fingerprint collection. The ID and creation timestamp do not change across later loads.

4. **Windows V1 root identity is deterministic**  
   Given relative, dotted, or trailing-separator spellings of the same local folder, when root identity is established, then FolderPrint uses `Path.GetFullPath`, trims a non-root ending directory separator, and compares registered roots with `StringComparer.OrdinalIgnoreCase`. The normalized absolute root is the value scanned and stored. Symlink, network, and cross-platform equivalence are not inferred.

5. **Duplicate registration is rejected without mutation**  
   Given the normalized root is already present in the loaded catalog, when registration is attempted againâ€”including an equivalent casing or trailing-separator spelling on Windowsâ€”then FolderPrint returns a clear already-registered error and a documented nonzero existing exit code (`ExitCodes.UsageError`), does not scan or save a replacement, and leaves the existing catalog record and bytes unchanged.

6. **Invalid roots fail without catalog changes**  
   Given the supplied path does not exist or resolves to a file rather than a directory, when registration runs, then FolderPrint writes a clear error, returns `ExitCodes.NotFound`, emits no success message, and leaves any existing catalog unchanged.

7. **Unreadable files make registration all-or-nothing**  
   Given scanning returns one or more `UnreadableFiles`, when registration runs, then FolderPrint reports the unreadable relative paths, returns `ExitCodes.ScanError`, does not add a `RegisteredFolder`, and does not call save or create a catalog containing an incomplete baseline.

8. **Other expected scan failures are mapped**  
   Given recursive enumeration or scanning fails with an expected `IOException` or `UnauthorizedAccessException` not represented as a completed snapshot, when registration runs, then FolderPrint returns `ExitCodes.ScanError`, reports a concise scan error, and leaves catalog state unchanged.

9. **Catalog load failures stop before scanning**  
   Given an existing malformed or unreadable catalog, when registration runs, then FolderPrint returns `ExitCodes.CatalogError`, reports the typed load error, does not scan or save, and preserves the original catalog bytes.

10. **Catalog save failures are not reported as success**  
    Given a complete accepted snapshot but the catalog cannot be written or replaced, when save is attempted, then FolderPrint returns `ExitCodes.CatalogError`, emits no registration success message, and relies on the existing safe `CatalogStore.Save` behavior to preserve the last valid catalog.

11. **Empty readable folders are valid baselines**  
    Given an existing unregistered empty folder with no unreadable entries, when registration runs, then it succeeds, persists a registered folder with an empty `Files` collection, and reports a zero-file count. This is the minimal behavior consistent with the existing empty-collection persistence contract; no separate empty-folder rejection policy is introduced.

12. **Architecture and scope remain intact**  
    Given the completed change, reusable registration policy/outcomes live in Core where needed, Core has no dependency on CLI or `System.Console`, CLI owns output and exit-code mapping, no new project/package dependency exists, existing `list` and parser tests still pass, and no verification, duplicate-file detection, refresh, later-story, or V2 behavior is present.

13. **Regression validation passes**  
    Given Story 2.4 is implemented, repository-root restore, build, and the complete test suite pass, including existing parser, list, scanner, hasher, catalog, and model tests.

## Technical Notes

- Target .NET 8/C# and platform libraries only. Do not introduce `System.CommandLine`, a DI container, mocking package, database, or other runtime dependency.
- `CommandParser` already recognizes exactly `register <folder>` and produces `ParsedCommand(CommandKind.Register, FolderPath)`. Preserve it unless a failing test proves a narrow change is required.
- `CliRunner` currently injects `CatalogStore` and writers and only dispatches `list`. Extend this boundary for `Register`; preserve the existing constructor/list behavior. Add only the smallest scanner/registration seam needed for deterministic testsâ€”manual constructor injection is sufficient.
- Keep reusable policy out of console code. A small Core `RegistrationService`/`RegistrationResult` (or equivalent focused design) may own normalized-root duplicate checks and unreadable all-or-nothing acceptance. It must not reference `ExitCodes`, `TextWriter`, or CLI types.
- Recommended sequence:
  1. Normalize the requested root with `Path.GetFullPath` and `Path.TrimEndingDirectorySeparator` while preserving a filesystem root.
  2. Load the catalog and stop on typed failure.
  3. Check existing `RegisteredFolder.RootPath` values using the same normalization and `StringComparer.OrdinalIgnoreCase`; stop on duplicate.
  4. Scan the normalized root with the existing `FolderScanner`.
  5. Stop if `UnreadableFiles` is nonempty.
  6. Generate the ID and UTC creation timestamp once.
  7. Add via `IntegrityCatalog.AddRegisteredFolder`.
  8. Save once via `CatalogStore.Save`.
  9. Print success only after save succeeds.
- Do not rescan or rehash between acceptance and save. Persist the accepted snapshot exactly once.
- A platform `Guid` string is sufficient for the stable ID. The exact display format is not a user contract; tests should assert nonempty/stable identity rather than a literal generated value. A narrow ID factory delegate is acceptable if deterministic tests need it.
- Capture time with `DateTimeOffset.UtcNow` or a tiny injected UTC clock delegate. Tests should assert UTC/range unless a deterministic clock seam is used.
- Exit mapping for this story:
  - success => `Success` (0);
  - already registered => `UsageError` (2), because the valid command request conflicts with registration preconditions and no dedicated conflict code exists;
  - missing or non-directory root => `NotFound` (3);
  - catalog load/save failure => `CatalogError` (4);
  - unreadable snapshot or expected scan I/O/access failure => `ScanError` (5).
- Do not catch arbitrary exceptions and relabel them as expected failures. Preserve the existing unexpected-failure boundary rather than hiding programming defects.
- The scanner catches per-file `IOException`/`UnauthorizedAccessException` and reports paths in `UnreadableFiles`, but recursive enumeration can still throw. Cover both outcome shapes.
- Duplicate registration comparison is path-based only. Do not group or compare file hashes to detect duplicate content.
- Tests must use temporary directories and injected `CatalogStore` paths. Never invoke the default `%AppData%` catalog in automated tests.
- Prefer assertions on typed outcomes, exit codes, meaningful output fragments, reloaded catalog data, record counts, and byte preservation over brittle full-console text.

### Existing Components to Reuse

- `src/FolderPrint.Cli/CommandParser.cs`: already parses `register` and all V1 command shapes.
- `src/FolderPrint.Cli/ParsedCommand.cs` and `CommandKind.cs`: existing request contract.
- `src/FolderPrint.Cli/ExitCodes.cs`: existing exit taxonomy; do not add or renumber codes.
- `src/FolderPrint.Cli/CliRunner.cs`: current dispatch/output boundary to extend while preserving `list`.
- `src/FolderPrint.Core/Scanning/FolderScanner.cs`: root validation, recursive enumeration, fingerprint construction, unreadable-file reporting.
- `src/FolderPrint.Core/Scanning/FileHasher.cs`: only SHA-256 implementation.
- `src/FolderPrint.Core/Catalog/IntegrityCatalog.cs`: immutable add-only baseline creation; copies snapshot files and initializes `LastVerifiedAtUtc` to null.
- `src/FolderPrint.Core/Catalog/CatalogStore.cs`: typed load/save, first-write directory creation, malformed-catalog protection, staged safe replacement.
- `src/FolderPrint.Core/Catalog/CatalogLoadResult.cs` and `CatalogSaveResult.cs`: typed persistence outcomes.
- Existing `RegisteredFolder`, `FolderSnapshot`, and `FileFingerprint` records; do not create parallel DTOs for the same data.

### Likely Files to Update or Add

- Update `src/FolderPrint.Cli/CliRunner.cs` for `Register` dispatch, output, and exit mapping.
- Add a focused Core registration policy/service and typed result under `src/FolderPrint.Core/Registration/` only if needed to keep duplicate/unreadable rules reusable and testable.
- Update `tests/FolderPrint.Tests/Cli/CliRunnerTests.cs` with temporary-folder end-to-end cases.
- Add focused Core registration tests under `tests/FolderPrint.Tests/Registration/` if a Core service/result is introduced.
- Normally leave `CommandParser`, `FolderScanner`, `FileHasher`, `IntegrityCatalog`, and `CatalogStore` behavior unchanged; extend them only when a story AC cannot be met through composition.
- Do not modify Story 3.1 verification files or add `VerificationService` behavior.

### Testing Requirements

- Successful nested-folder registration: run through `CliRunner`, reload the injected catalog, and assert one complete record/fingerprint set plus success output/exit.
- Stable record metadata: nonempty ID, normalized absolute root, UTC creation, null verification time, stable reload.
- Empty-folder success with zero files.
- Missing path and file-as-root: `NotFound`, no success output, catalog absent or byte-for-byte unchanged.
- Duplicate root: exact input and equivalent normalized spelling; one existing record remains unchanged and no replacement occurs.
- Unreadable all-or-nothing: deterministic Core policy test using a constructed snapshot; add a locked-file CLI test only where platform behavior is reliable.
- Malformed catalog: `CatalogError`, unchanged bytes, no scan/save success.
- Save failure: deterministic invalid catalog target seam; `CatalogError`, no success output, previous state preserved where applicable.
- Existing `list`, parser, scanner, catalog, hasher, and model tests remain green.
- No full prose snapshot tests and no real user catalog access.

### Previous Story Intelligence

- Story 2.2 established deterministic ordinal ordering of native root-relative paths and reporting unreadable file paths without fabricated fingerprints. Registration must consume that snapshot rather than reinterpret it.
- Story 2.3 established safe catalog writes through a temporary sibling file and immutable baseline file copies. Code review specifically caught direct-write truncation and mutable-list aliasing; every Story 2.4 failure path must preserve trusted catalog state.
- `CatalogStore.Load()` treats a missing catalog as empty and reports malformed/read failures through `CatalogLoadResult`.
- `CatalogStore.Save()` already validates an existing catalog, creates missing parent directories, writes camelCase JSON, cleans temporary files, and returns `CatalogSaveResult`.
- Existing tests favor temporary filesystem state, injected paths, structural/reloaded-data assertions, and narrow output fragments.

### Git Intelligence

- Baseline commit: `d405b65a4f3030a3131516bfabbcd2571774ed6b` (`Creado SPRINT 003`).
- Recent work completed Story 2.3 review fixes before Sprint 003 planning; preserve those catalog safety invariants.
- The worktree was clean when this story was created.

### References

- Product registration workflow and non-goals: `docs/product-brief.md#The Solution`, `#V1 Scope`, `#Non-Goals`
- Registration/catalog requirements and errors: `docs/prd.md#7.1 Catalog Management`, `#7.2 Folder Registration`, `#8 Command Behavior`, `#11 Error Handling Requirements`, `#13 Edge Cases`
- Register data flow, boundaries, exit codes, and testing: `docs/architecture.md#Main Data Flows`, `#Error Handling Strategy`, `#Exit Code Strategy`, `#Testing Strategy`, `#Architecture Decisions`
- Story source and dependency chain: `docs/epics-and-stories.md#Story 2.4 Wire register folder Command`
- Sprint gate, tasks, and exclusions: `docs/sprint-plan-003.md#Story 2.4 Wire register folder Command`, `#Dependencies`, `#Risks`, `#Definition of Done`
- Prior scanner story: `docs/stories/story-005.md`
- Prior persistence story and resolved review findings: `docs/stories/story-006.md`
- Official .NET path APIs: [Path.GetFullPath](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.getfullpath), [Path.TrimEndingDirectorySeparator](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.trimendingdirectoryseparator), [StringComparer.OrdinalIgnoreCase](https://learn.microsoft.com/en-us/dotnet/api/system.stringcomparer.ordinalignorecase)

## Tasks

- [x] Define a minimal typed Core registration policy/outcome (or equivalent reusable seam) for duplicate-root and unreadable-snapshot rejection, with no CLI dependencies. (AC: 5, 7, 12)
- [x] Normalize the Windows V1 root once and use the same normalized absolute value for catalog identity, duplicate comparison, scanning, and persistence. (AC: 3-6)
- [x] Extend `CliRunner` to dispatch `CommandKind.Register` while preserving parser and `list` behavior. (AC: 1, 12)
- [x] Load the catalog first and map typed load failures to `CatalogError` without scanning or saving. (AC: 9)
- [x] Reject an existing normalized root without modifying the catalog. (AC: 5)
- [x] Scan only through the existing `FolderScanner`/`FileHasher` path and map invalid roots and expected scan failures to the specified exit codes. (AC: 2, 6, 8)
- [x] Reject any snapshot containing unreadable files and report all unreadable paths without adding or saving a baseline. (AC: 7)
- [x] Generate stable identity/UTC creation metadata once, add through `IntegrityCatalog.AddRegisteredFolder`, and save once through `CatalogStore.Save`. (AC: 1, 3, 10, 11)
- [x] Print concise success only after save succeeds; map duplicate, path, scan, catalog, and usage outcomes through existing `ExitCodes`. (AC: 1, 5-10)
- [x] Add focused Core tests for path identity, duplicate registration, unreadable all-or-nothing behavior, and immutable catalog outcomes. (AC: 4, 5, 7, 12)
- [x] Add CLI/integration-style tests for nested and empty registration, persisted metadata/fingerprints, invalid roots, duplicate variants, malformed catalog, save failure, and reliable unreadable-file behavior. (AC: 1-13)
- [x] Run full validation and confirm Story 3.1+, verification, duplicate-file detection, refresh, new dependencies, and V2 scope remain absent. (AC: 12, 13)

## Validation Commands

Run from repository root:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Optional scope check:

```powershell
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'VerificationService|MovedOrRenamed|DuplicateFinder|Refresh\(|SQLite|Sqlite|System.CommandLine|export-report'
```

Any smoke check must use a temporary folder and injected catalog path; do not run `register` against the default real `%AppData%` catalog.

## Definition of Done

- All acceptance criteria pass and every task is checked only after its tests pass.
- `folderprint register <folder>` persists exactly one complete trusted baseline for a valid readable folder and reports its file count.
- The command reuses the existing parser, scanner, hasher, catalog models, and safe persistence path.
- Root identity is normalized and duplicate registrations are rejected without overwrite.
- Invalid roots, unreadable snapshots, scan failures, catalog-load failures, and catalog-save failures return deterministic nonzero outcomes and never create a partial baseline.
- Empty readable folders register as valid zero-file baselines.
- Tests use injected temporary filesystem/catalog state and never touch real `%AppData%`.
- Existing parser, list, scanner, hasher, catalog, and model behavior remains green.
- Core remains independent from CLI/console concerns and has no new project or package dependency.
- No Story 3.1 comparison, verification, moved/renamed logic, duplicate-file detection, refresh, later-story behavior, or V2 scope is implemented.
- `dotnet restore`, `dotnet build`, and `dotnet test` pass from repository root.
- The dev agent updates this story's permitted workflow sections with validation evidence and changed files before moving it to review.

## Dev Agent Record

### Agent Model Used

OpenAI Codex (GPT-5)

### Debug Log References

- Red phase: `dotnet test --no-restore --filter "FullyQualifiedName~RegistrationServiceTests|FullyQualifiedName~CliRunnerTests.Run_Register"` failed because the Core registration types did not yet exist.
- Focused registration/CLI smoke validation: 14 tests passed using temporary folders and injected catalog paths.
- Full validation: `dotnet restore`, `dotnet build --no-restore`, and `dotnet test --no-build --no-restore` passed; 58 tests passed with 0 build warnings and 0 errors.
- Boundary validation: Core has no project references or package dependencies; excluded-scope scan and `git diff --check` passed.

### Completion Notes List

- Added a typed Core registration service/result/status contract for deterministic root normalization, catalog-first sequencing, duplicate rejection, scan acceptance, and safe baseline persistence.
- Reused `FolderScanner`, its existing `FileHasher`, `IntegrityCatalog.AddRegisteredFolder`, and `CatalogStore.Save`; no scanning, hashing, or persistence implementation was duplicated.
- Wired `CommandKind.Register` through `CliRunner` with existing exit codes and success/error output emitted only after the typed outcome is known.
- Added Core and CLI/integration tests for nested and empty folders, stable metadata/fingerprints, invalid roots, duplicate path variants, malformed catalogs, save failures, unreadable files, and post-registration `list` behavior.
- Confirmed Story 3.1 and all excluded verification, duplicate-file detection, refresh, dependency, and V2 scope remain absent.

### File List

- `_bmad-output/implementation-artifacts/2-4-wire-register-folder-command.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/stories/story-007.md`
- `src/FolderPrint.Cli/CliRunner.cs`
- `src/FolderPrint.Core/Registration/RegistrationResult.cs`
- `src/FolderPrint.Core/Registration/RegistrationService.cs`
- `src/FolderPrint.Core/Registration/RegistrationStatus.cs`
- `tests/FolderPrint.Tests/Cli/CliRunnerTests.cs`
- `tests/FolderPrint.Tests/Registration/RegistrationServiceTests.cs`

## Change Log

- 2026-07-13: Created implementation-ready Story 2.4 as Sprint 003 committed work; no implementation performed.
- 2026-07-14: Implemented end-to-end `register <folder>` orchestration, typed Core outcomes, CLI exit/output mapping, and registration regression coverage; moved story to review.