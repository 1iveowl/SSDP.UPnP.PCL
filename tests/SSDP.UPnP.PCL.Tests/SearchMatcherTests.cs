using System.Net;
using SSDP.UPnP.PCL.Internal;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class SearchMatcherTests
{
    private static readonly RootDeviceConfiguration Root = new()
    {
        DeviceUUID = "root-uuid",
        TypeName = "RootDevice",
        Version = 2,
        BOOTID = 100,
        CONFIGID = 9,
        CacheControl = TimeSpan.FromSeconds(1800),
        Location = new Uri("http://192.168.0.10/description.xml"),
        IpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.10"), 1901),
        Services =
        [
            new ServiceConfiguration { TypeName = "RootService", Version = 1 },
            new ServiceConfiguration { Domain = "domain-org", TypeName = "DomainService", Version = 2 }
        ],
        EmbeddedDevices =
        [
            new DeviceConfiguration
            {
                DeviceUUID = "embedded-uuid",
                TypeName = "EmbeddedDevice",
                Version = 3,
                BOOTID = 200,
                Services =
                [
                    new ServiceConfiguration { TypeName = "EmbeddedService", Version = 1 }
                ]
            }
        ]
    };

    private static ST Search(STType type, string? typeName = null, int version = 0, string? domain = null, string? uuid = null) =>
        new() { StSearchType = type, TypeName = typeName, Version = version, Domain = domain, DeviceUUID = uuid };

    [Fact]
    public void AdvertisementMessages_FollowUdaMatrix()
    {
        var messages = SearchMatcher.AdvertisementMessages(Root).ToList();

        // 3 for the root device + 2 for the embedded device + 3 services.
        Assert.Equal(8, messages.Count);

        var uris = messages.Select(message => message.Entity.ToUriString()).ToList();

        Assert.Equal(
        [
            "upnp:rootdevice",
            "uuid:root-uuid",
            "urn:schemas-upnp-org:device:RootDevice:2",
            "uuid:embedded-uuid",
            "urn:schemas-upnp-org:device:EmbeddedDevice:3",
            "urn:schemas-upnp-org:service:EmbeddedService:1",
            "urn:schemas-upnp-org:service:RootService:1",
            "urn:domain-org:service:DomainService:2"
        ], uris);
    }

    [Fact]
    public void AdvertisementMessages_DeriveDomainForms()
    {
        var domainRoot = Root with { Domain = "acme-com" };

        var deviceTypeMessage = SearchMatcher.AdvertisementMessages(domainRoot)
            .Single(message => message.Entity.EntityType is EntityType.DeviceType or EntityType.DomainDevice
                               && message.Owner.DeviceUUID == "root-uuid");

        Assert.Equal(EntityType.DomainDevice, deviceTypeMessage.Entity.EntityType);
        Assert.Equal("urn:acme-com:device:RootDevice:2", deviceTypeMessage.Entity.ToUriString());
    }

    [Fact]
    public void All_ReturnsFullMatrix()
    {
        Assert.Equal(8, SearchMatcher.MatchingMessages(Root, Search(STType.All)).Count());
    }

    [Fact]
    public void RootDeviceSearch_ReturnsRootMessageOnly()
    {
        var messages = SearchMatcher.MatchingMessages(Root, Search(STType.RootDeviceSearch)).ToList();

        var message = Assert.Single(messages);
        Assert.Equal(EntityType.RootDevice, message.Entity.EntityType);
        Assert.Equal("root-uuid", message.Owner.DeviceUUID);
    }

    [Theory]
    [InlineData("root-uuid")]
    [InlineData("embedded-uuid")]
    public void UuidSearch_FindsTheDevice(string uuid)
    {
        var messages = SearchMatcher.MatchingMessages(Root, Search(STType.UuidSearch, uuid: uuid)).ToList();

        var message = Assert.Single(messages);
        Assert.Equal(EntityType.Device, message.Entity.EntityType);
        Assert.Equal(uuid, message.Owner.DeviceUUID);
    }

    [Fact]
    public void UuidSearch_UnknownUuid_ReturnsNothing()
    {
        Assert.Empty(SearchMatcher.MatchingMessages(Root, Search(STType.UuidSearch, uuid: "nope")));
    }

    [Theory]
    [InlineData(1, true)]  // device version 3 answers searches for version 1
    [InlineData(3, true)]  // exact match
    [InlineData(4, false)] // future version is not supported
    public void DeviceTypeSearch_HonorsVersionBackwardsCompatibility(int searchVersion, bool expectMatch)
    {
        var messages = SearchMatcher.MatchingMessages(
            Root, Search(STType.DeviceTypeSearch, "EmbeddedDevice", searchVersion));

        Assert.Equal(expectMatch, messages.Any());
    }

    [Fact]
    public void DeviceTypeSearch_ComparesTypeName()
    {
        Assert.Empty(SearchMatcher.MatchingMessages(Root, Search(STType.DeviceTypeSearch, "SomeOtherDevice", 1)));
    }

    [Fact]
    public void ServiceTypeSearch_DoesNotMatchDomainServices()
    {
        Assert.Empty(SearchMatcher.MatchingMessages(Root, Search(STType.ServiceTypeSearch, "DomainService", 1)));
    }

    [Fact]
    public void DomainServiceSearch_MatchesDomain()
    {
        var messages = SearchMatcher.MatchingMessages(
            Root, Search(STType.DomainServiceSearch, "DomainService", 1, domain: "domain-org")).ToList();

        var message = Assert.Single(messages);
        Assert.Equal("DomainService", message.Entity.TypeName);
        Assert.Equal("root-uuid", message.Owner.DeviceUUID);
    }

    [Fact]
    public void DomainServiceSearch_WrongDomain_ReturnsNothing()
    {
        Assert.Empty(SearchMatcher.MatchingMessages(
            Root, Search(STType.DomainServiceSearch, "DomainService", 1, domain: "other-org")));
    }

    [Theory]
    [InlineData("RootService", "root-uuid")]
    [InlineData("EmbeddedService", "embedded-uuid")]
    public void ServiceMessages_CarryTheOwningDevice(string serviceType, string expectedOwnerUuid)
    {
        var messages = SearchMatcher.MatchingMessages(
            Root, Search(STType.ServiceTypeSearch, serviceType, 1)).ToList();

        var message = Assert.Single(messages);
        Assert.Equal(expectedOwnerUuid, message.Owner.DeviceUUID);
    }

    [Fact]
    public void ValueEqualServicesOnDifferentDevices_KeepTheirOwnOwners()
    {
        // Two field-identical service records under different devices must not be
        // confused with each other (regression: owner lookups must never rely on
        // record value equality).
        var duplicated = Root with
        {
            Services = [new ServiceConfiguration { TypeName = "DupService", Version = 1 }],
            EmbeddedDevices =
            [
                Root.EmbeddedDevices[0] with
                {
                    Services = [new ServiceConfiguration { TypeName = "DupService", Version = 1 }]
                }
            ]
        };

        var owners = SearchMatcher.MatchingMessages(duplicated, Search(STType.ServiceTypeSearch, "DupService", 1))
            .Select(message => message.Owner.DeviceUUID)
            .ToList();

        Assert.Equal(2, owners.Count);
        Assert.Contains("root-uuid", owners);
        Assert.Contains("embedded-uuid", owners);
    }

    [Fact]
    public void BuildResponses_UsesOwnerIdentityAndRootConfiguration()
    {
        var request = new MSearchRequest
        {
            ST = Search(STType.ServiceTypeSearch, "EmbeddedService", 1),
            MX = TimeSpan.FromSeconds(1),
            RemoteIpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 41000)
        };

        var date = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);

        var responses = SearchMatcher.BuildResponses(Root, request, date, searchPort: 1901).ToList();

        var response = Assert.Single(responses);
        Assert.Equal("embedded-uuid", response.USN?.DeviceUUID);
        Assert.Equal(200u, response.BOOTID);
        Assert.Equal(9, response.CONFIGID);
        Assert.Equal(1901, response.SEARCHPORT);
        Assert.Equal(Root.CacheControl, response.CacheControl);
        Assert.Equal(Root.Location, response.Location);
        Assert.Equal(date, response.Date);
        Assert.Equal(request.RemoteIpEndPoint, response.RemoteIpEndPoint);
        Assert.Equal("uuid:embedded-uuid::urn:schemas-upnp-org:service:EmbeddedService:1", response.USN?.ToUsnString());
    }

    [Fact]
    public void BuildResponses_EchoTheRequestedVersionInSt_ButKeepAdvertisedVersionInUsn()
    {
        // UDA 2.0 §1.3.3: a device supporting v3 answers a v1 search with ST v1,
        // while the USN carries the advertised (actual) identity.
        var request = new MSearchRequest
        {
            ST = Search(STType.DeviceTypeSearch, "EmbeddedDevice", 1),
            RemoteIpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 41000)
        };

        var response = Assert.Single(
            SearchMatcher.BuildResponses(Root, request, DateTimeOffset.UnixEpoch, searchPort: 1900));

        Assert.Equal(1, response.ST?.Version);
        Assert.Equal("urn:schemas-upnp-org:device:EmbeddedDevice:1", response.ST?.ToSearchTargetString());
        Assert.Equal("uuid:embedded-uuid::urn:schemas-upnp-org:device:EmbeddedDevice:3", response.USN?.ToUsnString());
    }

    [Fact]
    public void AdvertisementMessages_DedupeServiceTypesWithinOneDevice()
    {
        // UDA 2.0 §1.2.2: multiple instances of the same service type within one
        // device are advertised once; a different version is a different type URI.
        var root = Root with
        {
            Services =
            [
                new ServiceConfiguration { TypeName = "Twin", Version = 1 },
                new ServiceConfiguration { TypeName = "Twin", Version = 1 },
                new ServiceConfiguration { TypeName = "Twin", Version = 2 }
            ],
            EmbeddedDevices = []
        };

        var serviceUris = SearchMatcher.AdvertisementMessages(root)
            .Where(message => message.Entity.EntityType == EntityType.ServiceType)
            .Select(message => message.Entity.ToUriString())
            .ToList();

        Assert.Equal(
        [
            "urn:schemas-upnp-org:service:Twin:1",
            "urn:schemas-upnp-org:service:Twin:2"
        ], serviceUris);
    }

    [Fact]
    public void BuildResponses_OnDefaultPort_OmitsSearchPort()
    {
        var request = new MSearchRequest
        {
            ST = Search(STType.RootDeviceSearch),
            RemoteIpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 41000)
        };

        var response = Assert.Single(
            SearchMatcher.BuildResponses(Root, request, DateTimeOffset.UnixEpoch, searchPort: 1900));

        Assert.Null(response.SEARCHPORT);
    }
}
