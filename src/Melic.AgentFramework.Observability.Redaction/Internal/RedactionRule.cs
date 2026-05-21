// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;

namespace Melic.AgentFramework.Observability.Redaction.Internal;

internal sealed record RedactionRule(string Name, Regex Pattern)
{
    internal string Apply(string input, string replacementText, out int matchCount)
    {
        int count = 0;
        string result = Pattern.Replace(input, match =>
        {
            count++;
            return replacementText;
        });

        matchCount = count;
        return result;
    }
}
