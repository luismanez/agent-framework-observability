# Public API Contract: Tools Package

**Package**: `Melic.AgentFramework.Observability.Tools`
**Version**: 1.0.0 (first release)
**Feature Branch**: `002-tool-call-enrichment`
**Created**: 2026-05-19

---

## Overview

This contract defines the complete public API surface for the Tools package.
All types listed here are `public`. Internal types (`ToolTelemetryAgent`,
`ToolInvocationMapper`, `InvocationAttemptRegistry`, `ToolPayloadSerializer`) are
implementation details and subject to change without notice.

---

## Namespace: `Melic.AgentFramework.Observability.Tools`

### `ToolTelemetryAgentBuilderExtensions` (static class)

```csharp
/// <summary>
/// Extension methods for configuring tool-call telemetry on an <see cref="AIAgentBuilder"/> pipeline.
/// </summary>
public static class ToolTelemetryAgentBuilderExtensions
{
    /// <summary>
    /// Adds tool-call telemetry to the agent pipeline. Each intercepted tool execution enriches
    /// MAF's current <c>execute_tool</c> span with structured <c>genai.tool.*</c> attributes, or
    /// emits a fallback <c>agent_tool_call</c> span when no MAF tool span is current.
    /// </summary>
    /// <param name="builder">The <see cref="AIAgentBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="ToolTelemetryOptions"/>. When
    /// <see langword="null"/>, defaults are applied.
    /// </param>
    /// <returns>The same <see cref="AIAgentBuilder"/> instance for chaining.</returns>
    public static AIAgentBuilder UseToolTelemetry(
        this AIAgentBuilder builder,
        Action<ToolTelemetryOptions>? configure = null);
}
```

### `ToolTelemetryOptions` (sealed class)

```csharp
/// <summary>
/// Configuration options for the tool telemetry middleware.
/// Resolved once at agent build time and applied to all subsequent tool invocations.
/// </summary>
public sealed class ToolTelemetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether serialized tool input is emitted as
    /// <c>genai.tool.input</c>. Default: <see langword="true"/>.
    /// </summary>
    public bool CaptureInput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether serialized tool output is emitted as
    /// <c>genai.tool.output</c>. Default: <see langword="true"/>.
    /// </summary>
    public bool CaptureOutput { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum final character length for the JSON payload written to
    /// <c>genai.tool.input</c>. The emitted value remains valid JSON. Default: <c>2048</c>.
    /// </summary>
    public int MaxInputLength { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the maximum final character length for the JSON payload written to
    /// <c>genai.tool.output</c>. The emitted value remains valid JSON. Default: <c>2048</c>.
    /// </summary>
    public int MaxOutputLength { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the <see cref="ActivitySource"/> name used for fallback <c>agent_tool_call</c> spans
    /// when no MAF <c>execute_tool</c> span is current.
    /// Default: <c>"Melic.AgentFramework.Observability.Tools"</c>.
    /// </summary>
    public string ActivitySourceName { get; set; } = "Melic.AgentFramework.Observability.Tools";
}
```

---

## Namespace: `Melic.AgentFramework.Observability.Abstractions`

### `ToolAttributeNames` (static class) — new constants added by this feature

```csharp
/// <summary>
/// Constants for telemetry attribute names emitted by the Tools package.
/// </summary>
public static class ToolAttributeNames
{
    // Fallback agent_tool_call identity attributes. MAF execute_tool spans already use gen_ai.tool.*.
    public static readonly string ToolName     = "genai.tool.name";
    public static readonly string ToolCallId   = "genai.tool.call_id";
    public static readonly string ToolInput    = "genai.tool.input";
    public static readonly string ToolOutput   = "genai.tool.output";
    public static readonly string IsRetry      = "genai.tool.is_retry";
    public static readonly string AttemptIndex = "genai.tool.attempt_index";
}
```

---

## Behavioral Guarantees

- `UseToolTelemetry()` is optional and opt-in.
- When not configured, agent and tool behavior remain unchanged.
- MAF `execute_tool` spans are enriched in place when current.
- Fallback tool spans use `ActivityKind.Internal`.
- Input and output capture are independently configurable.
- All tool telemetry is best-effort: serialization or span-writing failures never alter tool execution results.
- Retry tracking is scoped to a single parent `invoke_agent` span and only applies when a non-empty tool `call_id` is available.

---

## Breaking Change Policy

- No public type, method, or property may be removed or have its signature changed in a patch or minor release.
- The default fallback `ActivitySourceName` (`"Melic.AgentFramework.Observability.Tools"`) is stable once published.
- `ToolAttributeNames` values are stable contract surface for telemetry consumers and must not be renamed in a patch or minor release.
