---
storyId: "4.1"
storyKey: "4-1-display-registered-folder-metadata"
title: "Display Registered Folder Metadata"
status: review
baseline_commit: f388df856ecc7e5f24a4fd5f84be6c1191900ae6
epic: "Epic 4: Manage Registered Folders and Baselines"
created: 2026-07-15
updated: 2026-07-15
sprint: "Sprint 006 committed"
previousStory: "3-4-wire-verify-folder-command-and-reporting.md"
source:
  - "../../docs/epics-and-stories.md"
  - "../../docs/sprint-plan-006.md"
  - "../../docs/architecture.md"
  - "sprint-status.yaml"
  - "epic-3-retro-2026-07-15.md"
  - "../../docs/retrospectives/sprint-005-retrospective.md"
---

# Story 4.1: Display Registered Folder Metadata

Status: review

## Story

As a CLI user, I want `folderprint list` to show registered folders and metadata, so that I can see what FolderPrint is tracking.

## Context

Story 4.1 is the committed outcome for Sprint 006 and the first story in Epic 4. The current `list` path already loads the JSON catalog, returns `CatalogError` for malformed top-level JSON, preserves the successful missing/empty-catalog message, and prints stored root paths for a non-empty catalog. This story enriches only the non-empty read path.

Epic 3 established two guardrails that apply directly here: persisted catalog data is untrusted, and deterministic output needs a complete ordinal ordering contract. Listing must therefore validate every stored registration before display, format metadata through a console-free reporting seam, and remain byte-for-byte read-only. It must not normalize the displayed value, inspect the registered root, scan, verify, update timestamps, or save the catalog.

Creating this artifact completes the Epic 3 learning-transfer action for Story 4.1. The separate conflict-aware mutation action remains open and gates Stories 4.2 and 4.3.

## Scope

- Extend the existing `folderprint list` behavior for a valid non-empty catalog.
- Display each registration's `Id`, stored `RootPath`, `CreatedAtUtc`, nullable `LastVerifiedAtUtc`, and baseline file count from `Files.Count`.
- Define deterministic record ordering, fixed field ordering, invariant UTC timestamp rendering, and a stable never-verified token.
- Reuse `CatalogStore.Load`; preserve existing missing/empty and malformed-catalog exit behavior.
- Validate persisted registrations consistently with the V1 validation rules established by Story 3.4.
- Keep output transformation independently testable and console-free in Core.
- Add focused formatting, CLI, catalog-validation, read-only, and regression tests.

## Out of Scope

- `unregister <folder>`, catalog removal, or any other catalog mutation.
- `refresh <folder>`, baseline replacement, acceptance of drift, or timestamp updates.
- Standalone `duplicates <folder>` behavior or `DuplicateFinder`.
- Scanning, hashing, verification, root existence/access checks, path rewriting, or separator-policy changes.
- Parser grammar, command names, existing exit-code values, JSON schema, or domain constructor changes.
- GUI, database/SQLite, cloud sync, encryption, real-time monitoring, network-share guarantees, export reporting, advanced ignore rules, dependencies, or V2 features.

## Acceptance Criteria

1. Given a valid catalog containing one or more registered folders, when the user runs `folderprint list`, then every registration is displayed with these fields in fixed order: `Id`, `Path` (the stored `RootPath`), `Registered` (the registration timestamp), `Last verified`, and `Baseline files` (the stored `Files.Count`); the command writes no error and returns `ExitCodes.Success` (`0`).
2. Timestamps are rendered after `ToUniversalTime()` with the invariant round-trip `"O"` format. A null `LastVerifiedAtUtc` renders exactly `Never`; no current/default timestamp is invented.
3. Folder records are ordered by stored `RootPath` using `StringComparer.Ordinal`, with `Id` as an ordinal defensive tie-breaker. Field order and record separators are fixed. Shuffling valid catalog input produces byte-identical output, and formatting does not mutate or reorder the input collection.
4. A missing catalog or a valid empty catalog preserves the existing output `No folders are registered.`, writes no error, returns `Success`, and does not create or rewrite the catalog.
5. Invalid JSON, JSON `null`, a missing/null `registeredFolders` collection, null registrations, blank required registration fields, null/invalid fingerprints, unsafe fingerprint paths, duplicate ordinal fingerprint paths, invalid stored roots, or duplicate normalized registered roots produce `CatalogError` (`4`). No partial/success listing is written and original catalog bytes remain unchanged.
6. Listing is strictly read-only: it calls no catalog save path; performs no root validation, filesystem enumeration, scan, hash, or verification; updates no timestamp or baseline; and does not touch target files. A registration whose physical root is missing or inaccessible still lists successfully from valid stored metadata.
7. Metadata transformation is a reusable Core reporting operation returning display-ready lines/data without `Console`, `TextWriter`, `ExitCodes`, or a Core-to-CLI reference. CLI owns catalog sequencing, writer calls, and exit-code mapping.
8. Existing missing/empty list behavior, malformed-catalog mapping, `register`, `verify`, parser, catalog persistence, scanner, and verification tests remain passing. Tests use temporary catalog paths and never touch the real AppData catalog.
9. No unregister, refresh, standalone duplicates command, `DuplicateFinder`, external runtime dependency, later-story behavior, or V2 feature is implemented.

## Output Contract

- Header for a non-empty catalog: `Registered folders:`.
- Each registration is rendered as a fixed metadata block with labels in this order:
  1. `Id`
  2. `Path`
  3. `Registered`
  4. `Last verified`
  5. `Baseline files`
- Use the stored `RootPath` exactly for display; normalization is allowed only inside shared catalog validation and must not rewrite output or access the target filesystem.
- Render `CreatedAtUtc` and non-null `LastVerifiedAtUtc` as UTC invariant round-trip values.
- Render null `LastVerifiedAtUtc` as `Never`.
- `Baseline files` is the count of stored fingerprints only. Do not compute live file counts, total bytes, hash summaries, or freshness.
- Keep block separators fixed and platform-independent; do not build a width-dependent console table.
- Empty/missing catalogs retain the exact established empty message rather than the non-empty header.

## Tasks / Subtasks

- [x] Add shared validation needed for safe catalog-wide display. (AC: 5-7)
  - [x] Extract or reuse Story 3.4 registration/fingerprint validation so list and verify cannot drift; do not call `RegisteredFolderLookup.Find` with a fabricated path or copy a weaker validation rule.
  - [x] Validate all registration entries and detect duplicate normalized registered roots without probing the filesystem.
  - [x] Return a typed/explicit validation failure that `CliRunner` can map to `CatalogError`; never throw malformed persisted data into `UnexpectedError`.
  - [x] Preserve `CatalogStore.Load` missing-file-as-empty behavior and do not add a save path.
- [x] Add deterministic Core metadata formatting. (AC: 1-3, 7)
  - [x] Extend `ReportFormatter` with a dedicated registered-folder operation, or add an equivalently narrow type under `FolderPrint.Core/Reporting`; do not format metadata with `Console` in Core.
  - [x] Sort independently materialized records by stored root path ordinally, then Id ordinally.
  - [x] Render the fixed field contract, invariant UTC `"O"` timestamps, `Never`, and stored baseline count.
  - [x] Preserve input collections and avoid culture, timezone, terminal-width, and current-time dependencies.
- [x] Wire validated metadata output through the existing list adapter. (AC: 1, 4-8)
  - [x] Keep `RunList` sequencing as load -> validate -> format -> write -> `Success`.
  - [x] Preserve current missing/empty behavior and expected catalog-error output separation.
  - [x] Do not call `Save`, `SaveIfUnchanged`, path normalization for display, root/file APIs, scanner, hasher, or verifier.
  - [x] Leave parser, command dispatch, register, and verify behavior unchanged.
- [x] Add focused and regression tests. (AC: 1-9)
  - [x] Cover one folder with every field, an empty baseline, non-zero timestamp offsets rendered in UTC, and null last verification rendered as `Never`.
  - [x] Compare whole formatter output for shuffled multiple-folder inputs and assert input order/objects remain unchanged.
  - [x] Cover missing and existing-empty catalogs with the established message and no catalog creation/mutation.
  - [x] Cover invalid nested registrations/fingerprints and duplicate normalized registrations as `CatalogError`, stderr-only, with original bytes preserved.
  - [x] Use missing registered roots plus throwing/counted injected verification scan/compare delegates to prove list does not inspect, scan, or verify them.
  - [x] Compare catalog bytes and loaded metadata before/after successful non-empty list; assert target files, if present, remain unchanged.
  - [x] Preserve existing register/list and all Story 3.4 verify regression coverage.
- [x] Run full validation and scope checks before marking tasks complete. (AC: 7-9)

## Dev Notes

### Current Implementation Seams to Reuse

- `src/FolderPrint.Cli/CliRunner.cs`: `RunList()` already loads through `CatalogStore.Load`, maps load failure to `CatalogError`, preserves the empty message, and writes non-empty output. Update this narrow branch; do not create a new dispatcher or catalog reader.
- `src/FolderPrint.Core/Catalog/CatalogStore.cs`: missing file returns `IntegrityCatalog.Empty`; malformed JSON and null/missing `registeredFolders` return typed catalog failure. Story 4.1 calls only `Load()`.
- `src/FolderPrint.Core/Catalog/RegisteredFolderLookup.cs`
- `src/FolderPrint.Core/Reporting/ReportFormatter.cs`: contains reviewed validation for registrations, fingerprints, hashes, safe relative paths, and duplicate fingerprint paths, but its validator is private and lookup-specific. Extract/reuse validation rather than duplicating it.
- `src/FolderPrint.Core/Models/RegisteredFolder.cs`: already exposes every required field; `Files.Count` is the only baseline summary required. No model or schema change is needed.
- `src/FolderPrint.Core/Reporting/ReportFormatter.cs`: already provides deterministic console-free verification formatting. A dedicated registered-folder formatter operation fits AD-6 without changing verification output.
- `tests/FolderPrint.Tests/Cli/CliRunnerTests.cs`: existing tests pin missing-catalog, malformed JSON, and register-then-list behavior.
- `tests/FolderPrint.Tests/Reporting/ReportFormatterTests.cs`: add focused pure formatting tests here unless a dedicated formatter type justifies a matching test file.
- `tests/FolderPrint.Tests/Catalog/RegisteredFolderLookupTests.cs` and/or a new focused validator test file should pin any extracted shared validation behavior.

### Read-Only and Error Guardrails

- Do not use `RegisteredFolderLookup.Find` with a fake requested path to validate the catalog.
- Do not normalize or canonicalize the displayed root; users must see the stored value.
- Validation may use the established pure V1 path normalization rule to detect invalid/duplicate stored roots, but it must not call `File`, `Directory`, scanner, or verifier APIs.
- Never skip malformed entries and print a partial list. Validation failure produces `CatalogError`, stderr only.
- Never save a catalog to “repair” malformed data.
- A missing physical root is valid for listing because list reports catalog state, not filesystem state.
- Do not add live file size, hash, existence, verification state, or freshness calculations.
- Keep the existing empty message exactly stable for Story 1.3 regression compatibility.

### Architecture Compliance

- Target .NET 8/C# and platform libraries only.
- Dependency direction remains `FolderPrint.Cli -> FolderPrint.Core`; Core must not reference CLI, `ExitCodes`, `TextWriter`, or `System.Console`.
- AD-3 keeps the V1 catalog as human-inspectable JSON; Story 4.1 performs no write.
- AD-4 forbids dependency growth without an architecture update; this story needs no package or framework.
- AD-5 preserves the manual parser; `list` is already parsed and dispatched.
- AD-6 requires typed/domain data before console formatting and a separate display-ready transformation.
- AD-7 requires temporary injected catalog paths in tests.
- No web research is required: the architecture pins .NET 8 platform APIs, `System.Text.Json`, and xUnit, and the story adds no dependency.

### Expected File Scope

- Update `src/FolderPrint.Cli/CliRunner.cs`.
- Update `src/FolderPrint.Core/Reporting/ReportFormatter.cs` or add one narrow registered-folder formatter under `Reporting/`.
- Update `src/FolderPrint.Core/Catalog/RegisteredFolderLookup.cs` only if extracting shared validation; add a narrow validator/result under `Catalog/` if needed.
- Update `tests/FolderPrint.Tests/Cli/CliRunnerTests.cs`.
- Update/add focused tests under `tests/FolderPrint.Tests/Reporting/` and `Catalog/`.
- Do not modify `CommandParser.cs`, `CommandKind.cs`, `ExitCodes.cs`, scanner, hasher, verifier, registration service, catalog save behavior, domain constructor shapes, project files, or package references without a failing test and a direct Story 4.1 necessity.

### Testing Requirements

- Continue xUnit and `MethodOrScenario_Condition_ExpectedOutcome` naming.
- Use fixed `DateTimeOffset` inputs, including a non-zero offset, and assert exact invariant UTC output.
- Prefer exact pure formatter line assertions and structural CLI assertions; avoid terminal-width-sensitive formatting.
- Prove permutation invariance with whole-output equality and prove formatter input immutability separately.
- Use valid 64-character SHA-256 fixtures when persisted catalog validation is involved.
- For read-only checks, compare raw catalog bytes before/after; do not rely only on re-serialization equality.
- Inject catalog paths and verification delegates; never use the default AppData path.
- The reviewed baseline is 131 passing tests. Full regression count must remain at least 131 plus new Story 4.1 coverage.

### Previous Story and Retrospective Intelligence

- Story 3.4 separated catalog load/validation, CLI orchestration, Core reporting, writer output, and typed exit mapping. Reuse that separation.
- Story 3.4 review found optimistic concurrency, null catalog structure, traversal-race classification, malformed fingerprints, unsafe paths, duplicate paths, and final ordering tie-breakers. Story 4.1 directly carries forward persisted-state validation and full determinism.
- The Epic 3 retrospective requires trust-boundary validation, conflict-aware future mutations, lossless typed contracts, and complete tie-breakers.
- The Sprint 005 retrospective recommends Story 4.1 remain deterministic, strictly read-only, compatible with empty/malformed catalogs, and proven not to scan or mutate.
- Story 4.2 remains backlog and cannot start until Story 4.1 is done and reviewed. The conflict-aware mutation action remains open for Stories 4.2/4.3.

### Git Intelligence Summary

- `f388df8` created the finalized Sprint 006 plan and is this story's implementation baseline.
- `d74a98b` recorded the Sprint 005 retrospective and kept Story 4.1 backlog.
- `bbdac01` completed the Epic 3 retrospective and recorded the learning-transfer/mutation action items.
- `ce0c9cb` applied Story 3.4 review corrections and marked it done.
- Recent work preserves role-based Core folders, matching test folders, no runtime dependency growth, and synchronized story/tracking metadata.

### References

- [Source: docs/epics-and-stories.md#Story-41-Display-Registered-Folder-Metadata]
- [Source: docs/sprint-plan-006.md#Story-41-Display-Registered-Folder-Metadata]
- [Source: docs/sprint-plan-006.md#Definition-of-Done]
- [Source: docs/architecture.md#List]
- [Source: docs/architecture.md#Component-Responsibilities]
- [Source: docs/architecture.md#Error-Handling-Strategy]
- [Source: docs/architecture.md#Architecture-Decisions]
- [Source: _bmad-output/implementation-artifacts/epic-3-retro-2026-07-15.md#Recommendation-for-Epic-4]
- [Source: docs/retrospectives/sprint-005-retrospective.md#Recommended-Next-Story]
- [Source: _bmad-output/implementation-artifacts/3-4-wire-verify-folder-command-and-reporting.md]
- [Source: src/FolderPrint.Cli/CliRunner.cs]
- [Source: src/FolderPrint.Core/Catalog/CatalogStore.cs]
- [Source: src/FolderPrint.Core/Catalog/RegisteredFolderLookup.cs]
- [Source: src/FolderPrint.Core/Models/RegisteredFolder.cs]
- [Source: src/FolderPrint.Core/Reporting/ReportFormatter.cs]

## Validation Commands

```powershell
dotnet restore
dotnet build FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release
dotnet test FolderPrint.sln --configuration Release --no-build --filter "FullyQualifiedName~CliRunnerTests|FullyQualifiedName~ReportFormatter|FullyQualifiedName~Catalog"
dotnet format FolderPrint.sln --verify-no-changes --no-restore
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj reference
dotnet list src/FolderPrint.Core/FolderPrint.Core.csproj package
git diff --check
```

Scope and boundary inspection:

```powershell
Select-String -Path src/FolderPrint.Core/**/*.cs -Pattern 'System\.Console|FolderPrint\.Cli|ExitCodes|TextWriter'
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'Refresh\(|Unregister\(|DuplicateFinder|SQLite|Sqlite|System\.CommandLine|export-report|cloud|encryption|monitoring'
git diff -- src/FolderPrint.Cli src/FolderPrint.Core tests/FolderPrint.Tests
```

The excluded-scope scan is an inspection aid because parser symbols/tests may already mention later V1 commands. Review only changed/new behavior.

## Definition of Done

- All acceptance criteria pass and tasks are checked only after validation evidence exists.
- Non-empty `list` shows every required metadata field and stored baseline count with the pinned deterministic contract.
- Missing/empty catalog behavior remains unchanged; malformed persisted state maps to `CatalogError` without partial output or byte changes.
- Listing remains strictly read-only and works without accessing registered roots.
- Metadata transformation is independently testable and Core remains free of CLI/console/exit concerns.
- Existing register, verify, parser, catalog, scanner, and verification behavior remains passing.
- No unregister, refresh, standalone duplicates, `DuplicateFinder`, dependency, project-file, or V2 scope enters implementation.
- Restore, Release build, focused/full tests, formatting, dependency checks, scope inspection, and `git diff --check` pass.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Implementation Plan

- Extract Story 3.4 persisted registration and fingerprint checks into a shared pure catalog validator, then route lookup and list through it.
- Add deterministic, console-free registered-folder metadata formatting with complete ordinal ordering and invariant UTC timestamps.
- Wire the existing `list` branch as load, validate, format, and write without catalog saves or target-folder access.
- Add focused Core and CLI coverage for deterministic output, malformed persisted state, read-only behavior, and register/verify regressions.

### Debug Log References

- RED (catalog validation): focused tests failed to compile because CatalogValidator did not exist.
- GREEN (catalog validation): 22 focused validator and lookup tests passed after extracting shared validation and duplicate normalized-root detection.
- RED (metadata reporting): focused tests failed to compile because FormatRegisteredFolders did not exist.
- GREEN (metadata reporting): 7 registered-folder and existing verification formatter tests passed with exact deterministic output and input immutability coverage.
- RED (CLI list adapter): 3 of 4 focused tests failed because list still printed only paths and accepted malformed semantic catalog data.
- GREEN (CLI list adapter): 27 list, register/list, and verify tests passed after wiring load, validation, formatting, and writer output without any save or target-root access.
- Final validation: restore succeeded; Release build succeeded with 0 warnings and 0 errors; all 146 tests passed; the 55-test focused suite, formatting, Core dependency/boundary, excluded-scope, and diff checks passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added a typed, filesystem-independent catalog validator shared by registered-folder lookup and future list display.
- Catalog validation now rejects malformed registrations, fingerprints, unsafe/duplicate relative paths, invalid roots, and duplicate normalized registrations without modifying catalog state.
- Added console-free registered-folder metadata reporting with invariant UTC timestamps, Never, stored baseline counts, fixed labels, and root/Id ordinal ordering.
- Wired non-empty `list` through shared validation and Core formatting while preserving the exact missing/empty message and typed catalog-error output separation.
- Added read-only regression proof using raw catalog bytes, loaded metadata, target bytes, missing roots, and injected scan/compare delegates that throw if called.
- Added 15 Story 4.1 test cases, increasing the full regression suite from 131 to 146 without new dependencies or excluded-scope behavior.

### File List

- `_bmad-output/implementation-artifacts/4-1-display-registered-folder-metadata.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/stories/story-012.md`
- `src/FolderPrint.Cli/CliRunner.cs`
- `src/FolderPrint.Core/Catalog/CatalogValidationResult.cs`
- `src/FolderPrint.Core/Catalog/CatalogValidator.cs`
- `src/FolderPrint.Core/Catalog/RegisteredFolderLookup.cs`
- `src/FolderPrint.Core/Reporting/ReportFormatter.cs`
- `tests/FolderPrint.Tests/Catalog/CatalogValidatorTests.cs`
- `tests/FolderPrint.Tests/Cli/CliListMetadataTests.cs`
- `tests/FolderPrint.Tests/Reporting/RegisteredFolderMetadataFormatterTests.cs`

## Change Log

- 2026-07-15: Implemented deterministic read-only registered-folder metadata listing, shared catalog validation, and 15 automated test cases; moved Story 4.1 to review.
