namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// An SSDP Unique Service Name (the <c>USN</c> header):
/// <c>uuid:[device-UUID]</c> optionally followed by <c>::[entity URI]</c>.
/// Immutable; create via an object initializer for outgoing messages or with
/// <see cref="Parse"/> for received values.
/// </summary>
public sealed record USN : Entity
{
    /// <summary>The unparsed header value, when this instance was produced by <see cref="Parse"/>.</summary>
    public string? USNString { get; init; }

    /// <summary>
    /// The SSDP wire representation of this USN, e.g.
    /// <c>uuid:[UUID]::upnp:rootdevice</c> or
    /// <c>uuid:[UUID]::urn:schemas-upnp-org:service:[type]:[version]</c>.
    /// </summary>
    /// <exception cref="SSDPException">The USN is not fully specified for its <see cref="Entity.EntityType"/>.</exception>
    public string ToUsnString()
    {
        // Every USN form starts with the device UUID, so an unset one produced
        // "uuid:" or "uuid:::upnp:rootdevice" - a malformed Required header rather
        // than an error.
        SsdpUri.RequireUuid(DeviceUUID, EntityType);

        return EntityType == EntityType.Device
            ? $"uuid:{DeviceUUID}"
            : $"uuid:{DeviceUUID}::{ToUriString()}";
    }

    /// <summary>
    /// Parses a USN header value.
    /// </summary>
    /// <param name="usn">The raw header value.</param>
    /// <returns>The parsed USN, or a failure describing why the value is invalid.</returns>
    public static ParseResult<USN> Parse(string? usn)
    {
        if (string.IsNullOrWhiteSpace(usn))
        {
            return ParseResult<USN>.Failure("USN is empty.");
        }

        var value = usn.AsSpan();
        var firstColon = value.IndexOf(':');
        var scheme = firstColon < 0 ? value : value[..firstColon];

        if (!scheme.Equals("uuid", StringComparison.OrdinalIgnoreCase))
        {
            return ParseResult<USN>.Failure("USN string must start with 'uuid:'.");
        }

        // The device UUID runs from after "uuid:" to the next colon, which is where
        // the "::" entity separator begins when there is one.
        var rest = firstColon < 0 ? ReadOnlySpan<char>.Empty : value[(firstColon + 1)..];
        var next = rest.IndexOf(':');
        var uuid = next < 0 ? rest : rest[..next];

        if (firstColon < 0 || uuid.IsEmpty)
        {
            return ParseResult<USN>.Failure("Device-UUID is empty. USN string must start with 'uuid:[device-UUID]'.");
        }

        var deviceUuid = uuid.ToString();

        var separatorIndex = usn.IndexOf("::", StringComparison.Ordinal);

        if (separatorIndex < 0)
        {
            return ParseResult<USN>.Success(new USN
            {
                EntityType = EntityType.Device,
                DeviceUUID = deviceUuid,
                USNString = usn
            });
        }

        var entityUri = usn[(separatorIndex + 2)..];

        if (entityUri.Equals("upnp:rootdevice", StringComparison.OrdinalIgnoreCase))
        {
            return ParseResult<USN>.Success(new USN
            {
                EntityType = EntityType.RootDevice,
                DeviceUUID = deviceUuid,
                USNString = usn
            });
        }

        // An unreadable entity part is not a reason to throw the message away. The
        // device UUID in front of it is the identity SSDP actually guarantees, and
        // real devices do put things here that no reading of UDA 2.0 allows - a
        // serviceId URN used as a search target, for one. Report what was
        // understood, mark the rest Unknown, and keep USNString so the caller can
        // see exactly what arrived.
        return ST.Parse(entityUri).Match(
            st => ParseResult<USN>.Success(new USN
            {
                EntityType = st.EntityType,
                TypeName = st.TypeName,
                Version = st.Version,
                Domain = st.Domain,
                DeviceUUID = deviceUuid,
                USNString = usn
            }),
            _ => ParseResult<USN>.Success(new USN
            {
                EntityType = EntityType.Unknown,
                DeviceUUID = deviceUuid,
                USNString = usn
            }));
    }
}
