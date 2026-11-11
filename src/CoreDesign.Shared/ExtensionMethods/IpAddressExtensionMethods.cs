using System.Net;

namespace CoreDesign.Shared.ExtensionMethods;

public static class IpAddressExtensionMethods
{
    // RFC 1918 private IPv4 ranges and RFC 4193 IPv6 unique local addresses.
    private static readonly IPNetwork[] PrivateNetworks =
    [
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("fc00::/7"),
    ];

    // RFC 3927 IPv4 and RFC 4291 IPv6 link-local ranges.
    private static readonly IPNetwork[] LinkLocalNetworks =
    [
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("fe80::/10"),
    ];

    /// <summary>
    ///     True for private network addresses: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16 and IPv6 unique
    ///     local addresses (fc00::/7). IPv4 addresses written in IPv6 form (::ffff:10.0.0.1) are recognized.
    /// </summary>
    public static bool IsPrivate(this IPAddress address) =>
        Contains(PrivateNetworks, Normalize(address));

    /// <summary>
    ///     True for addresses that cannot come from the public internet: private (<see cref="IsPrivate" />),
    ///     loopback and link-local. Use it to exempt traffic between your own services, such as a Blazor
    ///     server calling its API on a container network, from rate limiting.
    /// </summary>
    /// <remarks>
    ///     Behind a reverse proxy, check the client address only after forwarded headers are applied
    ///     (<c>UseForwardedHeaders</c>); otherwise every request carries the proxy's internal address.
    /// </remarks>
    public static bool IsInternal(this IPAddress address)
    {
        var normalized = Normalize(address);
        return IPAddress.IsLoopback(normalized)
               || Contains(PrivateNetworks, normalized)
               || Contains(LinkLocalNetworks, normalized);
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static bool Contains(IPNetwork[] networks, IPAddress address)
    {
        foreach (var network in networks)
        {
            if (network.Contains(address))
                return true;
        }

        return false;
    }
}
