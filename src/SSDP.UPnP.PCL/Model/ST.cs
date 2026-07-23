namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// An SSDP Search Target (the <c>ST</c> header of an M-SEARCH request or
/// response), per UPnP Device Architecture 2.0 section 1.3.2. Immutable; create
/// via an object initializer for outgoing searches or with <see cref="Parse"/>
/// for received values.
/// </summary>
public sealed record ST : Entity
{
    /// <summary>The kind of search this target expresses.</summary>
    public STType StSearchType { get; init; }

    /// <summary>The unparsed header value, when this instance was produced by <see cref="Parse"/>.</summary>
    public string? STString { get; init; }

    /// <summary>
    /// The SSDP wire representation of this search target, validating that the
    /// fields required by <see cref="StSearchType"/> are present.
    /// </summary>
    /// <exception cref="SSDPException">A field required by <see cref="StSearchType"/> is missing or invalid.</exception>
    public string ToSearchTargetString()
    {
        switch (StSearchType)
        {
            case STType.All:
                return "ssdp:all";
            case STType.RootDeviceSearch:
                return "upnp:rootdevice";
            case STType.UuidSearch:
                if (string.IsNullOrEmpty(DeviceUUID))
                {
                    throw new SSDPException("UUID Search requires a Device UUID to be specified.");
                }

                return $"uuid:{DeviceUUID}";
            case STType.DeviceTypeSearch:
            case STType.ServiceTypeSearch:
            case STType.DomainDeviceSearch:
            case STType.DomainServiceSearch:
                if ((StSearchType is STType.DomainDeviceSearch or STType.DomainServiceSearch) && string.IsNullOrEmpty(Domain))
                {
                    throw new SSDPException($"{StSearchType} requires a Domain to be specified.");
                }

                if (string.IsNullOrEmpty(TypeName))
                {
                    throw new SSDPException($"{StSearchType} requires a Type name to be specified.");
                }

                if (Version < 1)
                {
                    throw new SSDPException($"{StSearchType} requires a version (1 or greater) to be specified.");
                }

                var domain = StSearchType is STType.DeviceTypeSearch or STType.ServiceTypeSearch
                    ? "schemas-upnp-org"
                    : Domain;

                var kind = StSearchType is STType.DeviceTypeSearch or STType.DomainDeviceSearch
                    ? "device"
                    : "service";

                return $"urn:{domain}:{kind}:{TypeName}:{Version}";
            default:
                throw new SSDPException($"Unknown search target type: {StSearchType}.");
        }
    }

    /// <summary>
    /// Parses a Search Target header value (e.g. <c>ssdp:all</c>,
    /// <c>uuid:[device-UUID]</c> or <c>urn:schemas-upnp-org:device:[type]:[version]</c>).
    /// </summary>
    /// <param name="searchTarget">The raw header value.</param>
    /// <returns>The parsed search target, or a failure describing why the value is invalid.</returns>
    public static ParseResult<ST> Parse(string? searchTarget)
    {
        if (string.IsNullOrWhiteSpace(searchTarget))
        {
            return ParseResult<ST>.Failure("Search Target (ST) is empty.");
        }

        var parts = searchTarget.Split(':');

        switch (parts[0].ToLowerInvariant())
        {
            case "ssdp" when parts.Length == 2 && parts[1].Equals("all", StringComparison.OrdinalIgnoreCase):
                return ParseResult<ST>.Success(new ST
                {
                    StSearchType = STType.All,
                    STString = searchTarget
                });

            case "ssdp":
                return ParseResult<ST>.Failure(
                    $"Search Target (ST) value must be 'ssdp:all'. The value '{searchTarget}' is invalid.");

            case "upnp" when parts.Length == 2 && parts[1].Equals("rootdevice", StringComparison.OrdinalIgnoreCase):
                return ParseResult<ST>.Success(new ST
                {
                    StSearchType = STType.RootDeviceSearch,
                    EntityType = EntityType.RootDevice,
                    STString = searchTarget
                });

            case "upnp":
                return ParseResult<ST>.Failure(
                    $"Search Target (ST) value must be 'upnp:rootdevice'. The value '{searchTarget}' is invalid.");

            case "uuid" when parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]):
                return ParseResult<ST>.Success(new ST
                {
                    StSearchType = STType.UuidSearch,
                    EntityType = EntityType.Device,
                    DeviceUUID = searchTarget[5..],
                    STString = searchTarget
                });

            case "uuid":
                return ParseResult<ST>.Failure(
                    $"Search Target (ST) value must be 'uuid:[device-UUID]'. The value '{searchTarget}' is invalid.");

            case "urn":
                return ParseUrn(searchTarget, parts);

            default:
                return ParseResult<ST>.Failure(
                    $"Search Target (ST) '{searchTarget}' is invalid. See UPnP Device Architecture 2.0 section 1.3.2.");
        }
    }

    private static ParseResult<ST> ParseUrn(string searchTarget, string[] parts)
    {
        if (parts.Length != 5)
        {
            return ParseResult<ST>.Failure(
                $"Search Target (ST) value must be in the form 'urn:[domain]:[device or service]:[type]:[version]'. The value '{searchTarget}' is invalid.");
        }

        var isStandardDomain = parts[1].Equals("schemas-upnp-org", StringComparison.OrdinalIgnoreCase);

        if (!int.TryParse(parts[4], out var version))
        {
            version = -1;
        }

        return parts[2].ToLowerInvariant() switch
        {
            "device" => ParseResult<ST>.Success(new ST
            {
                StSearchType = isStandardDomain ? STType.DeviceTypeSearch : STType.DomainDeviceSearch,
                EntityType = isStandardDomain ? EntityType.DeviceType : EntityType.DomainDevice,
                Domain = isStandardDomain ? null : parts[1],
                TypeName = parts[3],
                Version = version,
                STString = searchTarget
            }),
            "service" => ParseResult<ST>.Success(new ST
            {
                StSearchType = isStandardDomain ? STType.ServiceTypeSearch : STType.DomainServiceSearch,
                EntityType = isStandardDomain ? EntityType.ServiceType : EntityType.DomainService,
                Domain = isStandardDomain ? null : parts[1],
                TypeName = parts[3],
                Version = version,
                STString = searchTarget
            }),
            _ => ParseResult<ST>.Failure(
                $"Search Target (ST) value must be in the form 'urn:[domain]:[device or service]:[type]:[version]'. The value '{searchTarget}' is invalid because of '{parts[2]}'.")
        };
    }
}
