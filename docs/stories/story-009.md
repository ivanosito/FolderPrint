---
storyId: "3.2"
storyKey: "3-2-detect-moved-or-renamed-files"
title: "Detect Moved or Renamed Files"
status: done
baseline_commit: 32e93630b9d8a862dd114bdeb628ac2957ac032a
epic: "Epic 3: Verify Folder Integrity"
created: 2026-07-14
updated: 2026-07-14
sprint: "Sprint 004 committed"
---

# Story 3.2: Detect Moved or Renamed Files

Status: done

## Story

As a user verifying a registered folder, I want unchanged content at a different path reported as moved or renamed, so I do not mistake it for separate missing and new files.

## Context

Story 3.1's reviewed pure `VerificationService.Compare` classifies same-path files as `Unchanged`/`Modified` and unmatched paths as `Missing`/`New`. Story 3.2 reconciles only unmatched readable fingerprints.

The model lacks typed ambiguity. Add only `FileChangeType.AmbiguousMovedOrRenamed`: one pathless marker per ambiguous hash while retaining affected `Missing`/`New` findings. This reports uncertainty without inventing paths or implementing Story 3.3 duplicate groups.

## Scope

- Extend the existing service; group unmatched candidates by ordinal SHA-256.
- Convert a 1:1 cross-side group to one `MovedOrRenamed` with both paths, suppressing its `Missing`/`New` pair.
- For 1:N, N:1, and N:M, retain all paths and add one ambiguity marker per hash.
- Preserve deterministic ordering, input immutability, metadata, `HasDifferences`, and empty duplicate/unreadable collections.

## Out of Scope

- Duplicate detection/groups, unreadable findings, `verify <folder>` wiring, reporting, catalog/scanner orchestration, refresh, rescanning, rehashing, arbitrary pairing, new DTOs, dependencies, GUI, SQLite, cloud, encryption, monitoring, network shares, ignore rules, or V2 scope.

## Acceptance Criteria

1. A unique unmatched equal-hash pair yields one `MovedOrRenamed` with both paths/hash and no separate `Missing`/`New`; cross-subfolder moves follow the same rule.
2. Same-path files remain `Unchanged`/`Modified` and never enter reconciliation.
3. A 1:N, N:1, or N:M group emits no exact pair; retain all paths plus one `AmbiguousMovedOrRenamed` marker.
4. The marker has null paths, shared hash, and `Move/rename is ambiguous: {baselineCount} baseline candidates and {currentCount} current candidates share this hash.`
5. One-sided repeated hashes remain ordinary findings; mixed groups reconcile independently and losslessly.
6. Shuffled inputs yield identical results without mutation. Path findings sort by effective path ordinally; pathless markers follow by hash with stable tie-breakers.
7. Move/ambiguity makes `HasDifferences` true; Story 3.1 clean semantics, metadata, and later-story collections remain unchanged.
8. Core stays independent of CLI/`System.Console`, no dependency is added, all tests pass, and no later scope enters.

## Technical Notes

- Target .NET 8/C#. Preserve null guards, native paths, ordinal hash equality, `baseline.RootPath`, and `current.ScannedAtUtc`.
- Preserve same-path merge; collect unmatched lists; group by hash; reconcile; materialize sorted changes. Never drop repeats via unsafe dictionaries.
- Add only `AmbiguousMovedOrRenamed` to `FileChangeType`; keep existing result constructors. Confident move message: `File was moved or renamed.`
- `HasDifferences` already treats non-`Unchanged` as drift. Do not use metadata, scan, normalize, or fabricate hashes.
- Expected files: `VerificationService.cs`, `FileChangeType.cs`, `VerificationServiceTests.cs`; `DomainModelTests.cs` only if needed. No CLI/scanner/catalog/registration/report/project/package changes.
- Tests use xUnit, fixed UTC records, and structural assertions. Story 3.1 ended with 79 passing tests and clean review.

References: `docs/prd.md#FR-12 Classify moved or renamed files`; `docs/architecture.md#Main Data Flows`; `docs/epics-and-stories.md#Story 3.2 Detect Moved or Renamed Files`; `docs/sprint-plan-004.md#Story 3.2 Detect Moved or Renamed Files`; `_bmad-output/implementation-artifacts/3-1-compare-basic-file-states.md`.

## Tasks

- [x] Preserve same-path behavior and collect unmatched candidates. (AC: 1-2, 6-8)
- [x] Reconcile unique and ambiguous hash groups losslessly and deterministically. (AC: 1-6)
- [x] Add tests for rename, subfolder move, same-path precedence, 1:N/N:1/N:M, one-sided/mixed groups, ordering, immutability, and differences. (AC: 1-8)

### Review Findings

- [x] [Review][Patch] Repair corrupted `New` references in both story copies [docs/stories/story-009.md:23]
- [x] [Review][Patch] Add the claimed cross-subfolder move and same-path precedence tests [tests/FolderPrint.Tests/Verification/VerificationServiceTests.cs:81]
- [x] [Review][Patch] Complete new-behavior assertions for `HasDifferences`, result collections/message, and current-only repeated hashes [tests/FolderPrint.Tests/Verification/VerificationServiceTests.cs:81]
## Validation Commands

```powershell
dotnet restore
dotnet build
dotnet test
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

## Definition of Done

- All ACs pass; unique matches yield one move and ambiguous groups retain every path plus one typed marker.
- Story 3.1 behavior, boundaries, and empty later-story collections remain correct.
- No scan/hash/catalog/console/command/timestamp/refresh/dependency/V2 behavior is added.
- Focused/full validation passes.

## Dev Agent Record

### Agent Model Used

OpenAI Codex (GPT-5)

### Implementation Plan

- Add failing moved/renamed and ambiguity tests first.
- Preserve same-path comparison, reconcile only unmatched fingerprints by ordinal SHA-256, then sort final typed findings deterministically.
- Keep duplicate/unreadable collections and all CLI/catalog behavior unchanged.

### Debug Log References

- Red: focused tests failed because move reconciliation and `AmbiguousMovedOrRenamed` did not exist.
- Green: 16 focused verification tests passed.
- Full validation: restore/build succeeded with 0 warnings/errors; all 87 tests passed; Core has no references or packages; `git diff --check` passed.
- Review patches: repaired story text and added subfolder, same-path precedence, `HasDifferences`, message/collection, and current-only repeated-hash coverage.

### Completion Notes List

- Added unique moved/renamed findings without duplicate `Missing`/`New` output.
- Added lossless typed ambiguity for 1:N, N:1, and N:M groups without arbitrary pairing.
- Preserved Story 3.1 classifications, result metadata, deterministic ordering, input immutability, and empty Story 3.3 collections.

### File List

- `_bmad-output/implementation-artifacts/3-2-detect-moved-or-renamed-files.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/stories/story-009.md`
- `src/FolderPrint.Core/Models/FileChangeType.cs`
- `src/FolderPrint.Core/Verification/VerificationService.cs`
- `tests/FolderPrint.Tests/Verification/VerificationServiceTests.cs`
## Change Log

- 2026-07-14: Created implementation-ready Story 3.2; no implementation performed.
- 2026-07-14: Implemented deterministic moved/renamed reconciliation and typed ambiguity behavior; moved to review.
- 2026-07-14: Addressed all 3 code-review findings; 87 tests pass; story marked done.
