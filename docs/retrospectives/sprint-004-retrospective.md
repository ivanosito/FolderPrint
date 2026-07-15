---
title: "Sprint 004 Retrospective: FolderPrint Verification Findings"
status: final
created: 2026-07-14
updated: 2026-07-14
project: FolderPrint
sprint: "Sprint 004"
source:
  - "docs/sprint-plan-004.md"
  - "docs/epics-and-stories.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "docs/stories/story-009.md"
  - "docs/stories/story-010.md"
---

# Sprint 004 Retrospective: FolderPrint Verification Findings

## Sprint Goal

Extend the reviewed basic comparison so FolderPrint detects unambiguous moves or renames, reports ambiguous hash matches honestly, and—after that behavior passed review—adds duplicate and unreadable current-folder findings without crossing the pure `FolderPrint.Core` verification boundary.

## Completed Work

- Story 3.2: Detect Moved or Renamed Files — committed story completed and reviewed.
- Story 3.3: Include Duplicate and Unreadable Findings in Verification — gated stretch completed and reviewed after Story 3.2 satisfied its gate.
- Verification now preserves basic classifications, detects unique moves or renames, retains lossless findings for ambiguous matches, and returns deterministic duplicate and unreadable collections.
- Final validation reached 95 passing tests with no Core-to-CLI reference or external Core package.

## Planned vs Actual Scope

The plan committed Story 3.2 and allowed Story 3.3 only as gated stretch. Both were delivered in the intended order. Story 3.4 remained backlog as explicitly planned. No `verify <folder>` wiring, reporting, catalog timestamp update, refresh behavior, standalone `DuplicateFinder`, dependency, or V2 scope entered Sprint 004.

## What Went Well

- The stretch gate protected Story 3.3 from redefining unstable move/rename behavior.
- Both stories extended the existing pure comparison service and reused established models.
- SHA-256 remained authoritative, inputs remained immutable, and explicit ordinal ordering kept results deterministic.
- Ambiguity and current-file duplication remained separate signals instead of being conflated.
- Focused tests and adversarial reviews converted subtle contracts into regression coverage.

## What Was Adjusted

- Story 3.2 review strengthened cross-subfolder, same-path precedence, one-sided repeat, `HasDifferences`, and result-collection coverage.
- Story 3.3 review changed duplicate qualification to use fingerprint count before emitting distinct paths.
- Duplicate-group ordering gained the required full ordinal path-sequence tie-breaker.
- Determinism tests now compare nested group structure, and story status metadata was synchronized.

## Issues Encountered

- One-to-many, many-to-one, and many-to-many hash matches required lossless ambiguity instead of convenient but incorrect pairing.
- Duplicate groups and move ambiguity overlap by hash but have different meanings and could not consume each other’s findings.
- Caller-constructed snapshots exposed repeated-path cases that normal filesystem scans do not produce, revealing gaps in qualification and tie-breaking.
- The unavailable Windows sandbox helper added workflow noise but did not affect implementation or validation outcomes.

## Lessons Learned

- Gate dependent stretch work on a clean review, not merely passing tests.
- Define deterministic ordering completely, including pathological tie cases.
- Qualify findings from source-domain facts before applying presentation-level deduplication.
- Test nested result shape directly; flattened assertions can hide partitioning defects.
- Keep uncertainty, duplication, and unreadability as distinct typed concepts even when they share underlying hashes or paths.

## Risks Carried Forward

- Story 3.4 must compose catalog lookup, scanning, comparison, formatting, exit codes, and `lastVerifiedAtUtc` updates without moving CLI orchestration into Core.
- Reporting must preserve deterministic ordering and clearly distinguish clean results, ordinary drift, ambiguity, duplicates, and unreadable findings.
- Failed verification must not corrupt the stored baseline or update verification metadata incorrectly.
- Story 5.1 will need a reusable standalone `DuplicateFinder` without creating inconsistent behavior relative to Story 3.3’s verification grouping.
- Refresh remains dependent on completed verification wiring and must stay outside Story 3.4.

## Recommended Next Story

Story 3.4: Wire `verify <folder>` Command and Reporting.

It should compose the reviewed verification engine with registered-folder lookup, current scanning, deterministic reporting, exit-code mapping, and safe verification timestamp persistence while leaving refresh and standalone duplicate detection out of scope.

## Recommendation for Sprint 005

- Commit Story 3.4 as the primary outcome to complete Epic 3.
- Consider Story 4.1 as gated stretch only after Story 3.4 passes implementation and adversarial review.
- Keep Stories 4.2, 4.3, 5.1, and 5.2 outside committed scope.
- Require focused CLI/integration tests plus full restore, build, test, dependency-boundary, and diff validation.
- Run the Epic 3 retrospective after Story 3.4 is done and reviewed.

## Completion Assessment

Sprint 004 is complete: committed Story 3.2 and gated stretch Story 3.3 are done, and no selected Sprint 004 story remains ready-for-dev, in-progress, or review.
