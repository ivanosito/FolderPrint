---
storyId: "2.2"
storyKey: "2-2-scan-folders-recursively"
title: "Scan Folders Recursively"
status: done
epic: "Epic 2: Register Trusted Folder Baselines"
created: 2026-07-13
updated: 2026-07-13
baseline_commit: "fdf4cbb6c855ef1e823e946c73a533d384b19c6a"
sprint: "Sprint 002"
---

# Story 2.2: Scan Folders Recursively

Status: done

## Story

As a user registering a trusted folder,  
I want FolderPrint to recursively scan files under that folder,  
so that nested files are included in the baseline.

## Context

Sprint 001 is complete. Stories 1.1, 1.2, 1.3, and 2.1 are done. Sprint 002 is active; Story 2.2 is committed. Story 2.3 is stretch and remains backlog until Story 2.2 is implemented and reviewed.

Story 2.1 established the Core records and SHA-256 primitive. This story adds the reusable Core scanner required by later workflows without implementing persistence, registration, verification, duplicate detection, or refresh.

## Scope

- Add `FolderScanner` under `src/FolderPrint.Core/Scanning/`.
- Recursively enumerate root-level and nested files beneath an existing root.
- Reuse the existing `FileHasher`.
- Return the existing `FolderSnapshot` with root path, one UTC scan timestamp, readable fingerprints, and relative unreadable-file paths.
- Populate fingerprints with root-relative path, lowercase SHA-256, byte size, and last-write UTC.
- On expected per-file open/hash failure, record the relative path in `UnreadableFiles`, omit its fingerprint, and continue.
- Add focused xUnit tests using temporary directories.

## Out of Scope

- Story 2.3 catalog persistence or schema expansion.
- Registration, verification, duplicate detection, refresh, unregister, CLI behavior, or reporting.
- Ignore rules, symlink policy, retries, concurrency, incremental/atomic scanning, or large-scale optimization.
- GUI, SQLite, cloud sync, encryption, real-time monitoring, network-share guarantees, export reports, or V2 scope.

## Acceptance Criteria

1. An existing root with readable root-level and nested files returns one `FileFingerprint` per readable file.
2. Each `RelativePath` is relative to the root, not absolute, and follows platform-native separator behavior covered by tests.
3. Each readable fingerprint contains the existing `FileHasher` result, byte size, and last-write timestamp in UTC.
4. `FolderSnapshot.RootPath` identifies the supplied root and `ScannedAtUtc` is captured once in UTC.
5. An enumerated file that cannot be opened or hashed appears by relative path in `UnreadableFiles`, receives no fingerprint or fabricated hash, and does not stop other enumerable files.
6. An empty root returns empty `Files` and `UnreadableFiles` without creating catalog state.
7. Missing and non-directory roots fail clearly through standard argument/filesystem exceptions; no CLI mapping is added.
8. Scanning remains in Core, Core has no CLI or `System.Console` dependency, no runtime package is added, and no later workflow is implemented.
9. Repository-root restore, build, and tests succeed without regressions.

## Technical Notes

- Target .NET 8 and C# with current nullable and implicit-using settings.
- Preserve `FolderSnapshot(string RootPath, DateTimeOffset ScannedAtUtc, IReadOnlyList<FileFingerprint> Files, IReadOnlyList<string> UnreadableFiles)` and `FileFingerprint(string RelativePath, string Sha256, long Size, DateTimeOffset LastModifiedUtc)`.
- `FileHasher.ComputeSha256(string filePath)` already uses `System.Security.Cryptography.SHA256`, returns lowercase hex, and propagates I/O exceptions. `FolderScanner` owns expected per-file unreadable handling.
- Base paths on `Path.GetRelativePath(scanRoot, filePath)`. Do not add slash or case canonicalization because normalization is deferred.
- Use `Directory.EnumerateFiles` and `FileInfo` or equivalent platform APIs. Catch expected per-file access/I/O failures around hashing and metadata; do not swallow arbitrary exceptions.
- Enumeration can fail on inaccessible directories. The approved model represents unreadable files, not directories; do not invent a directory-error DTO or traversal policy.
- Keep `UnreadableFiles` as `IReadOnlyList<string>`; a richer reason model needs a later architecture decision.
- Do not promise atomicity, retries, or enumeration order. Tests should locate results by relative path.
- Recent `.gitignore` commits resolved the sprint plan's older `bin`/`obj` warning.

### Files and Tests

- Add `src/FolderPrint.Core/Scanning/FolderScanner.cs`.
- Add `tests/FolderPrint.Tests/Scanning/FolderScannerTests.cs`.
- Reuse `FileHasher.cs`, `FolderSnapshot.cs`, and `FileFingerprint.cs`; do not change CLI or catalog files.
- Use unique temporary roots with reliable cleanup.
- Cover root, nested, empty, missing-root, and non-directory cases.
- Assert exact hashes and sizes, UTC timestamps with filesystem precision tolerance, and native relative paths without order assumptions.
- Test unreadable files only with a reliable platform mechanism, proving no fingerprint and continued processing.
- Do not touch user folders, `%AppData%`, network paths, or catalog data.
- Follow `MethodOrScenario_Condition_ExpectedOutcome` naming.

### Previous Story Intelligence

Story 2.1 created the exact records and hasher needed here. `FileHasher` intentionally leaves unreadable classification to `FolderScanner`. Existing tests use xUnit, temporary data, exact hashes, and `try/finally` cleanup. Preserve minimal dependencies and Core/CLI boundaries.

### References

- `docs/product-brief.md#V1 Scope` and `#Non-Goals`
- `docs/prd.md#6 Glossary`, `#7 Functional Requirements`, `#9 Data Requirements`, `#11 Error Handling Requirements`, `#13 Edge Cases`
- `docs/architecture.md#Component Responsibilities`, `#Domain Models`, `#Testing Strategy`, `#Architecture Decisions`
- `docs/epics-and-stories.md#Story 2.2 Scan Folders Recursively`
- `docs/sprint-plan-002.md#Story 2.2 Scan Folders Recursively`, `#Risks`, `#Definition of Done`
- Existing `FolderSnapshot.cs`, `FileFingerprint.cs`, and `FileHasher.cs`
- `_bmad-output/implementation-artifacts/2-1-implement-domain-models-and-sha-256-hashing.md`
- [.NET `Directory.EnumerateFiles`](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratefiles)
- [.NET `Path.GetRelativePath`](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.getrelativepath)
- [.NET `LastWriteTimeUtc`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.lastwritetimeutc)

## Tasks

- [x] Add `FolderScanner` and its Core API. (AC: 1, 4, 7, 8)
- [x] Recursively enumerate files without ignore rules or later-story behavior. (AC: 1, 6)
- [x] Reuse `FileHasher` and create fingerprints with relative path, size, and UTC timestamp. (AC: 2, 3)
- [x] Translate expected per-file failures into unreadable relative paths and continue. (AC: 5)
- [x] Return the existing `FolderSnapshot`. (AC: 1, 4-6)
- [x] Test recursion, separators, hashes, sizes, timestamps, empty and invalid roots. (AC: 1-7)
- [x] Add a reliable unreadable-file continuation test where supported. (AC: 5)
- [x] Run validation and confirm Story 2.3 stays backlog with no later work. (AC: 8, 9)

### Review Findings

- [x] [Review][Patch] Synchronize and complete both BMAD story records [_bmad-output/implementation-artifacts/2-2-scan-folders-recursively.md:5]
- [x] [Review][Patch] Strengthen scanner timing/metadata evidence and make filesystem test cleanup/timestamp assertions robust [tests/FolderPrint.Tests/Scanning/FolderScannerTests.cs:7]

## Validation Commands

```powershell
dotnet restore
dotnet build
dotnet test
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
```

Optional scope check:

```powershell
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'VerificationService|DuplicateFinder|ReportFormatter|System.CommandLine|SQLite|Sqlite|export-report'
```

## Definition of Done

- All acceptance criteria pass.
- Recursive scanning returns every enumerable readable file using existing contracts.
- Relative paths, hashes, sizes, and UTC timestamps are correct and tested.
- Per-file failures produce unreadable paths without fabricated fingerprints and do not stop other enumerable files.
- Tests use temporary data and are not order-dependent or permission-flaky.
- Core remains independent of CLI and `System.Console`; no runtime dependency is added.
- No catalog expansion, registration, verification, duplicate detection, refresh, unregister, reporting, V2, or excluded scope is implemented.
- `dotnet restore`, `dotnet build`, and `dotnet test` succeed.
- Story 2.3 remains backlog.
- The dev agent updates this artifact with completion notes, validation evidence, and changed files.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Red phase scanner tests failed before FolderScanner existed.
- dotnet restore
- dotnet build --no-restore
- dotnet test --no-build --no-restore (36 passed before review patches)
- Post-review: dotnet restore, dotnet build --no-restore, dotnet test --no-build --no-restore (36 passed)

### Completion Notes List

- Added Core-only recursive scanning using existing FileHasher.
- Added root validation, fingerprint metadata, deterministic ordering, and unreadable-file continuation.
- Added and review-hardened five xUnit tests; Story 2.3 and later workflows remain unimplemented.

### File List

- src/FolderPrint.Core/Scanning/FolderScanner.cs
- tests/FolderPrint.Tests/Scanning/FolderScannerTests.cs
- docs/stories/story-005.md
- _bmad-output/implementation-artifacts/2-2-scan-folders-recursively.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-07-13: Created implementation-ready Story 2.2 and marked it ready for development.
- 2026-07-13: Implemented recursive scanning and tests; moved Story 2.2 to review.
- 2026-07-13: Applied code-review patches to synchronize records and strengthen test evidence.
- 2026-07-13: Code review approved; Story 2.2 marked done.
