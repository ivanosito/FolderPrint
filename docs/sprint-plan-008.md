---
title: "Sprint Plan 008: Deterministic Duplicate Detection"
status: final
created: 2026-07-16
updated: 2026-07-16
source:
  - "docs/epics-and-stories.md"
  - "docs/retrospectives/sprint-007-retrospective.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/implementation-artifacts/epic-4-retro-2026-07-16.md"
tracking: "../_bmad-output/implementation-artifacts/sprint-status.yaml"
---

# Sprint Plan 008: Deterministic Duplicate Detection

## Sprint Goal

Start Epic 5 by implementing deterministic duplicate detection for current folder snapshots as a pure Core service. Story 5.1 is the committed outcome. Story 5.2 may enter the sprint only after Story 5.1 is complete, adversarially reviewed, and free of unresolved grouping, ordering, or Core-boundary concerns.

The sprint must not change catalog persistence or require registration for duplicate detection. Existing `register`, `verify`, `list`, `unregister`, and `refresh` behavior must remain compatible and passing.

## Selected Stories

### Committed

1. Story 5.1: Implement `DuplicateFinder` for Current Snapshots

### Gated Stretch

1. Story 5.2: Wire `duplicates <folder>` Command

Story 5.2 is not part of the committed forecast. Do not create its implementation-ready artifact or begin CLI wiring until Story 5.1 is `done` after review and the gate below is explicitly satisfied.

## Story Order and Gate

1. Create the implementation-ready Story 5.1 artifact with duplicate membership, group ordering, path ordering, unreadable exclusion, immutability, and verification-compatibility contracts pinned.
2. Implement Story 5.1 entirely in Core and add focused Core tests.
3. Run adversarial code review, resolve every actionable finding, run the full regression suite, and mark Story 5.1 `done` only when all checks pass.
4. Evaluate the Story 5.2 gate.
5. If the gate passes and capacity remains, create and implement Story 5.2, then review it separately. Otherwise, return Story 5.2 to the next sprint as backlog without weakening Sprint 008's success assessment.

### Story 5.2 Go/No-Go Gate

Story 5.2 may start only when all of the following are true:

- Story 5.1 is `done` after adversarial review, with no unresolved finding.
- Duplicate membership and ordering are specified and enforced by focused Core tests.
- `DuplicateFinder` is pure, deterministic, independent of CLI, console, scanning, and catalog persistence.
- Existing verification duplicate findings remain behaviorally compatible and deterministic.
- The full Release test suite, build, formatting, dependency, boundary, excluded-scope, and diff checks pass.
- Remaining sprint capacity is sufficient for CLI wiring, scan/error handling, deterministic reporting, tests, and a separate review without compressing the quality gate.

If any condition is false, the gate decision is **no-go** and Sprint 008 closes on Story 5.1 alone.

## Story 5.1: Implement `DuplicateFinder` for Current Snapshots

As a user checking a folder, I want duplicate readable files grouped by matching content hash, so that I can identify redundant files independent of baseline verification.

### Acceptance Criteria Summary

- `DuplicateFinder` accepts a current `FolderSnapshot` and returns duplicate groups without reading the filesystem, loading a catalog, or writing state.
- A hash represented by at least two readable fingerprint entries produces one duplicate group containing its relative paths according to the established verification semantics.
- Hashes represented by fewer than two readable fingerprints produce no group.
- Unreadable paths are excluded because they have no trusted content hash; the service does not fabricate hashes or mix unreadable findings into duplicate groups.
- Paths inside each group use deterministic ordinal ordering.
- Groups use the existing deterministic verification order: first path by ordinal comparison, then the full ordinal path sequence as the tie-breaker.
- Results are newly materialized and cannot change if caller-owned snapshot collections are later mutated.
- Null-input and edge-case behavior is explicit and covered by tests.
- Core remains independent from CLI, `System.Console`, exit codes, catalog persistence, and command parsing.
- Existing verification behavior remains compatible; duplicate grouping must not diverge into a second set of semantics.

### Pure Core Contract

- Keep `DuplicateFinder` in `FolderPrint.Core` and operate only on current snapshot data.
- Do not inject or call `FolderScanner`, `FileHasher`, `CatalogStore`, registration lookup, persistence, writers, or environment/global state.
- Do not add command dispatch, output prose, or exit-code decisions to Core.
- Prefer one shared duplicate-grouping implementation for on-demand detection and verification compatibility. Any integration with `VerificationService` must preserve its observable classifications, group membership, ordering, unreadable findings, and `HasDifferences` behavior.
- Do not mutate the supplied snapshot, its fingerprints, or its unreadable collection.

### Determinism and Compatibility Contract

- Use ordinal hash equality, matching the current verification behavior and lowercase SHA-256 domain convention.
- Preserve the established qualification rule based on fingerprint count and the established distinct ordinal path projection, including compatibility coverage for repeated fingerprint entries at the same path.
- Sort each group's distinct paths with `StringComparer.Ordinal`.
- Sort groups by their first path with `StringComparer.Ordinal`, then compare the complete ordered path sequence ordinally to resolve ties.
- Return no singleton groups and no group for unreadable paths.
- Prove results are invariant to input fingerprint order.
- Preserve current `verify` output and exit behavior through regression tests; Story 5.1 must not otherwise change `verify`.

### Tasks

- Create a pure Core `DuplicateFinder` and a typed, immutable duplicate-group result shape only if the existing model is insufficient.
- Consolidate or delegate the current verification duplicate-grouping logic so on-demand and verification paths cannot drift, without changing verification behavior.
- Add focused tests for one group, multiple groups, singleton hashes, an empty snapshot, unreadable-only input, mixed readable/unreadable input, input permutations, ordinal path ordering, full-sequence tie-breaking, repeated fingerprint entries at one path, and defensive materialization.
- Add compatibility tests proving verification retains its current duplicate groups, unreadable findings, deterministic ordering, and differences semantics.
- Run the full regression and architecture checks before review.

## Story 5.2: Wire `duplicates <folder>` Command (Gated Stretch)

As a CLI user, I want to run duplicate detection for any existing folder, so that I can find duplicate files without registering the folder first.

### Acceptance Criteria Summary

- The existing parser's `duplicates <folder>` command scans any valid existing folder without requiring registration or catalog access.
- Duplicate readable files are reported in the deterministic group and path order supplied by Core.
- A valid folder with no duplicates reports that none were found and exits successfully.
- A nonexistent, invalid, or file-valued target reports a clear deterministic error and exits non-zero through the established V1 mapping.
- Scan failures or unreadable files prevent reliable completion and map to the established scan-error behavior without fabricated duplicate results.
- CLI owns dispatch, writers, output text, and exit-code mapping; Core owns scanning and duplicate detection.
- The command performs no catalog load, creation, save, repair, registration lookup, or mutation.
- Existing command behavior remains passing.

### Stretch-Scope Guardrails

- Reuse the existing parser recognition, folder validation, scanner, typed error conventions, and deterministic reporting patterns where compatible.
- Do not pull Story 5.2 behavior into Story 5.1 or weaken the separate review gate.
- Use temporary folders for CLI tests and prove the command succeeds without a catalog and leaves any existing catalog bytes unchanged.
- Do not change catalog schema, catalog path rules, registration identity, or any participating-writer protocol.

## Validation Commands

Run from the repository root:

```powershell
dotnet restore
dotnet build FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release
dotnet format FolderPrint.sln --verify-no-changes --no-restore
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Architecture and scope inspection:

```powershell
Select-String -Path src/FolderPrint.Core/**/*.cs -Pattern 'System\.Console|FolderPrint\.Cli|ExitCodes|TextWriter|CatalogStore|SaveIfUnchanged'
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'SQLite|Sqlite|System\.CommandLine|export-report|cloud|encryption|monitoring|realtime|GUI'
```

Existing parser and catalog symbols may appear in unchanged code. Inspect the Sprint 008 diff and changed behavior rather than treating historical symbols as new scope.

## Dependencies and Current State

- Epics 1 through 4 are `done`; Epic 4 and its retrospective completed on 2026-07-16.
- The final Sprint 007 regression passed 213 Release tests and all documented quality checks.
- Story 2.2 provides recursive scanning and the current `FolderSnapshot` contract.
- Story 3.3 and `VerificationService` provide the existing deterministic duplicate-grouping semantics that Story 5.1 must preserve and make reusable.
- Epic 5 and Stories 5.1-5.2 remain `backlog` until the BMAD `create-story` workflow creates the first implementation artifact.
- Both open Epic 4 retrospective action items are addressed by this plan: Story 5.1 pins deterministic grouping, and Story 5.2 remains review-gated and catalog-independent.

## Risks

- A new service can drift from verification if duplicate logic is copied rather than shared or delegated.
- Ordering by hash or runtime collection order would make results unstable even when group membership is correct.
- Treating unreadable paths as duplicates would imply content equality without a trusted hash.
- Repeated fingerprint entries at one path are an established compatibility edge case; changing their qualification rule inside this sprint could alter verification behavior.
- CLI work started before Story 5.1 review could force output concerns into Core or conceal unresolved grouping semantics.
- Reusing registration or catalog workflows for an on-demand command could accidentally create or mutate persistent state.
- Stretch work can compromise review quality if it is treated as committed after the gate fails.

## Definition of Done

### Committed Sprint Success

- Story 5.1 satisfies its acceptance criteria, passes adversarial code review, and is marked `done`.
- Duplicate detection is a pure deterministic Core operation over current snapshots.
- Duplicate membership, ordinal path ordering, deterministic group ordering, unreadable exclusion, input-order independence, and defensive materialization are covered by focused tests.
- Verification duplicate behavior remains compatible and all existing commands remain passing.
- Core has no CLI, console, exit-code, catalog-persistence, database, or network dependency.
- Full restore, Release build, tests, formatting, dependency, boundary, excluded-scope, and diff checks pass.
- No catalog persistence, GUI, database, cloud sync, encryption, realtime monitoring, or V2 feature is added.

### Stretch Completion, If Gate Passes

- Story 5.2 separately satisfies its acceptance criteria, passes adversarial review, and is marked `done`.
- `duplicates <folder>` works against any existing folder without registration or catalog access.
- Duplicate, no-duplicate, invalid-target, unreadable, and scan-failure paths have deterministic CLI tests and V1 exit mapping.
- Tests prove the command does not create or mutate catalog state and does not change other command behavior.

Sprint 008 is successful when the committed definition is met even if Story 5.2 remains backlog.

## Tracking and Implementation Artifacts

- Keep `sprint-status.yaml` unchanged during planning: Epic 5 and Story 5.1 remain `backlog` until the BMAD `create-story` workflow creates the Story 5.1 implementation artifact.
- Create `5-1-implement-duplicatefinder-for-current-snapshots.md` next through the BMAD `create-story` workflow; that transition should move Epic 5 to `in-progress` and Story 5.1 to `ready-for-dev`.
- Create the Story 5.2 artifact only after the documented gate passes.
- No separate Sprint 008 implementation artifact is required by the installed BMAD sprint-planning workflow; this sprint plan and the existing sprint status file are the planning and tracking records.

## Recommendation for First Story to Create

Create Story 5.1 next: `5-1-implement-duplicatefinder-for-current-snapshots`.

Its implementation-ready artifact should preserve the existing verification qualification and ordering semantics, establish `DuplicateFinder` as a pure snapshot-to-groups Core service, require defensive materialization and input-order independence, and explicitly exclude CLI wiring, catalog access, persistence changes, and V2 scope.
