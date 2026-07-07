---
storyId: "1.2"
storyKey: "1-2-parse-v1-commands-and-define-exit-codes"
title: "Parse V1 Commands and Define Exit Codes"
status: review
epic: "Epic 1: Runnable CLI Foundation"
created: 2026-07-07
updated: 2026-07-07
baseline_commit: "38d1dcfc88e41a6360e9e459f0ef2abd01975291"
source:
  - "../../docs/product-brief.md"
  - "../../docs/prd.md"
  - "../../docs/architecture.md"
  - "../../docs/epics-and-stories.md"
  - "../../docs/sprint-plan-001.md"
  - "sprint-status.yaml"
previousStory:
  - "1-1-create-solution-and-project-boundaries.md"
---

# Story 1.2: Parse V1 Commands and Define Exit Codes

Status: review

## Story

As a CLI user,  
I want invalid or incomplete commands to produce clear usage behavior,  
so that I can understand how to run FolderPrint and scripts can detect command errors.

## Context

Story 1.1 created the .NET 8 solution, CLI project, Core project, and xUnit test project. Story 1.2 is the next committed Sprint 001 story and should establish only the CLI command-shape foundation: a small manual parser, named exit-code constants, and tests that prove V1 commands are recognized or rejected consistently.

This story does not implement FolderPrint product behavior such as scanning, hashing, catalog persistence, registration, verification, duplicate detection, refresh, unregister, or real list output. It prepares the CLI layer so later stories can dispatch parsed commands to Core services without revisiting command names or exit-code semantics.

## Scope

Implement parser-only CLI foundation in `FolderPrint.Cli`:

- Add a small manual parser for the V1 command set.
- Recognize these commands exactly:
  - `register <folder>`
  - `verify <folder>`
  - `list`
  - `unregister <folder>`
  - `duplicates <folder>`
  - `refresh <folder>`
- Validate required argument shape:
  - `list` accepts no folder argument.
  - `register`, `verify`, `unregister`, `duplicates`, and `refresh` require exactly one `<folder>` argument.
  - Unknown commands, missing required folders, empty command input, and extra arguments produce a usage-error result.
- Add named `ExitCodes` constants in `FolderPrint.Cli` using the architecture values.
- Add minimal usage text or usage-result data needed for invalid command behavior.
- Update `Program.cs` only enough to call the parser and return deterministic exit codes for parser success vs usage error.
- Add focused parser and exit-code tests.
- Add `FolderPrint.Tests` -> `FolderPrint.Cli` project reference if needed for CLI parser tests.

## Out of Scope

Do not implement:

- Folder path existence checks or filesystem validation.
- Catalog loading, catalog creation, or JSON persistence.
- `list` catalog behavior; Story 1.3 owns empty catalog load/list behavior.
- Command dispatch to Core services.
- `FolderScanner`, `FileHasher`, `IntegrityCatalog`, `VerificationService`, `DuplicateFinder`, `ReportFormatter`, or domain models.
- Registration, verification, duplicate detection, refresh, unregister, or real report formatting.
- `System.CommandLine` or any other CLI framework dependency.
- GUI, SQLite, cloud sync, encryption, real-time monitoring, network-share support, complex ignore rules, export-report, or other V2 scope.

## Acceptance Criteria

1. Given the V1 command set, when the parser receives `register <folder>`, then it returns a successful parsed command for `register` with the supplied folder argument.
2. Given the V1 command set, when the parser receives `verify <folder>`, then it returns a successful parsed command for `verify` with the supplied folder argument.
3. Given the V1 command set, when the parser receives `list`, then it returns a successful parsed command for `list` without requiring a folder argument.
4. Given the V1 command set, when the parser receives `unregister <folder>`, `duplicates <folder>`, or `refresh <folder>`, then it returns a successful parsed command with the supplied folder argument.
5. Given invalid input, when the parser receives an unknown command, empty argument list, missing required folder argument, or extra unexpected arguments, then it returns a usage-error result.
6. Given exit-code constants are needed by automation, when `ExitCodes` is inspected, then it defines named constants for `Success`, `DifferencesFound`, `UsageError`, `NotFound`, `CatalogError`, `ScanError`, and `UnexpectedError`.
7. Given architecture exit-code guidance, when constants are inspected, then values are `Success = 0`, `DifferencesFound = 1`, `UsageError = 2`, `NotFound = 3`, `CatalogError = 4`, `ScanError = 5`, and `UnexpectedError = 10`.
8. Given parser behavior is implemented, when CLI entry behavior is exercised for an unknown command or missing required folder argument, then it returns `ExitCodes.UsageError`.
9. Given parser behavior is implemented, when `dotnet build`, `dotnet test`, and `dotnet run --project src/FolderPrint.Cli -- list` run from the repository root, then they succeed without requiring catalog, scanner, or domain model implementation.

## Technical Notes

- Use .NET 8 and C#.
- Keep the parser manual for V1. Do not add `System.CommandLine`.
- Keep parser concerns in `FolderPrint.Cli`; do not put command parsing or exit-code concerns in `FolderPrint.Core`.
- `FolderPrint.Core` must not reference `FolderPrint.Cli`, `System.Console`, or CLI parsing types.
- Accept command names in lowercase exactly as listed for V1. Do not add aliases, short flags, shell completion, help options, nested commands, or options parsing in this story.
- The parser should operate on `string[] args` so tests can avoid shell-specific quoting behavior.
- Treat a folder path containing spaces as one argument when already supplied as one `string` in `args`.
- Treat extra arguments as usage errors in V1 to avoid ambiguous parsing.
- Usage text should be concise and deterministic, but tests should avoid brittle assertions against the full prose. Prefer tests against result type, command name, folder argument, and exit-code mapping.
- It is acceptable for recognized commands to return parser-level success only. Do not fake catalog/list/register/verify behavior.
- Suggested CLI files:
  - `src/FolderPrint.Cli/CommandParser.cs`
  - `src/FolderPrint.Cli/ParsedCommand.cs`
  - `src/FolderPrint.Cli/CommandKind.cs`
  - `src/FolderPrint.Cli/CommandParseResult.cs`
  - `src/FolderPrint.Cli/ExitCodes.cs`
- Suggested test location:
  - `tests/FolderPrint.Tests/Cli/CommandParserTests.cs`
  - `tests/FolderPrint.Tests/Cli/ExitCodesTests.cs`

## Tasks

- [x] Add CLI parser types in `src/FolderPrint.Cli` for parsed commands and usage errors. (AC: 1-5)
- [x] Add `ExitCodes` constants in `src/FolderPrint.Cli`. (AC: 6-7)
- [x] Update `Program.cs` only enough to call the parser, write minimal usage/error output for usage errors, and return parser-level exit codes. (AC: 8-9)
- [x] Add or update the test project reference so `FolderPrint.Tests` can test `FolderPrint.Cli` parser types. (AC: 1-8)
- [x] Add parser tests for all six valid V1 commands. (AC: 1-4)
- [x] Add parser tests for unknown command, empty input, missing folder argument, and extra arguments. (AC: 5)
- [x] Add exit-code tests for all required named constants and numeric values. (AC: 6-7)
- [x] Run build and test validation from the repository root. (AC: 9)
- [x] Confirm no catalog, scanner, hashing, domain model, command execution, or V2 behavior was implemented. (AC: 9)

## Validation Commands

Run from repository root:

```powershell
dotnet build
dotnet test
dotnet run --project src/FolderPrint.Cli -- list
```

Optional parser smoke checks:

```powershell
dotnet run --project src/FolderPrint.Cli -- unknown-command
dotnet run --project src/FolderPrint.Cli -- register
```

Expected validation interpretation:

- `dotnet build` succeeds.
- `dotnet test` succeeds.
- `dotnet run --project src/FolderPrint.Cli -- list` exits successfully because `list` is a recognized parser-level command.
- Unknown or incomplete commands exit with `ExitCodes.UsageError`; PowerShell may show the non-zero exit through `$LASTEXITCODE`.

## Definition of Done

- All acceptance criteria pass.
- Parser tests cover all V1 command names and invalid argument shapes.
- Exit-code constants exist with architecture-approved names and values.
- `Program.cs` maps parser usage errors to `ExitCodes.UsageError`.
- `dotnet build` and `dotnet test` pass from the repository root.
- `dotnet run --project src/FolderPrint.Cli -- list` succeeds without requiring catalog behavior.
- No new runtime dependencies are added.
- No `System.CommandLine` dependency is added.
- `FolderPrint.Core` remains free of CLI dependencies and console concerns.
- No Story 2.1 or later product behavior is introduced.

## Dev Notes

### Architecture Compliance

- Follow AD-1: `FolderPrint.Cli` may depend on `FolderPrint.Core`; `FolderPrint.Core` must not depend on `FolderPrint.Cli` or `System.Console`.
- Follow AD-4: keep dependencies minimal. xUnit test dependencies already exist from Story 1.1; avoid new runtime dependencies.
- Follow AD-5: use a small manual CLI parser for V1 commands. Do not introduce a CLI framework.
- Follow the architecture exit-code taxonomy from `docs/architecture.md#Exit Code Strategy`.
- CLI code may write concise usage output. Core code must not write to `Console`.

### Current Repository State

- Story 1.1 is done.
- `FolderPrint.sln` exists and includes `FolderPrint.Cli`, `FolderPrint.Core`, and `FolderPrint.Tests`.
- `FolderPrint.Cli` currently contains only the template `Program.cs` and references `FolderPrint.Core`.
- `FolderPrint.Core` currently contains only template `Class1.cs` and has no project references.
- `FolderPrint.Tests` currently references `FolderPrint.Core` and contains a project-reference smoke test.
- Story 1.2 may add a test reference from `FolderPrint.Tests` to `FolderPrint.Cli` to test CLI parser and exit-code types.

### Previous Story Intelligence

Story 1.1 established these patterns:

- Keep implementation minimal and scoped to the story.
- Use standard .NET 8 project structure and xUnit.
- Run `dotnet restore`, `dotnet build`, and `dotnet test` from repository root when validating.
- Clean generated `bin`/`obj` outputs after validation if they appear as untracked files and no `.gitignore` exists.
- Avoid implementing future components early.

### Testing Guidance

- Prefer direct unit tests of parser result objects over full console text assertions.
- Include command argument arrays directly in tests, for example `new[] { "register", "C:\\Data Folder" }`.
- Verify valid commands preserve the folder argument exactly as supplied.
- Verify invalid commands produce a usage-error result and CLI entry behavior maps that to `ExitCodes.UsageError`.
- Do not rely on real folders, `%AppData%`, catalog files, or filesystem permissions in this story.

### Source References

- V1 command scope and non-goals: `docs/product-brief.md#V1 Scope`, `docs/product-brief.md#Non-Goals`
- CLI parsing and exit-code requirements: `docs/prd.md#7.7 CLI Behavior`, `docs/prd.md#FR-19 Parse V1 commands manually`, `docs/prd.md#FR-20 Produce automation-friendly exit codes`
- Architecture dependency and exit-code rules: `docs/architecture.md#Architecture Overview`, `docs/architecture.md#Exit Code Strategy`, `docs/architecture.md#Architecture Decisions`
- Story source: `docs/epics-and-stories.md#Story 1.2 Parse V1 Commands and Define Exit Codes`
- Sprint scope: `docs/sprint-plan-001.md#Story 1.2 Parse V1 Commands and Define Exit Codes`
- Previous story artifact: `_bmad-output/implementation-artifacts/1-1-create-solution-and-project-boundaries.md`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test` red phase failed before parser types existed.
- `dotnet restore`
- `dotnet build`
- `dotnet test`
- `dotnet run --project src/FolderPrint.Cli -- list`
- `dotnet run --project src/FolderPrint.Cli -- unknown-command`
- `dotnet run --project src/FolderPrint.Cli -- register`
- `dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference`
- `dotnet list tests/FolderPrint.Tests/FolderPrint.Tests.csproj reference`
- Scope scan for out-of-story components and dependencies.

### Completion Notes List

- Added a minimal manual parser for the six V1 command names and required argument shapes.
- Added parser result, parsed command, command kind, and exit-code types in `FolderPrint.Cli`.
- Updated `Program.cs` to return parser-level success or usage-error exit codes without dispatching product behavior.
- Added CLI parser and exit-code tests covering valid commands, invalid shapes, and architecture-approved exit-code values.
- Added `FolderPrint.Tests` -> `FolderPrint.Cli` project reference for CLI parser tests.
- Verified restore, build, tests, CLI smoke checks, project references, and scope guardrails.
- Confirmed no catalog, scanner, hashing, domain model, command execution, or V2 behavior was implemented.

### File List

- `src/FolderPrint.Cli/CommandKind.cs`
- `src/FolderPrint.Cli/CommandParseResult.cs`
- `src/FolderPrint.Cli/CommandParser.cs`
- `src/FolderPrint.Cli/ExitCodes.cs`
- `src/FolderPrint.Cli/ParsedCommand.cs`
- `src/FolderPrint.Cli/Program.cs`
- `tests/FolderPrint.Tests/FolderPrint.Tests.csproj`
- `tests/FolderPrint.Tests/Cli/CommandParserTests.cs`
- `tests/FolderPrint.Tests/Cli/ExitCodesTests.cs`
- `_bmad-output/implementation-artifacts/1-2-parse-v1-commands-and-define-exit-codes.md`
- `docs/stories/story-002.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-07: Implemented Story 1.2 manual CLI parser, exit-code constants, parser-level Program wiring, tests, and BMAD status updates.