---
title: "Sprint Plan 002: FolderPrint Scanning Foundation"
status: final
created: 2026-07-08
updated: 2026-07-08
source:
  - "docs/product-brief.md"
  - "docs/prd.md"
  - "docs/architecture.md"
  - "docs/epics-and-stories.md"
  - "docs/sprint-plan-001.md"
  - "docs/retrospectives/sprint-001-retrospective.md"
tracking: "../implementation-artifacts/sprint-status.yaml"
---

# Sprint Plan 002: FolderPrint Scanning Foundation

## Sprint Goal

Implement the next narrow V1 foundation slice after domain models, SHA-256 hashing, and empty catalog/list support: recursive folder scanning that produces current file fingerprints and reports unreadable files without fabricating hashes.

This sprint deliberately avoids registration command behavior, full catalog baseline persistence, verification comparison, duplicate detection, refresh, unregister, GUI, SQLite, cloud sync, encryption, real-time monitoring, network-share guarantees, complex ignore rules, and V2 reporting scope.

## Selected Stories

### Committed

1. Story 2.2: Scan Folders Recursively

### Stretch

2. Story 2.3: Persist Registered Folder Baselines

Story 2.3 is stretch only. It should start only after Story 2.2 is complete, reviewed, and the scanner contracts are stable enough to persist. If started, keep it limited to catalog persistence and schema round-trip behavior; do not wire `register <folder>`.

## Story Order

1. Story 2.2: Scan Folders Recursively
2. Story 2.3: Persist Registered Folder Baselines, stretch

## Story 2.2: Scan Folders Recursively

As a user registering a trusted folder, I want FolderPrint to recursively scan files under that folder, so that nested files are included in the baseline.

### Acceptance Criteria Summary

- Given an existing folder with nested files, `FolderScanner` returns a `FolderSnapshot` containing readable file fingerprints.
- Each fingerprint includes `relativePath`, `sha256`, `size`, and `lastModifiedUtc`.
- Relative paths are rooted at the scanned folder.
- If a file cannot be opened, the unreadable file is reported without a fabricated hash.
- Scanner behavior stays in `FolderPrint.Core`; CLI command execution is not added in this story.

### Tasks

- Add `FolderScanner` under `src/FolderPrint.Core/Scanning/`.
- Use the existing `FileHasher` from Story 2.1 to compute SHA-256 for readable files.
- Validate that the supplied scan root exists and is a directory only if needed by the Core scanner contract; do not wire CLI validation yet.
- Walk files recursively beneath the scan root.
- Produce `FolderSnapshot` with root path, scan timestamp, readable file fingerprints, and unreadable-file entries.
- Compute file size and last modified UTC from filesystem metadata.
- Generate relative paths rooted at the scanned folder and cover separator behavior with tests.
- Report unreadable files without creating `FileFingerprint` entries for them.
- Add xUnit tests using temporary directories for nested files, relative paths, file sizes, timestamps, hash values, and unreadable-file handling where the platform permits reliable simulation.
- Confirm no catalog save behavior, registration, verification, duplicate detection, refresh, unregister, or V2 scope is introduced.

### Validation Commands

```powershell
dotnet restore
dotnet build
dotnet test
```

Optional scope checks:

```powershell
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'VerificationService|DuplicateFinder|ReportFormatter|System.CommandLine|SQLite|Sqlite|export-report'
```

## Story 2.3: Persist Registered Folder Baselines (Stretch)

As a user who registers a folder, I want the trusted baseline saved in a local JSON catalog, so that future CLI runs can verify against it.

### Acceptance Criteria Summary

- Given a completed `FolderSnapshot`, Core can add a registered folder record to the catalog.
- The catalog stores `id`, `rootPath`, `createdAtUtc`, `lastVerifiedAtUtc`, and `files`.
- Each file stores `relativePath`, `sha256`, `size`, and `lastModifiedUtc`.
- JSON uses camelCase fields.
- Catalog path resolution can be injected for tests and must not touch real `%AppData%` in tests.
- Existing Story 1.3 missing-catalog and malformed-JSON behavior remains intact.

### Tasks

- Extend Core catalog behavior for save/load round trips using `System.Text.Json`.
- Preserve malformed JSON protection: never silently overwrite invalid catalog files.
- Add registered-folder baseline creation/update APIs in Core only; do not wire CLI `register`.
- Keep the JSON shape aligned with `docs/architecture.md` and `docs/prd.md`.
- Add tests for save/load round trip, required JSON field names/casing, stable folder identity fields, file fingerprint persistence, and injectable temp catalog paths.
- Confirm no scanner expansion beyond what Story 2.2 completed and no registration command execution.

### Validation Commands

```powershell
dotnet restore
dotnet build
dotnet test
```

Optional scope checks:

```powershell
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'VerificationService|DuplicateFinder|ReportFormatter|System.CommandLine|SQLite|Sqlite|export-report'
```

## Dependencies

- Story 1.1 is done: solution, project boundaries, and xUnit test project exist.
- Story 1.2 is done: manual CLI parser and exit-code constants exist.
- Story 1.3 is done: minimal catalog load behavior and empty `list` output exist.
- Story 2.1 is done: Core domain models and `FileHasher` exist.
- Story 2.2 depends directly on Story 2.1's `FileHasher` and domain models.
- Story 2.3 depends on Story 2.2 because catalog persistence should store completed scan snapshots.

## Risks

- Filesystem traversal is more platform-sensitive than prior stories, especially unreadable-file simulation, path separators, and timestamp precision.
- Generated `bin`/`obj` artifacts are not ignored in the repo, which can create noisy review diffs after validation runs.
- Relative path normalization is still deferred in architecture; Story 2.2 should choose the smallest test-backed rule needed for V1 scanner correctness.
- Story 2.3 can easily creep into `register` command wiring. Keep command execution for Story 2.4.
- Catalog persistence must extend Story 1.3 without breaking empty-list and malformed-JSON behavior.

## Definition of Done

- Committed Story 2.2 meets all acceptance criteria and passes code review.
- `dotnet restore`, `dotnet build`, and `dotnet test` pass from repository root.
- `FolderPrint.Core` remains free of `FolderPrint.Cli` and `System.Console` dependencies.
- Scanner tests use temporary directories and avoid real user data.
- Unreadable files are reported explicitly and never receive fabricated fingerprints.
- No registration, verification, duplicate detection, refresh, unregister, GUI, SQLite, cloud sync, encryption, real-time monitoring, network-share support, complex ignore rules, or V2 reporting behavior is introduced.
- If stretch Story 2.3 starts, it is completed and reviewed before being marked done; otherwise it remains backlog.

## Recommendation for First Story to Create

Create Story 2.2 next: `2-2-scan-folders-recursively`.

This story is the required foundation for registration and catalog persistence. It should be created with explicit guardrails around temporary-directory tests, relative path behavior, unreadable-file handling, and reuse of the existing `FileHasher`.
