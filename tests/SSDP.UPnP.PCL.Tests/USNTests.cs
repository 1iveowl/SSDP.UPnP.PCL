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

    // A USN whose device UUID was never set used to compose "uuid:", and one whose
    // service type was never set used to compose "urn:schemas-upnp-org:service::0".
    // Both are Required headers carrying nonsense; ST has always rejected the same
    // shapes, and now so does this side.
    [Fact]
    public void ToUsnString_WithoutDeviceUuid_Throws()
    {
        Assert.Throws<SSDPException>(() => new USN { EntityType = EntityType.Device }.ToUsnString());
        Assert.Throws<SSDPException>(() => new USN { EntityType = EntityType.RootDevice }.ToUsnString());
    }

    // Version is set here on purpose. Leaving it at 0 would let the version guard
    // throw instead, and the test would pass with the type-name guard deleted.
    [Fact]
    public void ToUsnString_WithoutServiceType_Throws()
    {
        Assert.Throws<SSDPException>(() =>
            new USN { EntityType = EntityType.ServiceType, DeviceUUID = "device-1", Version = 1 }.ToUsnString());
    }

    [Fact]
    public void ToUsnString_WithoutVersion_Throws()
    {
        Assert.Throws<SSDPException>(() =>
            new USN { EntityType = EntityType.ServiceType, DeviceUUID = "device-1", TypeName = "Svc" }.ToUsnString());
    }

    [Fact]
    public void ToUsnString_DomainFormWithoutDomain_Throws()
    {
        Assert.Throws<SSDPException>(() =>
            new USN { EntityType = EntityType.DomainService, DeviceUUID = "device-1", TypeName = "Svc", Version = 1 }
                .ToUsnString());
    }

    [Fact]
    public void ToUsnString_FullySpecified_ComposesTheWireForm()
    {
        var usn = new USN
        {
            EntityType = EntityType.ServiceType,
            DeviceUUID = "device-1",
            TypeName = "ContentDirectory",
            Version = 1
        };

        Assert.Equal("uuid:device-1::urn:schemas-upnp-org:service:ContentDirectory:1", usn.ToUsnString());
    }

    // Observed on a real network: a Vera controller advertising a bridged Pioneer
    // receiver with "urn:pioneer-com:serviceId:Receiver:1" as the entity part. That
    // is a serviceId URN (UDA 2.0 section 2.3, description documents, and no
    // version suffix) used where a search target belongs. Unreadable - but the
    // device UUID in front of it is not, and the device is real.
    [Theory]
    [InlineData("uuid:4d494342-5342-5645-01d2-000002fc7f93::urn:pioneer-com:serviceId:Receiver:1")]
    [InlineData("uuid:device-1::not-a-urn-at-all")]
    [InlineData("uuid:device-1::urn:schemas-upnp-org:device:MediaServer:0")]
    [InlineData("uuid:device-1::urn:acme-org:widget:Thing:1")]
    public void Parse_UnreadableEntityPart_KeepsTheDeviceIdentity(string usn)
    {
        var result = USN.Parse(usn);

        Assert.True(result.IsSuccess);
        Assert.Equal(EntityType.Unknown, result.Value.EntityType);
        Assert.Equal(usn[5..usn.IndexOf("::", StringComparison.Ordinal)], result.Value.DeviceUUID);
        Assert.Equal(usn, result.Value.USNString);
    }

    // The reason Unknown exists rather than just keeping the UUID: a bare
    // "uuid:[id]" is a device advertising itself, which is a real statement.
    // Collapsing the two would report something the sender never said.
    [Fact]
    public void Parse_DistinguishesAnUnreadableEntityFromTheDeviceItself()
    {
        var deviceItself = USN.Parse("uuid:device-1");
        var unreadable = USN.Parse("uuid:device-1::urn:acme-org:widget:Thing:1");

        Assert.Equal(EntityType.Device, deviceItself.Value!.EntityType);
        Assert.Equal(EntityType.Unknown, unreadable.Value!.EntityType);
        Assert.Equal(deviceItself.Value.DeviceUUID, unreadable.Value.DeviceUUID);
    }

    // Lenient on receive does not mean composable on send: this library will not
    // put an entity it could not read back on the wire.
    [Fact]
    public void ToUsnString_OnAnUnreadableEntity_Throws()
    {
        var parsed = USN.Parse("uuid:device-1::urn:acme-org:widget:Thing:1").Value!;

        Assert.Throws<SSDPException>(() => parsed.ToUsnString());
        Assert.Throws<SSDPException>(() => parsed.ToUriString());
    }

    // A USN that identifies nothing at all is still rejected - the leniency is
    // about the entity part, not about the device UUID.
    [Theory]
    [InlineData("urn:schemas-upnp-org:device:MediaServer:1")]
    [InlineData("uuid:")]
    [InlineData("uuid:::x")]
    public void Parse_WithoutAUsableDeviceUuid_StillFails(string usn)
    {
        Assert.False(USN.Parse(usn).IsSuccess);
    }
}
