---
title: "Sprint Plan 004: FolderPrint Verification Findings"
status: final
created: 2026-07-14
updated: 2026-07-14
source:
  - "docs/product-brief.md"
  - "docs/prd.md"
  - "docs/architecture.md"
  - "docs/epics-and-stories.md"
  - "docs/sprint-plan-003.md"
  - "docs/retrospectives/sprint-003-retrospective.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
tracking: "../implementation-artifacts/sprint-status.yaml"
---

# Sprint Plan 004: FolderPrint Verification Findings

## Sprint Goal

Extend the reviewed basic verification comparison so FolderPrint can distinguish unambiguous moved or renamed files from separate missing and new findings while reporting ambiguity honestly when duplicate hashes prevent a confident match. If that committed work is completed and reviewed cleanly, add duplicate and unreadable current-folder findings to the typed verification result as gated stretch work.

Sprint 004 remains inside the pure `FolderPrint.Core` verification boundary. It does not wire `folderprint verify <folder>`, update catalog timestamps, implement refresh, or add any V2 behavior.

## Selected Stories

### Committed

1. Story 3.2: Detect Moved or Renamed Files

### Optional Gated Stretch

2. Story 3.3: Include Duplicate and Unreadable Findings in Verification

Story 3.3 may start only after Story 3.2 is implemented, validated, and approved in code review without unresolved actionable findings. If the gate is not met, Story 3.3 remains backlog.

Story 3.4 is explicitly outside committed Sprint 004 scope. It must not start merely because Stories 3.2 and 3.3 are selected or completed.

## Story Order

1. Story 3.2: Detect Moved or Renamed Files
2. Story 3.3: Include Duplicate and Unreadable Findings in Verification — gated stretch

## Story 3.2: Detect Moved or Renamed Files

As a user verifying a registered folder, I want files with unchanged content at different paths reported as moved or renamed, so that I do not mistake a rename for separate missing and new files.

### Acceptance Criteria Summary

- An unmatched baseline file and unmatched current file with the same SHA-256 hash are classified as one `MovedOrRenamed` finding when the match is unambiguous.
- The finding contains both the baseline relative path and current relative path.
- A matched moved or renamed file is not also emitted as separate `Missing` and `New` findings.
- A move between different subfolders follows the same behavior as a rename in place.
- Matching is limited to files left unmatched after same-path `Unchanged` and `Modified` classification from Story 3.1.
- When duplicate hashes create multiple possible source or destination pairings, the result reports deterministic ambiguity and does not invent an exact move.
- Ambiguous candidates are not silently consumed or paired by incidental input order.
- Existing basic classifications and `VerificationResult.HasDifferences` behavior remain correct.
- Results remain deterministic regardless of input collection order.
- Comparison remains pure Core behavior: no filesystem scan, catalog write, console output, command dispatch, or timestamp update is introduced.
- Duplicate grouping, unreadable verification findings, dedicated duplicate detection, and `verify` CLI wiring remain outside Story 3.2.

### Tasks

- Inspect the reviewed Story 3.1 comparison contract and extend its unmatched baseline/current reconciliation rather than replacing the existing same-path classification logic.
- Define the smallest typed representation for an ambiguous move/rename result using the established verification models; avoid introducing Story 3.3 duplicate-group semantics.
- Group unmatched baseline and current fingerprints by SHA-256 using deterministic ordering.
- Convert exactly one eligible baseline candidate and one eligible current candidate into one `MovedOrRenamed` finding with both paths.
- Remove successfully matched candidates from separate `Missing` and `New` output.
- Detect one-to-many, many-to-one, and many-to-many same-hash cases and report ambiguity without arbitrary pairing.
- Preserve ordinary `Missing` and `New` findings when hashes do not establish content continuity.
- Add focused xUnit tests for rename in place, move across subfolders, mixed moved/basic findings, no double-reporting, and deterministic ordering.
- Add boundary tests for one-to-many, many-to-one, many-to-many, same-path duplicates where representable, and shuffled input order.
- Confirm same-path same-hash and same-path different-hash behavior from Story 3.1 remains unchanged.
- Confirm no scanning, catalog mutation, CLI wiring, duplicate-group output, unreadable handling, refresh behavior, or V2 scope entered the implementation.

## Story 3.3: Include Duplicate and Unreadable Findings in Verification (Optional Gated Stretch)

As a user verifying a registered folder, I want duplicate and unreadable files surfaced in the verification result, so that integrity checks do not hide important current-folder risks.

### Entry Gate

- Story 3.2 is implemented and all acceptance criteria pass.
- The full test suite and architecture-boundary checks pass.
- Story 3.2 has completed adversarial code review with no unresolved actionable findings.
- Move/rename ambiguity behavior is stable enough that Story 3.3 will not redefine it.

### Acceptance Criteria Summary

- Two or more current readable files sharing a SHA-256 hash produce a duplicate group containing every matching current relative path.
- Hashes represented by only one current readable file do not produce duplicate groups.
- Duplicate groups and paths use deterministic ordering.
- Unreadable entries supplied by the current `FolderSnapshot` are carried into the typed verification result with their paths and available concise reasons.
- Unreadable files receive no fabricated hash and do not enter duplicate groups or move/rename matching.
- Duplicate and unreadable findings cause `VerificationResult.HasDifferences` to remain true while an unchanged-only result remains clean.
- Existing `Unchanged`, `Modified`, `Missing`, `New`, `MovedOrRenamed`, and ambiguity behavior remains intact.
- The work reuses the existing snapshot and verification model boundaries and does not rescan or rehash files inside `VerificationService`.
- Story 3.3 does not wire `folderprint verify <folder>`, format console reports, update `lastVerifiedAtUtc`, implement the standalone `duplicates` command, or implement refresh.

### Tasks

- Extend the typed verification result only as needed to carry deterministic duplicate groups and unreadable findings already anticipated by the architecture.
- Derive duplicate groups from current readable fingerprints by SHA-256 without introducing filesystem access or a second hashing path.
- Exclude singleton hashes and include all current paths for qualifying duplicate groups.
- Carry current snapshot unreadable entries into verification output without manufacturing fingerprints.
- Establish deterministic ordering for groups, paths, and unreadable findings.
- Integrate duplicate and unreadable findings with `HasDifferences` while preserving unchanged-only cleanliness.
- Add focused xUnit tests for duplicate groups, no-duplicate input, multiple groups, unreadable propagation, and exclusion of unreadable files from hash-based behavior.
- Add regression tests combining Story 3.3 findings with basic and moved/renamed classifications.
- Verify that ambiguity from Story 3.2 remains distinct from current-file duplicate grouping and is not silently reinterpreted.
- Confirm no CLI/report wiring, catalog mutation, timestamp update, standalone duplicate command, refresh behavior, external dependency, or V2 scope entered the implementation.

## Validation Commands

Run from the repository root for every implemented story:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Optional scope and architecture checks:

```powershell
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'CommandDispatcher|ReportFormatter|LastVerifiedAtUtc|Refresh\(|SQLite|Sqlite|System.CommandLine|export-report'
Select-String -Path src/FolderPrint.Core/**/*.cs -Pattern 'System\.Console|FolderPrint\.Cli'
```

The first command is an inspection aid: any match must be reviewed to ensure Sprint 004 did not wire the `verify` command, implement refresh, or introduce excluded dependencies or V2 behavior.

## Dependencies

- Stories 1.1–1.3 are done: solution boundaries, parser/exit codes, catalog loading, and empty-list behavior are stable.
- Stories 2.1–2.4 are done: domain models, hashing, recursive scanning, catalog persistence, and registration are implemented and reviewed.
- Story 3.1 is done and reviewed: `VerificationService` deterministically classifies `Unchanged`, `Modified`, `Missing`, and `New`, with SHA-256 authoritative for same-path content equality.
- Story 3.2 depends directly on Story 3.1 and is the next story in Epic 3.
- Story 3.3 depends on the snapshot behavior from Story 2.2 and verification behavior from Story 3.1; Sprint 004 additionally gates it on Story 3.2 completion and clean review.
- Story 3.4 depends on Stories 3.2 and 3.3 but remains outside committed Sprint 004 scope.
- Epic 3 is already `in-progress`; Stories 3.2 and 3.3 remain `backlog` until their individual story files are created by the create-story workflow.

## Risks

- Greedy hash matching can invent a move when duplicate hashes make the source or destination ambiguous. Match only unique eligible pairs and represent ambiguity explicitly.
- Reconciling moved files after basic comparison can double-report the same file as `MovedOrRenamed`, `Missing`, and `New`. Treat reconciliation as a controlled replacement of eligible unmatched findings.
- Input enumeration order can make ambiguous results unstable. Sort candidates and final findings using explicit ordinal rules.
- Ambiguity reporting can accidentally become duplicate grouping and consume Story 3.3 scope. Story 3.2 should expose uncertainty only to the minimum needed for honest move detection.
- Story 3.3 duplicate grouping can conflict with Story 3.2 ambiguity if baseline/current matching and current-only grouping are conflated. Keep these concepts and tests distinct.
- Unreadable entries can be accidentally treated as missing, new, or duplicate if verification fabricates fingerprints. Carry scanner outcomes without creating hashes.
- Changes to `HasDifferences` can regress the Story 3.1 clean-result correction. Pin unchanged-only and each true-finding category with tests.
- Existing model shapes may need a narrow extension, but broad report-format or CLI work would prematurely enter Story 3.4.
- Generated `bin`/`obj` artifacts may obscure review diffs; keep review focused on source and intentional test changes.

## Definition of Done

- Story 3.2 satisfies its acceptance criteria, passes adversarial code review, and is marked done.
- Unambiguous same-content path changes produce one deterministic `MovedOrRenamed` finding with both paths and no duplicate `Missing`/`New` reports.
- Ambiguous duplicate-hash candidates are reported honestly and deterministically without arbitrary pairing.
- Story 3.1 basic classifications and clean-result semantics remain fully covered and passing.
- If Story 3.3 starts, its entry gate was satisfied first, it remains limited to typed duplicate and unreadable verification findings, and it is completed and reviewed before being marked done; otherwise it remains backlog.
- `FolderPrint.Core` remains independent from CLI, `System.Console`, filesystem orchestration inside comparison, and new runtime packages.
- `dotnet restore`, `dotnet build`, the complete `dotnet test` suite, dependency-boundary checks, and `git diff --check` pass.
- No `verify <folder>` command wiring, report formatting, catalog timestamp mutation, refresh behavior, standalone duplicates command, GUI, SQLite, cloud sync, encryption, real-time monitoring, network-share support, complex ignore rules, export reporting, or other V2 scope is introduced.
- Sprint tracking is updated only as stories actually transition through create-story, development, review, and completion workflows.

## Recommendation for First Story to Create

Create Story 3.2 next: `3-2-detect-moved-or-renamed-files`.

The implementation-ready story should make unique-pair eligibility, ambiguity representation, deterministic ordering, replacement of eligible `Missing`/`New` findings, mixed-result regression coverage, and strict exclusions explicit. Do not create Story 3.3 until Story 3.2 is done and approved in code review.
