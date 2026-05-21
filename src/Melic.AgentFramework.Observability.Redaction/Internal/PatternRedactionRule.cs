// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;

namespace Melic.AgentFramework.Observability.Redaction.Internal;

internal static class PatternRedactionRule
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(100);

    internal static RedactionRule Create(string name, string pattern)
    {
        var regex = new Regex(
            pattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            DefaultTimeout);

        return new RedactionRule(name, regex);
    }

    internal static IReadOnlyList<RedactionRule> CreateDefaultRules() =>
    [
        Create("email", @"(?<![\w.%+-])[\w.%+-]+@[\w.-]+\.[A-Za-z]{2,}(?![\w.-])"),
        Create("phone", @"(?<!\w)\+?\d[\d\s().-]{7,}\d(?!\w)"),
        Create("bearer-token", @"(?i)\bBearer\s+[A-Za-z0-9._\-~+/]+=*"),
        Create("api-token-fragment", @"(?i)\b(api[-_ ]?key|token|access[-_ ]?token|refresh[-_ ]?token|secret)\b\s*[:=]\s*[""']?[^""',;\s}]+"),
        Create("connection-string-secret", @"(?i)\b(AccountKey|SharedAccessKey|Password|Pwd|User ID|Uid)\s*=\s*[^;\s]+"),
        Create("password-fragment", @"(?i)\b(password|passwd|pwd)\b\s*[:=]\s*[""']?[^""',;\s}]+")
    ];
}
