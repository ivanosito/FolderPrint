---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories`r`n  - step-04-final-validation
inputDocuments:
  - "_bmad-output/planning-artifacts/prds/prd-FolderPrint-2026-07-07/prd.md"
  - "_bmad-output/planning-artifacts/architecture/architecture-FolderPrint-2026-07-07/ARCHITECTURE-SPINE.md"
  - "docs/product-brief.md"
---

# FolderPrint - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for FolderPrint, decomposing requirements from the PRD, Architecture, and Product Brief into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: Create the local JSON catalog on first write, including the catalog directory and a valid `catalog.json`.

FR2: Load the existing JSON catalog before commands that read or modify registered folder state; invalid JSON must produce a clear catalog error.

FR3: Store registered folder records with `id`, `rootPath`, `createdAtUtc`, `lastVerifiedAtUtc`, and `files`.

FR4: Store file fingerprints with `relativePath`, `sha256`, `size`, and `lastModifiedUtc`.

FR5: Implement `folderprint register <folder>` to scan an existing folder and persist a trusted baseline.

FR6: Prevent registration from silently trusting unreadable files; registration must fail clearly if unreadable files prevent a reliable baseline.

FR7: Implement `folderprint verify <folder>` to compare the current folder state against the stored baseline.

FR8: Classify files as `Unchanged` when the same relative path exists with the same SHA-256 hash.

FR9: Classify files as `Modified` when the same relative path exists with a different SHA-256 hash.

FR10: Classify files as `Missing` when they existed in the baseline but no current file exists at that relative path and no moved or renamed match is found.

FR11: Classify files as `New` when they exist in the current scan but not in the baseline and are not moved or renamed.

FR12: Classify files as `Moved/Renamed` when the same SHA-256 hash exists in baseline and current scan at different relative paths, reporting ambiguity when duplicates prevent a confident match.

FR13: Identify duplicate current files when two or more readable files share the same SHA-256 hash.

FR14: Classify files as `Unreadable` when they exist but cannot be opened for hashing.

FR15: Implement `folderprint list` to show registered folders and handle an empty catalog.

FR16: Implement `folderprint unregister <folder>` to remove only the catalog entry for a registered folder.

FR17: Implement `folderprint duplicates <folder>` to scan any existing folder and report duplicate readable files by hash.

FR18: Implement `folderprint refresh <folder>` to replace a registered folder baseline after a successful scan while preserving identity metadata.

FR19: Implement a manual CLI parser for V1 commands: `register`, `verify`, `list`, `unregister`, `duplicates`, and `refresh`.

FR20: Produce automation-friendly process exit codes for success, differences found, usage errors, not found, catalog errors, scan errors, and unexpected errors.

### NonFunctional Requirements

NFR1: Target .NET 8 and implement in C#.

NFR2: Use minimal dependencies.

NFR3: Use `System.Security.Cryptography.SHA256` for hashing.

NFR4: Use `System.Text.Json` for JSON catalog persistence.

NFR5: Use xUnit for tests.

NFR6: Keep the catalog human-inspectable JSON.

NFR7: Operate locally without cloud services, network access, or background agents.

NFR8: Keep core scanning, hashing, comparison, catalog, and command behavior testable without relying on global machine state where practical.

NFR9: Keep console output deterministic enough for users to understand and for tests to assert meaningful behavior.

NFR10: Perform acceptably for ordinary backup, archive, and release package folders; very large-scale optimization is out of scope.

### Additional Requirements

- Use solution structure: `FolderPrint.sln`, `src/FolderPrint.Cli`, `src/FolderPrint.Core`, and `tests/FolderPrint.Tests`.
- `FolderPrint.Cli` handles command parsing, argument validation, console output, and exit codes.
- `FolderPrint.Core` handles scanning, hashing, comparison, duplicate detection, catalog persistence, and report data structures.
- `FolderPrint.Cli` may depend on `FolderPrint.Core`; `FolderPrint.Core` must not depend on `FolderPrint.Cli` or `System.Console`.
- Core components are `FolderScanner`, `FileHasher`, `IntegrityCatalog`, `VerificationService`, `DuplicateFinder`, and `ReportFormatter`.
- Domain models are `RegisteredFolder`, `FileFingerprint`, `FolderSnapshot`, `VerificationResult`, `FileChange`, and `FileChangeType`.
- The Windows V1 catalog path is `%AppData%\FolderPrint\catalog.json`.
- Catalog path resolution must be replaceable in tests to avoid mutating real user AppData.
- JSON fields use camelCase; timestamps use UTC ISO-8601 strings; SHA-256 values use lowercase hex strings.
- `ReportFormatter` converts typed report data into display-ready sections without writing directly to the console.
- Use named exit code constants in `FolderPrint.Cli.ExitCodes`.
- Tests should primarily target Core business behavior; CLI tests should focus on parser and exit-code mapping, not brittle console prose.
- No starter template is specified beyond a standard .NET solution with class library, console app, and xUnit test project.

### UX Design Requirements

No UX design document was found or required. FolderPrint V1 is a CLI product; UX work is represented through deterministic command behavior, readable console output, and clear exit codes.

### FR Coverage Map

FR1: Epic 1 - Create catalog on first write
FR2: Epic 1 - Load existing catalog and report catalog errors
FR3: Epic 2 - Store registered folder records
FR4: Epic 2 - Store file fingerprints
FR5: Epic 2 - Register a folder
FR6: Epic 2 - Fail registration on unreadable files
FR7: Epic 3 - Verify a registered folder
FR8: Epic 3 - Classify unchanged files
FR9: Epic 3 - Classify modified files
FR10: Epic 3 - Classify missing files
FR11: Epic 3 - Classify new files
FR12: Epic 3 - Classify moved/renamed files
FR13: Epic 3 and Epic 5 - Detect duplicate current files
FR14: Epic 3 and Epic 5 - Report unreadable files
FR15: Epic 1 and Epic 4 - List registered folders
FR16: Epic 4 - Unregister a folder
FR17: Epic 5 - Run duplicate detection for any existing folder
FR18: Epic 4 - Refresh a registered folder baseline
FR19: Epic 1 - Manual CLI parser
FR20: Epic 1 - Automation-friendly exit codes

## Epic List

### Epic 1: Runnable CLI Foundation

Users can run `folderprint`, receive deterministic usage/errors, and see registered-folder state even before any folder is registered.

**FRs covered:** FR1, FR2, FR15, FR19, FR20

### Epic 2: Register Trusted Folder Baselines

Users can register a trusted folder and persist a complete fingerprint baseline in the JSON catalog.

**FRs covered:** FR3, FR4, FR5, FR6

### Epic 3: Verify Folder Integrity

Users can verify a registered folder and receive clear classifications for unchanged, modified, missing, new, moved/renamed, duplicate, and unreadable files.

**FRs covered:** FR7, FR8, FR9, FR10, FR11, FR12, FR13, FR14

### Epic 4: Manage Registered Folders and Baselines

Users can list tracked folders, unregister folders, and refresh a baseline after intentionally accepting the current folder state.

**FRs covered:** FR15, FR16, FR18

### Epic 5: Find Duplicate Files On Demand

Users can run `folderprint duplicates <folder>` against any existing folder and get duplicate groups without requiring registration.

**FRs covered:** FR13, FR14, FR17

## Suggested Implementation Order

1. Story 1.1 - Create .NET solution and project boundaries
2. Story 1.2 - Add manual command parser and exit-code constants
3. Story 1.3 - Implement empty catalog load/list behavior
4. Story 2.1 - Implement domain models and file hashing
5. Story 2.2 - Implement recursive folder scanning
6. Story 2.3 - Persist registered folder baselines
7. Story 2.4 - Wire `register` command
8. Story 3.1 - Compare unchanged, modified, missing, and new files
9. Story 3.2 - Detect moved/renamed files and ambiguity
10. Story 3.3 - Include unreadable and duplicate findings in verification
11. Story 3.4 - Wire `verify` command
12. Story 4.1 - Complete registered folder listing
13. Story 4.2 - Implement unregister
14. Story 4.3 - Implement refresh
15. Story 5.1 - Implement duplicate grouping service
16. Story 5.2 - Wire `duplicates` command

## Story Sizing Warnings

- Epic 3 is the largest area. It is split across four stories because a single “verification engine” story would be too large and risky.
- Story 2.3 touches catalog persistence and schema stability. Keep it focused on persistence only; command wiring remains in Story 2.4.
- Story 3.2 should not also implement duplicate command output. It only handles moved/renamed ambiguity inside verification.
- No story includes V2 scope such as `export-report`, SQLite, GUI, cloud integration, real-time monitoring, encryption, network-share guarantees, large-scale optimization, or complex ignore rules.

## Epic 1: Runnable CLI Foundation

Users can run `folderprint`, receive deterministic usage/errors, and see registered-folder state even before any folder is registered.

**FRs covered:** FR1, FR2, FR15, FR19, FR20

### Story 1.1: Create Solution and Project Boundaries

As a developer,
I want the FolderPrint solution and project structure created,
So that CLI, Core, and tests have clear dependency boundaries from the start.

**Dependencies:** None.

**Acceptance Criteria:**

**Given** a clean repository
**When** the solution is created
**Then** `FolderPrint.sln` exists with `src/FolderPrint.Cli`, `src/FolderPrint.Core`, and `tests/FolderPrint.Tests`
**And** `FolderPrint.Cli` references `FolderPrint.Core`
**And** `FolderPrint.Tests` references the projects needed for tests
**And** `FolderPrint.Core` does not reference `FolderPrint.Cli`

**Testing Expectations:**

- Add a build verification that all projects compile.
- Add or confirm a simple test project smoke test.

### Story 1.2: Parse V1 Commands and Define Exit Codes

As a CLI user,
I want invalid or incomplete commands to produce clear usage behavior,
So that I can understand how to run FolderPrint and scripts can detect command errors.

**Dependencies:** Story 1.1.

**Acceptance Criteria:**

**Given** the V1 command set
**When** `folderprint` receives `register`, `verify`, `list`, `unregister`, `duplicates`, or `refresh`
**Then** the manual parser recognizes the command and required argument shape
**And** unknown commands return a usage error result
**And** missing required folder arguments return a usage error result
**And** `ExitCodes` defines named constants for success, differences found, usage error, not found, catalog error, scan error, and unexpected error

**Testing Expectations:**

- Unit test command parsing for all valid V1 commands.
- Unit test unknown commands and missing arguments.
- Avoid brittle full-console text assertions.

### Story 1.3: Load an Empty Catalog and List Registered Folders

As a CLI user,
I want `folderprint list` to work before any folder is registered,
So that I can see whether FolderPrint is tracking anything yet.

**Dependencies:** Stories 1.1, 1.2.

**Acceptance Criteria:**

**Given** no catalog exists
**When** the user runs `folderprint list`
**Then** FolderPrint treats the missing catalog as empty
**And** output clearly states that no folders are registered
**And** the command exits successfully

**Given** malformed catalog JSON
**When** a command loads the catalog
**Then** FolderPrint returns a catalog error result
**And** the CLI maps it to the catalog error exit code

**Testing Expectations:**

- Unit test missing catalog read behavior.
- Unit test malformed JSON handling.
- CLI test verifies `list` maps empty catalog to success.

## Epic 2: Register Trusted Folder Baselines

Users can register a trusted folder and persist a complete fingerprint baseline in the JSON catalog.

**FRs covered:** FR3, FR4, FR5, FR6

### Story 2.1: Implement Domain Models and SHA-256 Hashing

As a user who trusts a folder,
I want each file to have a stable content fingerprint,
So that later verification can compare file contents reliably.

**Dependencies:** Story 1.1.

**Acceptance Criteria:**

**Given** readable file content
**When** `FileHasher` hashes the file
**Then** it returns a lowercase hex SHA-256 value using `System.Security.Cryptography.SHA256`
**And** no third-party hashing dependency is introduced

**Given** domain model instances
**When** they are used by Core services
**Then** `RegisteredFolder`, `FileFingerprint`, `FolderSnapshot`, `VerificationResult`, `FileChange`, and `FileChangeType` represent the architecture-defined fields

**Testing Expectations:**

- Unit test SHA-256 output against a known test vector.
- Unit test model construction where useful, without over-testing simple property bags.

### Story 2.2: Scan Folders Recursively

As a user registering a trusted folder,
I want FolderPrint to recursively scan files under that folder,
So that nested files are included in the baseline.

**Dependencies:** Story 2.1.

**Acceptance Criteria:**

**Given** an existing folder with nested files
**When** `FolderScanner` scans the folder
**Then** it returns a `FolderSnapshot` containing readable file fingerprints
**And** each fingerprint includes relative path, SHA-256, size, and last modified UTC
**And** relative paths are rooted at the scanned folder

**Given** a file cannot be opened
**When** `FolderScanner` scans the folder
**Then** the unreadable file is reported without a fabricated hash

**Testing Expectations:**

- Use temporary directories with nested files.
- Test relative path generation.
- Test unreadable-file handling where the platform permits reliable simulation.

### Story 2.3: Persist Registered Folder Baselines

As a user who registers a folder,
I want the trusted baseline saved in a local JSON catalog,
So that future CLI runs can verify against it.

**Dependencies:** Stories 1.3, 2.2.

**Acceptance Criteria:**

**Given** a completed folder snapshot
**When** Core adds a registered folder
**Then** the catalog stores `id`, `rootPath`, `createdAtUtc`, `lastVerifiedAtUtc`, and `files`
**And** each file stores `relativePath`, `sha256`, `size`, and `lastModifiedUtc`
**And** JSON uses camelCase fields
**And** catalog path resolution can be injected for tests

**Testing Expectations:**

- Unit test catalog save/load round trip.
- Unit test required JSON fields and casing.
- Use temporary catalog paths, not real `%AppData%`.

### Story 2.4: Wire `register <folder>` Command

As a CLI user,
I want to register a trusted folder from the command line,
So that FolderPrint can establish the baseline I will verify later.

**Dependencies:** Stories 1.2, 2.3.

**Acceptance Criteria:**

**Given** an existing unregistered folder with readable files
**When** the user runs `folderprint register <folder>`
**Then** FolderPrint scans the folder and persists a registered folder baseline
**And** output reports how many files were registered
**And** the command exits successfully

**Given** a nonexistent path or file path
**When** the user runs `folderprint register <folder>`
**Then** FolderPrint reports a clear error and exits non-zero

**Given** unreadable files are encountered
**When** registration runs
**Then** FolderPrint reports unreadable files
**And** does not silently create an incomplete trusted baseline

**Testing Expectations:**

- CLI-level tests for successful register and invalid folder arguments.
- Core tests for duplicate registration and unreadable registration failure.

## Epic 3: Verify Folder Integrity

Users can verify a registered folder and receive clear classifications for unchanged, modified, missing, new, moved/renamed, duplicate, and unreadable files.

**FRs covered:** FR7, FR8, FR9, FR10, FR11, FR12, FR13, FR14

### Story 3.1: Compare Basic File States

As a user verifying a registered folder,
I want FolderPrint to identify unchanged, modified, missing, and new files,
So that I can understand ordinary folder drift.

**Dependencies:** Stories 2.2, 2.3.

**Acceptance Criteria:**

**Given** a baseline and current snapshot
**When** the same relative path has the same hash
**Then** the file is classified as `Unchanged`

**Given** the same relative path has a different hash
**When** verification compares snapshots
**Then** the file is classified as `Modified`

**Given** a baseline file path is absent from the current snapshot
**When** no move/rename match exists
**Then** the file is classified as `Missing`

**Given** a current file path is absent from the baseline
**When** no move/rename match exists
**Then** the file is classified as `New`

**Testing Expectations:**

- Unit test each basic classification directly against `VerificationService`.
- Include the case where timestamp changes but hash stays the same.

### Story 3.2: Detect Moved or Renamed Files

As a user verifying a registered folder,
I want files with unchanged content at different paths reported as moved or renamed,
So that I do not mistake a rename for separate missing and new files.

**Dependencies:** Story 3.1.

**Acceptance Criteria:**

**Given** a baseline file hash appears once at a different current relative path
**When** verification compares snapshots
**Then** the result includes a `MovedOrRenamed` change with baseline and current paths
**And** the same file is not also reported as separate `Missing` and `New`

**Given** duplicate hashes make a move/rename match ambiguous
**When** verification compares snapshots
**Then** FolderPrint reports ambiguity instead of pretending to know the exact move

**Testing Expectations:**

- Unit test unambiguous rename.
- Unit test moved file across subfolders.
- Unit test ambiguous duplicate-hash scenario.

### Story 3.3: Include Duplicate and Unreadable Findings in Verification

As a user verifying a registered folder,
I want duplicate and unreadable files surfaced in the verification result,
So that integrity checks do not hide important current-folder risks.

**Dependencies:** Stories 2.2, 3.1.

**Acceptance Criteria:**

**Given** two or more current readable files share the same SHA-256 hash
**When** verification runs
**Then** the result includes duplicate groups

**Given** a current file cannot be opened
**When** verification runs
**Then** the result includes an unreadable finding
**And** no fabricated hash is created for that file

**Testing Expectations:**

- Unit test duplicate grouping within verification.
- Unit test unreadable scan result is carried into verification output.

### Story 3.4: Wire `verify <folder>` Command and Reporting

As a CLI user,
I want to run `folderprint verify <folder>` and receive a clear integrity summary,
So that I can decide whether a folder still matches its trusted baseline.

**Dependencies:** Stories 1.2, 2.4, 3.1, 3.2, 3.3.

**Acceptance Criteria:**

**Given** a registered unchanged folder
**When** the user runs `folderprint verify <folder>`
**Then** FolderPrint reports a clean verification result
**And** exits successfully

**Given** a registered folder with drift
**When** the user runs `folderprint verify <folder>`
**Then** FolderPrint reports the relevant classifications
**And** exits with `DifferencesFound`

**Given** the folder is not registered
**When** the user runs `folderprint verify <folder>`
**Then** FolderPrint reports a not-registered error and exits non-zero

**Testing Expectations:**

- Integration-style CLI tests using temporary folders and injected catalog path.
- Verify exit-code mapping for clean, drift, and unregistered cases.
- Keep text assertions focused on classification labels and summary counts.

## Epic 4: Manage Registered Folders and Baselines

Users can list tracked folders, unregister folders, and refresh a baseline after intentionally accepting the current folder state.

**FRs covered:** FR15, FR16, FR18

### Story 4.1: Display Registered Folder Metadata

As a CLI user,
I want `folderprint list` to show registered folders and metadata,
So that I can see what FolderPrint is tracking.

**Dependencies:** Story 2.3.

**Acceptance Criteria:**

**Given** one or more registered folders exist
**When** the user runs `folderprint list`
**Then** output includes each folder `id`, `rootPath`, `createdAtUtc`, and `lastVerifiedAtUtc`
**And** the command exits successfully

**Testing Expectations:**

- Core or CLI test for non-empty catalog listing.
- Preserve the empty-list behavior from Story 1.3.

### Story 4.2: Unregister a Folder

As a CLI user,
I want to unregister a folder,
So that FolderPrint stops tracking it without touching files on disk.

**Dependencies:** Story 2.3.

**Acceptance Criteria:**

**Given** a registered folder exists
**When** the user runs `folderprint unregister <folder>`
**Then** the catalog entry is removed
**And** files in the target folder are not deleted or modified
**And** the command exits successfully

**Given** the folder is not registered
**When** the user runs `folderprint unregister <folder>`
**Then** FolderPrint reports a clear not-registered message and exits non-zero

**Testing Expectations:**

- Unit test catalog removal behavior.
- CLI test for success and not-registered exit mapping.
- Assert target files still exist after unregister.

### Story 4.3: Refresh a Registered Folder Baseline

As a CLI user,
I want to refresh a registered folder baseline after intentional changes,
So that future verifications compare against the newly trusted state.

**Dependencies:** Stories 2.4, 3.4.

**Acceptance Criteria:**

**Given** a registered folder has changed intentionally
**When** the user runs `folderprint refresh <folder>`
**Then** FolderPrint scans the current folder and replaces the stored file baseline
**And** preserves the registered folder `id` and `createdAtUtc`
**And** updates the verification or refresh timestamp consistently
**And** exits successfully

**Given** the folder is not registered
**When** the user runs `folderprint refresh <folder>`
**Then** FolderPrint reports a clear not-registered message and exits non-zero

**Testing Expectations:**

- Unit test baseline replacement preserves identity metadata.
- CLI test refresh followed by verify returns clean for the refreshed state.

## Epic 5: Find Duplicate Files On Demand

Users can run `folderprint duplicates <folder>` against any existing folder and get duplicate groups without requiring registration.

**FRs covered:** FR13, FR14, FR17

### Story 5.1: Implement DuplicateFinder for Current Snapshots

As a user checking a folder,
I want duplicate readable files grouped by matching content hash,
So that I can identify redundant files independent of baseline verification.

**Dependencies:** Story 2.2.

**Acceptance Criteria:**

**Given** a folder snapshot with two or more files sharing a hash
**When** `DuplicateFinder` analyzes the snapshot
**Then** it returns duplicate groups containing all matching relative paths

**Given** no hash appears more than once
**When** `DuplicateFinder` analyzes the snapshot
**Then** it returns no duplicate groups

**Given** unreadable files exist in the snapshot
**When** duplicates are found
**Then** unreadable files are excluded from duplicate groups

**Testing Expectations:**

- Unit test duplicate group creation.
- Unit test no-duplicates case.
- Unit test unreadable files are excluded.

### Story 5.2: Wire `duplicates <folder>` Command

As a CLI user,
I want to run duplicate detection for any existing folder,
So that I can find duplicate files without registering the folder first.

**Dependencies:** Stories 1.2, 5.1.

**Acceptance Criteria:**

**Given** an existing folder with duplicate readable files
**When** the user runs `folderprint duplicates <folder>`
**Then** FolderPrint reports duplicate groups
**And** exits successfully unless scan errors prevent reliable completion

**Given** an existing folder with no duplicates
**When** the user runs `folderprint duplicates <folder>`
**Then** FolderPrint reports that no duplicates were found
**And** exits successfully

**Given** a nonexistent folder
**When** the user runs `folderprint duplicates <folder>`
**Then** FolderPrint reports a clear error and exits non-zero

**Testing Expectations:**

- CLI tests for duplicates found, none found, and invalid folder.
- Use temporary folders and avoid relying on registered catalog state.

