using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class USNTests
{
    [Fact]
    public void Parse_DeviceOnly()
    {
        var result = USN.Parse("uuid:device-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(EntityType.Device, result.Value.EntityType);
        Assert.Equal("device-1", result.Value.DeviceUUID);
    }

    [Fact]
    public void Parse_RootDevice()
    {
        var result = USN.Parse("uuid:device-1::upnp:rootdevice");

        Assert.True(result.IsSuccess);
        Assert.Equal(EntityType.RootDevice, result.Value.EntityType);
        Assert.Equal("device-1", result.Value.DeviceUUID);
    }

    [Fact]
    public void Parse_ServiceType()
    {
        var result = USN.Parse("uuid:device-1::urn:schemas-upnp-org:service:ContentDirectory:1");

        Assert.True(result.IsSuccess);
        Assert.Equal(EntityType.ServiceType, result.Value.EntityType);
        Assert.Equal("ContentDirectory", result.Value.TypeName);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal("device-1", result.Value.DeviceUUID);
    }

    [Fact]
    public void Parse_DomainDeviceType()
    {
        var result = USN.Parse("uuid:device-1::urn:my-domain-org:device:CoffeeMaker:2");

        Assert.True(result.IsSuccess);
        Assert.Equal(EntityType.DomainDevice, result.Value.EntityType);
        Assert.Equal("my-domain-org", result.Value.Domain);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("urn:schemas-upnp-org:device:X:1")]
    [InlineData("uuid:")]
    public void Parse_InvalidValues_Fail(string? usn)
    {
        var result = USN.Parse(usn);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("uuid:device-1")]
    [InlineData("uuid:device-1::upnp:rootdevice")]
    [InlineData("uuid:device-1::urn:schemas-upnp-org:device:MediaServer:3")]
    [InlineData("uuid:device-1::urn:my-domain-org:service:Brewing:1")]
    public void Parse_ThenCompose_RoundTrips(string usn)
    {
        var result = USN.Parse(usn);

        Assert.True(result.IsSuccess);
        Assert.Equal(usn, result.Value.ToUsnString());
    }
}
