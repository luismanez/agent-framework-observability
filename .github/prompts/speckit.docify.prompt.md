---
description: Project completed spec content into human-facing documentation (README, /docs, CHANGELOG)
---

# speckit.docify

Bridge the gap between the process-oriented spec artifacts and the consumer-facing documentation
of the project. Take only the **public, durable** parts of a completed feature spec and project
them into the docs that humans (and future AI sessions) actually read.

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Outline

1. Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from
   repo root and parse `FEATURE_DIR`, `FEATURE_NAME`, and `AVAILABLE_DOCS`. All paths must be
   absolute. Abort if `tasks.md` does not exist or contains incomplete tasks (`- [ ]`); the feature
   must be fully implemented before docifying.

2. **Read the spec inputs** from `FEATURE_DIR`:
   - **REQUIRED**: `spec.md` — feature overview, user stories, acceptance criteria
   - **REQUIRED**: `tasks.md` — to confirm all tasks are completed
   - **IF EXISTS**: `quickstart.md` — usage scenarios (primary source for README quickstart)
   - **IF EXISTS**: `contracts/` — public API surface
   - **IF EXISTS**: `data-model.md` — public data structures
   - **IF EXISTS**: `plan.md` — only the "Tech Stack" and high-level architecture sections

3. **Read the current human-facing docs** to know what to update vs. create:
   - `README.md` at repo root
   - `CHANGELOG.md` at repo root (create if absent, follow Keep a Changelog format)
   - `docs/` directory listing (create `docs/features/` if absent)
   - Any existing `docs/features/<feature-slug>.md`

4. **Project content into the docs.** Apply these rules strictly:

   **What to INCLUDE:**
   - Public API surface (types, methods, options, attribute names)
   - Usage scenarios from `quickstart.md` (verbatim or lightly edited)
   - High-level capability description (one paragraph from `spec.md` overview)
   - Stable extension points (e.g., builder methods, options classes)
   - Links back to the full quickstart and the spec for readers who want depth

   **What to EXCLUDE:**
   - Functional Requirement IDs (FR-001, FR-017, etc.)
   - Task IDs, phases, dependency graphs
   - "Alternatives considered" / "Decisions" sections
   - Hypothetical edge cases not implemented
   - Internal type names (anything in `Internal/` namespace)
   - Test names or test counts

5. **Update `README.md`** (idempotent):
   - Update the "Quick start" section with `quickstart.md` Scenario 1 (or equivalent minimal example)
   - Update the "Packages" table if new packages were added
   - Add/update a "Features" bullet list referencing `docs/features/<feature>.md`
   - Do NOT touch sections unrelated to this feature

6. **Create or update `docs/features/<feature-slug>.md`**:
   - Use `FEATURE_NAME` minus the numeric prefix as the slug (e.g., `001-session-identity-enrichment` → `session-identity-enrichment.md`)
   - Sections: Overview, Installation, Usage (all relevant scenarios), Public API Reference, Configuration Options, Telemetry Schema (if applicable), See Also (link back to `specs/<FEATURE_DIR>/`)
   - This is the durable, consumer-oriented documentation — write it as if for an external user
     reading on NuGet.org

7. **Update `CHANGELOG.md`** (Keep a Changelog format):
   - Find or create the `[Unreleased]` section
   - Add one bullet per implemented user story under `### Added` (or `### Changed` if amending behavior)
   - Reference the feature directory: `(see specs/<FEATURE_DIR>/)`
   - Do NOT cut a version — leave under `[Unreleased]` (versioning is a release concern)

8. **Verification pass:**
   - Re-read each updated file end-to-end
   - Confirm no FR-IDs, task IDs, or "alternatives considered" leaked through
   - Confirm internal namespaces are not referenced
   - Confirm all code samples in updated docs would actually compile against the public API
     (cross-reference with `contracts/` and the actual source files)

9. **Report:**
   - Show a table: file → action (created / updated / skipped) → key sections touched
   - Suggest next steps: review the diff, then `/speckit.git.commit` to commit doc changes

## Notes

- This step is **idempotent**: running it twice on the same feature must produce the same result.
- This step is **non-destructive** to non-feature content: never overwrite unrelated sections of
  README or other docs.
- If the feature has no `quickstart.md`, derive a minimal usage example from the contracts and
  acceptance criteria in `spec.md`.
- This is a docs-only step. It does NOT modify source code, tests, or specs.
