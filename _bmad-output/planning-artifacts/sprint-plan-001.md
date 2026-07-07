---
title: "Sprint Plan 001: FolderPrint Foundation"
status: final
created: 2026-07-07
updated: 2026-07-07
source: "epics.md"
tracking: "../implementation-artifacts/sprint-status.yaml"
---

# Sprint Plan 001: FolderPrint Foundation

## Sprint Goal

Establish the FolderPrint .NET 8 solution foundation safely: create the solution and project boundaries, confirm build/test commands, add project references, and begin the manual CLI foundation without implementing the full application.

This sprint deliberately avoids catalog persistence, recursive scanning, verification, duplicate detection, GUI, SQLite, cloud sync, encryption, real-time monitoring, network-share guarantees, complex ignore rules, and V2 reporting scope.

## Selected Stories

### Committed

1. Story 1.1: Create Solution and Project Boundaries
2. Story 1.2: Parse V1 Commands and Define Exit Codes

### Stretch

3. Story 2.1: Implement Domain Models and SHA-256 Hashing

Story 2.1 is stretch only. It should start only after Story 1.1 and Story 1.2 are complete, tests pass, and the CLI/Core boundary is clean. If started, keep it limited to domain models and `FileHasher`; do not implement scanning, catalog persistence, registration, or verification.

## Story Order

1. Story 1.1: Create Solution and Project Boundaries
2. Story 1.2: Parse V1 Commands and Define Exit Codes
3. Story 2.1: Implement Domain Models and SHA-256 Hashing, stretch

## Story 1.1: Create Solution and Project Boundaries

As a developer, I want the FolderPrint solution and project structure created, so that CLI, Core, and tests have clear dependency boundaries from the start.

### Acceptance Criteria

- `FolderPrint.sln` exists at the repository root.
- `src/FolderPrint.Cli` exists as a .NET 8 console project.
- `src/FolderPrint.Core` exists as a .NET 8 class library.
- `tests/FolderPrint.Tests` exists as an xUnit test project.
- `FolderPrint.Cli` references `FolderPrint.Core`.
- `FolderPrint.Tests` references the projects needed for tests.
- `FolderPrint.Core` does not reference `FolderPrint.Cli`.
- The solution builds.
- The test project runs at least one smoke test.

### Tasks

- Create solution file.
- Create `FolderPrint.Cli` console project.
- Create `FolderPrint.Core` class library.
- Create `FolderPrint.Tests` xUnit project.
- Add all projects to the solution.
- Add `FolderPrint.Cli` -> `FolderPrint.Core` reference.
- Add test project references to the relevant projects.
- Add a minimal smoke test proving the test runner is wired.
- Confirm no runtime dependencies beyond the default .NET SDK packages and xUnit test packages.

### Validation Commands

```powershell
dotnet restore
dotnet build
dotnet test
```

## Story 1.2: Parse V1 Commands and Define Exit Codes

As a CLI user, I want invalid or incomplete commands to produce clear usage behavior, so that I can understand how to run FolderPrint and scripts can detect command errors.

### Acceptance Criteria

- Manual parser recognizes `register`, `verify`, `list`, `unregister`, `duplicates`, and `refresh`.
- Parser validates required `<folder>` arguments for all commands that need them.
- `list` is accepted without a folder argument.
- Unknown commands return a usage error result.
- Missing required folder arguments return a usage error result.
- `ExitCodes` defines named constants for success, differences found, usage error, not found, catalog error, scan error, and unexpected error.
- CLI parsing tests do not depend on full console prose.

### Tasks

- Add CLI parsing type or small parser class in `FolderPrint.Cli`.
- Add command result shape for parsed command vs usage error.
- Add `ExitCodes` constants.
- Add usage text only as needed to support error behavior.
- Add parser tests for all six valid commands.
- Add parser tests for unknown command and missing folder arguments.
- Keep parser manual; do not add `System.CommandLine` in Sprint 001.

### Validation Commands

```powershell
dotnet build
dotnet test
dotnet run --project src/FolderPrint.Cli -- list
```

## Story 2.1: Implement Domain Models and SHA-256 Hashing

Stretch story only.

As a user who trusts a folder, I want each file to have a stable content fingerprint, so that later verification can compare file contents reliably.

### Acceptance Criteria

- `RegisteredFolder`, `FileFingerprint`, `FolderSnapshot`, `VerificationResult`, `FileChange`, and `FileChangeType` exist in `FolderPrint.Core`.
- `FileHasher` computes lowercase hex SHA-256 using `System.Security.Cryptography.SHA256`.
- No third-party hashing dependency is introduced.
- SHA-256 output is covered by a known-value unit test.

### Tasks

- Add domain model files under `FolderPrint.Core`.
- Add `FileChangeType` enum values required by V1.
- Add `FileHasher`.
- Add unit test for SHA-256 known value.
- Avoid implementing `FolderScanner`, `CatalogStore`, registration, verification, or duplicate detection in this sprint.

### Validation Commands

```powershell
dotnet build
dotnet test
```

## Sprint-Wide Validation Commands

Run from repository root:

```powershell
dotnet restore
dotnet build
dotnet test
```

Optional CLI smoke checks after Story 1.2:

```powershell
dotnet run --project src/FolderPrint.Cli -- list
dotnet run --project src/FolderPrint.Cli -- unknown-command
```

## Risks

- The repo currently contains planning artifacts but no .NET solution; Story 1.1 may expose local SDK or template issues.
- Parser scope can creep into command execution. Keep Sprint 001 parser-only; do not implement catalog, scanner, register, verify, refresh, or duplicates behavior yet.
- Domain models can become over-designed. If Story 2.1 is started, model only the fields already approved in PRD and architecture.
- `dotnet test` may require NuGet package restore for xUnit. Keep dependencies minimal and standard.

## Definition of Done

- Selected committed stories meet their acceptance criteria.
- `dotnet restore`, `dotnet build`, and `dotnet test` pass from repository root.
- `FolderPrint.Core` has no dependency on `FolderPrint.Cli`.
- No V2 scope has been introduced.
- No GUI, SQLite, cloud sync, encryption, real-time monitoring, network-share guarantee, complex ignore rule support, or export-report behavior exists.
- Sprint status can be updated from backlog to ready/in-progress/review/done by story lifecycle.

## Not In Sprint 001

- Recursive folder scanning.
- JSON catalog persistence.
- `register` execution behavior.
- `verify` execution behavior.
- `duplicates` execution behavior.
- `refresh` execution behavior.
- `unregister` execution behavior.
- Report export.
- GUI or any non-CLI surface.
