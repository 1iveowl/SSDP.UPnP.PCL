using System.Reflection;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Analyzers.Tests;

/// <summary>
/// Holds <see cref="SsdpStub"/> and the analyzer's type-name constants to the real
/// library.
/// </summary>
/// <remarks>
/// The rules match on fully qualified metadata names, and the tests exercise them
/// against a stub. Both are strings, and strings do not fail to compile when
/// somebody renames a property. Without this, renaming
/// <c>RootDeviceConfiguration.CONFIGID</c> would leave every analyzer test green
/// and every real diagnostic silent - the exact failure a passing build cannot
/// distinguish from a correct one.
/// </remarks>
public class StubFidelityTests
{
    private static readonly Assembly Library = typeof(MulticastMSearch).Assembly;

    [Theory]
    [InlineData("SSDP.UPnP.PCL.Model.MxSeconds")]
    [InlineData("SSDP.UPnP.PCL.Model.DynamicPort")]
    [InlineData("SSDP.UPnP.PCL.Model.RootDeviceConfiguration")]
    [InlineData("SSDP.UPnP.PCL.Model.MulticastMSearch")]
    [InlineData("SSDP.UPnP.PCL.Model.UnicastMSearch")]
    public void TheAnalyzerLooksForTypesThatExist(string metadataName) =>
        Assert.NotNull(Library.GetType(metadataName, throwOnError: false));

    [Theory]
    [InlineData(typeof(RootDeviceConfiguration), "CONFIGID")]
    [InlineData(typeof(RootDeviceConfiguration), "IpEndPoint")]
    [InlineData(typeof(MulticastMSearch), "MX")]
    [InlineData(typeof(MulticastMSearch), "TCPPORT")]
    [InlineData(typeof(MulticastMSearch), "CPFN")]
    [InlineData(typeof(UnicastMSearch), "Target")]
    public void TheAnalyzerLooksForPropertiesThatExist(Type type, string propertyName) =>
        Assert.NotNull(type.GetProperty(propertyName));

    [Fact]
    public void MxSecondsTakesASingleIntConstructor()
    {
        var constructor = typeof(MxSeconds).GetConstructor([typeof(int)]);

        Assert.NotNull(constructor);
    }

    [Fact]
    public void DynamicPortTakesASingleIntConstructor()
    {
        var constructor = typeof(DynamicPort).GetConstructor([typeof(int)]);

        Assert.NotNull(constructor);
    }

    // The ranges are written into the analyzer as literals, because it cannot
    // reference the library. These pin them to the values the library enforces, so
    // the two cannot drift apart in silence.
    [Fact]
    public void TheDynamicPortRangeMatchesTheLibrary()
    {
        Assert.Equal(49152, DynamicPort.MinimumPort);
        Assert.Equal(65535, DynamicPort.MaximumPort);
    }

    [Fact]
    public void TheMxRecommendedMaximumMatchesTheLibrary()
    {
        Assert.Equal(5, MxSeconds.RecommendedMaximumSeconds);
        Assert.Equal(1, MxSeconds.MinimumSeconds);
    }

    [Fact]
    public void TheStubDeclaresTheTypesTheRulesMatchOn()
    {
        Assert.Contains("public readonly struct MxSeconds", SsdpStub.Source, StringComparison.Ordinal);
        Assert.Contains("public readonly struct DynamicPort", SsdpStub.Source, StringComparison.Ordinal);
        Assert.Contains("public sealed record RootDeviceConfiguration", SsdpStub.Source, StringComparison.Ordinal);
        Assert.Contains("public sealed record MulticastMSearch", SsdpStub.Source, StringComparison.Ordinal);
        Assert.Contains("namespace SSDP.UPnP.PCL.Model", SsdpStub.Source, StringComparison.Ordinal);
    }
}
