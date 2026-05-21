// SPDX-License-Identifier: MIT
using Melic.AgentFramework.Observability.Redaction;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionPackageIndependenceTests
{
    [Fact]
    public void RedactionAssemblyDoesNotReferenceSiblingOrExporterPackages()
    {
        string[] references = typeof(RedactionOptions).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("Melic.AgentFramework.Observability.Sessions", references);
        Assert.DoesNotContain("Melic.AgentFramework.Observability.Tools", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Azure", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("ApplicationInsights", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Exporter", StringComparison.Ordinal));
    }
}
