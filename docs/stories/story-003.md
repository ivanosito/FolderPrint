---
storyId: "2.1"
storyKey: "2-1-implement-domain-models-and-sha-256-hashing"
title: "Implement Domain Models and SHA-256 Hashing"
status: review
epic: "Epic 2: Register Trusted Folder Baselines"
created: 2026-07-07
updated: 2026-07-07
baseline_commit: "274343cad729e9208939753c376746c4ff0a1a3c"
sprint: "Sprint 001 stretch"
source:
  - "../../docs/product-brief.md"
  - "../../docs/prd.md"
  - "../../docs/architecture.md"
  - "../../docs/epics-and-stories.md"
  - "../../docs/sprint-plan-001.md"
  - "sprint-status.yaml"
previousStories:
  - "1-1-create-solution-and-project-boundaries.md"
  - "1-2-parse-v1-commands-and-define-exit-codes.md"
---

# Story 2.1: Implement Domain Models and SHA-256 Hashing

Status: review

## Story

As a user who trusts a folder,  
I want each file to have a stable content fingerprint,  
so that later verification can compare file contents reliably.

## Context

Story 2.1 is the first Epic 2 story and is pulled into Sprint 001 only as stretch work. Story 1.1 and Story 1.2 are done, so the solution structure, CLI/Core project boundary, parser-only CLI foundation, exit-code constants, and test project are already in place.

The Sprint 001 plan explicitly limits this stretch story to initial Core domain models and `FileHasher`. This story creates the typed Core contracts and hashing primitive that later scanner, catalog, registration, verification, duplicate detection, and refresh stories will consume. It must not implement those later behaviors.

## Scope

Implement initial Core model and hashing foundations:

- Add domain model files in `src/FolderPrint.Core/Models/`:
  - `RegisteredFolder`
  - `FileFingerprint`
  - `FolderSnapshot`
  - `VerificationResult`
  - `FileChange`
  - `FileChangeType`
- Add `FileHasher` in `src/FolderPrint.Core/Scanning/`.
- Use `System.Security.Cryptography.SHA256` to compute SHA-256 from readable file contents.
- Return SHA-256 values as lowercase hex strings.
- Keep models simple and explicit, using architecture-defined fields.
- Add focused xUnit tests for the SHA-256 known vector and basic model construction where useful.
- Remove the template `Class1.cs` only if replaced by real Core types.

## Out of Scope

Do not implement:

- Recursive folder scanning or `FolderScanner`.
- Catalog loading, catalog creation, catalog persistence, `%AppData%` path resolution, `IntegrityCatalog`, `CatalogStore`, or JSON serialization.
- Registration behavior or `register` command execution.
- Verification comparison logic or `VerificationService`.
- Duplicate detection or `DuplicateFinder`.
- Refresh behavior.
- Report formatting or `ReportFormatter`.
- CLI command behavior beyond the parser-only work already completed in Story 1.2.
- Unreadable-file classification; later scanner and verification stories own unreadable handling.
- GUI, SQLite or other database storage, cloud sync, encryption, real-time monitoring, guaranteed network share support, complex ignore rules, export-report, or other V2 scope.

## Acceptance Criteria

1. Given Core domain models are needed by later services, when `FolderPrint.Core` is inspected, then `RegisteredFolder`, `FileFingerprint`, `FolderSnapshot`, `VerificationResult`, `FileChange`, and `FileChangeType` exist in `FolderPrint.Core`.
2. Given a registered folder baseline is represented, when `RegisteredFolder` is inspected, then it includes `Id`, `RootPath`, `CreatedAtUtc`, `LastVerifiedAtUtc`, and `Files`.
3. Given a readable file fingerprint is represented, when `FileFingerprint` is inspected, then it includes `RelativePath`, `Sha256`, `Size`, and `LastModifiedUtc`.
4. Given a folder scan result is represented for future scanner work, when `FolderSnapshot` is inspected, then it includes `RootPath`, `ScannedAtUtc`, `Files`, and `UnreadableFiles`.
5. Given verification output is represented for future comparison work, when `VerificationResult` is inspected, then it includes `RootPath`, `VerifiedAtUtc`, `Changes`, `DuplicateGroups`, `UnreadableFiles`, and `HasDifferences`.
6. Given one verification finding is represented, when `FileChange` is inspected, then it includes `Type`, `BaselineRelativePath`, `CurrentRelativePath`, `Sha256`, and `Message`.
7. Given V1 verification classifications are represented, when `FileChangeType` is inspected, then it includes `Unchanged`, `Modified`, `Missing`, `New`, `MovedOrRenamed`, `Duplicate`, and `Unreadable`.
8. Given a readable file containing `abc`, when `FileHasher` hashes the file, then it returns `ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad`.
9. Given SHA-256 values must be stable for catalog and verification behavior, when `FileHasher` returns a hash, then the value is lowercase hexadecimal.
10. Given V1 dependency constraints, when project dependencies are inspected, then no third-party hashing dependency is introduced.
11. Given Sprint 001 stretch limits, when the change is inspected, then it does not add scanning, catalog persistence, registration, verification, duplicate detection, refresh behavior, report formatting, or new CLI command behavior.
12. Given the story is implemented, when `dotnet build` and `dotnet test` run from the repository root, then both succeed.

## Technical Notes

- Use .NET 8 and C#.
- Keep all Story 2.1 implementation in `FolderPrint.Core` and `FolderPrint.Tests`; do not change `FolderPrint.Cli` for this story.
- `FolderPrint.Core` must not reference `FolderPrint.Cli`, `System.Console`, or CLI parsing types.
- Use `System.Security.Cryptography.SHA256`; do not add a hashing package.
- Use xUnit for tests.
- Prefer simple public records or sealed classes with PascalCase property names. JSON camelCase is a later persistence concern and should not drive attributes in this story.
- Suggested namespaces:
  - `FolderPrint.Core.Models`
  - `FolderPrint.Core.Scanning`
- Suggested `FileHasher` API:
  - `public sealed class FileHasher`
  - `public string ComputeSha256(string filePath)`
- `FileHasher` may allow file I/O exceptions to propagate. Do not classify unreadable files here; `FolderScanner` will own unreadable-file reporting in a later story.
- For lowercase hex output, `Convert.ToHexString(hash).ToLowerInvariant()` is acceptable.
- Keep duplicate groups in `VerificationResult` simple because no `DuplicateGroup` model is approved for this story. A collection such as `IReadOnlyList<IReadOnlyList<string>>` is sufficient unless the implementation has a clearer minimal typed shape without adding extra story scope.
- Do not add `System.Text.Json` usage in this story; catalog serialization is later work.

## Tasks

- [x] Add model files under `src/FolderPrint.Core/Models/`. (AC: 1-7)
- [x] Add `FileChangeType` enum values required by V1. (AC: 7)
- [x] Add `FileHasher` under `src/FolderPrint.Core/Scanning/` using `System.Security.Cryptography.SHA256`. (AC: 8-10)
- [x] Add xUnit tests for SHA-256 known vector `abc`. (AC: 8)
- [x] Add a test or assertion that hash output is lowercase hex. (AC: 9)
- [x] Add lightweight model construction tests where useful without over-testing property bags. (AC: 1-7)
- [x] Run `dotnet build` from the repository root. (AC: 12)
- [x] Run `dotnet test` from the repository root. (AC: 12)
- [x] Confirm no scanner, catalog, verification, duplicate detection, refresh, report formatting, or CLI execution behavior was implemented. (AC: 11)

## Validation Commands

Run from repository root:

```powershell
dotnet build
dotnet test
```

Optional scope check:

```powershell
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'FolderScanner|CatalogStore|IntegrityCatalog|VerificationService|DuplicateFinder|ReportFormatter|System.Text.Json|System.CommandLine'
```

Expected validation interpretation:

- `dotnet build` succeeds.
- `dotnet test` succeeds.
- The known-vector hash test passes.
- The optional scope check does not show newly implemented out-of-scope components.

## Definition of Done

- All acceptance criteria pass.
- Core contains the six approved domain model types.
- `FileHasher` computes lowercase hex SHA-256 with `System.Security.Cryptography.SHA256`.
- SHA-256 behavior is covered by a known-vector xUnit test.
- No third-party hashing dependency is added.
- No scanner, catalog persistence, registration, verification, duplicate detection, refresh, report formatting, or new CLI command behavior is added.
- `dotnet build` and `dotnet test` pass from the repository root.
- Story artifact is updated by the dev agent after implementation with completion notes, validation commands, and file list.

## Dev Notes

### Architecture Compliance

- Follow AD-1: `FolderPrint.Cli` may depend on `FolderPrint.Core`; `FolderPrint.Core` must not depend on `FolderPrint.Cli` or `System.Console`.
- Follow AD-4: keep dependencies minimal and prefer platform libraries.
- Follow AD-6: Core returns typed result data; console formatting remains outside these models.
- Public domain types use PascalCase nouns.
- Enum values use PascalCase, including `MovedOrRenamed`.
- Tests use `MethodOrScenario_Condition_ExpectedOutcome` naming where practical.

### Current Repository State

- Story 1.1 is done.
- Story 1.2 is done.
- `FolderPrint.sln` exists and includes `FolderPrint.Cli`, `FolderPrint.Core`, and `FolderPrint.Tests`.
- `FolderPrint.Cli` contains parser-only Story 1.2 types and should not be changed for this story.
- `FolderPrint.Core` currently contains only the template `Class1.cs` and `FolderPrint.Core.csproj`.
- `FolderPrint.Tests` references both Core and CLI; this story should add Core-focused tests.

### Previous Story Intelligence

Story 1.1 and Story 1.2 established these patterns:

- Keep implementation tightly scoped to the current story.
- Use standard .NET 8 project structure and xUnit.
- Run `dotnet restore`, `dotnet build`, and `dotnet test` from repository root when validating.
- Avoid adding dependencies unless the story explicitly requires them.
- Avoid implementing future components early.
- Keep console and parser concerns in CLI; Core should remain clean business logic.

### Testing Guidance

- Create temporary files in test-controlled temporary directories for hashing tests.
- Use the known SHA-256 vector for ASCII content `abc`.
- Assert the exact hash string and lowercase output.
- Do not use real user folders, `%AppData%`, catalog files, network paths, or permission-dependent unreadable-file scenarios in this story.
- Model tests should remain lightweight; avoid brittle tests that merely duplicate every auto-property unless they clarify required construction shape.

### Source References

- Product brief: `docs/product-brief.md#The Solution`, `docs/product-brief.md#V1 Scope`, `docs/product-brief.md#Non-Goals`
- PRD: `docs/prd.md#6 Glossary`, `docs/prd.md#9 Data Requirements`, `docs/prd.md#10 Non-Functional Requirements`
- Architecture: `docs/architecture.md#Domain Models`, `docs/architecture.md#Testing Strategy`, `docs/architecture.md#Naming Conventions`, `docs/architecture.md#Architecture Decisions`
- Story source: `docs/epics-and-stories.md#Story 2.1 Implement Domain Models and SHA-256 Hashing`
- Sprint stretch scope: `docs/sprint-plan-001.md#Story 2.1 Implement Domain Models and SHA-256 Hashing`
- Previous story artifact: `_bmad-output/implementation-artifacts/1-2-parse-v1-commands-and-define-exit-codes.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test` red phase failed before Core model and scanning namespaces existed.
- `dotnet test` green phase initially failed because the old smoke test referenced removed template `Class1`.
- `dotnet restore`
- `dotnet build`
- `dotnet test` returned 27 passing tests.
- `dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference` confirmed Core has no project references.
- Scope scan for out-of-story components and dependencies returned no matches.

### Completion Notes List

- Implemented the six Story 2.1 Core domain model types under `FolderPrint.Core.Models`.
- Implemented `FileChangeType` with all V1 classification values, including `MovedOrRenamed`.
- Implemented `FileHasher` under `FolderPrint.Core.Scanning` using `System.Security.Cryptography.SHA256` and lowercase hex output.
- Added Core-focused xUnit coverage for model construction, `HasDifferences`, enum values, and the SHA-256 `abc` known vector.
- Updated the existing project-reference smoke test to use a real Core type after removing the template `Class1` placeholder.
- Confirmed no scanner, catalog persistence, verification logic, duplicate detection, refresh behavior, report formatting, new CLI command behavior, or external dependency was added.

### File List

- `src/FolderPrint.Core/Class1.cs` (deleted)
- `src/FolderPrint.Core/Models/FileChange.cs`
- `src/FolderPrint.Core/Models/FileChangeType.cs`
- `src/FolderPrint.Core/Models/FileFingerprint.cs`
- `src/FolderPrint.Core/Models/FolderSnapshot.cs`
- `src/FolderPrint.Core/Models/RegisteredFolder.cs`
- `src/FolderPrint.Core/Models/VerificationResult.cs`
- `src/FolderPrint.Core/Scanning/FileHasher.cs`
- `tests/FolderPrint.Tests/UnitTest1.cs`
- `tests/FolderPrint.Tests/Models/DomainModelTests.cs`
- `tests/FolderPrint.Tests/Scanning/FileHasherTests.cs`
- `_bmad-output/implementation-artifacts/2-1-implement-domain-models-and-sha-256-hashing.md`
- `docs/stories/story-003.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-07: Created Story 2.1 as Sprint 001 stretch story limited to domain models and FileHasher.
- 2026-07-07: Implemented Story 2.1 domain models, SHA-256 hasher, tests, and BMAD review status update.



