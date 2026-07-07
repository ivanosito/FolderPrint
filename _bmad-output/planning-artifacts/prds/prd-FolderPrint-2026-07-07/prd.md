---
title: "PRD: FolderPrint"
status: final
created: 2026-07-07
updated: 2026-07-07
source: "../../../../docs/product-brief.md"
---

# PRD: FolderPrint

## 0. Document Purpose

This Product Requirements Document defines FolderPrint V1 for product, architecture, implementation, and test planning. It is aligned with `docs/product-brief.md` and the V1 scope supplied on 2026-07-07. Requirements are grouped by capability and use stable IDs so downstream BMAD architecture, epics, stories, and acceptance tests can reference them directly.

## 1. Product Overview

FolderPrint is a .NET 8 console application that verifies whether files in registered folders have changed since the user first trusted them. The app scans a folder, calculates a SHA-256 hash for every readable file, stores that baseline in a local JSON catalog, and later compares the current folder state against the stored baseline.

FolderPrint answers practical integrity questions:

- Did any files change since this folder was registered?
- Did any files disappear?
- Were new files added?
- Was a file renamed or moved?
- Are there duplicate files?
- Is a backup, archive, release package, or deployment folder still intact?

V1 is intentionally local, command-line first, dependency-light, and focused on correctness for ordinary folders rather than large-scale optimization.

## 2. Goals

- Provide a repeatable local way to register a trusted folder baseline.
- Detect and clearly classify folder drift during later verification.
- Keep the catalog human-inspectable by storing it as JSON.
- Support backup verification, archive checks, release package validation, and deployment folder integrity checks.
- Keep V1 small enough to test thoroughly and reason about confidently.
- Use boring .NET platform capabilities before introducing third-party dependencies.

## 3. Non-Goals

FolderPrint V1 will not include:

- GUI.
- SQLite or other database storage.
- Cloud backup integration.
- Real-time file monitoring.
- Encryption.
- Network share support as a guaranteed scenario.
- Very large-scale optimization.
- Complex ignore rules.
- Rich report export beyond console output.

## 4. Target Users and Personas

### 4.1 Technical Backup Verifier

A developer, power user, or system maintainer who copies important folders to backup media and wants confidence that the copied contents remain unchanged over time.

### 4.2 Release Package Owner

A developer or release coordinator who creates a folder-based release artifact and wants to verify later that no files were added, removed, or modified after approval.

### 4.3 Archive Steward

A user responsible for document archives who needs a simple audit-friendly check that archived files still match a known trusted state.

### 4.4 Non-Users for V1

FolderPrint V1 is not intended for users who need continuous monitoring, cloud backup orchestration, encrypted storage, network-scale inventory, or GUI-driven workflows.

## 5. User Workflows

### UJ-1: Register a trusted folder

The user has a folder they currently trust, such as a backup, archive, or release package. They run `folderprint register <folder>`. FolderPrint scans the folder, hashes readable files, stores the baseline in the local catalog, and reports that the folder is registered.

### UJ-2: Verify a folder later

The user returns later and runs `folderprint verify <folder>`. FolderPrint loads the stored baseline, scans the current folder, compares current fingerprints with baseline fingerprints, and reports unchanged, modified, missing, new, moved or renamed, duplicate, and unreadable files.

### UJ-3: Review registered folders

The user runs `folderprint list` to see which folders have trusted baselines, including enough information to identify each registered folder and its last verification time.

### UJ-4: Accept a new trusted state

After intentionally changing a folder, the user runs `folderprint refresh <folder>`. FolderPrint replaces the stored baseline with a new scan so future verification compares against the accepted current state.

### UJ-5: Remove a folder from tracking

The user no longer wants FolderPrint to track a folder. They run `folderprint unregister <folder>`, and FolderPrint removes the folder entry from the catalog without deleting files from disk.

### UJ-6: Find duplicate files

The user runs `folderprint duplicates <folder>`. FolderPrint scans the current folder and reports groups of two or more readable files that share the same SHA-256 hash.

## 6. Glossary

- **Baseline**: The trusted recorded state of a registered folder at registration or refresh time.
- **Catalog**: The local JSON file containing registered folders and file fingerprints.
- **File Fingerprint**: A stored record for one file containing `relativePath`, `sha256`, `size`, and `lastModifiedUtc`.
- **Registered Folder**: A folder that has an entry in the catalog.
- **Root Path**: The absolute folder path supplied by the user and stored in the registered folder entry.
- **Scan**: The process of walking a folder and producing current file fingerprints or unreadable-file results.
- **Verification Result**: A classification produced by comparing a current scan against the baseline.

## 7. Functional Requirements

### 7.1 Catalog Management

#### FR-1: Create catalog on first write

FolderPrint must create the local JSON catalog when a command first needs to persist registered folder data.

**Acceptance criteria:**

- If the catalog directory does not exist, FolderPrint creates it.
- If `catalog.json` does not exist, FolderPrint creates a valid JSON catalog.
- The suggested Windows path is `%AppData%\FolderPrint\catalog.json`.

#### FR-2: Load existing catalog

FolderPrint must load the existing JSON catalog before commands that read or modify registered folder state.

**Acceptance criteria:**

- A valid catalog can be read across separate CLI invocations.
- Invalid JSON produces a clear catalog error and a non-zero exit code.
- Missing catalog is treated as an empty catalog for read-only commands where appropriate.

#### FR-3: Store registered folder records

The catalog must store one record per registered folder with `id`, `rootPath`, `createdAtUtc`, `lastVerifiedAtUtc`, and `files`.

**Acceptance criteria:**

- `id` is stable after registration.
- `rootPath` identifies the registered folder.
- `createdAtUtc` is set when the folder is first registered.
- `lastVerifiedAtUtc` is updated after a successful verification.
- `files` contains the baseline file fingerprints.

#### FR-4: Store file fingerprints

Each file fingerprint must include `relativePath`, `sha256`, `size`, and `lastModifiedUtc`.

**Acceptance criteria:**

- `relativePath` is relative to the registered folder root.
- `sha256` is calculated from file contents.
- `size` reflects file length in bytes at scan time.
- `lastModifiedUtc` reflects the file's last write timestamp at scan time.

### 7.2 Folder Registration

#### FR-5: Register a folder

`folderprint register <folder>` must scan the supplied folder and persist a new trusted baseline.

**Acceptance criteria:**

- Existing files are fingerprinted with SHA-256 when readable.
- The command reports how many files were registered.
- The command fails clearly if `<folder>` does not exist or is not a directory.
- The command fails clearly if the folder is already registered, unless later design explicitly adds overwrite behavior.

#### FR-6: Preserve unreadable registration failures

Registration must not silently trust files that cannot be opened.

**Acceptance criteria:**

- If any file cannot be opened during registration, the command reports unreadable files.
- The command does not create a trusted baseline that silently omits unreadable files.
- The command exits non-zero when registration cannot complete reliably.

### 7.3 Verification

#### FR-7: Verify a registered folder

`folderprint verify <folder>` must compare the current folder state against the stored baseline.

**Acceptance criteria:**

- The command fails clearly if the folder is not registered.
- The command scans the current folder and compares it with baseline fingerprints.
- The command reports each verification classification present in the result.
- The command updates `lastVerifiedAtUtc` after a completed verification.

#### FR-8: Classify unchanged files

A file must be classified as `Unchanged` when the same relative path exists in the baseline and current scan with the same SHA-256 hash.

**Acceptance criteria:**

- Same path and same hash produces `Unchanged`.
- Size and timestamp differences do not override matching content hash for unchanged classification.

#### FR-9: Classify modified files

A file must be classified as `Modified` when the same relative path exists in the baseline and current scan but the SHA-256 hash differs.

**Acceptance criteria:**

- Same path and different hash produces `Modified`.
- Modified output includes the relative path.

#### FR-10: Classify missing files

A file must be classified as `Missing` when it existed in the baseline but no current file exists at that relative path and no moved or renamed match is found.

**Acceptance criteria:**

- Baseline-only path produces `Missing`.
- Missing output includes the baseline relative path.

#### FR-11: Classify new files

A file must be classified as `New` when it exists in the current scan but did not exist in the baseline and is not classified as moved or renamed.

**Acceptance criteria:**

- Current-only path produces `New`.
- New output includes the current relative path.

#### FR-12: Classify moved or renamed files

A file must be classified as `Moved/Renamed` when the same SHA-256 hash exists in the baseline and current scan at different relative paths.

**Acceptance criteria:**

- The output includes the baseline relative path and current relative path.
- Moved or renamed matches reduce false `Missing` plus `New` reports when content continuity is clear.
- If duplicate hashes make the move ambiguous, FolderPrint reports the ambiguity rather than pretending to know the exact move.

#### FR-13: Classify duplicate files

FolderPrint must identify duplicate current files when two or more current readable files share the same SHA-256 hash.

**Acceptance criteria:**

- Duplicate groups include all current relative paths sharing the same hash.
- Duplicates can be reported during verification and by the dedicated `duplicates` command.

#### FR-14: Classify unreadable files

FolderPrint must classify a file as `Unreadable` when it exists but cannot be opened for hashing.

**Acceptance criteria:**

- Permission errors and file locks are reported as unreadable.
- Unreadable output includes the file path and a concise reason when available.
- Unreadable files do not get a fabricated hash.

### 7.4 Listing and Unregistering

#### FR-15: List registered folders

`folderprint list` must show registered folders from the catalog.

**Acceptance criteria:**

- Output includes each registered folder's `id`, `rootPath`, `createdAtUtc`, and `lastVerifiedAtUtc`.
- Empty catalog output clearly states that no folders are registered.

#### FR-16: Unregister a folder

`folderprint unregister <folder>` must remove the folder's record from the catalog.

**Acceptance criteria:**

- The command removes only the catalog entry.
- The command does not delete or modify files in the target folder.
- The command reports a clear message if the folder is not registered.

### 7.5 Duplicate Detection

#### FR-17: Report duplicates for a folder

`folderprint duplicates <folder>` must scan the current folder and report groups of duplicate readable files by SHA-256 hash.

**Acceptance criteria:**

- The command works for any existing folder, whether or not it is registered.
- The command reports no duplicates when no hash appears more than once.
- Unreadable files are reported separately and excluded from duplicate groups.

### 7.6 Refreshing Baselines

#### FR-18: Refresh a registered folder

`folderprint refresh <folder>` must replace the stored baseline with a new scan of the current folder.

**Acceptance criteria:**

- The command fails clearly if the folder is not registered.
- The command replaces the `files` collection with current readable fingerprints only after a successful scan.
- The command preserves the registered folder `id` and `createdAtUtc`.
- The command updates `lastVerifiedAtUtc` or equivalent refreshed timestamp behavior consistently.

### 7.7 CLI Behavior

#### FR-19: Parse V1 commands manually

FolderPrint V1 must support the required commands through a manual CLI parser unless implementation proves a strong reason to use `System.CommandLine`.

**Acceptance criteria:**

- Unknown commands produce usage guidance and a non-zero exit code.
- Missing required `<folder>` arguments produce usage guidance and a non-zero exit code.
- Command names are stable: `register`, `verify`, `list`, `unregister`, `duplicates`, and `refresh`.

#### FR-20: Produce automation-friendly exit codes

FolderPrint must return deterministic process exit codes suitable for scripts.

**Acceptance criteria:**

- Successful commands return `0`.
- Usage errors return non-zero.
- Catalog, scan, and verification integrity failures return non-zero.
- Verification with detected drift returns non-zero so scripts can detect failure.

## 8. Command Behavior

### `folderprint register <folder>`

- Validates that `<folder>` exists and is a directory.
- Scans all files under the folder.
- Calculates SHA-256 for readable files.
- Creates a catalog entry with a trusted baseline.
- Fails if unreadable files prevent a trustworthy baseline.
- Fails if the folder is already registered.

### `folderprint verify <folder>`

- Validates that `<folder>` is registered.
- Scans the current folder.
- Compares current fingerprints against baseline fingerprints.
- Reports verification classifications.
- Updates `lastVerifiedAtUtc` after verification completes.
- Returns non-zero when drift or unreadable files are detected.

### `folderprint list`

- Reads the catalog.
- Displays registered folders and relevant metadata.
- Handles an empty catalog gracefully.

### `folderprint unregister <folder>`

- Validates that `<folder>` is registered.
- Removes the registered folder entry from the catalog.
- Does not touch files on disk.

### `folderprint duplicates <folder>`

- Validates that `<folder>` exists and is a directory.
- Scans current files.
- Groups readable files by SHA-256.
- Reports groups with two or more files.
- Reports unreadable files separately.

### `folderprint refresh <folder>`

- Validates that `<folder>` is registered.
- Scans the current folder.
- Replaces the baseline after a successful scan.
- Preserves folder identity and creation metadata.

## 9. Data Requirements

### 9.1 Catalog Location

The suggested Windows catalog path is:

```text
%AppData%\FolderPrint\catalog.json
```

The implementation should isolate path resolution so non-Windows behavior can be decided later without changing domain logic.

### 9.2 Catalog Shape

The catalog must contain registered folder records. Exact JSON field casing should remain stable once implemented.

```json
{
  "registeredFolders": [
    {
      "id": "string",
      "rootPath": "string",
      "createdAtUtc": "2026-07-07T00:00:00Z",
      "lastVerifiedAtUtc": "2026-07-07T00:00:00Z",
      "files": [
        {
          "relativePath": "docs/example.txt",
          "sha256": "hex-encoded-sha256",
          "size": 1234,
          "lastModifiedUtc": "2026-07-07T00:00:00Z"
        }
      ]
    }
  ]
}
```

### 9.3 Registered Folder Fields

- `id`: Stable unique identifier for the registered folder.
- `rootPath`: Stored absolute path to the registered folder.
- `createdAtUtc`: UTC timestamp when the folder was first registered.
- `lastVerifiedAtUtc`: UTC timestamp from the most recent completed verification or refresh.
- `files`: Baseline file fingerprints.

### 9.4 File Fingerprint Fields

- `relativePath`: Path relative to the registered folder root.
- `sha256`: SHA-256 content hash, hex encoded.
- `size`: File size in bytes.
- `lastModifiedUtc`: Last modified timestamp in UTC at scan time.

## 10. Non-Functional Requirements

### NFR-1: Runtime and language

FolderPrint must target .NET 8 and be implemented in C#.

### NFR-2: Dependency policy

FolderPrint V1 must use minimal dependencies. SHA-256 must use `System.Security.Cryptography`; JSON must use `System.Text.Json`; tests must use xUnit.

### NFR-3: Catalog inspectability

The catalog must remain human-inspectable JSON. V1 must not require SQLite, a service, or a proprietary binary format.

### NFR-4: Local operation

FolderPrint V1 must run locally without cloud services, network access, or background agents.

### NFR-5: Testability

Core scanning, hashing, comparison, catalog, and command behavior must be testable without relying on global machine state where practical.

### NFR-6: Predictable output

Console output must be deterministic enough for users to understand and for tests to assert meaningful behavior.

### NFR-7: Ordinary-folder performance

V1 should perform acceptably for ordinary folders used in backups, archives, and release packages, but very large-scale optimization is explicitly out of scope.

## 11. Error Handling Requirements

- EH-1: Missing or invalid command arguments must produce concise usage guidance and a non-zero exit code.
- EH-2: Nonexistent folder paths must produce a clear error and a non-zero exit code.
- EH-3: File paths that exist but are not directories must produce a clear error and a non-zero exit code.
- EH-4: Catalog read failures must produce a clear catalog error and a non-zero exit code.
- EH-5: Catalog write failures must produce a clear persistence error and a non-zero exit code.
- EH-6: Invalid catalog JSON must not be silently overwritten.
- EH-7: Unreadable files must be reported explicitly and must not receive fabricated fingerprints.
- EH-8: Verification drift must be reported clearly and return a non-zero exit code.
- EH-9: Ambiguous moved or renamed detection caused by duplicate hashes must be reported as ambiguous.
- EH-10: Commands must not delete or modify user files in registered folders.

## 12. Acceptance Criteria

V1 is acceptable when:

- AC-1: A user can register an existing folder and see a persisted catalog entry.
- AC-2: A user can verify an unchanged registered folder and receive a successful result.
- AC-3: A modified file is classified as `Modified`.
- AC-4: A deleted baseline file is classified as `Missing`.
- AC-5: A new current file is classified as `New`.
- AC-6: A file with the same hash at a different relative path is classified as `Moved/Renamed` when unambiguous.
- AC-7: Duplicate current files are grouped by matching SHA-256 hash.
- AC-8: Unreadable files are reported without being silently ignored.
- AC-9: `list` shows registered folders and handles an empty catalog.
- AC-10: `unregister` removes catalog entries without touching files on disk.
- AC-11: `refresh` replaces the baseline while preserving registered folder identity.
- AC-12: Invalid commands and missing arguments produce usage guidance.
- AC-13: Core behaviors have xUnit coverage.
- AC-14: The implementation uses .NET 8, C#, `System.Security.Cryptography`, `System.Text.Json`, and minimal dependencies.

## 13. Edge Cases

- EC-1: Empty folder registration.
- EC-2: Folder contains nested subfolders.
- EC-3: File changes while being scanned.
- EC-4: File is locked or permission denied during scan.
- EC-5: Folder is deleted after registration.
- EC-6: Registered folder path casing differs between invocations on Windows.
- EC-7: Relative path separators differ across environments.
- EC-8: Duplicate files make moved or renamed detection ambiguous.
- EC-9: Same relative path exists with changed timestamp but identical hash.
- EC-10: Catalog directory is missing.
- EC-11: Catalog JSON is malformed.
- EC-12: Catalog write is denied.
- EC-13: User attempts to register the same folder twice.
- EC-14: User attempts to unregister or refresh an unregistered folder.
- EC-15: Very long paths or unusual characters in file names.

## 14. Out-of-Scope Items

- Export reports in V1.
- GUI or interactive wizard.
- Background monitoring.
- Cloud synchronization or backup.
- Encryption or secure vault storage.
- Database-backed catalog.
- Advanced ignore file syntax.
- Network share guarantees.
- Enterprise policy management.
- Large-scale indexing or incremental scan optimization.

## 15. Future Considerations

- `folderprint export-report <folder> --format json` for audit and automation workflows.
- Optional machine-readable verification output for CI or deployment pipelines.
- Configurable ignore rules once real use cases prove the needed shape.
- Cross-platform catalog path policy.
- Safer handling for very large folders, including progress reporting or incremental comparison.
- Optional report formats beyond JSON.
- Stronger ambiguity handling for moved or renamed files in folders with many duplicate hashes.

## 16. Open Questions

- OQ-1: Should `register` ever support an explicit overwrite flag, or should users always use `refresh` after registration?
- OQ-2: Should `lastVerifiedAtUtc` be updated after verification that detects drift, or only after clean verification?
- OQ-3: What exact exit code taxonomy should V1 use beyond zero and non-zero?
- OQ-4: Should `duplicates <folder>` require registration, or remain available for any folder as specified here?
- OQ-5: Should empty folder registration be allowed as a valid baseline?

## 17. Assumptions Index

- The first implementation targets Windows catalog path behavior because the suggested path is `%AppData%\FolderPrint\catalog.json`.
- A verification that detects drift should return non-zero for script friendliness.
- `duplicates <folder>` should work on any existing folder, not only registered folders.
- Registration should fail if unreadable files prevent a complete trusted baseline.
