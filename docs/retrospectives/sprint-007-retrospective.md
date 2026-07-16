---
title: "Sprint 007 Retrospective: Refresh Registered Folder Baselines"
status: final
created: 2026-07-16
updated: 2026-07-16
project: FolderPrint
sprint: "Sprint 007"
source:
  - "docs/sprint-plan-007.md"
  - "docs/epics-and-stories.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/implementation-artifacts/4-3-refresh-a-registered-folder-baseline.md"
  - "_bmad-output/implementation-artifacts/epic-4-retro-2026-07-16.md"
---

# Sprint 007 Retrospective: Refresh Registered Folder Baselines

## Sprint Goal

Complete Epic 4 by implementing `refresh <folder>` with the conflict-aware catalog mutation pattern established by Story 4.2, while preserving registered-folder identity, unrelated catalog state, and target-file safety.

## Completed Work

- Story 4.3: Refresh a Registered Folder Baseline - completed and approved after review.
- Added typed Core refresh orchestration, immutable index-based baseline replacement, one reliable scan, one injected UTC completion timestamp, and one guarded `SaveIfUnchanged`.
- Wired deterministic CLI output and V1 exit-code mapping for success, invalid or missing roots, catalog failures, scan failures, and unreadable files.
- Added conflict, failure-atomicity, survivor-preservation, target-safety, and `refresh -> verify` regression coverage.
- Marked Epic 4 done and completed the Epic 4 retrospective.

## Planned vs Actual Scope

Sprint 007 committed only Story 4.3, with no gated stretch scope. Story 4.3 was implemented, reviewed, corrected, and marked `done`; Epic 4 and its retrospective are also `done`. No Sprint 007 story remains ready-for-dev, in-progress, or review. Epic 5 did not start, as required by the sprint plan.

## What Went Well

- Story 4.3 reused the reviewed Story 4.2 persistence protocol instead of creating another catalog-write path.
- Immutable replacement and deep survivor assertions protected registration identity, metadata, ordering, and unrelated baselines.
- Reliable-scan gating prevented unreadable or partial snapshots from becoming trusted baselines.
- Deterministic concurrency tests covered register, verify, unregister, refresh, and visible external edits without retries or sleeps.
- Target-tree and raw-catalog-byte assertions directly proved safety on both success and failure.
- The final review suite passed 213 Release tests, including 96 focused tests, with clean build, formatting, boundary, scope, safety, and diff checks.

## What Was Adjusted

- Review strengthened catalog containment to account for filesystem-link aliases, not only lexical paths.
- Hash-provider cryptographic exceptions were mapped into the typed scan-error contract rather than escaping as unexpected failures.
- The canonical project story mirror was synchronized from `ready-for-dev` to `done`.
- The implementation retained the existing `lastVerifiedAtUtc` field for the refresh completion timestamp rather than adding schema or parser changes.

## Issues Encountered

- The pre-story CLI path recognized `refresh` but silently returned success without dispatching scan or persistence work.
- Lexical containment checks alone could not prevent a symlink or junction alias from placing active catalog artifacts inside the scanned target.
- Hashing failures outside ordinary I/O and authorization exceptions required explicit typed handling.
- Refresh combined trusted-state replacement with a long-running scan, making stale-catalog protection and success-after-save sequencing essential.

## Lessons Learned

- Reusing a reviewed mutation protocol lowers risk only when the new workflow carries the original catalog version through every intervening operation.
- Safety claims need tests against physical path aliases, target-tree state, and raw persisted bytes.
- A refresh is reliable only when incomplete scans fail closed and success is withheld until guarded persistence completes.
- Immutable index-based transformations make identity and survivor preservation explicit.
- A single-story sprint can close a risky epic cleanly when excluded scope and review gates remain firm.

## Risks Carried Forward

- Participating-writer coordination cannot guarantee behavior against every external editor or filesystem implementation.
- Cross-platform catalog paths and relative-separator policy remain deferred.
- Epic 5 duplicate grouping must be deterministic and compatible with existing verification semantics without coupling `DuplicateFinder` to verification or catalog management.
- Unreadable findings currently carry paths rather than rich reasons; Epic 5 should not expand that model without an explicit decision.

## Recommended Next Story

Story 5.1: Implement `DuplicateFinder` for Current Snapshots.

Keep it a pure Core operation over current snapshots. Pin duplicate membership, group ordering, path ordering, no-duplicate behavior, and exclusion of unreadable files before adding CLI behavior.

## Recommendation for Sprint 008

- Commit Story 5.1 as the primary outcome.
- Treat Story 5.2: Wire `duplicates <folder>` Command as gated stretch only after Story 5.1 is done, reviewed, and has no unresolved grouping or ordering ambiguity.
- Ensure Story 5.2 works for any existing folder without requiring registration or catalog access.
- Preserve deterministic output, the Core/CLI boundary, minimal dependencies, and all V1 exclusions.

## Completion Assessment

Sprint 007 is complete: Story 4.3 is done and reviewed, Epic 4 and its retrospective are done, no selected story remains active, and Epic 5 correctly remains unstarted.
