---
title: "Product Brief: FolderPrint"
status: final-draft
created: 2026-07-06
updated: 2026-07-06
---

# Product Brief: FolderPrint

## Executive Summary

FolderPrint is a .NET 8 console application for verifying whether files in registered folders still match a trusted baseline. A user registers a folder when they trust its contents; FolderPrint records each file's path, size, and SHA-256 hash in a local JSON catalog. Later, the user can verify the folder to detect changed, missing, added, moved, renamed, or duplicate files.

The product is designed for practical integrity checks where users need confidence that a backup, archive, release package, or deployment folder has not drifted. It is intentionally local, dependency-light, and command-line first. V1 favors a small reliable tool over a broad file-management platform.

## The Problem

Files often need to remain trustworthy after they are copied, archived, deployed, or stored for long periods. Operating systems can show names, timestamps, and sizes, but those signals do not reliably answer whether content is unchanged. Users who care about integrity typically fall back to manual checks, ad hoc scripts, or heavyweight tools that are more complex than the job requires.

FolderPrint addresses six recurring questions:

- Did any files change since I registered this folder?
- Did any files disappear?
- Were new files added?
- Was a file renamed or moved?
- Are there duplicate files?
- Is this backup, archive, release package, or deployment folder still intact?

The cost of the status quo is uncertainty. A backup may exist but be corrupted. A release package may have been modified after approval. A document archive may have silent drift. A deployment folder may no longer match the trusted snapshot.

## The Solution

FolderPrint creates and verifies trusted folder fingerprints.

In V1, users interact through a focused CLI:

- `folderprint register <folder>` creates the trusted baseline for a folder.
- `folderprint verify <folder>` compares the current folder state against the baseline.
- `folderprint list` shows registered folders.
- `folderprint unregister <folder>` removes a folder from the local catalog.
- `folderprint duplicates <folder>` reports duplicate files by content hash.
- `folderprint refresh <folder>` replaces the stored baseline after the user intentionally accepts the current folder state.

The core mechanism is simple: scan the folder, calculate SHA-256 for every file, persist the catalog as JSON, and compare later scans against the stored baseline. The output should make integrity status obvious and explain exactly what changed.

## Who This Serves

FolderPrint serves users who need local, repeatable file-integrity checks without adopting a larger backup, compliance, or deployment system.

Primary use cases:

- Backup verification, especially after copying or restoring folders.
- Document archives where long-term integrity matters.
- Release package validation before or after handoff.
- Compliance-friendly audit checks where local evidence is useful.
- Detecting accidental file modifications.
- Comparing trusted snapshots over time.

The primary user is technical enough to run a CLI but should not need to write scripts, manage a database, or learn a complex rules engine.

## Product Principles

- Local first: V1 works without cloud services, network assumptions, or external infrastructure.
- Trust is explicit: a folder is only verified against a baseline the user intentionally registered or refreshed.
- Explain drift clearly: changed, missing, added, renamed or moved, and duplicate files should be distinct outcomes.
- Keep the data inspectable: the catalog is JSON, not a hidden database.
- Prefer boring technology: .NET 8, C#, `System.Security.Cryptography`, `System.Text.Json`, and xUnit.
- Optimize for correctness before scale: V1 should be reliable for ordinary folders before pursuing very large-scale performance work.

## V1 Scope

FolderPrint V1 includes:

- .NET 8 C# console application.
- Manual CLI parser unless implementation proves a strong need for `System.CommandLine`.
- SHA-256 hashing via `System.Security.Cryptography`.
- Local JSON catalog via `System.Text.Json`.
- xUnit test coverage for registration, verification, catalog behavior, duplicate detection, and refresh semantics.
- Commands: `register`, `verify`, `list`, `unregister`, `duplicates`, and `refresh`.

V1 should detect and report:

- Changed files.
- Missing files.
- Added files.
- Renamed or moved files when content hash indicates continuity.
- Duplicate files by matching SHA-256 hash.

## Non-Goals

V1 explicitly does not include:

- GUI.
- SQLite or other database storage.
- Cloud backup integration.
- Real-time file monitoring.
- Encryption.
- Network share support as a guaranteed scenario.
- Very large-scale optimization.
- Complex ignore rules.

These exclusions keep the first version small enough to build, test, and trust.

## Later Options

A likely post-V1 addition is:

- `folderprint export-report <folder> --format json`

This would support downstream audit, automation, or CI-style workflows without expanding the core product into a reporting platform too early.

## Success Criteria

FolderPrint V1 is successful when:

- A user can register a folder and later verify it with deterministic, understandable results.
- Verification distinguishes changed, missing, added, moved or renamed, and duplicate files.
- The local JSON catalog remains human-inspectable and stable enough for tests.
- The tool has meaningful xUnit coverage around core integrity behavior.
- The CLI remains small enough that usage is easy to remember.
- The implementation avoids unnecessary infrastructure while preserving a path to future reporting.

## Vision

FolderPrint can become a dependable local integrity companion for anyone who needs to trust folder contents over time. The long-term opportunity is not to replace backup systems, deployment tooling, or compliance platforms; it is to provide a small, transparent verification layer that can fit beside them.

If V1 proves useful, the product can grow carefully into richer reporting, automation-friendly outputs, and better comparison workflows while preserving its central promise: register what you trust, then verify whether it stayed trustworthy.
