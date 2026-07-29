using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;
using Microsoft.CodeAnalysis.Testing;
using SSDP.UPnP.PCL.Analyzers.CodeFixes;

namespace SSDP.UPnP.PCL.Analyzers.Tests;

/// <summary>
/// Test harness: prepends <see cref="SsdpStub"/> to each source and uses
/// <see cref="DefaultVerifier"/> rather than a framework-specific package, since
/// the <c>*.XUnit</c> ones are deprecated.
/// </summary>
internal static class Verify
{
    // net9.0 is the ceiling in Microsoft.CodeAnalysis.Testing, and SSDP.UPnP.PCL is
    // net10.0 - which is why the rules are exercised against a source stub rather
    // than a reference to the real library. StubFidelityTests keeps the stub honest.
    private static readonly ReferenceAssemblies References = ReferenceAssemblies.Net.Net90;

    internal static Task AnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SsdpValueRangeAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = References,
            TestState = { Sources = { SsdpStub.Source, source } }
        };

        test.ExpectedDiagnostics.AddRange(expected);

        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    internal static Task CodeFixAsync(string source, string fixedSource)
    {
        var test = new CSharpCodeFixTest<
            SsdpValueRangeAnalyzer,
            ClampMxToRecommendedMaximumCodeFixProvider,
            DefaultVerifier>
        {
            ReferenceAssemblies = References,
            TestState = { Sources = { SsdpStub.Source, source } },
            FixedState = { Sources = { SsdpStub.Source, fixedSource } }
        };

        return test.RunAsync(TestContext.Current.CancellationToken);
    }
}
