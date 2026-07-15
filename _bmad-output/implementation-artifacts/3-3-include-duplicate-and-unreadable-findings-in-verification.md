---
storyId: "3.3"
storyKey: "3-3-include-duplicate-and-unreadable-findings-in-verification"
title: "Include Duplicate and Unreadable Findings in Verification"
status: ready-for-dev
baseline_commit: 48ecc51dbe865673adec7e346242b61bb78564e7
epic: "Epic 3: Verify Folder Integrity"
created: 2026-07-14
updated: 2026-07-14
sprint: "Sprint 004 gated stretch"
previousStory: "3-2-detect-moved-or-renamed-files.md"
---

# Story 3.3: Include Duplicate and Unreadable Findings in Verification

Status: review

## Story

As a user verifying a registered folder, I want duplicate and unreadable files surfaced in the verification result, so that integrity checks do not hide important current-folder risks.

## Context

Story 3.3 is the optional gated stretch story for Sprint 004. Its entry gate is satisfied: Story 3.2 is done, passed full validation and clean review, and established stable `MovedOrRenamed` and `AmbiguousMovedOrRenamed` behavior. Stories 3.1 and 3.2 already provide pure, deterministic comparison of baseline/current readable fingerprints.

`FolderSnapshot` already separates readable `Files` from `UnreadableFiles`. `VerificationResult` already exposes `DuplicateGroups` and `UnreadableFiles`, and `HasDifferences` already treats either non-empty collection as drift. Story 3.3 must populate those existing result collections without rescanning, rehashing, fabricating fingerprints, or changing the constructor shapes.

Duplicate grouping and move ambiguity are related but different signals. A repeated hash among current readable files produces a duplicate group even if that hash also participates in Story 3.2 ambiguity. Preserve both results; never replace or reinterpret the ambiguity marker.

## Scope

- Extend the existing pure `VerificationService.Compare` result assembly.
- Derive duplicate groups from `current.Files` only, grouping readable fingerprints by ordinal SHA-256.
- Include a group only when at least two current readable files share the hash.
- Include every matching current relative path exactly once, sorted with `StringComparer.Ordinal`.
- Sort duplicate groups deterministically by their first path using `StringComparer.Ordinal`, with a full path-sequence tie-breaker if necessary.
- Copy `current.UnreadableFiles` into `VerificationResult.UnreadableFiles`, sorted ordinally and independently materialized.
- Exclude unreadable paths from changes, hash matching, moved/renamed reconciliation, and duplicate groups because they have no `FileFingerprint` or trustworthy hash.
- Preserve all Story 3.1/3.2 classifications, ambiguity behavior, final change ordering, result metadata, and input immutability.
- Preserve `HasDifferences`: duplicates or unreadables make it true; empty/unchanged-only results remain false.
- Add focused xUnit tests with constructed snapshots; no filesystem setup is required.

## Out of Scope

- Story 3.4 `verify <folder>` command dispatch, catalog lookup, scanning orchestration, report formatting, console output, exit-code mapping, or `lastVerifiedAtUtc` updates.
- Story 5.1 `DuplicateFinder` or Story 5.2 standalone `duplicates <folder>` behavior. Do not add a public duplicate service in this story.
- Adding `Duplicate` or `Unreadable` entries to `VerificationResult.Changes`; use the existing dedicated collections.
- Changing `FileChange`, `FileChangeType`, `FolderSnapshot`, or `VerificationResult` constructor shapes unless compilation proves a narrow compatibility fix is unavoidable.
- Rescanning, rehashing, filesystem access, catalog access/mutation, baseline mutation, registration changes, or refresh behavior.
- Inventing hashes for unreadable files, inferring duplicate membership from path/size/timestamp, or parsing unreadable strings into a new model.
- Deduplicating repeated unreadable strings unless an existing scanner invariant explicitly requires it; preserve every supplied entry while sorting.
- GUI, SQLite, cloud sync, encryption, real-time monitoring, network-share support, complex ignore rules, export reporting, dependencies, or V2 scope.

## Acceptance Criteria

1. Given two or more current readable fingerprints sharing the same SHA-256 hash, comparison returns one duplicate group containing every matching current relative path in ordinal order.
2. Given multiple duplicate hashes, each qualifying hash produces one group and groups have deterministic ordering independent of input order.
3. Given singleton current hashes or no readable current files, no duplicate group is returned.
4. Given unreadable entries in `FolderSnapshot.UnreadableFiles`, comparison returns all entries in `VerificationResult.UnreadableFiles` in ordinal order without fabricating hashes or `FileChange` entries.
5. Unreadable entries never participate in duplicate grouping, basic classification, move matching, or move ambiguity.
6. Given a repeated current hash that also creates Story 3.2 move ambiguity, comparison preserves the existing `Missing`, `New`, and `AmbiguousMovedOrRenamed` findings and also returns the current duplicate group.
7. Existing `Unchanged`, `Modified`, `Missing`, `New`, `MovedOrRenamed`, and `AmbiguousMovedOrRenamed` results and deterministic ordering remain unchanged when duplicate/unreadable collections are added.
8. `HasDifferences` is true for duplicate-only, unreadable-only, or combined findings; it remains false for empty and unchanged-only results with no duplicates/unreadables.
9. Returned duplicate/unreadable collections are independently materialized and comparisons do not mutate caller-owned baseline, current files, or unreadable collections.
10. Core remains independent of CLI and `System.Console`; no project/package dependency, command wiring, catalog mutation, refresh, later-story, or V2 behavior is added.

## Technical Notes

- Target .NET 8/C# and platform libraries only.
- Primary update: `src/FolderPrint.Core/Verification/VerificationService.cs`. Build duplicate/unreadable collections after or alongside current Story 3.2 comparison, then pass them into the existing `VerificationResult` constructor instead of `[], []`.
- Reuse `FileFingerprint.Sha256` and `RelativePath`; hash comparison and path ordering remain `StringComparer.Ordinal`.
- Duplicate grouping is based on current readable fingerprints regardless of whether those files are `Unchanged`, `New`, or involved in ambiguity. Do not group baseline-only hashes.
- Prefer a private helper in `VerificationService`; do not create `DuplicateFinder`, because its reusable standalone behavior belongs to Story 5.1.
- Materialize each duplicate group as a new array/list and materialize the outer collection. Do not expose LINQ iterators or aliases to input collections.
- Materialize unreadables as a new ordinally sorted array. Preserve string content exactly; `FolderSnapshot` currently exposes `IReadOnlyList<string>` rather than a reason-bearing DTO.
- Do not emit `FileChangeType.Duplicate` or `FileChangeType.Unreadable`; the architecture already models these through `DuplicateGroups` and `UnreadableFiles`.
- `VerificationResult.HasDifferences` already checks both collections. Add regression tests rather than rewriting it unless a failing test demonstrates a defect.
- Preserve the existing path-bearing-before-pathless change ordering and `AmbiguousMovedOrRenamed` contract from Story 3.2.

### Expected File Scope

- Update `src/FolderPrint.Core/Verification/VerificationService.cs`.
- Update `tests/FolderPrint.Tests/Verification/VerificationServiceTests.cs`.
- Update `tests/FolderPrint.Tests/Models/DomainModelTests.cs` only if needed to strengthen existing `HasDifferences` coverage.
- Do not modify CLI, scanner, hashing, catalog, registration, persistence, reporting, project, package, or public duplicate-service files.

### Previous Story Intelligence

- Story 3.2 uses same-path classification followed by unmatched hash reconciliation. Do not let duplicate grouping consume or reorder those findings.
- Unique 1:1 cross-side hashes become `MovedOrRenamed`; multi-candidate cross-side hashes retain all `Missing`/`New` findings plus one pathless ambiguity marker.
- Story 3.2 review added missing subfolder, same-path precedence, symmetric one-sided, `HasDifferences`, and result-collection tests. Preserve all 87 passing tests.
- Comparison independently materializes and ordinally orders outputs without mutating inputs.
- Current tests use xUnit, fixed UTC constructed records, `Compare_Condition_ExpectedOutcome` names, and structural assertions.

### References

- `docs/product-brief.md#V1 Scope`, `#Non-Goals`
- `docs/prd.md#FR-13 Classify duplicate files`, `#FR-14 Classify unreadable files`, `#Error Handling Requirements`
- `docs/architecture.md#Domain Models`, `#Verify`, `#Testing Strategy`, `#Architecture Decisions`
- `docs/epics-and-stories.md#Story 3.3 Include Duplicate and Unreadable Findings in Verification`
- `docs/sprint-plan-004.md#Story 3.3 Include Duplicate and Unreadable Findings in Verification Optional Gated Stretch`
- `_bmad-output/implementation-artifacts/3-2-detect-moved-or-renamed-files.md`
- `src/FolderPrint.Core/Verification/VerificationService.cs`
- `src/FolderPrint.Core/Models/VerificationResult.cs`
- `src/FolderPrint.Core/Models/FolderSnapshot.cs`

## Tasks

- [x] Derive independently materialized, deterministic duplicate groups from current readable fingerprints only. (AC: 1-3, 6, 9-10)
- [x] Carry independently materialized, ordinally sorted snapshot unreadable entries into the verification result without hashes or change entries. (AC: 4-5, 9-10)
- [x] Integrate both collections without changing Story 3.1/3.2 findings, ambiguity, metadata, ordering, or input ownership. (AC: 6-10)
- [x] Add focused tests for one/multiple/no duplicate groups, deterministic shuffled inputs, unreadable-only/combined results, ambiguity plus duplicates, exclusion from changes, `HasDifferences`, and immutability. (AC: 1-10)
- [x] Run full validation and confirm no CLI, scanner, catalog, refresh, standalone duplicate service, dependency, later-story, or V2 scope entered. (AC: 7-10)

## Validation Commands

```powershell
dotnet restore
dotnet build
dotnet test
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Optional focused/scope checks:

```powershell
dotnet test --no-restore --filter "FullyQualifiedName~VerificationServiceTests"
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'DuplicateFinder|CommandDispatcher|ReportFormatter|LastVerifiedAtUtc|Refresh\(|SQLite|Sqlite|System.CommandLine|export-report'
Select-String -Path src/FolderPrint.Core/**/*.cs -Pattern 'System\.Console|FolderPrint\.Cli'
git diff -- src/FolderPrint.Cli src/FolderPrint.Core/Catalog src/FolderPrint.Core/Scanning src/FolderPrint.Core/Registration src/FolderPrint.Core/Reporting
```

## Definition of Done

- All acceptance criteria pass and tasks are checked only with validation evidence.
- Duplicate groups contain every current readable path sharing a hash and are deterministic; singleton hashes are absent.
- Unreadable entries are preserved, sorted, independently materialized, and never receive hashes or change entries.
- Story 3.1/3.2 classifications, ambiguity, ordering, metadata, clean semantics, and input immutability remain correct.
- Duplicate/unreadable findings make `HasDifferences` true without new result-model shapes or `FileChange` entries.
- No scan/hash/catalog/console/command/timestamp/refresh/standalone-duplicate/dependency/later-story/V2 behavior is added.
- Focused and full tests, restore, build, dependency checks, and `git diff --check` pass.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- RED: `dotnet test --no-restore --filter "FullyQualifiedName~VerificationServiceTests"` failed 5 of 24 tests because duplicate and unreadable result collections were empty.
- GREEN: focused verification suite passed 25 of 25 tests after the scoped comparison update.
- Full validation: restore, build, 93 tests, dependency/boundary scans, and `git diff --check` passed.

### Completion Notes List

- Derived deterministic duplicate groups from current readable SHA-256 fingerprints only, with ordinal path and group ordering.
- Carried every current unreadable entry into an independently materialized ordinal result collection without creating `FileChange` entries.
- Preserved Story 3.2 move/rename ambiguity behavior and existing change ordering.
- Added regression coverage for one, multiple, and no duplicate groups; duplicate-only and unreadable-only semantics; ambiguity coexistence; deterministic input order; separate findings; and input ownership.

### File List

- `_bmad-output/implementation-artifacts/3-3-include-duplicate-and-unreadable-findings-in-verification.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/stories/story-010.md`
- `src/FolderPrint.Core/Verification/VerificationService.cs`
- `tests/FolderPrint.Tests/Verification/VerificationServiceTests.cs`

## Change Log

- 2026-07-14: Created implementation-ready Story 3.3 as the eligible Sprint 004 gated stretch story; no implementation performed.
- 2026-07-14: Implemented duplicate and unreadable verification findings with deterministic materialization and regression tests; moved story to review.