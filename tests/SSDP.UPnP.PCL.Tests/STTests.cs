using SSDP.UPnP.PCL;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class STTests
{
    [Fact]
    public void Parse_SsdpAll()
    {
        var result = ST.Parse("ssdp:all");

        Assert.True(result.IsSuccess);
        Assert.Equal(STType.All, result.Value.StSearchType);
        Assert.Equal("ssdp:all", result.Value.STString);
    }

    [Fact]
    public void Parse_RootDevice()
    {
        var result = ST.Parse("upnp:rootdevice");

        Assert.True(result.IsSuccess);
        Assert.Equal(STType.RootDeviceSearch, result.Value.StSearchType);
        Assert.Equal(EntityType.RootDevice, result.Value.EntityType);
    }

    [Fact]
    public void Parse_Uuid_KeepsTheUuid()
    {
        var result = ST.Parse("uuid:0e5e8f0a-4dd4-4f8e-b6a5-9d9f160664bc");

        Assert.True(result.IsSuccess);
        Assert.Equal(STType.UuidSearch, result.Value.StSearchType);
        Assert.Equal("0e5e8f0a-4dd4-4f8e-b6a5-9d9f160664bc", result.Value.DeviceUUID);
    }

    [Fact]
    public void Parse_StandardDeviceType()
    {
        var result = ST.Parse("urn:schemas-upnp-org:device:MediaServer:3");

        Assert.True(result.IsSuccess);
        Assert.Equal(STType.DeviceTypeSearch, result.Value.StSearchType);
        Assert.Equal(EntityType.DeviceType, result.Value.EntityType);
        Assert.Equal("MediaServer", result.Value.TypeName);
        Assert.Equal(3, result.Value.Version);
        Assert.Null(result.Value.Domain);
    }

    [Fact]
    public void Parse_StandardServiceType()
    {
        var result = ST.Parse("urn:schemas-upnp-org:service:ContentDirectory:1");

        Assert.True(result.IsSuccess);
        Assert.Equal(STType.ServiceTypeSearch, result.Value.StSearchType);
        Assert.Equal("ContentDirectory", result.Value.TypeName);
    }

    [Fact]
    public void Parse_DomainDeviceType()
    {
        var result = ST.Parse("urn:my-domain-org:device:CoffeeMaker:2");

        Assert.True(result.IsSuccess);
        Assert.Equal(STType.DomainDeviceSearch, result.Value.StSearchType);
        Assert.Equal("my-domain-org", result.Value.Domain);
        Assert.Equal("CoffeeMaker", result.Value.TypeName);
        Assert.Equal(2, result.Value.Version);
    }

    [Fact]
    public void Parse_DomainServiceType()
    {
        var result = ST.Parse("urn:my-domain-org:service:Brewing:1");

        Assert.True(result.IsSuccess);
        Assert.Equal(STType.DomainServiceSearch, result.Value.StSearchType);
        Assert.Equal("my-domain-org", result.Value.Domain);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ssdp:discover")]
    [InlineData("upnp:something")]
    [InlineData("uuid:")]
    [InlineData("urn:schemas-upnp-org:device:TooShort")]
    [InlineData("urn:schemas-upnp-org:gadget:Name:1")]
    [InlineData("urn:schemas-upnp-org:device:Name:0")]
    [InlineData("urn:schemas-upnp-org:device:Name:abc")]
    [InlineData("gibberish")]
    public void Parse_InvalidValues_Fail(string? searchTarget)
    {
        var result = ST.Parse(searchTarget);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("ssdp:all")]
    [InlineData("upnp:rootdevice")]
    [InlineData("uuid:12345")]
    [InlineData("urn:schemas-upnp-org:device:MediaServer:3")]
    [InlineData("urn:schemas-upnp-org:service:ContentDirectory:1")]
    [InlineData("urn:my-domain-org:device:CoffeeMaker:2")]
    [InlineData("urn:my-domain-org:service:Brewing:1")]
    public void Parse_ThenCompose_RoundTrips(string searchTarget)
    {
        var result = ST.Parse(searchTarget);

        Assert.True(result.IsSuccess);
        Assert.Equal(searchTarget, result.Value.ToSearchTargetString());
    }

    [Fact]
    public void ToSearchTargetString_TypeSearchWithoutTypeName_Throws()
    {
        var st = new ST { StSearchType = STType.DeviceTypeSearch, Version = 1 };

        Assert.Throws<SSDPException>(() => st.ToSearchTargetString());
    }

    [Fact]
    public void ToSearchTargetString_TypeSearchWithVersionOne_DoesNotThrow()
    {
        var st = new ST { StSearchType = STType.ServiceTypeSearch, TypeName = "X", Version = 1 };

        Assert.Equal("urn:schemas-upnp-org:service:X:1", st.ToSearchTargetString());
    }

    [Fact]
    public void ToSearchTargetString_TypeSearchWithoutVersion_Throws()
    {
        var st = new ST { StSearchType = STType.ServiceTypeSearch, TypeName = "X" };

        Assert.Throws<SSDPException>(() => st.ToSearchTargetString());
    }

    [Fact]
    public void ToSearchTargetString_DomainSearchWithoutDomain_Throws()
    {
        var st = new ST { StSearchType = STType.DomainDeviceSearch, TypeName = "X", Version = 1 };

        Assert.Throws<SSDPException>(() => st.ToSearchTargetString());
    }

    // Span-boundary cases. Split handled these incidentally; a hand-written span
    // walk is exactly where that stops being true, so they are pinned explicitly.
    [Theory]
    [InlineData("ssdp:")]
    [InlineData("ssdp")]
    [InlineData("ssdp:all:extra")]
    [InlineData("upnp:")]
    [InlineData("upnp")]
    [InlineData("upnp:rootdevice:extra")]
    [InlineData("uuid:")]
    [InlineData("uuid")]
    [InlineData("urn:")]
    [InlineData("urn:schemas-upnp-org:device:MediaServer")]
    [InlineData("urn:schemas-upnp-org:device:MediaServer:1:extra")]
    [InlineData(":")]
    [InlineData("::")]
    public void Parse_MalformedEdges_Fail(string value)
    {
        Assert.False(ST.Parse(value).IsSuccess);
    }

    [Theory]
    [InlineData("SSDP:ALL", STType.All)]
    [InlineData("UPnP:RootDevice", STType.RootDeviceSearch)]
    [InlineData("UUID:abc", STType.UuidSearch)]
    [InlineData("URN:schemas-upnp-org:DEVICE:MediaServer:1", STType.DeviceTypeSearch)]
    [InlineData("urn:SCHEMAS-UPNP-ORG:service:MediaServer:1", STType.ServiceTypeSearch)]
    public void Parse_IsCaseInsensitiveOnTheSchemeAndKind(string value, STType expected)
    {
        var result = ST.Parse(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.StSearchType);
    }

    // A UUID may itself contain colons, and everything after "uuid:" belongs to it.
    [Fact]
    public void Parse_UuidWithColons_KeepsTheWholeRemainder()
    {
        var result = ST.Parse("uuid:a:b:c");

        Assert.True(result.IsSuccess);
        Assert.Equal("a:b:c", result.Value.DeviceUUID);
    }
}
