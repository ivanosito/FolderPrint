---
storyId: "1.1"
storyKey: "1-1-create-solution-and-project-boundaries"
title: "Create Solution and Project Boundaries"
status: ready-for-dev
epic: "Epic 1: Runnable CLI Foundation"
created: 2026-07-07
updated: 2026-07-07
source:
  - "../../docs/product-brief.md"
  - "../../docs/prd.md"
  - "../../docs/architecture.md"
  - "../../docs/epics-and-stories.md"
  - "../../docs/sprint-plan-001.md"
  - "sprint-status.yaml"
---

# Story 1.1: Create Solution and Project Boundaries

Status: ready-for-dev

## Story

As a developer,  
I want the FolderPrint solution and project structure created,  
so that CLI, Core, and tests have clear dependency boundaries from the start.

## Context

FolderPrint is a .NET 8 console application that will register trusted folders, scan files recursively, calculate SHA-256 hashes, store baselines in a local JSON catalog, and later verify changes. This first story does not implement those product behaviors. It establishes the project skeleton required to implement them safely in later stories.

This is the first committed Sprint 001 story. There is currently no `FolderPrint.sln`, `src/`, or `tests/` implementation tree. The repository contains BMAD planning artifacts and project knowledge docs only.

## Scope

Create the greenfield .NET 8 solution foundation:

- `FolderPrint.sln` at repository root.
- `src/FolderPrint.Cli` as a .NET 8 console project.
- `src/FolderPrint.Core` as a .NET 8 class library.
- `tests/FolderPrint.Tests` as an xUnit test project.
- Solution membership for all three projects.
- Project reference from `FolderPrint.Cli` to `FolderPrint.Core`.
- Test project references needed to test the created projects.
- A minimal smoke test that proves `dotnet test` is wired.
- Verified build and test commands from repository root.

## Out of Scope

Do not implement:

- Manual command parser or exit-code constants. That is Story 1.2.
- Domain models or `FileHasher`. That is Story 2.1.
- `FolderScanner`, catalog persistence, registration, verification, duplicate detection, refresh, unregister, or list behavior.
- JSON catalog files or `%AppData%\FolderPrint\catalog.json`.
- GUI, SQLite, cloud sync, encryption, real-time monitoring, network-share support, complex ignore rules, or export-report behavior.
- Any V2 scope.

## Acceptance Criteria

1. Given a clean repository, when the solution is created, then `FolderPrint.sln` exists at the repository root.
2. Given the solution is created, when project folders are inspected, then `src/FolderPrint.Cli`, `src/FolderPrint.Core`, and `tests/FolderPrint.Tests` exist.
3. Given the projects are created, when their target frameworks are inspected, then all projects target .NET 8.
4. Given project references are configured, when references are inspected, then `FolderPrint.Cli` references `FolderPrint.Core`.
5. Given project references are configured, when references are inspected, then `FolderPrint.Core` does not reference `FolderPrint.Cli`.
6. Given the test project is configured, when references are inspected, then `FolderPrint.Tests` references the projects needed for smoke tests.
7. Given a minimal smoke test exists, when `dotnet test` runs from the repository root, then the test passes.
8. Given the solution is complete, when `dotnet restore` and `dotnet build` run from the repository root, then both commands succeed.

## Technical Notes

- Use .NET 8 and C#.
- Keep runtime dependencies minimal. Story 1.1 should only introduce standard .NET project scaffolding and normal xUnit test package dependencies.
- Preserve the architecture dependency direction: `FolderPrint.Cli` may depend on `FolderPrint.Core`; `FolderPrint.Core` must not depend on `FolderPrint.Cli` or `System.Console`.
- Recommended commands:

```powershell
dotnet new sln -n FolderPrint
dotnet new console -n FolderPrint.Cli -o src/FolderPrint.Cli --framework net8.0
dotnet new classlib -n FolderPrint.Core -o src/FolderPrint.Core --framework net8.0
dotnet new xunit -n FolderPrint.Tests -o tests/FolderPrint.Tests --framework net8.0
dotnet sln FolderPrint.sln add src/FolderPrint.Cli/FolderPrint.Cli.csproj
dotnet sln FolderPrint.sln add src/FolderPrint.Core/FolderPrint.Core.csproj
dotnet sln FolderPrint.sln add tests/FolderPrint.Tests/FolderPrint.Tests.csproj
dotnet add src/FolderPrint.Cli/FolderPrint.Cli.csproj reference src/FolderPrint.Core/FolderPrint.Core.csproj
dotnet add tests/FolderPrint.Tests/FolderPrint.Tests.csproj reference src/FolderPrint.Core/FolderPrint.Core.csproj
dotnet add tests/FolderPrint.Tests/FolderPrint.Tests.csproj reference src/FolderPrint.Cli/FolderPrint.Cli.csproj
```

- It is acceptable for the initial console project to retain or minimally adjust template-generated `Program.cs`; do not implement command behavior yet.
- It is acceptable for `FolderPrint.Core` to contain only template code or a minimal placeholder after this story; domain model work belongs to Story 2.1.
- Keep any smoke test intentionally small. Its purpose is to prove test infrastructure and project references, not to assert product behavior.

## Tasks

- [ ] Create `FolderPrint.sln`.
- [ ] Create `src/FolderPrint.Cli` as a .NET 8 console project.
- [ ] Create `src/FolderPrint.Core` as a .NET 8 class library.
- [ ] Create `tests/FolderPrint.Tests` as a .NET 8 xUnit project.
- [ ] Add all projects to `FolderPrint.sln`.
- [ ] Add `FolderPrint.Cli` -> `FolderPrint.Core` project reference.
- [ ] Add test project references required for smoke tests.
- [ ] Add or keep one minimal smoke test.
- [ ] Run restore, build, and test from repository root.
- [ ] Confirm no out-of-scope product behavior was implemented.

## Validation Commands

Run from repository root:

```powershell
dotnet restore
dotnet build
dotnet test
```

Optional structure/reference checks:

```powershell
dotnet sln FolderPrint.sln list
dotnet list src/FolderPrint.Cli/FolderPrint.Cli.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list tests/FolderPrint.Tests/FolderPrint.Tests.csproj reference
```

## Definition of Done

- All acceptance criteria pass.
- `dotnet restore`, `dotnet build`, and `dotnet test` succeed from repository root.
- Solution and project names match the architecture exactly.
- `FolderPrint.Core` has no dependency on `FolderPrint.Cli`.
- No V2 scope or V1 non-goal behavior is introduced.
- Sprint status for this story can move from `ready-for-dev` to `in-progress` when implementation starts.

## Dev Notes

### Architecture Compliance

- Follow AD-1: `FolderPrint.Cli` may depend on `FolderPrint.Core`; `FolderPrint.Core` must not depend on `FolderPrint.Cli` or `System.Console`.
- Follow AD-4: keep dependencies minimal.
- Follow the approved structure from architecture:

```text
FolderPrint.sln
src/
  FolderPrint.Cli/
  FolderPrint.Core/
tests/
  FolderPrint.Tests/
```

### Current Repository State

- No existing solution file was found.
- No existing `src/` or `tests/` implementation tree was found.
- Recent git history shows planning artifacts only; no implementation conventions have been established yet.

### Source References

- Product scope and V1 non-goals: `docs/product-brief.md`
- Functional and non-functional requirements: `docs/prd.md`
- Project structure and dependency direction: `docs/architecture.md`
- Story source and acceptance criteria: `docs/epics-and-stories.md`
- Sprint 001 scope: `docs/sprint-plan-001.md`
- Tracking status: `_bmad-output/implementation-artifacts/sprint-status.yaml`
- Current .NET CLI reference: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new

## Dev Agent Record

### Agent Model Used

TBD by dev agent.

### Debug Log References

TBD by dev agent.

### Completion Notes List

TBD by dev agent.

### File List

TBD by dev agent.
