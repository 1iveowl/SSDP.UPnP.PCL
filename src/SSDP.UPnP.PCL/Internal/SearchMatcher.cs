using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL.Internal;

/// <summary>
/// Pure functions implementing the UDA 2.0 discovery rules: the advertisement
/// messages a root device emits, which of them answer a given M-SEARCH request,
/// and the responses they produce.
/// </summary>
internal static class SearchMatcher
{
    /// <summary>All devices of a root device: the root itself plus embedded devices.</summary>
    internal static IEnumerable<DeviceConfiguration> AllDevices(RootDeviceConfiguration root) =>
        root.EmbeddedDevices.Append<DeviceConfiguration>(root);

    /// <summary>
    /// The full advertisement message set of a root device per UDA 2.0 section
    /// 1.2.2 (table 1-1): three messages for the root device
    /// (<c>upnp:rootdevice</c>, <c>uuid:...</c>, device type), two per embedded
    /// device (<c>uuid:...</c>, device type), and one per service. Each message
    /// pairs the advertised entity identity with the device that owns it, so USNs
    /// and BOOTIDs never need a reverse lookup.
    /// </summary>
    internal static IEnumerable<(DeviceConfiguration Owner, Entity Entity)> AdvertisementMessages(RootDeviceConfiguration root)
    {
        yield return (root, new Entity { EntityType = EntityType.RootDevice, DeviceUUID = root.DeviceUUID });
        yield return (root, new Entity { EntityType = EntityType.Device, DeviceUUID = root.DeviceUUID });

        if (!string.IsNullOrEmpty(root.TypeName))
        {
            yield return (root, DeviceTypeEntity(root));
        }

        foreach (var embedded in root.EmbeddedDevices)
        {
            yield return (embedded, new Entity { EntityType = EntityType.Device, DeviceUUID = embedded.DeviceUUID });

            if (!string.IsNullOrEmpty(embedded.TypeName))
            {
                yield return (embedded, DeviceTypeEntity(embedded));
            }
        }

        foreach (var device in AllDevices(root))
        {
            // UDA 2.0 §1.2.2: multiple instances of the same service type within one
            // device are advertised once; the same type on different devices is
            // advertised separately per device.
            var seenServiceTypes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var service in device.Services)
            {
                var entity = ServiceTypeEntity(device, service);

                if (seenServiceTypes.Add(entity.ToUriString()))
                {
                    yield return (device, entity);
                }
            }
        }
    }

    // The device-type message identity: standard vs vendor-domain form is derived
    // from whether the configuration carries a Domain.
    private static Entity DeviceTypeEntity(DeviceConfiguration device) => new()
    {
        EntityType = string.IsNullOrEmpty(device.Domain) ? EntityType.DeviceType : EntityType.DomainDevice,
        TypeName = device.TypeName,
        Domain = device.Domain,
        Version = device.Version,
        DeviceUUID = device.DeviceUUID
    };

    private static Entity ServiceTypeEntity(DeviceConfiguration owner, ServiceConfiguration service) => new()
    {
        EntityType = string.IsNullOrEmpty(service.Domain) ? EntityType.ServiceType : EntityType.DomainService,
        TypeName = service.TypeName,
        Domain = service.Domain,
        Version = service.Version,
        DeviceUUID = owner.DeviceUUID
    };

    /// <summary>
    /// The advertisement messages of <paramref name="root"/> that answer a search
    /// target, per UDA 2.0 section 1.3.3.
    /// </summary>
    internal static IEnumerable<(DeviceConfiguration Owner, Entity Entity)> MatchingMessages(RootDeviceConfiguration root, ST st) =>
        st.StSearchType switch
        {
            STType.All => AdvertisementMessages(root),
            STType.RootDeviceSearch => AdvertisementMessages(root)
                .Where(message => message.Entity.EntityType == EntityType.RootDevice),
            STType.UuidSearch => AdvertisementMessages(root)
                .Where(message => message.Entity.EntityType == EntityType.Device && message.Entity.DeviceUUID == st.DeviceUUID),
            STType.DeviceTypeSearch or STType.DomainDeviceSearch => AdvertisementMessages(root)
                .Where(message => message.Entity.EntityType is EntityType.DeviceType or EntityType.DomainDevice)
                .Where(message => IsMatch(message.Entity, st)),
            STType.ServiceTypeSearch or STType.DomainServiceSearch => AdvertisementMessages(root)
                .Where(message => message.Entity.EntityType is EntityType.ServiceType or EntityType.DomainService)
                .Where(message => IsMatch(message.Entity, st)),
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
    /// Builds the M-SEARCH responses a root device sends for a request: one response
    /// per matching advertisement message, with ST/USN describing the advertised
    /// entity and BOOTID/CONFIGID taken from the configuration.
    /// </summary>
    /// <param name="root">The root device configuration answering the search.</param>
    /// <param name="request">The parsed search request.</param>
    /// <param name="date">The timestamp for the <c>DATE</c> header.</param>
    /// <param name="searchPort">
    /// The value for <c>SEARCHPORT.UPNP.ORG</c>, already normalized by
    /// <see cref="RootDeviceInterface.SearchPort"/> (<see langword="null"/> when the
    /// device listens on 1900 and the header is omitted).
    /// </param>
    internal static IEnumerable<MSearchResponse> BuildResponses(
        RootDeviceConfiguration root,
        ReceivedMSearch request,
        DateTimeOffset date,
        DynamicPort? searchPort)
    {
        return MatchingMessages(root, request.ST)
            .Select(message => BuildResponse(root, message.Owner, message.Entity, request, date, searchPort));
    }

    private static MSearchResponse BuildResponse(
        RootDeviceConfiguration root,
        DeviceConfiguration owner,
        Entity entity,
        ReceivedMSearch request,
        DateTimeOffset date,
        DynamicPort? searchPort)
    {
        // UDA 2.0 §1.3.3: for type searches the response ST must echo the version
        // from the request (a device supporting v2 answers a v1 search with v1);
        // the USN keeps the advertised (actual) version.
        var isTypeSearch = request.ST.StSearchType
            is STType.DeviceTypeSearch or STType.ServiceTypeSearch
            or STType.DomainDeviceSearch or STType.DomainServiceSearch;

        return new MSearchResponse
        {
            StatusCode = 200,
            ResponseReason = "OK",
            MaxAge = root.CacheControl,
            Date = date,
            Location = root.Location,
            Server = root.Server,
            ST = new ST
            {
                StSearchType = request.ST.StSearchType,
                EntityType = entity.EntityType,
                TypeName = entity.TypeName,
                Domain = entity.Domain,
                Version = isTypeSearch ? request.ST.Version : entity.Version,
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
            SEARCHPORT = searchPort,
            SECURELOCATION = root.SecureLocation?.AbsoluteUri,
            MX = request.MX,
            RemoteIpEndPoint = request.RemoteIpEndPoint
        };
    }
}
