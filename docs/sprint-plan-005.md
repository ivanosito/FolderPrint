---
title: "Sprint Plan 005: FolderPrint CLI Verification"
status: final
created: 2026-07-15
updated: 2026-07-15
source:
  - "docs/epics-and-stories.md"
  - "docs/sprint-plan-004.md"
  - "docs/retrospectives/sprint-004-retrospective.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
tracking: "../implementation-artifacts/sprint-status.yaml"
---

# Sprint Plan 005: FolderPrint CLI Verification

## Sprint Goal

Wire the reviewed verification engine into the V1 CLI so users can run `folderprint verify <folder>` and receive deterministic, useful reporting with automation-friendly exit codes.

Sprint 005 completes the user-facing verification flow by composing existing catalog lookup, folder scanning, typed comparison, report formatting, and exit-code behavior without moving CLI orchestration into `FolderPrint.Core`. After that committed outcome is implemented, validated, and approved in adversarial review, complete registered-folder metadata display as gated stretch work.

Refresh, unregister, the standalone duplicates command, `DuplicateFinder`, and all V2 behavior are explicitly outside Sprint 005.

## Selected Stories

### Committed

1. Story 3.4: Wire `verify <folder>` Command and Reporting

### Optional Gated Stretch

2. Story 4.1: Display Registered Folder Metadata

Story 4.1 may start only after Story 3.4 is implemented, validated, and approved in adversarial code review without unresolved actionable findings. The Epic 3 retrospective must be completed, or its relevant findings incorporated into Story 4.1, before stretch work begins. If the gate is not met, Story 4.1 remains backlog.

Stories 4.2, 4.3, 5.1, and 5.2 are outside Sprint 005 scope and must not start merely because the committed story finishes early.

## Story Order

1. Story 3.4: Wire `verify <folder>` Command and Reporting
2. Epic 3 retrospective after Story 3.4 is done and reviewed
3. Story 4.1: Display Registered Folder Metadata — gated stretch

## Story 3.4: Wire `verify <folder>` Command and Reporting

As a CLI user, I want to run `folderprint verify <folder>` and receive a clear integrity summary, so that I can decide whether a folder still matches its trusted baseline.

### Acceptance Criteria Summary

- Given a registered unchanged folder, `folderprint verify <folder>` loads its trusted baseline, scans the current folder, runs the existing verification engine, reports a clean result, and returns `ExitCodes.Success`.
- Given a registered folder with drift or other typed verification findings, the command reports the relevant classifications and returns `ExitCodes.DifferencesFound`.
- Reporting clearly distinguishes `Modified`, `Missing`, `New`, `MovedOrRenamed`, move/rename ambiguity, duplicate groups, and unreadable findings without conflating their meanings.
- Summary counts, sections, findings, duplicate groups, paths, and concise unreadable reasons use explicit deterministic ordering independent of input enumeration order.
- Given a folder that is not registered, the command reports a clear not-registered result, returns the named not-found exit code, and does not scan or mutate the catalog.
- Invalid folder inputs, catalog load failures, scan failures, catalog save failures, and unexpected failures map to the existing named exit codes and concise deterministic output.
- A completed verification result updates `lastVerifiedAtUtc` consistently and persists it without changing the registered identity, creation timestamp, root path, or trusted file baseline.
- A failure before a reliable verification result exists does not update `lastVerifiedAtUtc`, replace the baseline, or otherwise corrupt catalog state.
- The exact timestamp policy is covered for clean and differences-found results as well as lookup, catalog, and scan failures; tests inject or control time where needed.
- CLI code remains a thin orchestration and presentation boundary. Scanning, comparison, typed result behavior, catalog persistence, and reusable report transformation remain in Core.
- `FolderPrint.Core` does not reference `FolderPrint.Cli`, `System.Console`, or CLI exit-code concepts.
- Tests use temporary folders and injected catalog paths and never read or modify the real `%AppData%\FolderPrint\catalog.json`.
- Story 3.4 does not implement refresh, unregister, the standalone duplicates command, `DuplicateFinder`, or any V2 feature.

### Tasks

- Inspect the reviewed Story 3.1–3.3 contracts and existing register/list CLI seams before defining the smallest verification orchestration change.
- Route the existing parsed `verify` request through the CLI dispatcher or runner without duplicating parser, scanner, hasher, catalog, or comparison behavior.
- Load the catalog through the existing persistence boundary and resolve the requested folder using the established V1 path-identity rule.
- Stop with a typed not-registered outcome before scanning when no matching registration exists.
- Scan the current folder once through `FolderScanner` and pass the resulting snapshot with the stored baseline to the existing `VerificationService`.
- Transform the typed result into deterministic display-ready sections through `ReportFormatter` or the established Core reporting boundary; keep direct console writing in CLI.
- Define concise output for clean verification, ordinary drift, moved/renamed findings, ambiguity, duplicate groups, unreadable findings, and mixed results.
- Map `HasDifferences == false` to `ExitCodes.Success` and `HasDifferences == true` to `ExitCodes.DifferencesFound` without deriving the exit code from formatted text.
- Map not registered, invalid input, catalog failure, scan failure, persistence failure, and unexpected failure through existing named exit codes.
- Persist `lastVerifiedAtUtc` only after a reliable verification result is produced, preserving the trusted baseline and registered-folder identity metadata.
- Ensure a timestamp save failure is reported honestly and does not claim full success; do not introduce refresh or baseline replacement semantics.
- Add focused report-format tests that assert meaningful sections, labels, counts, and complete ordering without brittle assertions over incidental whitespace.
- Add CLI/integration-style tests for unchanged, each major difference category, mixed findings, unregistered folders, invalid roots, catalog errors, scan errors where reliably simulated, timestamp success/failure behavior, and exit-code mapping.
- Add regression coverage proving existing registration and empty-list behavior remain unchanged.
- Confirm no excluded command, standalone duplicate service, external runtime dependency, or V2 behavior entered the implementation.

## Story 4.1: Display Registered Folder Metadata (Optional Gated Stretch)

As a CLI user, I want `folderprint list` to show registered folders and metadata, so that I can see what FolderPrint is tracking.

### Entry Gate

- Story 3.4 is implemented and all acceptance criteria pass.
- The full test suite, dependency-boundary checks, and scope checks pass.
- Story 3.4 has completed adversarial code review with no unresolved actionable findings.
- The Epic 3 retrospective is complete, or its applicable lessons have been explicitly incorporated into the implementation-ready Story 4.1 artifact.
- Story 4.1 can be completed without changing verification semantics or entering unregister or refresh scope.

### Acceptance Criteria Summary

- Given one or more registered folders, `folderprint list` displays each folder's `id`, `rootPath`, `createdAtUtc`, and `lastVerifiedAtUtc`.
- A missing `lastVerifiedAtUtc` value is represented clearly and consistently without inventing a timestamp.
- Multiple registered folders and their displayed fields use explicit deterministic ordering.
- Existing no-catalog and empty-catalog behavior from Story 1.3 remains successful and clearly reports that no folders are registered.
- Malformed catalog data continues to map to the catalog error exit code.
- Listing is read-only: it does not scan folders, verify contents, update timestamps, or modify catalog state.
- Output transformation remains testable without introducing `System.Console` into Core.
- Story 4.1 does not implement unregister, refresh, verification changes, the standalone duplicates command, `DuplicateFinder`, or V2 behavior.

### Tasks

- Reuse the existing catalog load and `list` command paths rather than adding a second catalog-reading implementation.
- Define one deterministic registered-folder ordering rule and deterministic field rendering, including the never-verified case.
- Extend the established report or CLI presentation boundary only as narrowly as required for non-empty metadata output.
- Preserve the reviewed empty-list and malformed-catalog behavior.
- Add focused tests for one folder, multiple folders in shuffled catalog order, never-verified metadata, empty catalog, and malformed catalog mapping.
- Assert listing leaves catalog contents and target folders unchanged.
- Confirm no mutation command, verification behavior, duplicate service, external dependency, or V2 scope entered the implementation.

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

Architecture and scope checks:

```powershell
Select-String -Path src/FolderPrint.Core/**/*.cs -Pattern 'System\.Console|FolderPrint\.Cli|ExitCodes'
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'Refresh\(|Unregister\(|DuplicateFinder|SQLite|Sqlite|System\.CommandLine|export-report|cloud|encryption|monitoring'
```

The second command is an inspection aid. Existing parser symbols or tests may mention excluded V1 command names; every changed or new match must be reviewed to confirm Sprint 005 did not implement refresh, unregister, the standalone duplicates command, `DuplicateFinder`, or V2 behavior.

## Dependencies

- Stories 1.1–1.3 are done: solution boundaries, manual parsing and named exit codes, catalog loading, and empty-list behavior are stable.
- Stories 2.1–2.4 are done: domain models, hashing, recursive scanning, catalog persistence, and registration are implemented and reviewed.
- Stories 3.1–3.3 are done and reviewed: basic comparison, moved/renamed detection with honest ambiguity, and typed duplicate/unreadable verification findings are deterministic and available in Core.
- Story 3.4 depends on the completed parser, registration, scanner, catalog, and verification behavior and is the only remaining Epic 3 story.
- Story 4.1 depends on existing catalog persistence and list behavior; Sprint 005 additionally gates it on Story 3.4 completion, review, and learning transfer.
- Epic 3 remains `in-progress`, Story 3.4 remains `backlog`, and Story 4.1 remains `backlog` until their individual implementation artifacts are created through the BMAD `create-story` workflow.

## Risks

- CLI orchestration can absorb reusable policy or result interpretation. Keep exit-code selection and console writing at the CLI boundary while retaining typed scanning, comparison, persistence, and report transformation in Core.
- Folder identity mismatches can make a registered path appear unregistered. Reuse the V1 path-identity rule established by registration rather than defining a verification-only rule.
- Report output can become nondeterministic through dictionary or filesystem enumeration. Apply explicit ordinal ordering to every section, group, and path and test shuffled inputs.
- Formatting can collapse ambiguity, duplicates, and unreadability into a generic difference and lose actionable information. Preserve each typed concept in its own labeled output.
- Exit codes can drift from typed result semantics if inferred from report text. Map directly from typed outcomes and `HasDifferences`.
- Timestamp updates can accidentally mutate the trusted baseline, occur on failed verification, or turn a successful comparison into a misleading success when persistence fails. Preserve identity and baseline fields and test each persistence branch.
- Integration tests can touch the real catalog unless every test injects a temporary catalog path.
- Story 3.4 can expand into refresh because both touch baseline metadata. Updating `lastVerifiedAtUtc` must not replace stored fingerprints or accept current drift as trusted.
- Story 4.1 can expand into catalog management. Keep it strictly read-only and gated.
- Generated `bin`/`obj` artifacts may obscure review diffs; keep review focused on intentional source and test changes.

## Definition of Done

- Story 3.4 satisfies its acceptance criteria, passes adversarial code review, and is marked done.
- A user can verify a registered folder end to end and receive deterministic, useful output for clean, drift, moved/renamed, ambiguity, duplicate, unreadable, and mixed outcomes.
- Clean and differences-found results map to the correct named exit codes; lookup, input, catalog, scan, persistence, and unexpected failures map consistently without parsing report text.
- Successful verification timestamp behavior is deterministic and tested, while failures do not replace the trusted baseline or mutate catalog state incorrectly.
- Core remains independent from CLI, `System.Console`, and exit-code concepts; no new runtime package is introduced without an approved architecture change.
- Tests use temporary filesystem and catalog locations and never touch real user AppData.
- `dotnet restore`, `dotnet build`, the complete `dotnet test` suite, dependency-boundary checks, scope checks, and `git diff --check` pass.
- The Epic 3 retrospective is run after Story 3.4 is completed and reviewed.
- If Story 4.1 starts, its entry gate was satisfied first, it remains read-only metadata display, and it is completed and reviewed before being marked done; otherwise it remains backlog.
- No refresh, unregister, standalone duplicates command, `DuplicateFinder`, GUI, database, cloud sync, encryption, real-time monitoring, export reporting, or other V2 scope is introduced.
- Sprint tracking changes only when stories actually transition through create-story, development, review, completion, and retrospective workflows.

## Recommendation for First Story to Create

Create Story 3.4 next: `3-4-wire-verify-folder-command-and-reporting`.

The implementation-ready story should make folder identity, typed outcome and exit-code mapping, deterministic report structure, timestamp persistence semantics, temporary catalog injection, Core/CLI boundaries, and strict exclusions explicit. Do not create Story 4.1 until Story 3.4 is done, approved in code review, and its Epic 3 lessons have been captured.
