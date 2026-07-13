---
title: "Sprint Plan 003: FolderPrint Registration Flow"
status: final
created: 2026-07-13
updated: 2026-07-13
source:
  - "docs/product-brief.md"
  - "docs/prd.md"
  - "docs/architecture.md"
  - "docs/epics-and-stories.md"
  - "docs/sprint-plan-002.md"
  - "docs/retrospectives/sprint-002-retrospective.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
tracking: "../implementation-artifacts/sprint-status.yaml"
---

# Sprint Plan 003: FolderPrint Registration Flow

## Sprint Goal

Complete the first user-visible trusted-baseline workflow by wiring `folderprint register <folder>` end to end through the existing manual parser, recursive scanner, SHA-256 hasher, and JSON catalog persistence while rejecting invalid, duplicate, or incomplete registrations safely.

Sprint 003 keeps registration orchestration narrow. Verification comparison may begin only as gated stretch work after registration is complete and reviewed; moved/renamed logic, duplicate detection, unreadable verification findings, `verify` command wiring, refresh, and later/V2 behavior remain outside the sprint's committed scope.

## Selected Stories

### Committed

1. Story 2.4: Wire `register <folder>` Command

### Optional Gated Stretch

2. Story 3.1: Compare Basic File States

Story 3.1 may start only after Story 2.4 is implemented, validated, and approved in code review. If the gate is not met, Story 3.1 remains backlog.

## Story Order

1. Story 2.4: Wire `register <folder>` Command
2. Story 3.1: Compare Basic File States — gated stretch

## Story 2.4: Wire `register <folder>` Command

As a CLI user, I want to register a trusted folder from the command line, so that FolderPrint can establish the baseline I will verify later.

### Acceptance Criteria Summary

- Given an existing unregistered folder whose files are readable, `folderprint register <folder>` scans it recursively and persists one complete registered-folder baseline.
- The workflow reuses the existing `CommandParser`, `FolderScanner`, `FileHasher`, `IntegrityCatalog`, and `CatalogStore`; scanning, hashing, and JSON persistence are not duplicated in CLI code.
- Successful output reports the registered folder and readable file count, and the process returns `ExitCodes.Success`.
- A nonexistent path or a path that is a file produces a clear error, returns an appropriate non-zero exit code, and does not modify the catalog.
- A folder already represented in the loaded catalog is rejected without replacing or duplicating the existing baseline.
- If the scan reports any unreadable file, registration reports the unreadable paths, returns a scan-related non-zero exit code, and persists no partial baseline.
- Catalog load or save failures remain typed, map to `ExitCodes.CatalogError`, and do not silently overwrite invalid or previously valid catalog state.
- A newly registered record receives a stable ID and UTC creation timestamp, stores the completed snapshot fingerprints, and initializes `lastVerifiedAtUtc` to `null`.
- CLI output and exit mapping remain thin adapter behavior; reusable registration policy stays in Core where needed.
- No verification, moved/renamed classification, duplicate detection, refresh, unregister, non-empty list expansion, external dependency, or V2 behavior is introduced.

### Tasks

- Define the smallest typed Core registration outcome/service needed to coordinate catalog state and registration policy without console or exit-code dependencies.
- Reuse `FolderScanner` for recursive scanning and its existing `FileHasher` integration; do not add another traversal or hashing implementation.
- Load the catalog before registration and stop on typed catalog errors.
- Validate that the supplied root exists and is a directory, using existing scanner behavior where practical and mapping failures at the CLI boundary.
- Establish one documented path-identity comparison rule for duplicate registration on the current Windows V1 target, without introducing cross-platform or network-share policy.
- Reject snapshots containing unreadable files before adding or saving a trusted baseline.
- Create the registered-folder identity and UTC creation timestamp once, add the completed snapshot through existing Core catalog behavior, and save it through `CatalogStore`.
- Wire the existing parsed `register` request through the CLI runner/dispatcher to Core components.
- Map success, invalid path, duplicate registration, unreadable scan, catalog failure, and unexpected failure to existing named exit codes and concise deterministic output.
- Add Core tests for duplicate registration and unreadable all-or-nothing behavior.
- Add CLI/integration-style tests using temporary folders and injected catalog paths for successful registration, nested-file persistence, invalid roots, duplicate registration, unreadable files where reliable, and catalog failures.
- Confirm no later-story or excluded scope entered the implementation.

## Story 3.1: Compare Basic File States (Optional Gated Stretch)

As a user verifying a registered folder, I want FolderPrint to identify unchanged, modified, missing, and new files, so that I can understand ordinary folder drift.

### Acceptance Criteria Summary

- Given a baseline and current `FolderSnapshot`, the same relative path with the same SHA-256 hash is classified as `Unchanged`.
- The same relative path with a different SHA-256 hash is classified as `Modified`.
- A baseline-only relative path is classified as `Missing` for this basic comparison stage.
- A current-only relative path is classified as `New` for this basic comparison stage.
- Matching content hash determines `Unchanged` even when size or timestamp metadata differs.
- Results are deterministic and use the existing `VerificationResult`, `FileChange`, and `FileChangeType` models where they fit the architecture.
- Comparison behavior lives in `FolderPrint.Core` and performs no filesystem scan, catalog write, or console output.
- Moved/renamed matching, ambiguity, duplicate groups, unreadable verification findings, `verify` command wiring, and `lastVerifiedAtUtc` updates are not implemented.

### Tasks

- Add the minimal `VerificationService` comparison operation in `FolderPrint.Core` for basic path/hash states only.
- Index or compare baseline and current fingerprints by the established relative-path rule without rescanning or rehashing files.
- Produce deterministic `Unchanged`, `Modified`, `Missing`, and `New` findings using existing domain models.
- Make SHA-256 equality authoritative for unchanged content at the same path; do not classify from timestamp or size alone.
- Add focused xUnit tests for each basic classification and mixed snapshots.
- Add tests for identical hashes with changed timestamp/size metadata and deterministic result ordering.
- Confirm that moved/renamed, ambiguity, duplicates, unreadable findings, catalog updates, and CLI behavior remain absent.

## Validation Commands

Run from repository root for every implemented story:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
```

Optional scope checks:

```powershell
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'MovedOrRenamed|DuplicateFinder|Refresh\(|SQLite|Sqlite|System.CommandLine|export-report'
git diff --check
```

For Story 2.4, add a narrow smoke check against a temporary folder and injected catalog path only if the implementation-ready story defines a safe CLI seam. Do not write to the real `%AppData%` catalog during tests.

## Dependencies

- Stories 1.1–1.3 are done: solution boundaries, manual parsing/exit codes, catalog loading, and empty-list behavior exist.
- Story 2.1 is done: architecture domain models and `FileHasher` exist.
- Story 2.2 is done and reviewed: recursive scanning returns stable snapshots with readable fingerprints and unreadable paths.
- Story 2.3 is done and reviewed: registered baselines can be added and persisted safely through the JSON catalog.
- Story 2.4 depends on Stories 1.2, 2.2, and 2.3 and is the remaining Epic 2 story.
- Story 3.1 depends on the stable snapshot and baseline contracts from Stories 2.2 and 2.3; Sprint 003 additionally gates it on Story 2.4 completion and review.

## Risks

- Registration policy can leak into CLI code. Keep reusable duplicate and all-or-nothing decisions in Core, with CLI limited to orchestration and presentation.
- Windows path casing, trailing separators, relative inputs, and canonicalization can make duplicate registration inconsistent. Story 2.4 must choose a narrow documented V1 rule and test it.
- Files can become unreadable or change during scanning. Registration must never persist a snapshot when `UnreadableFiles` is non-empty.
- A catalog failure after scanning must not be reported as successful registration or damage the last valid catalog.
- Stable ID creation and timestamp capture must occur once per successful registration attempt and must not introduce overwrite/refresh semantics.
- CLI integration tests can accidentally touch real user state unless every catalog path is injected.
- Story 3.1 can expand prematurely into moved/renamed, duplicates, unreadable findings, or command wiring; its stretch gate and basic-state boundary must remain explicit.
- Generated `bin`/`obj` artifacts may obscure review diffs unless workspace hygiene is maintained.

## Definition of Done

- Story 2.4 satisfies its acceptance criteria, passes adversarial code review, and is marked done.
- A user can register a valid readable folder end to end and later load the persisted baseline from the injected JSON catalog.
- Invalid roots, duplicate registration, unreadable files, and catalog errors return deterministic non-zero outcomes without creating a partial or duplicate baseline.
- Existing parser, scanner, hasher, catalog models, and persistence behavior are reused without duplication.
- Core remains independent from CLI and `System.Console`; no external runtime package is added.
- Tests use temporary filesystem/catalog locations and do not touch real `%AppData%`.
- `dotnet restore`, `dotnet build`, and the complete `dotnet test` suite pass.
- No moved/renamed detection, duplicate detection, unreadable verification findings, `verify` command wiring, refresh, later-story work, or V2 scope is introduced.
- If Story 3.1 starts, Story 2.4 is already done and reviewed, Story 3.1 remains limited to basic Core comparison, and it is completed and reviewed before being marked done; otherwise it remains backlog.

## Recommendation for First Story to Create

Create Story 2.4 next: `2-4-wire-register-folder-command`.

The implementation-ready story should make path identity, stable ID creation, unreadable all-or-nothing behavior, duplicate registration, typed outcome mapping, temporary catalog injection, and strict exclusions explicit. Do not create Story 3.1 until Story 2.4 is done and approved.
