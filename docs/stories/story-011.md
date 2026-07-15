---
storyId: "3.4"
storyKey: "3-4-wire-verify-folder-command-and-reporting"
title: "Wire verify <folder> Command and Reporting"
status: ready-for-dev
baseline_commit: 1302a7285a97fcbeddc0a237c34db26f37d5fe6a
epic: "Epic 3: Verify Folder Integrity"
created: 2026-07-15
updated: 2026-07-15
sprint: "Sprint 005 committed"
previousStory: "3-3-include-duplicate-and-unreadable-findings-in-verification.md"
---

# Story 3.4: Wire `verify <folder>` Command and Reporting

Status: ready-for-dev

## Story

As a CLI user, I want to run `folderprint verify <folder>` and receive a clear integrity summary, so that I can decide whether a folder still matches its trusted baseline.

## Context

Story 3.4 is the committed outcome for Sprint 005 and the final story in Epic 3. Stories 3.1–3.3 are done and reviewed: `VerificationService.Compare` already returns pure, deterministic typed results for `Unchanged`, `Modified`, `Missing`, `New`, `MovedOrRenamed`, and `AmbiguousMovedOrRenamed`, plus separate duplicate-group and unreadable collections. This story must compose that engine with catalog lookup, one current scan, deterministic report transformation, console output, V1 exit-code mapping, and safe verification timestamp persistence.

The parser already accepts exactly `verify <folder>`, `CommandKind.Verify` already exists, and all required numeric exit codes already exist. Extend the current seams; do not replace the parser, comparison engine, scanner, catalog store, or exit-code taxonomy.

The Story 3.4 timestamp rule resolves the older PRD/architecture open question: every reliable completed verification updates `lastVerifiedAtUtc` to the `VerificationResult.VerifiedAtUtc` value, whether the result is clean or contains differences. Duplicate-only and unreadable-only results are completed verifications and therefore update the timestamp. Invalid input, catalog failure, unregistered lookup, whole-scan failure, comparison failure, and catalog-save failure must not leave a new timestamp persisted.

## Scope

- Dispatch the existing parsed `verify <folder>` command from `CliRunner`.
- Normalize the requested path using the established V1 registration identity rule.
- Load the JSON catalog and find exactly one registered baseline for the normalized folder.
- Reject an unregistered folder before scanning or mutating catalog state.
- Scan the current folder exactly once with the existing `FolderScanner`/`FileHasher` path.
- Compare the stored `RegisteredFolder` with the current `FolderSnapshot` through the existing `VerificationService.Compare` method.
- Produce deterministic display-ready reporting without writing to `Console` from Core.
- Print clean status, summary counts, and present findings for unchanged, modified, missing, new, moved/renamed, ambiguous moved/renamed, duplicate groups, and unreadable files.
- Update only the matched registration's `LastVerifiedAtUtc` after a reliable result, preserving its `Id`, `RootPath`, `CreatedAtUtc`, and baseline `Files`.
- Save the timestamp update atomically through the existing `CatalogStore` behavior before claiming command success.
- Return the established V1 exit code for clean, differences, not found, catalog failure, scan failure, usage failure, or unexpected failure.
- Add focused Core and CLI tests using temporary catalog/folder paths and injectable seams.

## Out of Scope

- `refresh <folder>` or any replacement, acceptance, or mutation of baseline fingerprints.
- `unregister <folder>` or catalog-entry removal.
- Standalone `duplicates <folder>` dispatch or output.
- `DuplicateFinder`, a duplicate service, or moving existing verification duplicate grouping out of `VerificationService`.
- Story 4.1 registered-folder metadata expansion; preserve existing list behavior.
- Changing parser grammar, command names, or existing numeric exit codes.
- Reworking Story 3.1–3.3 comparison, move ambiguity, duplicate qualification, unreadable propagation, or result ordering unless a failing regression proves a narrow compatibility defect.
- Adding `Duplicate` or `Unreadable` changes to `VerificationResult.Changes`; use the existing dedicated collections.
- GUI, database/SQLite, cloud sync, encryption, real-time monitoring, network-share guarantees, export reporting, advanced ignore rules, shell completion, progress UI, or any V2 feature.
- New runtime packages, `System.CommandLine`, or a Core dependency on CLI or `System.Console`.

## Acceptance Criteria

1. Given a registered unchanged folder, when the user runs `folderprint verify <folder>`, FolderPrint loads the baseline, scans once, compares through the existing verification engine, prints a deterministic clean report, persists the result timestamp, and returns `ExitCodes.Success` (`0`).
2. Given a registered folder with any non-unchanged change, duplicate group, or unreadable finding, verification prints every typed finding without conflating categories, persists the result timestamp, and returns `ExitCodes.DifferencesFound` (`1`). Duplicate-only and unreadable-only results must follow this rule.
3. Reporting includes deterministic summary counts and supports these distinct categories: `Unchanged`, `Modified`, `Missing`, `New`, `MovedOrRenamed`, `AmbiguousMovedOrRenamed`, duplicate groups, and unreadable findings.
4. A moved/renamed finding reports both baseline and current relative paths. A pathless ambiguity reports its SHA-256/message without assuming either path is non-null. Duplicate groups preserve their nested boundaries and returned paths; unreadables remain separate findings.
5. Report section order, finding order, duplicate-group order, paths within groups, and unreadable order are explicit and ordinally deterministic regardless of source enumeration order. Formatting must not mutate or reclassify the typed result.
6. Given a syntactically valid path that has no registered baseline, verification returns `ExitCodes.NotFound` (`3`), writes a clear error, and performs no scan, comparison, catalog save, or timestamp mutation.
7. Requested and stored roots use the existing V1 identity rule: `Path.GetFullPath`, trimmed ending separator, then `StringComparer.OrdinalIgnoreCase`. Null/invalid catalog entries or multiple normalized matches produce `CatalogError` rather than an arbitrary match or crash.
8. After registered-baseline lookup succeeds, CLI orchestration validates the root before scanning. A missing root, file root, or malformed filesystem path returns `NotFound` (`3`); parser argument-shape errors alone remain `UsageError` (`2`). Traversal-wide I/O/access failures after a valid directory is established return `ScanError` (`5`). None updates the catalog.
9. Missing catalog is treated as an empty catalog and therefore produces NotFound. Malformed/unreadable catalog load and catalog timestamp-save failures return `CatalogError` (`4`) without reporting verification success or overwriting an invalid prior catalog.
10. After a reliable clean or differences-found result, the matched record's `LastVerifiedAtUtc` equals `VerificationResult.VerifiedAtUtc` (the current snapshot scan time). `Id`, normalized/stored `RootPath`, `CreatedAtUtc`, baseline `Files`, other registered records, and catalog order remain unchanged. This update is not refresh.
11. Unexpected failures are caught at the CLI boundary, produce concise deterministic error output, and return `UnexpectedError` (`10`) without exposing Core to exit-code or console concepts.
12. `FolderPrint.Core` remains independent of `FolderPrint.Cli`, `ExitCodes`, and `System.Console`; report transformation is testable separately from `TextWriter`/console output; no new runtime package is introduced.
13. Existing `register`, `list`, parser, catalog persistence, scanner, and Story 3.1–3.3 verification tests remain passing. Tests never touch the real `%AppData%\FolderPrint\catalog.json`.
14. No refresh, unregister, standalone duplicates command, `DuplicateFinder`, later-story, or V2 behavior is implemented.

## Reporting Contract

The formatter may return a string, lines, or typed display sections, but the contract must be console-free and deterministic. Pin the following semantics without overfitting tests to incidental whitespace:

- Identify the verified root and whether the result is clean or differences were found.
- Include stable counts for every supported category, including zero counts where needed to make the summary unambiguous. Change-category counts are the number of matching `FileChange` entries; duplicate reporting includes the group count (and may separately include displayed-path count); unreadable count includes every returned entry, including repetitions.
- Use one fixed category order: `Unchanged`, `Modified`, `Missing`, `New`, `MovedOrRenamed`, `AmbiguousMovedOrRenamed`, duplicate groups, unreadable findings.
- For ordinary changes, use the applicable relative path and the typed `FileChange.Message` where useful.
- For `MovedOrRenamed`, show baseline path followed by current path (for example, `old -> new`).
- For `AmbiguousMovedOrRenamed`, render the hash and typed message; both path properties are intentionally null.
- Preserve duplicate groups as separate ordered groups. Do not flatten them, infer a hash not present in the result, or require two distinct displayed paths: Story 3.3 qualifies a group by fingerprint count before distinct-path presentation.
- Render every unreadable string in returned order, including repeated strings. The current model contains paths only, so do not invent reasons or hashes.
- Use `VerificationResult.HasDifferences`, not formatted text or change counts alone, to choose clean versus differences-found behavior.
- Write successful verification reports to the normal output writer. Expected failures go to the error writer and must not be accompanied by a misleading success/differences report.

## Tasks / Subtasks

- [ ] Add only the reusable Core support needed by CLI orchestration while preserving pure comparison. (AC: 7, 9-10, 12)
  - [ ] Reuse `RegistrationService.NormalizeRootPath` or extract one shared Core path-identity helper that preserves registration behavior exactly; do not create a second normalization rule.
  - [ ] Add narrow catalog validation/lookup support if useful, but leave load → lookup → root validation → scan → compare → save → report sequencing at the CLI boundary.
  - [ ] Treat null registered entries, null `Files`, null fingerprints, or missing/invalid required registration/fingerprint identity fields as `CatalogError` rather than allowing a null-reference/unexpected failure.
  - [ ] Keep Core support free of CLI exit codes, writers, and command-dispatch concepts.
- [ ] Add immutable catalog timestamp update behavior. (AC: 1-2, 9-10)
  - [ ] Replace only the matched `RegisteredFolder` with a copy whose `LastVerifiedAtUtc` is `VerificationResult.VerifiedAtUtc`.
  - [ ] Preserve registration identity, trusted files, other records, and collection order with independently materialized collections where appropriate.
  - [ ] Save after comparison and before reporting success; map a failed save to a typed catalog failure.
  - [ ] Prove clean, ordinary-difference, duplicate-only, and unreadable-only results update the timestamp, while every pre-result/save failure leaves the prior persisted catalog intact.
- [ ] Add deterministic Core report transformation. (AC: 2-5, 12)
  - [ ] Create the architecture-anticipated `src/FolderPrint.Core/Reporting/ReportFormatter.cs` or an equivalently explicit console-free reporting type.
  - [ ] Format all change categories, pathless ambiguity, nested duplicate groups, and repeated unreadable strings according to the Reporting Contract.
  - [ ] Preserve or defensively impose ordinal ordering without changing typed semantics or modifying input collections.
  - [ ] Add focused formatter tests for clean, mixed categories, shuffled inputs, null-path ambiguity, nested group boundaries, single-distinct-path duplicate groups, and repeated unreadables.
- [ ] Wire verification into the existing CLI adapter. (AC: 1-3, 6, 8-13)
  - [ ] Add `CommandKind.Verify` dispatch to `CliRunner`; do not change the already-correct parser grammar.
  - [ ] In CLI orchestration, sequence normalized request → catalog load/validation → exactly-one baseline lookup → root validation → one scan → existing `VerificationService.Compare` → timestamp save → formatting/output.
  - [ ] Add the narrow constructor/service/delegate injection needed for deterministic tests while preserving the current default `Program` composition and existing constructor callers.
  - [ ] Map malformed filesystem paths, missing roots, and file roots to `NotFound`; map only parser argument-shape failures to `UsageError`; map traversal-wide I/O/access failures after directory validation to `ScanError`.
  - [ ] Map a clean typed result to `Success`, any `HasDifferences` result to `DifferencesFound`, missing registration/root to `NotFound`, catalog failure to `CatalogError`, and whole-scan failure to `ScanError`.
  - [ ] Add a concise top-level unexpected-error boundary that returns `UnexpectedError` without swallowing expected typed outcomes.
  - [ ] Write report lines only after the timestamp save succeeds; write expected failures only to the error writer.
- [ ] Add CLI/integration and regression coverage. (AC: 1-14)
  - [ ] Cover unchanged, modified, missing, new, moved/renamed, ambiguity, duplicate-only, unreadable-only, and mixed reporting with exact exit-code assertions.
  - [ ] Cover trailing separators and path-case identity on Windows, missing registration before scan, missing root, file root, malformed catalog, whole-scan failure, and timestamp-save failure.
  - [ ] Cover timestamp preservation/update fields for clean and differences results and no mutation on all failure branches.
  - [ ] Assert meaningful labels, counts, paths, section/group ordering, and output/error separation without brittle whole-output prose snapshots.
  - [ ] Preserve existing register/list behavior and all Story 3.1–3.3 result tests.
- [ ] Run full validation and scope checks before marking tasks complete. (AC: 12-14)

## Dev Notes

### Current Implementation Seams to Reuse

- `src/FolderPrint.Cli/CommandParser.cs` already recognizes `verify` with exactly one folder argument. No parser implementation is required.
- `src/FolderPrint.Cli/CommandKind.cs` already defines `Verify`.
- `src/FolderPrint.Cli/ExitCodes.cs` already defines `Success = 0`, `DifferencesFound = 1`, `UsageError = 2`, `NotFound = 3`, `CatalogError = 4`, `ScanError = 5`, and `UnexpectedError = 10`. Do not renumber them.
- `src/FolderPrint.Cli/CliRunner.cs` currently dispatches only list/register and is the CLI integration point. Its constructor injects `CatalogStore` and writers but hard-wires registration/scanning; preserve existing call sites while adding a narrow verify test seam.
- `src/FolderPrint.Core/Registration/RegistrationService.cs` owns the current public V1 normalization rule: `NormalizeRootPath` calls `Path.GetFullPath` and trims the ending separator; catalog identity uses `StringComparer.OrdinalIgnoreCase`. Reuse or safely extract this behavior instead of creating a verification-only rule.
- `src/FolderPrint.Core/Catalog/CatalogStore.cs` already provides typed load/save results, treats a missing file as an empty catalog, validates existing catalog bytes before saving, and replaces through a temporary file. Do not add a second persistence path.
- `src/FolderPrint.Core/Catalog/IntegrityCatalog.cs` currently supports immutable addition only. Add a narrow immutable verification-timestamp operation or equivalent; never replace `Files`.
- `src/FolderPrint.Core/Scanning/FolderScanner.cs` sorts readable/unreadable paths and converts per-file I/O/access failures to unreadables. CLI orchestration must distinguish its missing/file-root failures (`NotFound`) from traversal-wide I/O/access failures (`ScanError`).
- `src/FolderPrint.Core/Verification/VerificationService.cs` is complete comparison behavior. Its output uses snapshot `ScannedAtUtc` as `VerifiedAtUtc`; use that exact value for catalog timestamp persistence.
- `src/FolderPrint.Core/Models/VerificationResult.cs` already includes `HasDifferences` across non-unchanged changes, duplicate groups, and unreadables.
- No reporting folder or `ReportFormatter` exists yet even though the architecture defines it. Add it in Core without `Console` or CLI references.

### Result-Shape Guardrails from Story 3.3

- `Changes` may contain path-bearing `Unchanged`, `Modified`, `Missing`, `New`, and `MovedOrRenamed` entries plus pathless `AmbiguousMovedOrRenamed` entries.
- Duplicate groups and unreadables are separate from `Changes`; never search for `FileChangeType.Duplicate` or `FileChangeType.Unreadable` to report them.
- Duplicate and unreadable collections already make `HasDifferences` true.
- Move ambiguity and current duplicate groups may coexist for the same hash and must both be printed.
- Unreadables are ordinally sorted, retain repeated strings, and have no trustworthy hash or reason-bearing DTO.
- Story 3.3 review commit `1c66e4d` qualifies duplicate groups by fingerprint count before distinct path output. A valid group can therefore display one unique path; do not reject or suppress it.
- The same review added full ordinal path-sequence tie-breaking and nested determinism assertions. Preserve group boundaries rather than flattening output for convenience.

### Error and State Rules

- Lookup happens before scan. A missing registration must not touch the filesystem target beyond path normalization or update catalog state.
- Treat null registered entries, invalid stored roots, or multiple matches as catalog corruption/error. Do not silently skip or pick one.
- A snapshot containing unreadable paths is still a reliable completed verification result: compare it, report differences, persist its verification time, and return `DifferencesFound`.
- A malformed, missing, or file root is `NotFound`; parser shape errors alone are `UsageError`. A traversal-wide I/O/access exception after validating a directory is `ScanError`. None persists a timestamp.
- Save failure after comparison overrides the prospective `Success`/`DifferencesFound` outcome. Do not print a successful verification report when required timestamp persistence failed.
- Timestamp update must leave baseline fingerprints byte-for-byte/equality-equivalent. Replacing them would implement refresh and violate scope.
- Use a generic stable message for unexpected errors if raw exception text would make output environment-dependent.

### Architecture Compliance

- Target .NET 8 and C# with platform libraries only.
- Dependency direction remains `FolderPrint.Cli -> FolderPrint.Core`; Core must not reference CLI, `ExitCodes`, or `System.Console`.
- Core provides scanning, comparison, catalog types/store, immutable catalog operations, typed results, and report transformation. CLI owns command sequencing/orchestration, parsing, final writer/console calls, and exit-code mapping.
- Preserve the manual parser; do not add `System.CommandLine`.
- JSON remains camelCase through existing `System.Text.Json` options; timestamps remain UTC ISO-8601.
- No external/latest-package research is needed for this story because the architecture pins .NET 8 platform APIs and forbids new runtime dependencies.

### Expected File Scope

- Update `src/FolderPrint.Cli/CliRunner.cs`.
- Add only narrowly reusable Core helpers under the matching role folder if needed; do not add an end-to-end verification command orchestrator to Core or overload the pure comparer with command, console, or exit concerns.
- Update `src/FolderPrint.Core/Catalog/IntegrityCatalog.cs` for immutable timestamp replacement if that is the chosen seam.
- Add `src/FolderPrint.Core/Reporting/ReportFormatter.cs` and any minimal display-data type under `Reporting/` if useful.
- Add/update focused tests under `tests/FolderPrint.Tests/Cli/`, `Catalog/`, `Verification/`, and a new `Reporting/` folder as dictated by the implementation.
- `CommandParser.cs`, `CommandKind.cs`, `ExitCodes.cs`, `VerificationService.cs`, and domain constructor shapes should not require behavior changes. Any modification needs a failing test and a narrow compatibility rationale.
- Do not add or modify production files for refresh, unregister, standalone duplicates, `DuplicateFinder`, database, cloud, GUI, encryption, monitoring, export, or V2 behavior.

### Testing Requirements

- Continue xUnit and `MethodOrScenario_Condition_ExpectedOutcome` naming.
- Use fixed UTC `DateTimeOffset` values and injected scan delegates/snapshots for deterministic timestamp assertions.
- Use temporary roots/catalog paths for integration tests; never use the default AppData catalog.
- Prefer structural assertions for report sections and nested duplicate groups; avoid a single brittle full-console snapshot.
- Pin all exit codes exactly, including duplicate-only/unreadable-only `DifferencesFound` and persistence-failure `CatalogError`.
- Prove scan delegate call count is zero for not registered and one for successful/differences verification.
- Prove timestamp updates preserve identity/baseline/other records and do not occur on load, lookup, scan, compare, or save failures.
- Existing validation baseline is 95 passing tests. The final total must be at least 95 plus new Story 3.4 coverage, with no regressions.

### Project Structure Notes

- The architecture names `CommandDispatcher` and `ConsoleOutput`, but the brownfield implementation currently centralizes both roles in `CliRunner`. Extend the existing seam unless a small extraction is directly justified by Story 3.4 tests; do not perform an unrelated CLI refactor.
- The architecture anticipates `ReportFormatter`, but the folder/type does not exist. Story 3.4 should add this missing architecture component rather than embedding all reusable report shaping in `CliRunner`.
- `FileChangeType` contains `Duplicate` and `Unreadable` enum values, but reviewed Story 3.3 uses dedicated result collections. Do not reinterpret the unused enum values as permission to duplicate findings.

### Previous Story Intelligence

- Story 3.3 finalized deterministic, independently materialized duplicate and unreadable result collections while preserving Story 3.2 ambiguity.
- Review corrections in commit `1c66e4d` strengthened duplicate qualification, full group ordering, nested structural testing, and status synchronization.
- Recent implementations keep Core behavior in role-specific folders and tests in matching folders; no new runtime dependencies were added.
- Story transitions have kept implementation artifact metadata and `sprint-status.yaml` synchronized. Preserve that pattern.

### Git Intelligence Summary

- `48ecc51` created Story 3.3 and moved tracking to ready-for-dev.
- `b96e237` implemented Story 3.3 and moved it to review.
- `1c66e4d` applied review corrections, synchronized metadata, and marked Story 3.3 done.
- `c1639b9` completed the Sprint 004 retrospective.
- `1302a72` added the finalized Sprint 005 plan and is this story's implementation baseline.

### References

- [Source: docs/epics-and-stories.md#Story-34-Wire-verify-folder-Command-and-Reporting]
- [Source: docs/sprint-plan-005.md#Story-34-Wire-verify-folder-Command-and-Reporting]
- [Source: docs/sprint-plan-005.md#Definition-of-Done]
- [Source: docs/architecture.md#Verify]
- [Source: docs/architecture.md#Error-Handling-Strategy]
- [Source: docs/architecture.md#Exit-Code-Strategy]
- [Source: docs/architecture.md#Architecture-Decisions]
- [Source: docs/prd.md#FR-7-Verify-a-registered-folder]
- [Source: docs/prd.md#FR-20-Produce-automation-friendly-exit-codes]
- [Source: _bmad-output/implementation-artifacts/3-3-include-duplicate-and-unreadable-findings-in-verification.md]
- [Source: src/FolderPrint.Cli/CliRunner.cs]
- [Source: src/FolderPrint.Core/Registration/RegistrationService.cs]
- [Source: src/FolderPrint.Core/Catalog/CatalogStore.cs]
- [Source: src/FolderPrint.Core/Catalog/IntegrityCatalog.cs]
- [Source: src/FolderPrint.Core/Scanning/FolderScanner.cs]
- [Source: src/FolderPrint.Core/Verification/VerificationService.cs]
- [Source: src/FolderPrint.Core/Models/VerificationResult.cs]

## Validation Commands

```powershell
dotnet restore
dotnet build
dotnet test
dotnet test --no-restore --filter "FullyQualifiedName~CliRunnerTests|FullyQualifiedName~Verification|FullyQualifiedName~ReportFormatter|FullyQualifiedName~IntegrityCatalogTests"
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Scope and boundary inspection:

```powershell
Select-String -Path src/FolderPrint.Core/**/*.cs -Pattern 'System\.Console|FolderPrint\.Cli|ExitCodes'
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'Refresh\(|Unregister\(|DuplicateFinder|SQLite|Sqlite|System\.CommandLine|export-report|cloud|encryption|monitoring'
git diff -- src/FolderPrint.Cli src/FolderPrint.Core tests/FolderPrint.Tests
```

The second scan is an inspection aid because existing parser symbols/tests may mention excluded V1 command names. Review every changed/new match; Story 3.4 must not implement them.

## Definition of Done

- All acceptance criteria pass and tasks are checked only with validation evidence.
- `verify <folder>` works end to end for clean and every existing typed difference category with deterministic, useful reporting.
- Exit mapping is driven by typed outcomes and `VerificationResult.HasDifferences`, not formatted text.
- Catalog lookup uses the established V1 folder identity rule and occurs before scanning.
- A reliable clean or differences result persists its exact verification timestamp without changing identity or baseline files; all failure paths preserve prior catalog state.
- Reporting preserves pathless ambiguity, nested duplicate groups (including a single distinct path), repeated unreadables, and deterministic category/path ordering.
- `FolderPrint.Core` remains independent of CLI, `System.Console`, and exit codes; no runtime package is added.
- Existing register/list/parser/catalog/scanner and Story 3.1–3.3 behavior remains passing.
- Tests use injected/temporary state and never touch real AppData.
- Restore, build, focused/full tests, dependency checks, boundary scans, and `git diff --check` pass.
- No refresh, unregister, standalone duplicates command, `DuplicateFinder`, later-story, or V2 scope is introduced.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- No implementation performed; story is ready for `bmad-dev-story`.

### File List

- `_bmad-output/implementation-artifacts/3-4-wire-verify-folder-command-and-reporting.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/stories/story-011.md`

## Change Log

- 2026-07-15: Created implementation-ready Story 3.4 as the committed Sprint 005 story; no production code implemented.
