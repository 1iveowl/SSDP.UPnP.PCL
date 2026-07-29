namespace SSDP.UPnP.PCL.Analyzers.Tests;

/// <summary>
/// The slice of SSDP.UPnP.PCL the rules look at, as source.
/// </summary>
/// <remarks>
/// <para>
/// Test compilations cannot reference the real library:
/// <c>ReferenceAssemblies.Net</c> stops at net9.0 and SSDP.UPnP.PCL targets
/// net10.0, so the analyzer test framework has nothing to compile it against.
/// Every test therefore prepends this stub to its source.
/// </para>
/// <para>
/// A stub can drift from the thing it stands for, silently, and then the rules pass
/// their tests while failing on real code. <see cref="StubFidelityTests"/> is what
/// stops that: it compares these declarations against the real types by reflection,
/// so a rename in the library fails a test here rather than in a consumer's build.
/// </para>
/// </remarks>
internal static class SsdpStub
{
    internal const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Net;

        namespace SSDP.UPnP.PCL.Model
        {
            public readonly struct MxSeconds
            {
                public MxSeconds(int seconds) { Seconds = seconds; }
                public int Seconds { get; }
            }

            public readonly struct DynamicPort
            {
                public DynamicPort(int port) { Port = port; }
                public int Port { get; }
            }

            public enum STType { All, RootDeviceSearch, UuidSearch, DeviceTypeSearch }

            public record Entity
            {
                public string? TypeName { get; init; }
                public int Version { get; init; }
                public string? Domain { get; init; }
                public string? DeviceUUID { get; init; }
            }

            public sealed record ST : Entity
            {
                public STType StSearchType { get; init; }
            }

            public sealed record Server { }

            public sealed record UserAgent { }

            public abstract record MSearchRequest
            {
                public required ST ST { get; init; }
                public UserAgent UserAgent { get; init; } = new();
            }

            public sealed record MulticastMSearch : MSearchRequest
            {
                public required string CPFN { get; init; }
                public MxSeconds MX { get; init; }
                public string? CPUUID { get; init; }
                public DynamicPort? TCPPORT { get; init; }
                public int SendCount { get; init; } = 2;
            }

            public sealed record UnicastMSearch : MSearchRequest
            {
                public required IPEndPoint Target { get; init; }
            }

            public record DeviceConfiguration : Entity
            {
                public uint BOOTID { get; init; }
            }

            public sealed record RootDeviceConfiguration : DeviceConfiguration
            {
                public IPEndPoint? IpEndPoint { get; init; }
                public Server Server { get; init; } = new();
                public Uri? Location { get; init; }
                public Uri? SecureLocation { get; init; }
                public int CONFIGID { get; init; }
                public TimeSpan CacheControl { get; init; } = TimeSpan.FromSeconds(1800);
                public IReadOnlyList<DeviceConfiguration> EmbeddedDevices { get; init; } = new List<DeviceConfiguration>();
            }
        }
        """;
}
