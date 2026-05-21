// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Redaction.Internal;

internal sealed class AttributeTargetMatcher
{
    private readonly HashSet<string> _exactAttributes;
    private readonly List<string> _prefixes;
    private readonly HashSet<string> _excludedAttributes;
    private readonly HashSet<string> _standardExactAttributes;
    private readonly List<string> _standardPrefixes;

    internal AttributeTargetMatcher(
        IEnumerable<string> exactAttributes,
        IEnumerable<string> prefixes,
        IEnumerable<string> excludedAttributes,
        IEnumerable<string> standardExactAttributes,
        IEnumerable<string> standardPrefixes)
    {
        _exactAttributes = new HashSet<string>(exactAttributes, StringComparer.Ordinal);
        _prefixes = [.. prefixes];
        _excludedAttributes = new HashSet<string>(excludedAttributes, StringComparer.Ordinal);
        _standardExactAttributes = new HashSet<string>(standardExactAttributes, StringComparer.Ordinal);
        _standardPrefixes = [.. standardPrefixes];
    }

    internal bool IsTargeted(string attributeName)
    {
        if (_excludedAttributes.Contains(attributeName))
        {
            return false;
        }

        if (attributeName.StartsWith("gen_ai.", StringComparison.Ordinal))
        {
            return _standardExactAttributes.Contains(attributeName) || HasPrefix(attributeName, _standardPrefixes);
        }

        return _exactAttributes.Contains(attributeName) || HasPrefix(attributeName, _prefixes);
    }

    private static bool HasPrefix(string value, IReadOnlyList<string> prefixes)
    {
        foreach (string prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
