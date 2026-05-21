// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Redaction.Internal;

internal readonly record struct RedactionResult(
    string Value,
    bool Changed,
    int MatchCount,
    int FailureCount,
    bool WasTooLong);
