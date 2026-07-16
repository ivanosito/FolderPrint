---
storyId: '5.1'
storyKey: '5-1-implement-duplicatefinder-for-current-snapshots'
title: 'Implement DuplicateFinder for Current Snapshots'
status: review
baseline_commit: 63ad449606908d103fbb7d2c74b2f3aff0bac85a
epic: 'Epic 5: Find Duplicate Files On Demand'
created: 2026-07-16
updated: 2026-07-16
sprint: 'Sprint 008 committed'
source:
  - '../../docs/epics-and-stories.md'
  - '../../docs/sprint-plan-008.md'
  - '../../docs/architecture.md'
  - '../planning-artifacts/prds/prd-FolderPrint-2026-07-07/prd.md'
  - 'sprint-status.yaml'
  - 'epic-4-retro-2026-07-16.md'
  - '../../docs/retrospectives/sprint-007-retrospective.md'
---

# Story 5.1: Implement DuplicateFinder for Current Snapshots

Status: review

## Story

As a user checking a folder, I want duplicate readable files grouped by matching content hash, so that I can identify redundant files independent of baseline verification.

## Context

Story 5.1 is Sprint 008's committed outcome and starts Epic 5. Story 2.2 already produces `FolderSnapshot` values with readable `FileFingerprint` records in `Files` and unhashed paths in `UnreadableFiles`.

Story 3.3 already implemented deterministic duplicate grouping as a private `VerificationService` helper. Extract that behavior into the architecture-defined `DuplicateFinder` and make verification delegate to the shared service. Do not create two grouping implementations.

This story is Core-only. It does not scan, hash, access the catalog, format output, map exits, or wire `duplicates <folder>`. Story 5.2 remains backlog until Story 5.1 is reviewed and done.

## Scope

- Add a pure `DuplicateFinder` under `FolderPrint.Core.Verification`.
- Analyze an existing current snapshot and group readable fingerprints by SHA-256.
- Preserve verification membership and ordering semantics.
- Make `VerificationService` delegate without observable behavior changes.
- Add focused in-memory xUnit tests and retain the full regression suite.

## Out of Scope

- The `duplicates <folder>` CLI command, dispatch, formatting, or CLI tests.
- Scanner, hasher, filesystem, catalog, persistence, registration lookup, writers, environment, or clocks inside `DuplicateFinder`.
- Catalog schema/path/validation/load/save/locking/conflict changes.
- Behavioral changes to register, verify, list, unregister, or refresh beyond internal verification delegation.
- New report prose, exits, parser behavior, model validation, rich unreadable reasons, packages, or project references.
- GUI, database/SQLite, cloud, encryption, realtime monitoring, background agents, network guarantees, export, optimization, ignore rules, or V2.

## Acceptance Criteria

1. Add public sealed `DuplicateFinder` at `src/FolderPrint.Core/Verification/DuplicateFinder.cs` with `Find(FolderSnapshot snapshot)` returning `IReadOnlyList<IReadOnlyList<string>>`. Null input throws `ArgumentNullException`; no new result model is needed.
2. A `Sha256` represented by at least two readable fingerprint entries yields one group containing all distinct matching `RelativePath` values. Hash equality is `StringComparer.Ordinal`; size and timestamps do not affect membership.
3. Hashes represented by fewer than two entries yield no group. Empty and all-singleton snapshots return an empty materialized result.
4. `UnreadableFiles` never participates, even when its path text matches a readable path. The finder does not return unreadables, fabricate hashes, or inspect content.
5. Paths are distinct and sorted with `StringComparer.Ordinal`. Group equality and order never depend on culture, OS, or source enumeration.
6. Sort groups lexicographically by each complete ordinal-sorted path sequence; if one sequence is an exact prefix, the shorter sequence sorts first. Results are invariant to input order.
7. Preserve the existing compatibility edge: qualify on raw fingerprint-entry count before distinct path projection. Two same-hash/same-path entries qualify and return that path once. Scanner snapshots normally have one fingerprint per readable file; this prevents an incidental verify change.
8. Fully materialize outer and inner results. Clearing or mutating caller-owned `Files` or `UnreadableFiles` after `Find` does not change results, and `Find` does not mutate inputs.
9. `VerificationService.Compare` uses the shared finder while preserving its signature, classifications, ordering, duplicate groups, separately sorted unreadables, metadata, and `HasDifferences`. Existing verification tests pass unchanged.
10. Core stays independent of CLI, console, writers, exits, scanning, hashing, filesystem, catalog, global state, and new packages. All quality checks pass.

## Deterministic Grouping Contract

For `snapshot.Files` only:

1. `GroupBy(file => file.Sha256, StringComparer.Ordinal)`.
2. Record raw fingerprint count.
3. Project paths, `Distinct(StringComparer.Ordinal)`, then ordinal-sort.
4. Retain raw counts of two or more.
5. Sort retained groups by complete ordinal path sequence, then sequence length.
6. Return owned inner arrays inside an owned outer array.

Do not order by hash, current culture, filesystem order, or LINQ source order. Do not normalize hashes or paths.

## Tasks / Subtasks

- [x] Add pure Core `DuplicateFinder`. (AC: 1-8, 10)
  - [x] Create `Verification/DuplicateFinder.cs` with `Find(FolderSnapshot)`.
  - [x] Move the existing grouping and path-sequence comparison from `VerificationService`.
  - [x] Keep it stateless and free of I/O, scanning, hashing, catalog, CLI, writer, clock, and environment seams.
- [x] Make verification consume one implementation. (AC: 7, 9)
  - [x] Delegate current-snapshot grouping to `DuplicateFinder`.
  - [x] Remove the moved private helper/comparer from `VerificationService`.
  - [x] Preserve parameterless use and the public `Compare` signature; add no breaking constructor.
- [x] Add `DuplicateFinderTests`. (AC: 1-8)
  - [x] Cover null, empty, singleton, two-file, 3+ file, and multiple groups.
  - [x] Cover permutations, ordinal case/path order, nested paths, same-first-path and prefix tie-breaks.
  - [x] Cover metadata differences, unreadable-only, mixed input, and matching readable/unreadable path text.
  - [x] Pin repeated same-hash/same-path qualification and distinct projection.
  - [x] Prove defensive materialization and input-order preservation.
- [x] Preserve regressions and validate. (AC: 9-10)
  - [x] Keep verification tests for duplicate/unreadable separation, group ordering, repeated paths, ambiguity, materialization, and `HasDifferences`.
  - [x] Add only a narrow delegation regression if existing coverage is insufficient.
  - [x] Run full checks and inspect the diff for excluded files/features.

## Dev Notes

### Existing Code to Reuse

- `VerificationService.CreateDuplicateGroups` already implements the required algorithm. Move it; do not copy or redesign it.
- Move `OrdinalPathSequenceComparer` and `ComparePathSequences` with the algorithm.
- `FolderSnapshot.Files` is the readable candidate set; `UnreadableFiles` has no hashes.
- Reuse `VerificationResult.DuplicateGroups` type: `IReadOnlyList<IReadOnlyList<string>>`.
- `VerificationResult.HasDifferences` already treats any duplicate group as a difference.

### Expected File Scope

- **NEW** `src/FolderPrint.Core/Verification/DuplicateFinder.cs`.
- **UPDATE** `src/FolderPrint.Core/Verification/VerificationService.cs`: change only grouping delegation and remove moved helper code.
- **NEW** `tests/FolderPrint.Tests/Verification/DuplicateFinderTests.cs`.
- **UPDATE only if needed** `VerificationServiceTests.cs` for a narrow delegation regression.

No model, scanner, hasher, CLI, reporting, catalog, registration, project, or package file should change.

### Architecture Compliance

- Use repository-pinned .NET 8/C#, nullable, implicit usings, platform libraries, and xUnit 2.5.3.
- Preserve `FolderPrint.Cli -> FolderPrint.Core`; Core cannot reference CLI or `System.Console`.
- Core owns duplicate rules; Story 5.2 will own CLI orchestration/output.
- Use explicit `StringComparer.Ordinal` for culture-independent programmatic hash/path behavior.
- Avoid speculative streaming, parallelism, caching, validation, or large-scale optimization.
- Official .NET documentation confirms ordinal comparison is case-sensitive and culture-independent and `GroupBy` accepts an explicit comparer. No upgrade is authorized.

### Testing Requirements

- Construct snapshots/fingerprints in memory; use no temporary folders or mocks.
- Assert nested boundaries and exact order, not flattened membership only.
- Shuffle inputs and use case/culture-distinct paths.
- Mutate caller lists after `Find` to prove detached nested results.
- Add no filesystem, catalog, CLI output, or exit-code tests.
- Baseline at creation: 213 Release tests passed after Story 4.3.

### Retrospective and Git Intelligence

Epic 4 review requires direct proof of determinism and absence of side effects. Here that means input-preservation/materialization tests and zero scanner, hasher, catalog, CLI, or filesystem dependency.

Baseline commit: `63ad449606908d103fbb7d2c74b2f3aff0bac85a` (`Creado SPRINT 008`). Recent commits after Story 4.3 are planning/retrospective changes. Hashing and filesystem failures remain scanner/orchestration concerns.

### Project Structure Notes

- Architecture reserves `Verification/DuplicateFinder.cs`; do not place it in CLI, Scanning, Catalog, Registration, or Reporting.
- Tests belong in `tests/FolderPrint.Tests/Verification`.
- Keep `docs/stories/story-015.md` synchronized with the BMAD artifact.
- No `project-context.md` or UX document exists; this CLI-only story uses the listed sources.

### References

- [Source: docs/epics-and-stories.md#Story-51-Implement-DuplicateFinder-for-Current-Snapshots]
- [Source: docs/sprint-plan-008.md#Determinism-and-Compatibility-Contract]
- [Source: docs/architecture.md#Duplicates]
- [Source: docs/architecture.md#Component-Responsibilities]
- [Source: docs/architecture.md#AD-1-Layered-CLI-Core-boundary-ADOPTED]
- [Source: _bmad-output/planning-artifacts/prds/prd-FolderPrint-2026-07-07/prd.md#FR-13-Classify-duplicate-files]
- [Source: _bmad-output/implementation-artifacts/epic-4-retro-2026-07-16.md#Recommendation-for-Epic-5]
- [Source: docs/retrospectives/sprint-007-retrospective.md#Recommended-Next-Story]
- [Source: src/FolderPrint.Core/Verification/VerificationService.cs]
- [Source: src/FolderPrint.Core/Models/FolderSnapshot.cs]
- [Source: src/FolderPrint.Core/Models/FileFingerprint.cs]
- [Source: tests/FolderPrint.Tests/Verification/VerificationServiceTests.cs]
- [Source: Microsoft Learn - StringComparer.Ordinal](https://learn.microsoft.com/en-us/dotnet/api/system.stringcomparer.ordinal)
- [Source: Microsoft Learn - Enumerable.GroupBy](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.groupby)

## Validation Commands

```powershell
dotnet restore
dotnet build FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release --no-build --filter 'FullyQualifiedName~DuplicateFinder|FullyQualifiedName~VerificationService'
dotnet format FolderPrint.sln --verify-no-changes --no-restore
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Inspect the changed finder for CLI, console, scanner, hasher, catalog, filesystem, and environment references, and inspect the final diff for excluded scope.

## Definition of Done

- Every AC passes and tasks are checked only with evidence.
- One shared pure Core implementation owns current-snapshot duplicate grouping.
- Membership, entry-count qualification, distinct paths, ordinal ordering, unreadable exclusion, input independence, and materialization have focused tests.
- Verification and all existing commands remain unchanged and the full regression passes.
- No CLI wiring, scan/hash operation, catalog change, model expansion, package, or excluded feature is introduced.
- Full/focused Release tests, build, formatting, dependency, boundary, scope, and diff checks pass.
- Move to review only after validation; Story 5.2 stays backlog until Story 5.1 is reviewed and done.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Implementation Plan

- Add focused in-memory tests first and confirm the missing-service failure.
- Move the existing verification grouping algorithm into a stateless `DuplicateFinder`.
- Delegate verification to the shared service, then run focused and full validation.

### Debug Log References

- Red: focused build failed with CS0246 because `DuplicateFinder` did not exist.
- Green: 35 focused `DuplicateFinder` and `VerificationService` tests passed.
- Validation: Release build passed with zero warnings/errors; all 221 tests and formatting checks passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Implemented a pure snapshot-to-groups Core `DuplicateFinder` using ordinal hash equality, distinct ordinal paths, lexicographic path-sequence ordering, and defensive materialization.
- Moved duplicate grouping out of `VerificationService` and delegated to the shared service without changing verification results or public APIs.
- Added eight focused in-memory tests covering null, empty/singleton, multi-group, ordinal/prefix order, metadata independence, unreadable exclusion, repeated-path compatibility, and input/result materialization.
- Verified Core remains free of CLI, scanning, hashing, filesystem, catalog, and runtime package dependencies; no CLI command was wired.

### File List

- _bmad-output/implementation-artifacts/5-1-implement-duplicatefinder-for-current-snapshots.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- docs/stories/story-015.md
- src/FolderPrint.Core/Verification/DuplicateFinder.cs
- src/FolderPrint.Core/Verification/VerificationService.cs
- tests/FolderPrint.Tests/Verification/DuplicateFinderTests.cs

## Change Log

- 2026-07-16: Implemented deterministic current-snapshot duplicate grouping, verification delegation, and focused Core tests; moved story to review.
