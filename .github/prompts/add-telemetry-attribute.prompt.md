---
description: Add a new genai.* telemetry attribute end-to-end — from constant declaration to verified test coverage
---

# add-telemetry-attribute

Walk through the four mandatory steps for introducing a new `genai.*` telemetry attribute
into the `Melic.AgentFramework.Observability` library. Each step is a constitution gate;
skipping any step violates Principles V and VI.

## User Input

```text
$ARGUMENTS
```

If the user provides an attribute name or description, use it. Otherwise ask:
"What is the new attribute name (e.g. `genai.session.latency_ms`) and which package will write it?"

## Steps

### Step 1 — Declare the Constant in Abstractions

**File**: `src/Melic.AgentFramework.Observability.Abstractions/<AttributeNamesClass>.cs`

Rules (Principle V — non-negotiable):
- Prefix MUST be `genai.` — never `gen_ai.` (that prefix is reserved for OTel Semantic Conventions)
- Name MUST be lowercase, dot-separated, and self-describing
- Field MUST be `public static readonly string`
- Field MUST have an XML doc comment
- Constant MUST be declared here BEFORE it is referenced in any other package

Example:
```csharp
/// <summary>The latency in milliseconds for a single agent invocation.</summary>
public static readonly string InvocationLatencyMs = "genai.session.latency_ms";
```

Verify the constant does not duplicate an existing one in the same file.

### Step 2 — Write the Attribute in the Agent or Extension Method

**File**: whichever `internal` agent class or `public` extension method is responsible.

Rules:
- Reference the constant from Abstractions — never use a string literal
- Follow the best-effort pattern (Principle VII): attribute writes MUST be inside a
  try/catch or in a code path that cannot throw to the caller
- If writing to an `Activity`, use `Activity.SetTag(AttributeNames.Xxx, value)`
- If writing to a span event, include it in the event tags dictionary
- Update the XML doc on the parent method if the new attribute changes its observable behaviour

### Step 3 — Update the Public API Contract

**File**: `specs/<NNN>-<feature-name>/contracts/<package>-api.md`

Rules (Principle VI):
- Add the attribute to the `SessionAttributeNames` (or equivalent) table in the contract
- If the attribute is written by a public extension method, verify the method signature in
  the contract includes ALL parameters (including optional ones with their default values)
- Contracts must reflect the full observable surface — no silent omissions

Example row to add to the attribute table:
```
| `genai.session.latency_ms` | `long` | Latency in ms for the invocation | Always |
```

### Step 4 — Add a Test That Verifies the Attribute on the Activity

**File**: `tests/Melic.AgentFramework.Observability.<Package>.Tests/<AreaTests>.cs`

Rules (Principle IV):
- Use a real `ActivitySource` + `ActivityListener` (via `ActivityRecorder` helper) — no mocking
- Assert that `activity.GetTagItem(SessionAttributeNames.Xxx)` is not null and has the
  expected value after an invocation
- Cover at least: (a) happy path value is correct, (b) value after session restoration
  (if the attribute is persisted in StateBag), (c) value when the source data is absent
  (e.g., `AgentResponse.Usage == null`) — attribute must be absent or zero, not throw

Example assertion:
```csharp
var tag = activity.GetTagItem(SessionAttributeNames.InvocationLatencyMs);
Assert.NotNull(tag);
Assert.True((long)tag > 0);
```

## Checklist

Before opening a PR, confirm all four steps are complete:

- [ ] Constant declared in Abstractions with `public static readonly string` and XML doc
- [ ] Constant uses `genai.` prefix (not `gen_ai.`)
- [ ] Attribute written via the constant, not a string literal
- [ ] Write path is inside a best-effort try/catch boundary
- [ ] Contract updated with the new attribute (full signature if on an extension method)
- [ ] Test asserts the attribute appears on the `Activity` with the correct value
- [ ] `dotnet build` passes with zero warnings
- [ ] `dotnet test` passes across all three TFMs

## Constitution Gates

This workflow touches three principles directly:

| Principle | Gate |
|-----------|------|
| **V — Attribute Naming** | `genai.` prefix, declared in Abstractions first |
| **VI — API Surface Completeness** | Contract updated, XML doc on constant |
| **VII — Best-Effort Telemetry** | Write path cannot throw to caller |
