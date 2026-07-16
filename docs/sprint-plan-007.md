---
title: "Sprint Plan 007: Refresh Registered Folder Baselines"
status: final
created: 2026-07-15
updated: 2026-07-15
source:
  - "docs/epics-and-stories.md"
  - "docs/retrospectives/sprint-006-retrospective.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/implementation-artifacts/4-2-unregister-a-folder.md"
tracking: "../_bmad-output/implementation-artifacts/sprint-status.yaml"
---

# Sprint Plan 007: Refresh Registered Folder Baselines

## Sprint Goal

Complete Epic 4 by implementing `refresh <folder>` for registered folder baselines using the conflict-aware catalog mutation patterns established by Story 4.2.

Sprint 007 has one committed outcome. Refresh accepts an intentional current folder state as the new trusted baseline only after a complete reliable scan and a guarded catalog save. It must preserve registered-folder identity and all unrelated catalog state while never deleting, moving, or modifying target files.

The standalone duplicates command, `DuplicateFinder`, and all V2 behavior remain outside Sprint 007.

## Selected Stories

### Committed

1. Story 4.3: Refresh a Registered Folder Baseline

There is no gated stretch scope. Epic 5 work must not begin inside Sprint 007.

## Story Order

1. Create the implementation-ready Story 4.3 artifact with refresh identity, timestamp, scan-reliability, guarded-persistence, and failure-atomicity contracts pinned.
2. Implement and validate Story 4.3 without changing target files or introducing Epic 5 behavior.
3. Run adversarial code review and resolve every actionable finding.
4. Mark Story 4.3 and Epic 4 done only after all checks pass, then create the Epic 4 retrospective.

## Story 4.3: Refresh a Registered Folder Baseline

As a CLI user, I want to refresh a registered folder baseline after intentional changes, so that future verifications compare against the newly trusted state.

### Acceptance Criteria Summary

- `folderprint refresh <folder>` finds exactly one registered folder using the established normalized, ordinal-ignore-case V1 identity rule.
- A successful refresh performs one reliable scan, replaces only the matched registration's stored file baseline, preserves its `id` and `createdAtUtc`, and records one deterministic injected UTC refresh timestamp in the existing verification timestamp field.
- Every unrelated registration, its metadata, baseline, and catalog ordering remain unchanged.
- Refresh reads target files only for scanning and hashing; it never deletes, moves, renames, writes, or otherwise modifies target files or directories.
- Unreadable findings or an incomplete/failed scan prevent baseline replacement and preserve the prior catalog bytes.
- Missing, invalid, file-valued, or unregistered target paths map to the established V1 error outcomes without catalog mutation.
- Malformed or structurally invalid catalogs produce `CatalogError` without repair, partial output, or overwrite.
- Refresh carries the initial catalog version into exactly one guarded mutation and fails safely on stale state, lock, flush, write, or replace failure.
- Success is reported only after persistence completes; output and V1 exit-code mapping are deterministic and testable.
- Core owns typed refresh behavior and results; CLI owns dispatch, writers, and exit-code mapping.
- Existing register, list, verify, and unregister behavior remains passing.

### Conflict-Aware Mutation Contract

- Reuse the Story 4.2 guarded catalog-save path; do not add a second write protocol.
- Carry the version returned by the initial catalog load through scanning to `SaveIfUnchanged` or its established equivalent.
- Perform one immutable matched-registration replacement and one guarded save; do not merge, retry, or fall back to unconditional save.
- Keep compare, same-directory temporary write, durable flush, final pre-replacement version check, and replacement inside the shared filesystem-coordinated operation.
- On conflict or persistence failure, preserve the latest committed/prior catalog, remove temporary artifacts, suppress success output, and return the command-neutral catalog failure.

### Baseline and Target-Safety Contract

- A refresh baseline contains only fingerprints from a complete reliable scan; never trust a partial snapshot.
- Preserve the stored root identity rather than rewriting it from a command-line alias.
- Preserve the matched registration's `id` and `createdAtUtc`; replace only its files and the existing nullable verification timestamp according to the pinned refresh rule.
- Preserve all survivor objects, metadata, baselines, and ordering exactly.
- Target files may be opened for hashing but must never be created, deleted, moved, renamed, or written.
- Tests must compare target bytes and structure before and after success and failure, and compare raw catalog bytes for every pre-persistence failure.

### Tasks

- Reuse `RegisteredFolderLookup`, catalog-wide validation, V1 path normalization, `FolderScanner`, and the shared guarded persistence seam.
- Add a narrow immutable catalog replacement operation or equivalent Core transformation that preserves identity, survivors, and ordering.
- Add typed Core refresh orchestration for load, validated lookup, one reliable scan, immutable replacement, and one guarded save.
- Wire the existing parser's refresh command through CLI output and established V1 exit codes.
- Add focused tests for successful baseline replacement, unchanged identity, timestamp behavior, survivor preservation, aliases, missing/unregistered/invalid targets, unreadable or failed scans, malformed catalog data, concurrent catalog changes, save failures, temporary cleanup, and target-file preservation.
- Add an integration regression showing `refresh` followed by `verify` is clean for the refreshed state.
- Run the full regression, build, formatting, dependency, boundary, excluded-scope, target-safety, and diff checks before review.

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
Select-String -Path src/FolderPrint.Core/**/*.cs -Pattern 'System\.Console|FolderPrint\.Cli|ExitCodes|TextWriter'
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'DuplicateFinder|SQLite|Sqlite|System\.CommandLine|export-report|cloud|encryption|monitoring'
```

Existing parser symbols may mention excluded commands; inspect changed behavior rather than treating historical symbols as new scope.

## Dependencies and Current State

- Epic 4 is `in-progress`.
- Stories 4.1 and 4.2 are done and reviewed.
- Story 4.2 established shared catalog validation, immutable mutation, filesystem-coordinated guarded persistence, a final pre-replacement version check, deterministic concurrency tests, and operation-neutral conflict reporting.
- Story 4.3 remains `backlog` until its BMAD `create-story` workflow creates the implementation artifact.
- The Sprint 006 retrospective is complete and identifies Story 4.3 as the logical next candidate.
- Epic 5 and Stories 5.1-5.2 remain backlog.

## Risks

- Refresh can silently bless an incomplete baseline if unreadable files or scan failures are not treated as fatal.
- A catalog change during scanning can overwrite newer state unless the initial version is carried into the shared guarded save.
- Reconstructing the registration incorrectly can lose its identity, timestamps, survivor metadata, baseline ordering, or stored root representation.
- Success output emitted before persistence can falsely report a refresh that was rejected by conflict or save failure.
- Tests that assert only the new baseline can miss accidental target-file mutation or damage to unrelated registrations.
- Refresh can expand into duplicate reporting or general catalog management; keep it limited to reliable baseline replacement.

## Definition of Done

- Story 4.3 satisfies its acceptance criteria, passes adversarial code review, and is marked `done`.
- A complete reliable scan becomes the new baseline for exactly one registered folder; identity and unrelated catalog state are preserved.
- The shared Story 4.2 conflict-aware mutation protocol is reused without an alternate write path.
- Every scan, validation, conflict, and persistence failure preserves prior/latest catalog state and produces deterministic V1 output and exit mapping.
- Target files and directories are never deleted, moved, renamed, or modified.
- Core remains independent from CLI, console, and exit-code concepts; no runtime dependency is added.
- Full restore, Release build, tests, formatting, dependency, boundary, scope, target-safety, and diff checks pass.
- No duplicates command, `DuplicateFinder`, GUI, database, cloud sync, encryption, realtime monitoring, or V2 feature is introduced.
- Story 4.3 and Epic 4 status change only when their BMAD workflow transitions are actually completed.

## Recommendation for First Story to Create

Create Story 4.3 next: `4-3-refresh-a-registered-folder-baseline`.

Its implementation-ready artifact should pin the exact refresh timestamp rule, reliable-scan criteria, immutable matched-registration replacement, survivor preservation, shared guarded-save behavior, failure byte preservation, deterministic output and exit codes, target-file non-mutation proof, and the strict exclusion of Epic 5 and V2 scope.
