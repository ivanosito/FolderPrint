---
title: "Sprint Plan 006: Registered Folder Management"
status: final
created: 2026-07-15
updated: 2026-07-15
source:
  - "docs/epics-and-stories.md"
  - "docs/retrospectives/sprint-005-retrospective.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
tracking: "../_bmad-output/implementation-artifacts/sprint-status.yaml"
---

# Sprint Plan 006: Registered Folder Management

## Sprint Goal

Start Epic 4 by improving registered-folder management, beginning with deterministic read-only metadata display and optionally adding unregister support if Story 4.1 completes cleanly.

Sprint 006 builds on the reviewed catalog and CLI seams without changing verification behavior. Story 4.1 is the committed outcome. Story 4.2 is gated stretch because it introduces catalog mutation and must inherit the conflict-safety lessons from Epic 3.

Refresh, the standalone duplicates command, `DuplicateFinder`, and all V2 behavior are outside Sprint 006.

## Selected Stories

### Committed

1. Story 4.1: Display Registered Folder Metadata

### Optional Gated Stretch

2. Story 4.2: Unregister a Folder

Story 4.2 may start only after Story 4.1 is implemented, fully validated, marked `done`, and approved in code review without unresolved actionable findings. Its implementation-ready artifact must also define conflict-aware catalog mutation behavior before development begins. If the gate is not met, Story 4.2 remains backlog.

Story 4.3 and all Epic 5 work remain outside Sprint 006 scope.

## Story Order

1. Create, implement, and review Story 4.1.
2. Reassess the Story 4.2 gate and incorporate Story 4.1 review lessons.
3. Create Story 4.2 only if every gate condition is satisfied.

## Story 4.1: Display Registered Folder Metadata

As a CLI user, I want `folderprint list` to show registered folders and metadata, so that I can see what FolderPrint is tracking.

### Acceptance Criteria Summary

- A non-empty catalog displays each registered folder's `id`, `rootPath`, `createdAtUtc`, and `lastVerifiedAtUtc`.
- A missing `lastVerifiedAtUtc` is represented explicitly and consistently without inventing a timestamp.
- Multiple registered folders use one documented deterministic ordering rule; fields and timestamps render deterministically.
- Existing missing-catalog and empty-catalog behavior remains successful and unchanged.
- Malformed or structurally invalid catalog data maps to `CatalogError` and is never silently normalized or overwritten.
- Listing is strictly read-only: it performs no scan, verification, timestamp update, catalog save, or target-folder mutation.
- Output transformation is independently testable and keeps `FolderPrint.Core` free of console and exit-code concepts.
- Existing register and verify behavior remains passing.

### Tasks

- Reuse the existing `CatalogStore.Load` and `CliRunner` list path; do not add a second catalog reader.
- Define deterministic folder ordering and stable rendering for all required fields, including never-verified folders.
- Extend the existing reporting/presentation seam only as narrowly as necessary for metadata display.
- Add focused tests for one folder, shuffled multiple folders, never-verified metadata, empty/missing catalog, malformed catalog, and read-only behavior.
- Prove listing does not inspect target folders or change catalog bytes.
- Run the full regression, boundary, scope, formatting, and diff checks before review.

## Story 4.2: Unregister a Folder (Optional Gated Stretch)

As a CLI user, I want to unregister a folder, so that FolderPrint stops tracking it without touching files on disk.

### Entry Gate

- Story 4.1 is `done`, fully validated, and approved in adversarial code review.
- Story 4.1 has no unresolved actionable finding or metadata-format contract ambiguity.
- The Story 4.2 artifact explicitly incorporates the Epic 3 action item for conflict-aware catalog mutations.
- Story 4.2 can be completed without implementing refresh or changing verification semantics.

### Acceptance Criteria Summary

- `folderprint unregister <folder>` removes exactly one matching catalog entry and exits successfully.
- An unregistered folder produces a clear not-registered outcome and the established non-zero exit code without saving.
- Folder identity uses the established normalized, ordinal-ignore-case V1 rule.
- The command never deletes, modifies, scans, or requires the target folder to still exist.
- Other catalog entries, their order, metadata, and trusted file baselines remain unchanged.
- Invalid/malformed catalogs and write failures return `CatalogError` without overwriting prior state.
- A catalog changed after initial load is not overwritten; the command fails safely under the shared conflict-aware persistence contract.
- Output and exit mapping are deterministic and testable; Core remains independent from CLI.

### Tasks

- Create an immutable catalog removal operation or equivalent narrow Core behavior that preserves all non-target entries and collection ownership.
- Reuse the existing folder normalization, registered-folder lookup, catalog load/save, and CLI exit-code seams.
- Apply conflict-aware persistence to the removal; do not add a second direct-write path.
- Add tests for success, not registered, normalized identity, missing target folder, multiple entries, malformed catalog, save/conflict failure, and target-file preservation.
- Confirm no refresh, scanning, baseline replacement, standalone duplicate behavior, dependency, or V2 scope enters the implementation.

## Validation Commands

Run from the repository root for every implemented story:

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
Select-String -Path src/FolderPrint.Core/**/*.cs -Pattern 'System\.Console|FolderPrint\.Cli|ExitCodes'
Select-String -Path src/**/*.cs,tests/**/*.cs -Pattern 'Refresh\(|DuplicateFinder|SQLite|Sqlite|System\.CommandLine|export-report|cloud|encryption|monitoring'
```

Existing parser symbols may mention excluded commands; inspect changed matches rather than treating every historical symbol as new scope.

## Dependencies and Current State

- Epic 3 and Stories 3.1-3.4 are done; verification and conflict-aware timestamp persistence are reviewed foundations.
- The Epic 3 and Sprint 005 retrospectives are complete.
- Story 4.1 and Story 4.2 remain `backlog` until their individual BMAD `create-story` workflows run.
- The open Epic 3 Story 4.1 learning-transfer action is satisfied when the implementation-ready Story 4.1 artifact explicitly incorporates the constraints in this plan.
- The open conflict-aware mutation action remains a gate for Story 4.2 and Story 4.3.

## Risks

- Metadata display can become nondeterministic through catalog order or culture-sensitive timestamp formatting.
- Listing can accidentally become stateful if it reuses verification or save paths rather than the read-only catalog path.
- A never-verified folder can be misrepresented if `null` is replaced with an invented timestamp.
- Unregister can overwrite concurrent catalog changes unless it uses guarded persistence.
- Unregister must not require the physical folder to exist or touch user files.
- Story 4.2 can expand into refresh or general catalog management; keep it to exact removal only.

## Definition of Done

- Story 4.1 satisfies its acceptance criteria, passes adversarial code review, and is marked `done`.
- Registered-folder metadata output is deterministic, testable, and strictly read-only.
- Empty, missing, malformed, and non-empty catalog behavior remains explicit and covered.
- Core remains independent from CLI, console, and exit-code concepts; no runtime dependency is added.
- Full restore, Release build, tests, formatting, dependency, scope, and diff checks pass.
- If Story 4.2 starts, every entry gate was satisfied first and it is completed and reviewed before being marked `done`; otherwise it remains backlog.
- No refresh, duplicates command, `DuplicateFinder`, GUI, database, cloud sync, encryption, real-time monitoring, or V2 feature is introduced.
- Sprint tracking changes only when a story actually transitions through create-story, development, review, or completion.

## Recommendation for First Story to Create

Create Story 4.1 next: `4-1-display-registered-folder-metadata`.

Its implementation-ready artifact should pin deterministic ordering, invariant timestamp rendering, the never-verified representation, malformed-catalog behavior, read-only proof, Core/CLI boundaries, and the strict exclusion of unregister and refresh.
