---
storyId: '5.2'
storyKey: '5-2-wire-duplicates-folder-command'
title: 'Wire duplicates <folder> Command'
status: done
baseline_commit: 4ee8ea76db649a9185d733aab838213f4a8b2589
epic: 'Epic 5: Find Duplicate Files On Demand'
created: 2026-07-16
updated: 2026-07-16
sprint: 'Sprint 008 gated stretch'
previousStory: '5-1-implement-duplicatefinder-for-current-snapshots.md'
source:
  - '../../docs/epics-and-stories.md'
  - '../../docs/sprint-plan-008.md'
  - '../../docs/architecture.md'
  - '../planning-artifacts/prds/prd-FolderPrint-2026-07-07/prd.md'
  - 'sprint-status.yaml'
  - '5-1-implement-duplicatefinder-for-current-snapshots.md'
  - '../../docs/stories/story-015.md'
  - '../../docs/retrospectives/sprint-007-retrospective.md'
---

# Story 5.2: Wire `duplicates <folder>` Command

Status: done

## Story

As a CLI user, I want to run duplicate detection for any existing folder, so that I can find duplicate files without registering the folder first.

## Gate Decision

**GO - 2026-07-16.** Story 5.1 is `done` after adversarial review with every finding resolved. Its artifact records 36 focused and 222 full Release tests plus clean build, formatting, dependency, boundary, excluded-scope, and diff checks. `DuplicateFinder` is the reviewed pure, deterministic grouping authority and verification remains compatible. The user explicitly initiated this gated-stretch create-story workflow after the Sprint 008 status check, satisfying the remaining-capacity decision.

The Epic 4 catalog-independence action stays open until Story 5.2 implementation and review provide direct proof; creating this artifact does not close it.

## Context

The parser and command model already recognize exactly `duplicates <folder>`. The current defect is dispatch: `CliRunner.Run` has no `CommandKind.Duplicates` arm, so a valid parsed command falls through and returns silent success without validation, scanning, detection, or reporting.

Story 5.1 added the only duplicate-grouping implementation: `DuplicateFinder.Find(FolderSnapshot)`. It groups readable fingerprints by ordinal SHA-256 equality, excludes unreadables, retains only qualifying groups, sorts paths and groups by the complete ordinal path-sequence contract, and materializes its result. This story orchestrates that service; it must not reproduce or alter those rules.

The intended flow is `CLI validation -> FolderScanner -> FolderSnapshot -> DuplicateFinder -> ReportFormatter -> CLI writers/exit`. Catalog independence means the command invokes no catalog API and makes no registration decision. If a catalog file is physically located inside the chosen target, the unchanged scanner treats it like any other target file; Story 5.2 adds no exclusion or ignore rule and still must not mutate it.

## Scope

- Dispatch the already-parsed command and validate one folder path with established V1 behavior.
- Scan the validated folder exactly once with the existing scanner.
- Fail closed on scan exceptions or any unreadable path.
- Pass the current snapshot to the existing finder exactly once.
- Format deterministic duplicate/no-duplicate output and map established exits.
- Add focused CLI/reporting tests, real-scanner temp-folder coverage, catalog-independence proof, target-safety proof, and full regressions.

## Out of Scope

- Catalog load, validation, creation, repair, lookup, save, mutation, schema, path, locking, conflict, or writer-protocol changes.
- Registration, baseline lookup/comparison, verify, refresh, unregister, or list behavior changes.
- Any change to `DuplicateFinder`, `VerificationService`, duplicate membership, qualification, distinct-path projection, or group/path ordering.
- Scanning/hashing inside `DuplicateFinder`, a second scan, rehashing, or direct file-content inspection in CLI.
- Creating, deleting, moving, renaming, or writing target files/directories.
- Parser, enum, exit-value, model, project-reference, package, or framework changes unless a compatibility test proves one unavoidable.
- Hash display, rich unreadable reasons, progress, ignore rules, separator/symlink policy, parallelism, caching, optimization, GUI, database/SQLite, cloud, encryption, realtime monitoring, background agents, export, network guarantees, or V2.

## Acceptance Criteria

1. `CliRunner.Run([duplicates, folder])` dispatches explicitly. The manual parser, `CommandKind.Duplicates`, exact-one-folder rule, usage output, and `ExitCodes` values remain unchanged.
2. Normalize with existing `RegistrationService.NormalizeRootPath` behavior, used only as a path utility. Invalid syntax (`ArgumentException`, `NotSupportedException`, `PathTooLongException`), a missing path, or a file-valued path produces deterministic stderr, empty stdout, no scan/finder/catalog operation, and `NotFound` (3).
3. A valid directory is scanned exactly once through the existing scanner. Pass that complete current snapshot exactly once to the reviewed finder. CLI does not scan, hash, group, distinct, filter, sort, or mutate fingerprints.
4. For duplicate readable files, stdout is exactly the formatter lines below, stderr is empty, and exit is `Success` (0), not `DifferencesFound`:
   - `Duplicates: <normalized-root-path>`
   - `Duplicate groups: <count>`
   - `[Duplicate Groups]`
   - `Group 1:`, then each path as `  <relative-path>`; number later groups consecutively.
   Hashes are not printed because the finder returns path groups, not a hash/group DTO.
5. Reporting preserves the complete group and path order supplied by `DuplicateFinder`, including nested boundaries, ordinal case distinctions, common prefixes, and 3+ path groups. The formatter returns an owned line array, does not mutate/regroup inputs, and performs no I/O.
6. A valid empty folder or all-singleton snapshot writes exactly `Duplicates: <normalized-root-path>{NL}No duplicates found.{NL}`, writes no stderr, and returns `Success` (0).
7. Any unreadable path makes completion unreliable. Before finder invocation or success stdout, write `Folder duplicate scan failed because one or more files could not be read.` followed by ordinally sorted `Unreadable: <relative-path>` lines to stderr and return `ScanError` (5). Emit no partial groups or fabricated fingerprint.
8. Scanner `IOException`, `UnauthorizedAccessException`, or `CryptographicException` writes exactly `Folder scan failed.{NL}` to stderr, writes no stdout, exposes no exception detail, and returns `ScanError` (5). Reuse root reclassification for `FileNotFoundException`/`DirectoryNotFoundException`: a missing root maps to 3; child/traversal failure while the root remains a directory maps to 5. Other unexpected scanner, finder, or formatter failures occur before the first stdout write and produce only `Unexpected error.` with `UnexpectedError` (10). A writer failure after output begins is sanitized by the existing catch-all but cannot guarantee atomic rollback of text already accepted by the writer.
9. The duplicates path never calls catalog load/save/validation/lookup/mutation. It succeeds with no catalog and malformed/inaccessible catalog state. Tests prove missing-catalog non-creation, malformed/inaccessible-catalog irrelevance, byte-for-byte preservation of one existing sentinel on success and one representative failure, and a source/diff boundary with no catalog calls.
10. Registered and unregistered folders behave identically. The command does not read or change timestamps, identities, baselines, ordering, or any persisted field.
11. The command is read-only toward the target. Controlled tests prove no create/delete/move/rename/write and preserve content, existence, relative tree, last-write timestamps, and attributes. Do not assert last-access time or promise protection from external actors.
12. Existing commands and parser/exit/finder/verification tests remain passing. Core stays independent of CLI, console, writers, and exits; no package/project reference is added; all quality checks pass.

## Tasks / Subtasks

- [x] Wire duplicate orchestration in `CliRunner`. (AC: 1-3, 7-10, 12)
  - [x] Add the missing `CommandKind.Duplicates` switch arm.
  - [x] Append optional duplicate-scan and duplicate-find seams after every existing constructor parameter so callers remain compatible.
  - [x] Default them to the existing `FolderScanner.Scan` and `new DuplicateFinder().Find` methods.
  - [x] Add `RunDuplicates`; reuse or narrowly generalize root validation/failure helpers without changing verify.
  - [x] Scan once, reject unreadables before finder, find once, materialize formatter lines, then write.
- [x] Add deterministic duplicate reporting. (AC: 4-6, 12)
  - [x] Add a focused `ReportFormatter` method accepting normalized root and typed groups.
  - [x] Emit the pinned shapes and preserve supplied nested group/path order.
  - [x] Keep it pure and writer-free; do not fabricate a `VerificationResult` or new model.
- [x] Add Story 5.2 tests. (AC: 1-12)
  - [x] Cover real-scanner duplicates, 3+ files, multiple/nested groups, none, and empty folder.
  - [x] Cover malformed/missing/file roots, root disappearance, traversal/IO/access/hash failures, unreadable-only, and mixed snapshots.
  - [x] Assert rejected targets call neither seam; success scans/finds once; unreadables never call finder; no failure emits partial stdout.
  - [x] Prove missing/malformed/inaccessible catalog independence, sentinel-byte preservation on success and one representative failure, and identical behavior for a registered folder.
  - [x] Prove target-tree read-only behavior and add formatter order/materialization tests.
- [x] Preserve regressions and validate. (AC: 12)
  - [x] Run focused duplicate CLI/reporting tests and existing parser, scanner, finder, verification, and command tests.
  - [x] Run full Release build/tests, formatting, dependency, boundary, excluded-scope, catalog/target safety, and diff checks.
  - [x] Keep catalog, registration, finder, verification, parser, enum, exit, model, project, and package files unchanged unless a documented compatibility failure requires otherwise.

### Review Findings

- [x] [Review][Patch] Extend target-safety proof to directory state on success and a representative failure [tests/FolderPrint.Tests/Cli/CliDuplicatesTests.cs:16]
- [x] [Review][Patch] Add an unreadable-only snapshot case in addition to the existing mixed snapshot [tests/FolderPrint.Tests/Cli/CliDuplicatesTests.cs:146]
- [x] [Review][Patch] Cover unexpected scanner and formatter-materialization failures, not only finder failure [tests/FolderPrint.Tests/Cli/CliDuplicatesTests.cs:225]
- [x] [Review][Patch] Cover FileNotFoundException reclassification while the root remains and after it disappears [tests/FolderPrint.Tests/Cli/CliDuplicatesTests.cs:181]

## Dev Notes

### Existing Code to Reuse

- `CommandParser.FolderCommands` and `CommandParserTests` already cover `duplicates`. Do not introduce `System.CommandLine`.
- `CliRunner.Run` currently omits duplicate dispatch; its default returns the successful parse exit, causing the silent no-op.
- Reuse `RegistrationService.NormalizeRootPath`, `ValidateVerificationRoot`, `ClassifyScanFailure`, and `WriteRootNotFound` behavior. Generalize private names only if necessary and preserve verify tests.
- `FolderScanner.Scan` recursively hashes readable files and returns ordinal-sorted fingerprints/unreadables. Still ordinal-sort injected unreadables before diagnostics.
- `DuplicateFinder.Find` is the sole membership/order authority. Never add `GroupBy`, `Distinct`, hash ordering, filesystem ordering, or culture-sensitive ordering to CLI/reporting.
- Follow `ReportFormatter.FormatVerification` conventions: owned line arrays, `[Duplicate Groups]`, `Group N:`, and two-space indentation. Add a dedicated formatter rather than a fake verification result.
- Finding duplicates is a successful query; return 0, unlike verification drift.

### Current Files and Required Changes

- **UPDATE** `src/FolderPrint.Cli/CliRunner.cs`: append duplicate seams, dispatch, orchestration, and narrowly shared validation/failure handling. Preserve constructor compatibility and every existing command.
- **UPDATE** `src/FolderPrint.Core/Reporting/ReportFormatter.cs`: add only pure duplicate/no-duplicate line formatting; preserve existing APIs/output and Core independence.
- **NEW** `tests/FolderPrint.Tests/Cli/CliDuplicatesTests.cs`: injected orchestration/failure checks plus real scanner/hash integration.
- **UPDATE** `tests/FolderPrint.Tests/Reporting/ReportFormatterTests.cs`: focused duplicate formatting/order/materialization coverage.

Expected unchanged: parser, command enum/model, exits, scanner, hasher, finder, verification, domain models, catalog/registration services, and all project/package files.

### Error and Output Contract

| Outcome | Stdout | Stderr | Exit |
| --- | --- | --- | --- |
| Duplicates | Group report | Empty | `Success` 0 |
| None/empty | Explicit none report | Empty | `Success` 0 |
| Invalid/missing/file root | Empty | Root error | `NotFound` 3 |
| Unreadables | Empty | Header + sorted paths | `ScanError` 5 |
| Scan failure | Empty | `Folder scan failed.` | `ScanError` 5, or 3 if root disappeared |
| Unexpected | Empty | `Unexpected error.` | `UnexpectedError` 10 |

Do not expose unexpected exception details. Validation, scan, finder, and formatter materialization complete before the first stdout write. Existing per-line writer behavior is retained; text already accepted by a failing writer cannot be rolled back.

### Architecture Compliance

- Use repository-pinned .NET 8/C#, nullable, implicit usings, platform libraries, and xUnit 2.5.3.
- Preserve `FolderPrint.Cli -> FolderPrint.Core`; Core cannot reference CLI, `ExitCodes`, writers, or `System.Console`.
- Core owns traversal, hashing, duplicate rules, and pure display-line formatting; CLI owns dispatch, writers, and exits.
- No runtime dependency, CLI framework, model expansion, catalog behavior, or architecture update is authorized.
- Official .NET documentation confirms recursive enumeration can surface directory, I/O, access, and path failures and that platform I/O failures may use different `IOException` subtypes. Classify broadly by the established V1 contract, never exception text.

### Testing Requirements

- Use an isolated temp root/catalog plus `StringWriter` streams. Append constructor seams; do not reorder the existing seven parameters.
- Real scans create same-content files for equal hashes and distinct content for singletons. Use `Path.Combine` for nested expectations.
- Inject snapshots/exceptions for unreadable, traversal, access, crypto, disappearing-root, and unexpected-finder paths; a Windows lock test is supplemental only.
- Assert representative exact catalog bytes and target-tree state, not only catalog counts. A physical catalog file under the target is scanned normally and remains unchanged. Exclude last-access time.
- Keep console assertions focused on the pinned contract. Baseline: Story 5.1 review records 222 full Release and 36 focused tests passing.

### Previous Story and Git Intelligence

- Story 5.1 review resolved one-materialized-file-set, nested-path, and unreadable-only findings. Consume the reviewed service without reopening semantics.
- Finder compatibility includes raw-entry qualification before distinct paths, ordinal comparison, complete path-sequence order, and owned arrays.
- Sprint 007/Epic 4 lessons require fail-closed incomplete scans, cryptographic scan-error mapping, direct side-effect proofs, and deterministic output.
- Baseline commit: `4ee8ea76db649a9185d733aab838213f4a8b2589` (`Done Story 5.1`). Recent work used narrow scope, focused/full evidence, separate review, and synchronized story/tracking artifacts.

### Project Structure Notes

- Actual dispatch/output live in `CliRunner.cs`; do not create the architecture's conceptual `CommandDispatcher`/`ConsoleOutput` files for this last V1 command.
- Tests belong under `tests/FolderPrint.Tests/Cli` and `Reporting`.
- Keep `docs/stories/story-016.md` synchronized. No project-context or UX file exists; deterministic CLI behavior is the UX contract.

### References

- [Source: docs/epics-and-stories.md#Story-52-Wire-duplicates-folder-Command]
- [Source: docs/sprint-plan-008.md#Story-52-GoNo-Go-Gate]
- [Source: docs/sprint-plan-008.md#Story-52-Wire-duplicates-folder-Command-Gated-Stretch]
- [Source: docs/architecture.md#Duplicates]
- [Source: docs/architecture.md#Error-Handling-Strategy]
- [Source: docs/architecture.md#AD-1-Layered-CLI-Core-boundary-ADOPTED]
- [Source: docs/architecture.md#AD-6-Typed-results-before-console-formatting-ADOPTED]
- [Source: _bmad-output/planning-artifacts/prds/prd-FolderPrint-2026-07-07/prd.md#FR-17-Report-duplicates-for-a-folder]
- [Source: _bmad-output/implementation-artifacts/5-1-implement-duplicatefinder-for-current-snapshots.md]
- [Source: docs/retrospectives/sprint-007-retrospective.md#Recommendation-for-Sprint-008]
- [Source: src/FolderPrint.Cli/CliRunner.cs]
- [Source: src/FolderPrint.Core/Scanning/FolderScanner.cs]
- [Source: src/FolderPrint.Core/Verification/DuplicateFinder.cs]
- [Source: src/FolderPrint.Core/Reporting/ReportFormatter.cs]
- [Source: tests/FolderPrint.Tests/Cli/CliVerifyTests.cs]
- [Source: tests/FolderPrint.Tests/Cli/CliRefreshTests.cs]
- [Source: Microsoft Learn - Directory.EnumerateFiles](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratefiles)
- [Source: Microsoft Learn - Handling I/O errors in .NET](https://learn.microsoft.com/en-us/dotnet/standard/io/handling-io-errors)

## Validation Commands

```powershell
dotnet restore
dotnet build FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release --no-build --filter 'FullyQualifiedName~CliDuplicates|FullyQualifiedName~ReportFormatter|FullyQualifiedName~DuplicateFinder'
dotnet format FolderPrint.sln --verify-no-changes --no-restore
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Also inspect the diff for Core references to `System.Console|FolderPrint.Cli|ExitCodes|TextWriter`, excluded features, catalog operations in duplicate dispatch, grouping outside the finder, target writes, parser/exit/package changes, and unintended command behavior.

## Definition of Done

- Every AC has automated evidence and tasks are checked only with evidence.
- The command validates, scans once, rejects unreliable snapshots, finds once, and emits deterministic materialized output.
- Success/failure mappings are pinned and no failure emits partial output.
- Catalog absence/byte preservation and target safety are directly proven.
- No duplicate algorithm, verification, scanner, hasher, catalog, registration, parser, enum, exit, model, project, package, or excluded-feature change is introduced.
- Full/focused Release tests, build, formatting, dependency, boundary, scope, catalog/target-safety, and diff checks pass.
- Move to review only after validation. After separate adversarial review resolves all findings, synchronize both story copies, mark Story 5.2 and Epic 5 done, and close the remaining Epic 4 action item.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Implementation Plan

- Add failing CLI and reporting tests for dispatch, deterministic output, reliability failures, catalog independence, and target safety.
- Append test seams and implement one scan-to-finder CLI path without catalog operations.
- Add pure materialized duplicate formatting, then run focused and full validation.

### Debug Log References

- Red: focused build failed with CS1739 for missing duplicate seams and CS0117 for missing `FormatDuplicates`.
- Green: 20 focused CLI/reporting tests passed after minimal production implementation.
- Final review validation: 33 focused tests and all 243 Release tests passed; Release build had zero warnings/errors.
- Formatting, Core reference/package, boundary, excluded-scope, catalog-safety, target-safety, and diff checks passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Sprint 008 Story 5.2 gate recorded as GO after reviewed Story 5.1 completion and explicit stretch-capacity authorization.
- Wired `duplicates <folder>` to validate and scan one current folder, fail closed on unreliable scans, call the existing `DuplicateFinder`, and return established V1 exits.
- Added pure deterministic duplicate/no-duplicate formatting without changing grouping semantics or Core/CLI dependency direction.
- Added real scanner/finder integration and focused tests for multiple/3+ groups, empty/singleton folders, invalid targets, unreadables, scan/crypto failures, unexpected failures, catalog independence, and target safety.
- Preserved parser, exit-code, scanner, finder, verification, catalog, registration, model, project, and package behavior.
- Adversarial review resolved all four actionable findings with test-only patches; Story 5.2, Epic 5, and the remaining Epic 4 catalog-independence action are complete.

### File List

- _bmad-output/implementation-artifacts/5-2-wire-duplicates-folder-command.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- docs/stories/story-016.md
- src/FolderPrint.Cli/CliRunner.cs
- src/FolderPrint.Core/Reporting/ReportFormatter.cs
- tests/FolderPrint.Tests/Cli/CliDuplicatesTests.cs
- tests/FolderPrint.Tests/Reporting/ReportFormatterTests.cs

## Change Log

- 2026-07-16: Created implementation-ready Story 5.2 artifact and opened the gated stretch story for development.
- 2026-07-16: Implemented deterministic `duplicates <folder>` CLI orchestration, reporting, error mapping, and automated coverage; moved story to review.
- 2026-07-16: Completed adversarial review, applied four test-evidence patches, passed 33 focused and 243 full Release tests, and marked the story done.
