---
storyId: "1.3"
storyKey: "1-3-load-an-empty-catalog-and-list-registered-folders"
title: "Load an Empty Catalog and List Registered Folders"
status: review
epic: "Epic 1: Runnable CLI Foundation"
created: 2026-07-08
updated: 2026-07-08
baseline_commit: '5f74e2c8b6aa529a9f1deed43032aba83c50d792'
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
  - "2-1-implement-domain-models-and-sha-256-hashing.md"
---

# Story 1.3: Load an Empty Catalog and List Registered Folders

Status: review

## Story

As a CLI user,  
I want `folderprint list` to work before any folder is registered,  
so that I can see whether FolderPrint is tracking anything yet.

## Context

Story 1.1 created the .NET 8 solution, CLI project, Core project, and xUnit test project. Story 1.2 added the manual command parser, parser-level `Program.cs` behavior, and architecture-approved exit-code constants. Story 2.1 added the approved Core domain model types and `FileHasher` as Sprint 001 stretch work.

Story 1.3 is the next Epic 1 story. It introduces the first real command execution behavior for `list` only: loading the local JSON catalog as empty when it does not exist, reporting an empty registered-folder list, and treating malformed catalog JSON as a catalog error. It must preserve the parser and CLI/Core boundary already established.

## Scope

- Implement minimal catalog read behavior needed by `folderprint list`.
- Treat a missing catalog file as an empty catalog for `list`.
- Detect malformed catalog JSON and return a typed catalog error that maps to `ExitCodes.CatalogError`.
- Add the Core catalog types needed for this story, likely under `src/FolderPrint.Core/Catalog/`.
- Add a replaceable catalog path for tests; do not read or write the real `%AppData%` path in tests.
- Add a `list` dispatch path in the CLI that calls Core catalog loading and writes deterministic empty-list output.
- Keep existing parser behavior for all six V1 command shapes.
- Keep non-`list` commands at parser-level success or an explicit not-yet-implemented path only if required by the implementation; do not implement their product behavior.
- Add focused xUnit coverage for missing catalog, malformed JSON, empty-list CLI mapping, and no regressions to parser/exit-code behavior.

## Out of Scope

Do not implement:

- Creating or saving `catalog.json` on `list`.
- Registering folders or writing trusted baselines.
- Displaying non-empty registered folder metadata beyond what is structurally necessary for tests; Story 4.1 owns complete registered-folder listing.
- Recursive folder scanning or `FolderScanner`.
- JSON persistence of registered-folder baselines; Story 2.3 owns save/load round-trip and schema pinning for baselines.
- `register`, `verify`, `unregister`, `duplicates`, or `refresh` execution behavior.
- Verification, duplicate detection, refresh behavior, report formatting, or moved/renamed logic.
- Any V2 scope: GUI, SQLite, cloud sync, encryption, real-time monitoring, network share support, complex ignore rules, export/report commands, shell completion, or CLI framework adoption.

## Acceptance Criteria

1. Given no catalog file exists, when `folderprint list` runs, then FolderPrint treats the catalog as empty.
2. Given no catalog file exists, when `folderprint list` runs, then output clearly states that no folders are registered.
3. Given no catalog file exists, when `folderprint list` runs, then the command exits with `ExitCodes.Success`.
4. Given malformed catalog JSON exists at the configured catalog path, when a command loads the catalog, then Core returns a catalog error result without overwriting the file.
5. Given malformed catalog JSON is encountered through `folderprint list`, when the CLI maps the result, then it returns `ExitCodes.CatalogError`.
6. Given catalog loading is implemented, when tests run, then catalog path resolution is replaceable so tests do not touch the real `%AppData%\FolderPrint\catalog.json`.
7. Given the existing manual parser, when parser tests for all V1 commands run, then Story 1.2 behavior remains intact.
8. Given architecture dependency rules, when project references are inspected, then `FolderPrint.Core` still does not reference `FolderPrint.Cli`.
9. Given Story 1.3 is implemented, when `dotnet build`, `dotnet test`, and `dotnet run --project src/FolderPrint.Cli -- list` run from the repository root, then they succeed.

## Technical Notes

- Use .NET 8 and C#.
- Use `System.Text.Json` for catalog JSON parsing. Do not add a JSON dependency.
- Keep Core free of `System.Console`, CLI parser types, and CLI exit-code constants.
- Keep CLI responsible for command dispatch, console output, and exit-code mapping.
- Prefer typed Core results over exceptions for expected catalog outcomes:
  - missing catalog => success with empty catalog
  - valid empty catalog => success with empty catalog
  - malformed JSON => catalog error
- Suggested Core files:
  - `src/FolderPrint.Core/Catalog/IntegrityCatalog.cs`
  - `src/FolderPrint.Core/Catalog/CatalogStore.cs`
  - `src/FolderPrint.Core/Catalog/CatalogPathProvider.cs`
  - a small catalog load result type if needed
- `IntegrityCatalog` may use the existing `RegisteredFolder` model from Story 2.1.
- For this story, valid empty catalog JSON can be limited to the architecture schema shape with `registeredFolders`, including an empty array.
- Do not silently rewrite malformed JSON. Preserve the user's catalog file and surface a catalog error.
- Keep empty-list output deterministic and concise. Tests should assert stable required text such as "No folders are registered" rather than full console prose if the implementation includes additional usage context.
- `Program.cs` currently calls `CommandParser.Parse(args)` and returns parser success for recognized commands. This story should add dispatch for `CommandKind.List` while preserving usage-error handling.
- If a path provider reads `%AppData%`, isolate it behind an injectable interface or constructor parameter so tests can pass a temporary catalog path.
- Do not introduce `System.CommandLine`, options parsing, aliases, flags, help modes, or shell completion.

## Tasks

- [x] Add minimal Core catalog model/load types for an empty catalog. (AC: 1, 4, 6)
- [x] Add catalog path resolution with a test-replaceable path. (AC: 6)
- [x] Implement catalog load behavior: missing file returns empty catalog; malformed JSON returns catalog error. (AC: 1, 4)
- [x] Add CLI dispatch for `CommandKind.List` to load the catalog and print empty-list output. (AC: 2, 3, 5)
- [x] Map catalog load errors from `list` to `ExitCodes.CatalogError`. (AC: 5)
- [x] Preserve existing usage-error behavior for invalid parser input. (AC: 7)
- [x] Add xUnit tests for missing catalog load behavior. (AC: 1)
- [x] Add xUnit tests for malformed JSON handling and no overwrite behavior. (AC: 4)
- [x] Add CLI-level test or focused entry-point seam test proving empty `list` exits success. (AC: 2, 3)
- [x] Add CLI-level test or focused entry-point seam test proving malformed catalog maps to `CatalogError`. (AC: 5)
- [x] Run validation commands from the repository root. (AC: 8, 9)
- [x] Confirm no scanner, registration, verification, duplicate detection, refresh, unregister, V2 scope, or new runtime dependency was implemented.

## Validation Commands

Run from repository root:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/FolderPrint.Cli -- list
```

Optional scope checks:

```powershell
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'FolderScanner|VerificationService|DuplicateFinder|ReportFormatter|System.CommandLine|SQLite|Sqlite|export-report'
```

## Definition of Done

- All acceptance criteria pass.
- `folderprint list` works when the catalog does not exist.
- Empty-list output is clear and deterministic.
- Malformed catalog JSON is reported as a catalog error and maps to `ExitCodes.CatalogError`.
- Tests use temporary catalog paths and do not touch real `%AppData%`.
- Existing parser and exit-code tests still pass.
- `FolderPrint.Core` remains independent of CLI and console concerns.
- No catalog write behavior, registration, scanning, verification, duplicate detection, refresh, unregister, or V2 scope is added.
- Story artifact is updated by the dev agent after implementation with completion notes, validation commands, and file list.

## Dev Notes

### Architecture Compliance

- Follow AD-1: `FolderPrint.Cli` may depend on `FolderPrint.Core`; `FolderPrint.Core` must not depend on `FolderPrint.Cli` or `System.Console`.
- Follow AD-3: V1 catalog state is human-inspectable JSON using `System.Text.Json`, not SQLite or binary storage.
- Follow AD-4: keep dependencies minimal. No new runtime dependency is needed.
- Follow AD-6: Core returns typed result data; CLI handles output and exit-code mapping.
- Follow AD-7: catalog paths must be injectable or replaceable in tests.

### Current Repository State

- Story 1.1 is done.
- Story 1.2 is done.
- Story 2.1 is done as Sprint 001 stretch.
- `FolderPrint.Cli` currently contains parser-only types: `CommandKind`, `CommandParser`, `CommandParseResult`, `ParsedCommand`, `ExitCodes`, and a simple `Program.cs`.
- `Program.cs` currently returns parser-level success for `list`; it does not load a catalog yet.
- `FolderPrint.Core` contains Story 2.1 model types under `Models/` and `FileHasher` under `Scanning/`.
- There is no `Catalog/` folder yet in `FolderPrint.Core`.
- `FolderPrint.Tests` references both Core and CLI and has parser, exit-code, model, and hasher tests.

### Previous Story Intelligence

- Story 1.2 established exact lowercase V1 command names and strict argument shapes. Do not add aliases, flags, or extra argument handling.
- Story 1.2 tests prefer result object assertions over brittle console prose. Continue that pattern.
- Story 2.1 established Core model namespace `FolderPrint.Core.Models` and scanning namespace `FolderPrint.Core.Scanning`.
- Story 2.1 intentionally did not add JSON serialization or catalog behavior; Story 1.3 may add only the catalog read behavior needed for empty `list`.
- Prior stories emphasize tight scope, standard .NET 8/xUnit validation, minimal dependencies, and no future components ahead of their story.

### Testing Guidance

- Use temporary directories/files for catalog tests.
- Test missing catalog by pointing the store to a non-existent temp file.
- Test malformed JSON by writing invalid text to a temp file, loading it, asserting catalog error, then asserting the file contents were not replaced.
- Keep CLI tests focused on exit code and required output signal for `list`.
- Avoid real `%AppData%`, real user catalog files, real registered folders, network paths, permission-dependent unreadable files, and scanner behavior.

### Source References

- Product scope and exclusions: `docs/product-brief.md#V1 Scope`, `docs/product-brief.md#Non-Goals`
- Catalog and list requirements: `docs/prd.md#7.1 Catalog Management`, `docs/prd.md#7.4 Listing and Unregistering`, `docs/prd.md#9 Data Requirements`
- CLI and exit-code requirements: `docs/prd.md#7.7 CLI Behavior`, `docs/prd.md#11 Error Handling Requirements`
- Architecture catalog and CLI/Core boundary: `docs/architecture.md#Folder and Project Structure`, `docs/architecture.md#Component Responsibilities`, `docs/architecture.md#JSON Catalog Schema`, `docs/architecture.md#Error Handling Strategy`, `docs/architecture.md#Exit Code Strategy`, `docs/architecture.md#Architecture Decisions`
- Story source: `docs/epics-and-stories.md#Story 1.3 Load an Empty Catalog and List Registered Folders`
- Sprint guardrails: `docs/sprint-plan-001.md#Sprint Goal`, `docs/sprint-plan-001.md#Definition of Done`, `docs/sprint-plan-001.md#Not In Sprint 001`
- Previous story artifact: `_bmad-output/implementation-artifacts/1-2-parse-v1-commands-and-define-exit-codes.md`
- Stretch story artifact already done: `_bmad-output/implementation-artifacts/2-1-implement-domain-models-and-sha-256-hashing.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test` red phase failed because `FolderPrint.Core.Catalog` and `CliRunner` did not exist yet.
- `dotnet test` green phase passed with 31 tests.
- `dotnet restore`
- `dotnet build`
- `dotnet test`
- `dotnet run --project src/FolderPrint.Cli -- list`
- `dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference`
- Scope scan for out-of-story components and dependencies returned no matches.

### Completion Notes List

- Added minimal Core catalog load types under `FolderPrint.Core.Catalog`.
- Implemented missing-catalog load behavior as an empty `IntegrityCatalog` without creating or saving `catalog.json`.
- Implemented malformed JSON handling as a typed catalog error without overwriting the file.
- Added injectable catalog path behavior through `CatalogStore` constructor and default `%AppData%\FolderPrint\catalog.json` resolution through `CatalogPathProvider`.
- Added `CliRunner` to keep CLI dispatch/output separate from Core catalog behavior.
- Updated `Program.cs` to dispatch through `CliRunner`.
- Implemented `list` output for empty catalog and catalog-error exit mapping.
- Added xUnit coverage for missing catalog, malformed JSON preservation, empty list success, and malformed list catalog error.
- Confirmed no scanner, registration, verification, duplicate detection, refresh, unregister, V2 scope, or new external dependency was implemented.

### File List

- `src/FolderPrint.Core/Catalog/IntegrityCatalog.cs`
- `src/FolderPrint.Core/Catalog/CatalogLoadResult.cs`
- `src/FolderPrint.Core/Catalog/CatalogPathProvider.cs`
- `src/FolderPrint.Core/Catalog/CatalogStore.cs`
- `src/FolderPrint.Cli/CliRunner.cs`
- `src/FolderPrint.Cli/Program.cs`
- `tests/FolderPrint.Tests/Catalog/CatalogStoreTests.cs`
- `tests/FolderPrint.Tests/Cli/CliRunnerTests.cs`
- `_bmad-output/implementation-artifacts/1-3-load-an-empty-catalog-and-list-registered-folders.md`
- `docs/stories/story-004.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-08: Implemented Story 1.3 empty catalog loading, list dispatch, tests, and BMAD review status update.
- 2026-07-08: Created implementation-ready Story 1.3 and marked it ready for dev.




