using System.Net;
using SSDP.UPnP.PCL.Internal;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class SearchMatcherTests
{
    private static readonly RootDeviceConfiguration Root = new()
    {
        EntityType = EntityType.RootDevice,
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
            new ServiceConfiguration { EntityType = EntityType.ServiceType, TypeName = "RootService", Version = 1 },
            new ServiceConfiguration { EntityType = EntityType.DomainService, Domain = "domain-org", TypeName = "DomainService", Version = 2 }
        ],
        EmbeddedDevices =
        [
            new DeviceConfiguration
            {
                EntityType = EntityType.Device,
                DeviceUUID = "embedded-uuid",
                TypeName = "EmbeddedDevice",
                Version = 3,
                BOOTID = 200,
                Services =
                [
                    new ServiceConfiguration { EntityType = EntityType.ServiceType, TypeName = "EmbeddedService", Version = 1 }
                ]
            }
        ]
    };

    private static ST Search(STType type, string? typeName = null, int version = 0, string? domain = null, string? uuid = null) =>
        new() { StSearchType = type, TypeName = typeName, Version = version, Domain = domain, DeviceUUID = uuid };

    [Fact]
    public void All_ReturnsEveryDeviceAndService()
    {
        var entities = SearchMatcher.MatchingEntities(Root, Search(STType.All)).ToList();

        // 2 devices (root + embedded) + 3 services.
        Assert.Equal(5, entities.Count);
    }

    [Fact]
    public void RootDeviceSearch_ReturnsRootOnly()
    {
        var entities = SearchMatcher.MatchingEntities(Root, Search(STType.RootDeviceSearch)).ToList();

        var entity = Assert.Single(entities);
        Assert.Same(Root, entity);
    }

    [Fact]
    public void UuidSearch_FindsEmbeddedDevice()
    {
        var entities = SearchMatcher.MatchingEntities(Root, Search(STType.UuidSearch, uuid: "embedded-uuid")).ToList();

        var entity = Assert.Single(entities);
        Assert.Equal("embedded-uuid", entity.DeviceUUID);
    }

    [Fact]
    public void UuidSearch_UnknownUuid_ReturnsNothing()
    {
        Assert.Empty(SearchMatcher.MatchingEntities(Root, Search(STType.UuidSearch, uuid: "nope")));
    }

    [Theory]
    [InlineData(1, true)]  // device version 3 answers searches for version 1
    [InlineData(3, true)]  // exact match
    [InlineData(4, false)] // future version is not supported
    public void DeviceTypeSearch_HonorsVersionBackwardsCompatibility(int searchVersion, bool expectMatch)
    {
        var entities = SearchMatcher.MatchingEntities(
            Root, Search(STType.DeviceTypeSearch, "EmbeddedDevice", searchVersion));

        Assert.Equal(expectMatch, entities.Any());
    }

    [Fact]
    public void DeviceTypeSearch_ComparesTypeName()
    {
        Assert.Empty(SearchMatcher.MatchingEntities(Root, Search(STType.DeviceTypeSearch, "SomeOtherDevice", 1)));
    }

    [Fact]
    public void ServiceTypeSearch_DoesNotMatchDomainServices()
    {
        Assert.Empty(SearchMatcher.MatchingEntities(Root, Search(STType.ServiceTypeSearch, "DomainService", 1)));
    }

    [Fact]
    public void DomainServiceSearch_MatchesDomain()
    {
        var entities = SearchMatcher.MatchingEntities(
            Root, Search(STType.DomainServiceSearch, "DomainService", 1, domain: "domain-org")).ToList();

        var entity = Assert.Single(entities);
        Assert.Equal("DomainService", entity.TypeName);
    }

    [Fact]
    public void DomainServiceSearch_WrongDomain_ReturnsNothing()
    {
        Assert.Empty(SearchMatcher.MatchingEntities(
            Root, Search(STType.DomainServiceSearch, "DomainService", 1, domain: "other-org")));
    }

    [Fact]
    public void OwnerDevice_ForEmbeddedService_IsTheEmbeddedDevice()
    {
        var embeddedService = Root.EmbeddedDevices[0].Services[0];

        var owner = SearchMatcher.OwnerDevice(Root, embeddedService);

        Assert.Equal("embedded-uuid", owner.DeviceUUID);
    }

    [Fact]
    public void OwnerDevice_ForRootService_IsTheRoot()
    {
        var owner = SearchMatcher.OwnerDevice(Root, Root.Services[0]);

        Assert.Equal("root-uuid", owner.DeviceUUID);
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
