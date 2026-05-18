---
description: Review completed feature for Copilot customization improvements (instructions, skills, agents, constitution, hooks)
---

# speckit.retrospect

Bridge the gap between what was built during a feature and what future AI sessions should know.
After each completed feature, inspect whether any pattern, convention, domain knowledge, or
workflow improvement that emerged deserves to be codified in the Copilot customization layer.

**This command is strictly read-first, propose-second.** It NEVER writes files without
explicit user approval of a concrete remediation list.

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Pre-Execution Checks

Check `.specify/extensions.yml` for `hooks.before_retrospect` and process any enabled hooks
(same logic as other Spec Kit commands: skip hooks with `enabled: false`; execute mandatory
hooks, offer optional ones).

## Goal

Identify improvements to Copilot customization files that are motivated by the just-completed
feature. This is not a general code review — it is focused exclusively on the question:
**"What did building this feature teach us that an AI assistant should know going forward?"**

## Execution Steps

### 1. Initialize Context

Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
from repo root and parse `FEATURE_DIR` and `FEATURE_NAME`. Derive absolute paths:

- SPEC   = FEATURE_DIR/spec.md
- PLAN   = FEATURE_DIR/plan.md
- TASKS  = FEATURE_DIR/tasks.md

Abort with a clear error if any required file is missing or tasks are incomplete (`- [ ]`).
The feature must be fully implemented before retrospecting.

### 2. Load Feature Artifacts (Read-Only)

Read selectively — focus on signal, not volume:

**From spec.md**: Overview, Functional Requirements, User Stories, Edge Cases
**From plan.md**: Architecture decisions, technology choices, technical constraints, Constitution Check outcomes
**From tasks.md**: Phase structure, task descriptions (patterns of work)
**From contracts/** (if present): Public API surface — what was designed vs. what actually shipped

### 3. Load Current Customization Files (Read-Only)

Read the following files in full:

- `.github/copilot-instructions.md` — global project instructions (content outside SPECKIT block)
- `.specify/memory/constitution.md` — project principles
- `.specify/extensions.yml` — registered hooks
- All files under `.github/agents/` — custom agent modes
- All files under `.github/prompts/` — custom skill prompts

### 4. Retrospect Passes

For each customization area, reason about whether the completed feature surfaces improvements.

#### A. Global Instructions (`.github/copilot-instructions.md`)

Ask: Did this feature reveal project facts, conventions, or constraints that every AI session
should know but are not yet in the global section (outside the SPECKIT block)?

Examples of good candidates:
- A new package that now has real code (update the Package Map)
- A runtime constraint discovered during implementation (e.g., thread-safety requirement)
- A library version pinned for a specific reason future sessions should know
- A pattern consistently applied across the feature (e.g., ConditionalWeakTable for per-bag locking)

Do NOT suggest adding feature-specific implementation details — only durable, project-wide facts.

#### B. Constitution (`.specify/memory/constitution.md`)

Ask: Did this feature surface a principle violation, a near-miss, or a best practice that was
applied consistently enough to deserve elevation to a principle?

Examples of good candidates:
- A new "never do X" rule that was violated in the spec/contract and caught by analyze
- A testing pattern that was consistently required (e.g., "always test concurrent access for stateful components")
- A naming convention that emerged and should be enforced going forward

Only propose constitution changes for genuinely reusable, project-wide rules. Do NOT
propose changes that merely restate existing principles.

#### C. Skills / Prompts (`.github/prompts/`)

Ask: Did this feature introduce a domain workflow or knowledge area that would benefit
from a dedicated reusable skill prompt?

Examples of good candidates:
- A recurring multi-step process (e.g., "how to add a new telemetry attribute end-to-end")
- Domain knowledge that future AI sessions would need to reconstruct from scratch each time
- A debugging or validation workflow specific to this library

Do NOT suggest a skill just because the feature was complex. The test is: "Would a fresh AI
session benefit from having this as a standalone prompt it can invoke by name?"

#### D. Custom Agents (`.github/agents/`)

Ask: Did this feature reveal a specialized mode of working that deserves a dedicated agent
configuration — a distinct persona with specific tool permissions or focus areas?

Examples of good candidates:
- A QA/review agent that knows the OTel semantic conventions and can validate attribute naming
- A release agent that knows the MinVer tagging process and NuGet publishing steps

Do NOT suggest a custom agent unless the specialization is deep enough that a general agent
with instructions would be insufficient.

#### E. Hooks (`.specify/extensions.yml`)

Ask: Did the workflow feel incomplete? Were there manual steps done after `implement`,
`analyze`, or `docify` that should be automated as hooks?

Examples of good candidates:
- A validation script that should run before every `implement`
- A cleanup step that should run after every `analyze`

### 5. Produce Retrospect Report

Output a Markdown report (no file writes yet) with the following structure:

---

## Retrospect Report — {FEATURE_NAME}

### A. Global Instructions

| # | Proposed Change | Rationale | File Section | Priority |
|---|-----------------|-----------|--------------|----------|

### B. Constitution

| # | Proposed Change | Rationale | Section | Priority |
|---|-----------------|-----------|---------|----------|

### C. Skills / Prompts

| # | Skill Name | Description | When to Invoke | Priority |
|---|-----------|-------------|----------------|----------|

### D. Custom Agents

| # | Agent Name | Specialization | Priority |
|---|-----------|----------------|----------|

### E. Hooks

| # | Hook Point | Command | Rationale | Priority |
|---|-----------|---------|-----------|----------|

**Priority scale**: HIGH = should be done before next feature; MEDIUM = useful but not blocking;
LOW = nice to have.

**Summary**: X proposed changes across Y areas. Z HIGH-priority items.

---

### 6. Offer Remediation

Ask the user:

> "Which of the above would you like me to apply? You can say 'apply all HIGH', list specific
> item numbers (e.g. 'A1, B2, C1'), or 'skip' to close without changes."

Wait for explicit approval. Do NOT apply any change until the user responds.

### 7. Apply Approved Changes

For each approved item, apply the concrete edit to the target file using the appropriate
edit tool. After all edits:

1. Summarize what was changed
2. List any items that were skipped and why (if any required clarification)

### 8. Post-Execution Hooks

Check `.specify/extensions.yml` for `hooks.after_retrospect` and process any enabled hooks.

## Operating Principles

- **Read-only until approved**: never write files in steps 1–6
- **Signal over noise**: propose only changes with clear, recurring motivation from the feature
- **Concrete proposals**: every proposed change must include the exact text addition/replacement,
  not vague suggestions like "consider updating the instructions"
- **Preserve existing structure**: edits to `copilot-instructions.md` must not touch the
  `<!-- SPECKIT START/END -->` block; edits to `constitution.md` must follow its heading hierarchy
