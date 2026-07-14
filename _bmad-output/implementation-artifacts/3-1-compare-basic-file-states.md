---
storyId: "3.1"
storyKey: "3-1-compare-basic-file-states"
title: "Compare Basic File States"
status: done
epic: "Epic 3: Verify Folder Integrity"
created: 2026-07-14
updated: 2026-07-14
sprint: "Sprint 003 gated stretch"
baseline_commit: "663da58123d9b09ca1530907ab56151fd2c34289"
source:
  - "../../docs/product-brief.md"
  - "../../docs/prd.md"
  - "../../docs/architecture.md"
  - "../../docs/epics-and-stories.md"
  - "../../docs/sprint-plan-003.md"
  - "sprint-status.yaml"
previousStories:
  - "2-2-scan-folders-recursively.md"
  - "2-3-persist-registered-folder-baselines.md"
  - "2-4-wire-register-folder-command.md"
---

# Story 3.1: Compare Basic File States

Status: done

## Story

As a user verifying a registered folder,  
I want FolderPrint to identify unchanged, modified, missing, and new files,  
so that I can understand ordinary folder drift.

## Context

Sprint 003 is active. Its committed Story 2.4 is done and passed code review, so the gate for this optional stretch story is satisfied. Stories 2.2 and 2.3 already provide the stable current-snapshot and persisted-baseline contracts that comparison must consume.

Story 3.1 is the first, deliberately narrow slice of Epic 3. It adds pure comparison behavior inside `FolderPrint.Core`; it does not create the end-to-end `verify` workflow. Baseline-only and current-only files are classified as `Missing` and `New` at this stage even when their hashes match. Story 3.2 may later reconcile eligible pairs into moved/renamed findings.

## Scope

- Add the minimal `VerificationService` comparison operation under `FolderPrint.Core/Verification`.
- Compare the existing `RegisteredFolder.Files` baseline with an existing current `FolderSnapshot.Files` collection.
- Produce typed `FileChange` entries for `Unchanged`, `Modified`, `Missing`, and `New` using the existing `VerificationResult` and `FileChangeType` contracts.
- Treat SHA-256 equality as authoritative for files at the same relative path; size and timestamp differences do not make matching content modified.
- Preserve the existing scanner-produced relative paths and use the established ordinal relative-path convention without normalization, filesystem probing, or platform-policy expansion.
- Return results in deterministic ordinal relative-path order, with a stable tie-breaker if needed.
- Keep `DuplicateGroups` and `UnreadableFiles` empty in this story; Story 3.1 does not translate unreadable scan entries or identify duplicates.
- Ensure `VerificationResult.HasDifferences` is false for an empty or unchanged-only result and true when any `Modified`, `Missing`, or `New` finding exists.
- Add focused xUnit tests using constructed domain records and snapshots; no filesystem or catalog setup is needed for pure comparison tests.
- Preserve all existing scanner, hashing, catalog, registration, CLI, and domain-model behavior.

## Out of Scope

- Story 3.2 moved/renamed detection, hash-based continuity matching, or ambiguity handling.
- Story 3.3 duplicate groups, duplicate detection, or unreadable verification findings.
- Story 3.4 `verify <folder>` command dispatch, catalog lookup, scanning orchestration, report formatting, console output, exit-code mapping, or `lastVerifiedAtUtc` updates.
- Any filesystem scan, re-scan, re-hash, catalog read/write, baseline mutation, registration change, or refresh behavior.
- Changing `FolderScanner`, `FileHasher`, `CatalogStore`, `IntegrityCatalog`, `RegistrationService`, `CliRunner`, command parsing, or JSON schema.
- Removing future `FileChangeType` values merely because Story 3.1 does not implement them.
- Cross-platform path normalization, separator conversion, case-folding policy, network-share behavior, symlink identity, complex ignore rules, or very-large-folder optimization.
- GUI, SQLite or another database, cloud synchronization, encryption, real-time monitoring, export reports, external runtime dependencies, or any other V2 scope.

## Acceptance Criteria

1. **Same path and same hash is unchanged**  
   Given a baseline fingerprint and current fingerprint with the same relative path and SHA-256 hash, when comparison runs, then exactly one `Unchanged` change is returned for that path.

2. **Content hash overrides metadata differences**  
   Given the same relative path and SHA-256 hash but different `Size` and/or `LastModifiedUtc`, when comparison runs, then the file remains `Unchanged`.

3. **Same path and different hash is modified**  
   Given baseline and current fingerprints with the same relative path but different SHA-256 hashes, when comparison runs, then exactly one `Modified` change is returned with both relative-path fields set to that path and the current hash represented in the typed finding.

4. **Baseline-only path is missing at this stage**  
   Given a baseline fingerprint whose relative path is absent from the current readable fingerprints, when comparison runs, then one `Missing` change is returned with the baseline path, a null current path, and the baseline hash.

5. **Current-only path is new at this stage**  
   Given a current fingerprint whose relative path is absent from the baseline, when comparison runs, then one `New` change is returned with a null baseline path, the current path, and the current hash.

6. **Moved/renamed matching is not performed**  
   Given a baseline-only file and current-only file with the same SHA-256 hash at different paths, when comparison runs, then Story 3.1 returns separate `Missing` and `New` changes and does not return `MovedOrRenamed`, ambiguity, or duplicate findings.

7. **Mixed results are complete and deterministic**  
   Given a mixture of unchanged, modified, baseline-only, and current-only files supplied in arbitrary collection order, when comparison runs repeatedly, then every readable input path is classified exactly once and the resulting changes have the same ordinal relative-path ordering every time.

8. **Empty and unchanged-only results are clean**  
   Given empty baseline/current collections or only unchanged files, when comparison runs, then `VerificationResult.HasDifferences` is false. Given any `Modified`, `Missing`, or `New` change, it is true.

9. **Comparison is pure Core behavior**  
   Given comparison inputs already exist in memory, when Story 3.1 runs, then it performs no scan or hash computation, reads or writes no catalog, mutates neither input collection, writes no console output, and does not update registered-folder metadata.

10. **Later findings remain absent**  
    Given `FolderSnapshot.UnreadableFiles` or repeated content hashes are present, when basic comparison runs, then Story 3.1 creates no unreadable or duplicate findings and returns empty `UnreadableFiles` and `DuplicateGroups` result collections.

11. **Architecture and regression boundaries hold**  
    Given the completed change, Core remains independent of CLI and `System.Console`, no project/package dependency is added, the full existing test suite passes, and no Story 3.2-or-later behavior is introduced.

## Technical Notes

- Target .NET 8/C# and platform libraries only. Do not add `System.CommandLine`, a DI container, a mocking package, a database, or another runtime dependency.
- Add `src/FolderPrint.Core/Verification/VerificationService.cs`. The comparison API should consume the existing baseline/current contracts, preferably `Compare(RegisteredFolder baseline, FolderSnapshot current)`, and return the existing `VerificationResult`.
- Use `RegisteredFolder.RootPath` as the result root and a deterministic existing input timestamp such as `current.ScannedAtUtc` as `VerifiedAtUtc`; do not introduce a clock solely for this pure comparison operation.
- Reuse these existing records without parallel DTOs:
  - `FileFingerprint(RelativePath, Sha256, Size, LastModifiedUtc)`
  - `RegisteredFolder(..., Files)`
  - `FolderSnapshot(..., Files, UnreadableFiles)`
  - `FileChange(Type, BaselineRelativePath, CurrentRelativePath, Sha256, Message)`
  - `VerificationResult(RootPath, VerifiedAtUtc, Changes, DuplicateGroups, UnreadableFiles)`
- `FileChangeType` already contains all V1 values. Use only `Unchanged`, `Modified`, `Missing`, and `New`; preserve the later values unchanged.
- Scanner output is currently ordered with `StringComparer.Ordinal` and stores native root-relative paths. Match relative paths with the same ordinal convention for this story. Do not add case folding, separator normalization, or cross-platform policy.
- Compare the stored lowercase SHA-256 strings directly using ordinal equality. Do not use size or timestamp to determine content equality, and do not rehash.
- Produce concise deterministic `Message` values, but keep assertions centered on typed fields rather than full prose.
- `VerificationResult.HasDifferences` currently treats any `Changes` entry as drift. Because Story 3.1 must return `Unchanged` entries, update that computed property narrowly so only non-`Unchanged` changes (plus future duplicate/unreadable collections) count as differences. Preserve existing constructor shape and future enum values.
- Do not mutate, sort in place, or retain mutable aliases to caller-owned lists. Materialize the result collections independently.
- Existing scanner/catalog contracts normally produce unique relative paths. Do not invent duplicate-path conflict policy in this story; keep inputs within established domain invariants and avoid hiding duplicates through an unsafe dictionary overwrite.

### Existing Components to Reuse

- `src/FolderPrint.Core/Models/FileFingerprint.cs`
- `src/FolderPrint.Core/Models/RegisteredFolder.cs`
- `src/FolderPrint.Core/Models/FolderSnapshot.cs`
- `src/FolderPrint.Core/Models/FileChange.cs`
- `src/FolderPrint.Core/Models/FileChangeType.cs`
- `src/FolderPrint.Core/Models/VerificationResult.cs`
- `src/FolderPrint.Core/Scanning/FolderScanner.cs` for its established output contract only; do not call or modify it.

### Expected File Scope

- Add `src/FolderPrint.Core/Verification/VerificationService.cs`.
- Add `tests/FolderPrint.Tests/Verification/VerificationServiceTests.cs`.
- Narrowly update `src/FolderPrint.Core/Models/VerificationResult.cs` and its model tests only as required for unchanged-only `HasDifferences` semantics.
- Do not modify CLI, scanning, hashing, catalog, persistence, registration, reporting, or project dependency files.

### Previous Story Intelligence

- Story 2.2 established deterministic ordinal ordering, native root-relative paths, lowercase SHA-256 fingerprints, and separate unreadable-path reporting. Consume that contract; do not reinterpret or regenerate it.
- Story 2.3 review hardened catalog writes and collection ownership after direct-write corruption and mutable-list aliasing were found. Story 3.1 must remain read-only and return independently materialized result collections.
- Story 2.4 review completed cleanly after catalog-overlap, null-record, access-error, deterministic scan-seam, and file-root findings were resolved. Its registration and CLI boundaries must remain untouched.
- Existing tests use xUnit, `MethodOrScenario_Condition_ExpectedOutcome` naming, and structural typed assertions rather than brittle full-message snapshots.

### Testing Requirements

- Direct unit tests for `Unchanged`, `Modified`, `Missing`, and `New`.
- Same-hash/same-path tests with changed size, changed timestamp, and both changed.
- Mixed snapshot test containing exactly one of each classification.
- Same-hash/different-path test proving separate `Missing` and `New` results and no `MovedOrRenamed` result.
- Arbitrarily ordered inputs test proving deterministic ordinal output.
- Empty/empty and unchanged-only tests proving `HasDifferences == false`.
- Modified-only, missing-only, and new-only tests proving `HasDifferences == true`.
- Snapshot unreadable-path and repeated-hash inputs proving no Story 3.3 findings or groups are produced.
- Input collections remain unchanged after comparison.
- Existing domain-model and full regression tests remain green.

### References

- Product purpose, principles, V1 boundaries, and non-goals: `docs/product-brief.md#The Solution`, `#Product Principles`, `#V1 Scope`, `#Non-Goals`
- Verification and basic classifications: `docs/prd.md#7.3 Verification`, `#8 Command Behavior`, `#10 Non-Functional Requirements`, `#13 Edge Cases`
- Core verification contracts and boundaries: `docs/architecture.md#Architecture Overview`, `#Component Responsibilities`, `#Domain Models`, `#Main Data Flows`, `#Testing Strategy`, `#Architecture Decisions`
- Epic sequencing and Story 3.1 foundation: `docs/epics-and-stories.md#Epic 3 Verify Folder Integrity`, `#Story 3.1 Compare Basic File States`
- Sprint gate, Story 3.1 scope, risks, and validation: `docs/sprint-plan-003.md#Story 3.1 Compare Basic File States Optional Gated Stretch`, `#Dependencies`, `#Risks`, `#Definition of Done`
- Established scanner output behavior: `src/FolderPrint.Core/Scanning/FolderScanner.cs`
- Completed gate story and review evidence: `_bmad-output/implementation-artifacts/2-4-wire-register-folder-command.md`

## Tasks

- [x] Add the minimal Core `VerificationService` comparison operation using existing `RegisteredFolder`, `FolderSnapshot`, and verification result models. (AC: 1-11)
- [x] Index or compare baseline and current readable fingerprints by ordinal relative path without scanning, hashing, catalog access, or input mutation. (AC: 1-7, 9)
- [x] Create typed `Unchanged` and `Modified` findings for shared paths using SHA-256 as the sole content-equality authority. (AC: 1-3)
- [x] Create typed `Missing` and `New` findings for unmatched paths without attempting moved/renamed reconciliation. (AC: 4-6)
- [x] Materialize all changes in deterministic ordinal relative-path order with stable typed path/hash fields. (AC: 3-7, 9)
- [x] Return empty duplicate/unreadable collections and keep later-story classifications absent. (AC: 6, 10-11)
- [x] Narrowly correct `VerificationResult.HasDifferences` so unchanged-only changes are clean while all non-unchanged/future findings still count. (AC: 8, 11)
- [x] Add focused xUnit coverage for every classification, metadata-insensitive hash equality, mixed and empty inputs, ordering, clean/drift semantics, later-scope exclusion, and input immutability. (AC: 1-11)
- [x] Run full validation and confirm no CLI, catalog, scanner, registration, moved/renamed, duplicate, unreadable-finding, refresh, dependency, or V2 scope entered the implementation. (AC: 9-11)

## Validation Commands

Run from the repository root after implementation:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Optional scope checks:

```powershell
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'MovedOrRenamed|DuplicateFinder|Unreadable|CliRunner|Refresh\(|SQLite|Sqlite|System.CommandLine|export-report'
git diff -- src/FolderPrint.Cli src/FolderPrint.Core/Catalog src/FolderPrint.Core/Scanning src/FolderPrint.Core/Registration
```

## Definition of Done

- All acceptance criteria pass and every task is checked only after its validation evidence exists.
- `VerificationService` classifies same-path fingerprints as `Unchanged` or `Modified` from SHA-256 equality and classifies unmatched paths as `Missing` or `New` for this basic stage.
- Same-hash files with changed size/timestamp remain `Unchanged`.
- Results are deterministic, typed, independently materialized, and complete for every readable input path.
- Empty and unchanged-only comparisons report no differences; modified, missing, or new findings report differences.
- Core performs no filesystem scan, hash computation, catalog mutation, console output, command wiring, or metadata update.
- No moved/renamed or ambiguity handling, duplicate detection/groups, unreadable verification findings, refresh behavior, later-story work, or V2 scope is implemented.
- Existing domain model shapes, future enum values, scanner/hash/catalog/registration behavior, CLI behavior, and JSON schema remain compatible.
- No external runtime or project dependency is added; Core remains independent of CLI and `System.Console`.
- Focused Story 3.1 tests and the complete existing test suite pass.
- Repository-root restore, build, test, dependency-boundary checks, and `git diff --check` pass.
- The dev agent updates only the permitted workflow sections with validation evidence and changed files before moving the story to review.

## Dev Agent Record

### Agent Model Used

OpenAI Codex (GPT-5)

### Implementation Plan

- Add focused failing xUnit tests for the four basic classifications, hash-authoritative metadata behavior, deterministic ordering, clean/drift semantics, scope exclusions, and input immutability.
- Implement a pure Core `VerificationService` using an ordinal merge over independently sorted copies of the baseline and current fingerprint collections.
- Narrow `VerificationResult.HasDifferences` so `Unchanged` entries remain clean while all non-unchanged and future finding collections remain differences.
- Run focused and full regression validation plus dependency and excluded-scope checks.

### Debug Log References

- Red phase: `dotnet test --no-restore --filter "FullyQualifiedName~VerificationServiceTests"` failed with CS0234 because `FolderPrint.Core.Verification` did not exist.
- Focused green phase: comparison and domain-model tests passed (20 tests).
- Full validation: restore, build, and all 79 tests passed; build completed with 0 warnings and 0 errors.
- Boundary validation: Core has no project references or package dependencies; excluded CLI/catalog/scanner/registration diff was empty; `git diff --check` passed.

### Completion Notes List

- Added a pure Core comparison service that classifies every readable path as `Unchanged`, `Modified`, `Missing`, or `New` using ordinal path and SHA-256 equality.
- Used an independently sorted two-pointer merge for deterministic output without mutating or aliasing caller-owned collections.
- Kept same-hash files at different paths as separate `Missing` and `New` findings; no moved/renamed, duplicate, or unreadable verification behavior was added.
- Returned empty duplicate and unreadable result collections and left all CLI, scanner, catalog, registration, refresh, and later-story boundaries unchanged.
- Corrected `HasDifferences` so unchanged-only results are clean while modified/missing/new and future duplicate/unreadable collections remain differences.
- Added focused coverage for all acceptance criteria and confirmed the complete 79-test suite passes.

### File List

- `_bmad-output/implementation-artifacts/3-1-compare-basic-file-states.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/stories/story-008.md`
- `src/FolderPrint.Core/Models/VerificationResult.cs`
- `src/FolderPrint.Core/Verification/VerificationService.cs`
- `tests/FolderPrint.Tests/Models/DomainModelTests.cs`
- `tests/FolderPrint.Tests/Verification/VerificationServiceTests.cs`

## Change Log

- 2026-07-14: Created implementation-ready Story 3.1 as Sprint 003 gated stretch work after Story 2.4 completed and passed code review; no implementation performed.
- 2026-07-14: Implemented deterministic basic file-state comparison and focused regression coverage; 79 tests pass; story moved to review.
- 2026-07-14: Code review approved with no actionable findings; all 79 tests pass; story marked done.
