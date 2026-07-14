---
title: "Sprint 003 Retrospective: FolderPrint Registration Flow"
status: final
created: 2026-07-14
updated: 2026-07-14
project: FolderPrint
sprint: "Sprint 003"
source:
  - "docs/sprint-plan-003.md"
  - "docs/epics-and-stories.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "docs/stories/story-007.md"
  - "docs/stories/story-008.md"
---

# Sprint 003 Retrospective: FolderPrint Registration Flow

## Sprint Goal

Complete the first user-visible trusted-baseline workflow by wiring `folderprint register <folder>` end to end through the existing parser, scanner, hasher, and safe JSON catalog persistence. Begin basic verification comparison only as gated stretch work after registration completed and passed review.

## Completed Work

- Story 2.4: Wire `register <folder>` Command — committed story completed and reviewed.
- Story 3.1: Compare Basic File States — gated stretch story completed and reviewed after Story 2.4 satisfied the gate.
- Registration now rejects invalid roots, duplicate registrations, unreadable snapshots, and catalog failures without creating a partial or misleading baseline.
- Core comparison now classifies readable fingerprints as `Unchanged`, `Modified`, `Missing`, or `New` using SHA-256-authoritative, deterministic comparison.
- The final suite passed all 79 tests with Core remaining independent from CLI and external packages.

## Planned vs Actual Scope

The plan committed Story 2.4 and allowed Story 3.1 only as gated stretch work. Actual delivery completed both in the intended order. Stories 3.2, 3.3, and 3.4 remained backlog and were not part of Sprint 003. No moved/renamed detection, duplicate detection, unreadable verification findings, `verify` command wiring, refresh behavior, external dependency, or V2 scope entered the sprint.

## What Went Well

- The stretch gate was respected: Story 3.1 started only after Story 2.4 was done and reviewed.
- Story 2.4 composed existing parser, scanner, hasher, catalog, and persistence behavior instead of duplicating them.
- Registration remained all-or-nothing and kept reusable policy in Core while CLI owned output and exit codes.
- Story 3.1 reused existing domain models and implemented comparison as pure, deterministic Core behavior.
- Red-green-refactor and focused regression tests made acceptance criteria directly observable.
- Adversarial review strengthened Story 2.4 and approved Story 3.1 without actionable findings.

## What Was Adjusted

- Story 2.4 review added safeguards for a catalog path inside the registered root, null catalog records, access-denied root inspection, deterministic scan-failure testing, and file-as-root CLI coverage.
- Story 3.1 narrowed `VerificationResult.HasDifferences` so unchanged-only results remain clean while real or future findings still count as differences.
- Story 3.1 explicitly retained same-hash files at different paths as separate `Missing` and `New` findings, preserving moved/renamed work for Story 3.2.
- Epic tracking was reconciled during the sprint so completed Epics 1 and 2 accurately show `done`.

## Issues Encountered

- Registration orchestration exposed edge cases that were not visible in isolated scanner or catalog stories, especially catalog overlap and access-denied path inspection.
- Deterministic simulation was needed to test whole-scan I/O and access failures reliably.
- The comparison result model initially treated any change entry, including `Unchanged`, as drift and required a narrow correction.
- Generated artifacts and the unavailable local Windows sandbox helper created workflow noise, but did not change production scope or validation outcomes.

## Lessons Learned

- Gate stretch work on reviewed upstream behavior; stable registration contracts made the comparison story safer and smaller.
- Integrity workflows need preservation guarantees on every failure path, not merely typed error reporting.
- Pure domain services with constructed inputs produce fast, deterministic tests and keep filesystem concerns out of comparison logic.
- Define later-story boundaries explicitly in both acceptance criteria and tests; negative scope tests prevented premature moved/renamed, duplicate, and unreadable behavior.
- Reconcile tracking promptly when all stories in an epic are done so sprint decisions are based on accurate state.

## Risks Carried Forward

- Story 3.2 must reconcile eligible `Missing` and `New` findings into moved/renamed results without double-reporting them.
- Duplicate hashes can make move/rename matching ambiguous; Story 3.2 must report ambiguity without implementing Story 3.3 duplicate groups.
- Relative-path comparison remains ordinal and based on established scanner output; broader separator, casing, cross-platform, and network-share policy remains deferred.
- Story 3.3 must add duplicate and unreadable findings without weakening Story 3.1 clean-result semantics.
- Story 3.4 must compose catalog lookup, scanning, comparison, reporting, exit codes, and timestamp updates without moving orchestration into Core domain logic.
- Generated artifact hygiene may continue to obscure review diffs.

## Recommended Next Story

Story 3.2: Detect Moved or Renamed Files.

It should build directly on the reviewed Story 3.1 comparison contract, match same-content files across different paths only when unambiguous, remove matched pairs from separate `Missing` and `New` reporting, and keep duplicate grouping, unreadable findings, and CLI wiring out of scope.

## Recommendation for Sprint 004

- Commit Story 3.2 as the primary sprint outcome.
- Consider Story 3.3 only as gated stretch work after Story 3.2 is implemented and reviewed.
- Preserve deterministic ordering, pure Core comparison, and existing `HasDifferences` behavior.
- Keep Story 3.4 `verify` command wiring out of the sprint unless Stories 3.2 and 3.3 both complete and pass review under an explicit gate.
- Continue full restore/build/test, dependency-boundary checks, and adversarial review before marking stories done.

## Completion Assessment

Sprint 003 is complete: its committed Story 2.4 and gated stretch Story 3.1 are done, and no selected Sprint 003 story remains ready-for-dev, in-progress, or review.