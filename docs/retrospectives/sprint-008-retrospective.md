---
title: "Sprint 008 Retrospective: Deterministic Duplicate Detection"
status: final
created: 2026-07-16
updated: 2026-07-16
project: FolderPrint
sprint: "Sprint 008"
source:
  - "docs/sprint-plan-008.md"
  - "docs/epics-and-stories.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/implementation-artifacts/5-1-implement-duplicatefinder-for-current-snapshots.md"
  - "_bmad-output/implementation-artifacts/5-2-wire-duplicates-folder-command.md"
  - "_bmad-output/implementation-artifacts/epic-5-retro-2026-07-16.md"
---

# Sprint 008 Retrospective: Deterministic Duplicate Detection

## Sprint Goal

Start Epic 5 by delivering deterministic duplicate grouping as a pure Core service, then admit the `duplicates <folder>` CLI story only after the Core contract was complete, reviewed, and free of unresolved boundary or ordering concerns.

## Completed Work

- Story 5.1 delivered `DuplicateFinder` as a pure Core service over existing `FolderSnapshot` data. It groups readable fingerprints by SHA-256, excludes non-duplicates and unreadable paths, preserves verification compatibility, and materializes deterministically ordered groups and paths.
- Story 5.2 wired `duplicates <folder>` into the CLI using the existing scanner and `DuplicateFinder`, without catalog dependency or target-file mutation.
- Both stories were separately reviewed and marked `done`; Epic 5 and its retrospective are `done`.
- The full Release suite passes with 243 tests, and build, formatting, dependency, boundary, scope, catalog-safety, target-safety, and diff checks are clean.
- V1 functional implementation is complete, with no Sprint 008 story remaining `ready-for-dev`, `in-progress`, or `review`.

## Planned vs Actual Scope

The committed scope was Story 5.1. Story 5.2 was gated stretch and entered only after Story 5.1 passed its review gate. Both were completed and reviewed without adding catalog persistence changes, new dependencies, or excluded V2 features. Sprint 008 therefore met its committed goal and also completed the gated stretch scope.

## What Went Well

- The gate kept CLI reporting and failure concerns out of the Core duplicate-grouping contract.
- One shared `DuplicateFinder` now serves on-demand detection and verification, preventing semantic drift.
- Deterministic behavior was specified as a complete contract: ordinal hash equality, qualification before distinct projection, full path-sequence ordering, input-order independence, and defensive ownership.
- Catalog independence and target safety were proven directly with absent, malformed, inaccessible, registered, and in-target catalog cases plus file and directory state assertions.
- Existing `register`, `verify`, `list`, `unregister`, and `refresh` behavior remained compatible.

## What Was Adjusted

- Story 5.1 review preserved one materialized verification file set and added nested-path and unreadable-only coverage.
- Story 5.2 review expanded target-safety proof to directories, added a true unreadable-only CLI case, covered unexpected scanner and formatter failures, and pinned `FileNotFoundException` reclassification for both surviving and disappearing roots.
- The gated stretch was explicitly admitted only after the committed story was done and reviewed.
- Both Epic 4 carry-forward actions were closed through deterministic Core tests and direct catalog-independence proof.

## Issues Encountered

- Extracting the private verification algorithm exposed a compatibility edge: raw fingerprint count qualifies a group before paths are deduplicated.
- CLI reliability required distinct handling for invalid roots, root disappearance, traversal failures, unreadable snapshots, cryptographic failures, and unexpected pipeline failures without partial success output.
- Read-only claims needed stronger evidence than file-content checks alone; empty directories, timestamps, attributes, and failure paths also mattered.
- One Story 5.2 review layer was unavailable because of model capacity; the workflow followed its failure policy, used the completed review layers, directly triaged the acceptance criteria, and resolved every actionable finding.

## Lessons Learned

- Gated stretch work is safe when the gate controls both start authorization and review quality.
- Compatibility refactors should pin unusual legacy semantics before extraction rather than ?cleaning them up? incidentally.
- Deterministic output depends on ownership and materialization as much as sorting.
- Failure-path coverage should exercise every pipeline stage and verify the absence of partial output and side effects.
- Functional completion and release readiness are separate decisions.

## Risks Carried Forward

- Cross-platform catalog-path and relative-separator policy remains deferred; V1 is Windows-focused.
- Unreadable findings expose paths but not rich reasons.
- A live folder can change during scanning; V1 does not provide filesystem snapshots or realtime monitoring.
- Output already accepted by a failing writer cannot be rolled back.
- Packaging, clean-environment installation, stakeholder acceptance, and release/deployment evidence have not yet been established by functional story completion.

## Recommendation for V1 Final Readiness Check

Proceed to the dedicated V1 final readiness check already recorded in sprint tracking. It should trace every V1 PRD acceptance criterion and all six commands to evidence, rerun the clean Release suite and isolated command smoke tests, confirm usage/catalog/exit-code documentation, disposition carried risks, and record stakeholder and release decisions. Do not reopen the functional backlog unless the check identifies a concrete release blocker.

## Recommendation for Post-V1 Release and Packaging Work

After the readiness check passes, create a separate release/packaging scope that:

- chooses and documents framework-dependent versus self-contained publishing;
- produces a versioned, reproducible Windows artifact with checksums and release notes;
- verifies invocation and catalog behavior from a clean environment;
- documents installation, upgrade, uninstall, supported platform, and known limitations;
- records dependency/license notices and the release approval decision.

Keep packaging work separate from V1 functional acceptance and do not introduce deferred GUI, database, cloud, encryption, monitoring, or other V2 scope.

## Completion Assessment

Sprint 008 is complete and can close. Story 5.1, Story 5.2, Epic 5, and the Epic 5 retrospective are done; all 243 Release tests pass; no selected story remains active; and FolderPrint V1 can proceed to final readiness assessment.
