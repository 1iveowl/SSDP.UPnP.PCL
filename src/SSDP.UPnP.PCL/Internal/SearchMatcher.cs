using System.Net;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL.Internal;

/// <summary>
/// Pure functions implementing the UDA 2.0 search-matching rules: which entities
/// of a root device answer a given M-SEARCH request, and the responses they produce.
/// </summary>
internal static class SearchMatcher
{
    /// <summary>All devices of a root device: the root itself plus embedded devices.</summary>
    internal static IEnumerable<DeviceConfiguration> AllDevices(RootDeviceConfiguration root) =>
        root.EmbeddedDevices.Append<DeviceConfiguration>(root);

    /// <summary>All services hosted by a root device, including those of embedded devices.</summary>
    internal static IEnumerable<ServiceConfiguration> AllServices(RootDeviceConfiguration root) =>
        AllDevices(root).SelectMany(device => device.Services);

    /// <summary>
    /// The entities of <paramref name="root"/> matching a search target, per UDA 2.0
    /// section 1.3.3.
    /// </summary>
    internal static IEnumerable<Entity> MatchingEntities(RootDeviceConfiguration root, ST st) => st.StSearchType switch
    {
        STType.All => AllDevices(root).Cast<Entity>().Concat(AllServices(root)),
        STType.RootDeviceSearch => [root],
        STType.UuidSearch => AllDevices(root).Where(device => device.DeviceUUID == st.DeviceUUID),
        STType.DeviceTypeSearch or STType.DomainDeviceSearch => AllDevices(root).Where(device => IsMatch(device, st)),
        STType.ServiceTypeSearch or STType.DomainServiceSearch => AllServices(root).Where(service => IsMatch(service, st)),
        _ => []
    };

    // A search matches an entity when the type name matches, the domain matches
    // (schemas-upnp-org searches match entities without a vendor domain), and the
    // entity's version is at least the requested version (UDA 2.0 backwards
    // compatibility rule: a device must respond to searches for any version it
    // supersedes).
    internal static bool IsMatch(Entity entity, ST st)
    {
        if (entity.TypeName != st.TypeName || entity.Version < st.Version)
        {
            return false;
        }

        return st.StSearchType switch
        {
            STType.DeviceTypeSearch or STType.ServiceTypeSearch => string.IsNullOrEmpty(entity.Domain),
            STType.DomainDeviceSearch or STType.DomainServiceSearch => entity.Domain == st.Domain,
            _ => false
        };
    }

    /// <summary>
    /// The device owning <paramref name="entity"/>: a device owns itself; a service
    /// is owned by the device whose Services collection contains it.
    /// </summary>
    internal static DeviceConfiguration OwnerDevice(RootDeviceConfiguration root, Entity entity) =>
        entity as DeviceConfiguration
        ?? AllDevices(root).FirstOrDefault(device => device.Services.Contains(entity))
        ?? root;

    /// <summary>
    /// Builds the M-SEARCH responses a root device sends for a request: one response
    /// per matching entity, with ST/USN describing the entity and BOOTID/CONFIGID
    /// taken from the configuration.
    /// </summary>
    /// <param name="root">The root device configuration answering the search.</param>
    /// <param name="request">The parsed search request.</param>
    /// <param name="date">The timestamp for the <c>DATE</c> header.</param>
    /// <param name="searchPort">The unicast search port, or <see langword="null"/> when listening on 1900 only.</param>
    internal static IEnumerable<MSearchResponse> BuildResponses(
        RootDeviceConfiguration root,
        MSearchRequest request,
        DateTimeOffset date,
        int? searchPort)
    {
        return MatchingEntities(root, request.ST)
            .Select(entity => BuildResponse(root, entity, request, date, searchPort));
    }

    private static MSearchResponse BuildResponse(
        RootDeviceConfiguration root,
        Entity entity,
        MSearchRequest request,
        DateTimeOffset date,
        int? searchPort)
    {
        var owner = OwnerDevice(root, entity);

        return new MSearchResponse
        {
            TransportType = TransportType.Unicast,
            StatusCode = 200,
            ResponseReason = "OK",
            CacheControl = root.CacheControl,
            Date = date,
            Ext = true,
            Location = root.Location,
            Server = root.Server,
            ST = new ST
            {
                StSearchType = request.ST.StSearchType,
                EntityType = entity.EntityType,
                TypeName = entity.TypeName,
                Domain = entity.Domain,
                Version = entity.Version,
                DeviceUUID = owner.DeviceUUID
            },
            USN = new USN
            {
                EntityType = entity.EntityType,
                TypeName = entity.TypeName,
                Domain = entity.Domain,
                Version = entity.Version,
                DeviceUUID = owner.DeviceUUID
            },
            BOOTID = owner.BOOTID,
            CONFIGID = root.CONFIGID,
            SEARCHPORT = searchPort == Constants.UdpSSDPMulticastPort ? null : searchPort,
            SECURELOCATION = root.SecureLocation?.AbsoluteUri,
            MX = request.MX,
            RemoteIpEndPoint = request.RemoteIpEndPoint
        };
    }
}
