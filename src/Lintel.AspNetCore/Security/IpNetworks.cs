using System.Collections;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace Lintel.AspNetCore.Security;

/// <summary>
/// A list of IP addresses and networks (<c>192.168.0.0/16</c>, <c>::1</c>), parsed once, so that an invalid entry stops
/// the application at startup instead of failing requests.
/// </summary>
/// <remarks>IPv4 addresses mapped to IPv6 (<c>::ffff:10.0.0.1</c>) are compared as IPv4.</remarks>
public sealed class IpNetworkList : IReadOnlyCollection<IPNetwork>
{
    private readonly List<IPNetwork> _networks;

    private IpNetworkList(List<IPNetwork> networks) => _networks = networks;

    /// <summary>An empty list.</summary>
    public static IpNetworkList Empty { get; } = new([]);

    /// <inheritdoc />
    public int Count => _networks.Count;

    /// <summary>Parses addresses and networks; a single address is a network of one address.</summary>
    /// <param name="entries">The entries, for example from configuration.</param>
    /// <returns>The list.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">If an entry is not an address or a network; the message names the entry.</exception>
    public static IpNetworkList Parse(IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new(entries.Select(ParseEntry).ToList());
    }

    /// <summary>Whether <paramref name="address"/> belongs to one of the networks.</summary>
    /// <param name="address">The address.</param>
    /// <returns><see langword="true"/> if it does.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="address"/> is <see langword="null"/>.</exception>
    public bool Contains(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var normalized = address.Normalize();
        return _networks.Exists(network => network.Contains(normalized));
    }

    /// <inheritdoc />
    public IEnumerator<IPNetwork> GetEnumerator() => _networks.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal static IPNetwork ParseEntry(string entry)
    {
        var text = entry?.Trim() ?? "";
        if (text.Contains('/', StringComparison.Ordinal))
        {
            return IPNetwork.TryParse(text, out var network)
                ? network
                : throw new FormatException($"'{entry}' is not a valid IP network; the expected format is 'address/prefix'.");
        }

        return IPAddress.TryParse(text, out var address)
            ? new IPNetwork(address.Normalize(), address.Normalize().AddressFamily == AddressFamily.InterNetwork ? 32 : 128)
            : throw new FormatException($"'{entry}' is not a valid IP address.");
    }
}

/// <summary>
/// IP addresses of requests.
/// </summary>
public static class IpAddressExtensions
{
    /// <summary>Converts an IPv4 address mapped to IPv6 (<c>::ffff:10.0.0.1</c>) to IPv4; other addresses are returned as is.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="address"/> is <see langword="null"/>.</exception>
    public static IPAddress Normalize(this IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    /// <summary>
    /// The IP address of the client: <c>Connection.RemoteIpAddress</c> after <c>UseForwardedHeaders</c> (normalized).
    /// Headers of the request are never read here: a client can put any address in them.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The address, or <see langword="null"/> when it is unknown (for example in tests).</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="context"/> is <see langword="null"/>.</exception>
    public static IPAddress? GetClientIpAddress(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Connection.RemoteIpAddress?.Normalize();
    }

    /// <summary>
    /// The key of the client for rate limits: the client address, and for IPv6 its network (<c>/64</c> by default),
    /// because one customer usually gets a whole IPv6 network and could change addresses within it.
    /// <c>unknown</c> when the address is unknown.
    /// </summary>
    /// <example><c>RateLimitPartition.GetFixedWindowLimiter(context.GetClientRateLimitKey(), _ =&gt; options)</c></example>
    /// <param name="context">The request.</param>
    /// <param name="ipv6PrefixLength">The prefix of the IPv6 network that is one client (0–128).</param>
    /// <returns>The key, for example <c>192.168.0.10</c> or <c>2001:db8:1:2::</c>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="ipv6PrefixLength"/> is not between 0 and 128.</exception>
    public static string GetClientRateLimitKey(this HttpContext context, int ipv6PrefixLength = 64)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(ipv6PrefixLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ipv6PrefixLength, 128);

        if (context.GetClientIpAddress() is not { } address)
        {
            return "unknown";
        }
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        for (var bit = ipv6PrefixLength; bit < 128; bit++)
        {
            bytes[bit / 8] &= (byte)~(0x80 >> (bit % 8));
        }
        return new IPAddress(bytes).ToString();
    }

    /// <summary>
    /// Whether the client address is still the address of a known proxy: the proxy did not pass the address of the client,
    /// so the request must not be treated as coming from the client's network.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns><see langword="true"/> if the address is a known proxy while forwarded headers are processed.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="context"/> is <see langword="null"/>.</exception>
    public static bool IsFromKnownProxy(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.RequestServices?.GetService<IOptions<ForwardedHeadersOptions>>()?.Value;
        if (options is null || options.ForwardedHeaders == ForwardedHeaders.None || context.GetClientIpAddress() is not { } address)
        {
            return false;
        }
        return options.KnownProxies.Any(proxy => proxy.Normalize().Equals(address))
            || options.KnownIPNetworks.Any(network => network.Contains(address));
    }
}

/// <summary>
/// Settings of a reverse proxy (nginx and the like), usually from configuration:
/// <c>"ForwardedHeaders": { "Enabled": true, "KnownProxies": [ "10.0.0.5" ] }</c>.
/// </summary>
public class ForwardedHeadersSettings
{
    /// <summary>Whether the headers of the proxy are applied.</summary>
    public bool Enabled { get; set; }

    /// <summary>The header with the client address. Default: <c>X-Forwarded-For</c> (nginx is often set up with <c>X-Real-IP</c>).</summary>
    public string ForwardedForHeaderName { get; set; } = ForwardedHeadersDefaults.XForwardedForHeaderName;

    /// <summary>The header with the scheme. Default: <c>X-Forwarded-Proto</c>.</summary>
    public string ForwardedProtoHeaderName { get; set; } = ForwardedHeadersDefaults.XForwardedProtoHeaderName;

    /// <summary>Addresses of the proxies whose headers are trusted.</summary>
    public IList<string> KnownProxies { get; } = [];

    /// <summary>Networks of the proxies whose headers are trusted (<c>address/prefix</c>).</summary>
    public IList<string> KnownNetworks { get; } = [];

    /// <summary>How many proxies are in front of the application. Default: 1.</summary>
    public int ForwardLimit { get; set; } = 1;
}

/// <summary>
/// Registration of reverse proxy settings and of allowed networks.
/// </summary>
public static class IpNetworkExtensions
{
    /// <summary>
    /// Configures <see cref="ForwardedHeadersOptions"/> from <paramref name="configuration"/> (<see cref="ForwardedHeadersSettings"/>):
    /// only the configured proxies are trusted (the default trust in loopback is removed), the client address and scheme
    /// are taken from their headers. Call <c>app.UseForwardedHeaders()</c> first in the pipeline.
    /// </summary>
    /// <remarks>
    /// The settings are read and checked at once: an invalid address, or no proxy at all while enabled, stops the
    /// application at startup. When disabled, forwarded headers are ignored.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If the settings are invalid.</exception>
    public static IServiceCollection AddForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = configuration.Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();
        var proxies = new List<IPAddress>();
        var networks = new List<IPNetwork>();
        if (settings.Enabled)
        {
            if (settings.KnownProxies.Count == 0 && settings.KnownNetworks.Count == 0)
            {
                throw new InvalidOperationException("ForwardedHeaders: KnownProxies or KnownNetworks must be set when Enabled is true.");
            }
            proxies.AddRange(settings.KnownProxies.Select(proxy => IPAddress.TryParse(proxy?.Trim(), out var address)
                ? address
                : throw new InvalidOperationException($"ForwardedHeaders.KnownProxies: '{proxy}' is not a valid IP address.")));
            networks.AddRange(settings.KnownNetworks.Select(network => IPNetwork.TryParse(network?.Trim(), out var parsed)
                ? parsed
                : throw new InvalidOperationException($"ForwardedHeaders.KnownNetworks: '{network}' is not a valid network ('address/prefix').")));
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            if (!settings.Enabled)
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardedForHeaderName = settings.ForwardedForHeaderName;
            options.ForwardedProtoHeaderName = settings.ForwardedProtoHeaderName;
            options.ForwardLimit = settings.ForwardLimit;
            proxies.ForEach(options.KnownProxies.Add);
            networks.ForEach(options.KnownIPNetworks.Add);
        });
        return services;
    }

    /// <summary>
    /// Lets requests from <paramref name="networks"/> through, and (by default) signed-in users from anywhere:
    /// <c>options.AddPolicy("Intranet", p =&gt; p.RequireAllowedNetwork(IpNetworkList.Parse(settings.AllowedIp)))</c>.
    /// </summary>
    /// <remarks>
    /// The client address is the one after <c>UseForwardedHeaders</c>. A request whose address is still a known proxy is not
    /// allowed: the proxy did not pass the address of the client. No handler has to be registered.
    /// </remarks>
    /// <param name="policy">The policy builder.</param>
    /// <param name="networks">The allowed networks.</param>
    /// <param name="allowAuthenticatedUsers">Whether signed-in users pass from any address.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static AuthorizationPolicyBuilder RequireAllowedNetwork(this AuthorizationPolicyBuilder policy, IpNetworkList networks,
        bool allowAuthenticatedUsers = true)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(networks);

        return policy.AddRequirements(new AllowedNetworkRequirement(networks, allowAuthenticatedUsers));
    }
}

/// <summary>
/// The requirement of <see cref="IpNetworkExtensions.RequireAllowedNetwork"/>; it is its own handler.
/// </summary>
/// <param name="networks">The allowed networks.</param>
/// <param name="allowAuthenticatedUsers">Whether signed-in users pass from any address.</param>
public sealed class AllowedNetworkRequirement(IpNetworkList networks, bool allowAuthenticatedUsers)
    : AuthorizationHandler<AllowedNetworkRequirement>, IAuthorizationRequirement
{
    /// <summary>The allowed networks.</summary>
    public IpNetworkList Networks { get; } = networks ?? throw new ArgumentNullException(nameof(networks));

    /// <summary>Whether signed-in users pass from any address.</summary>
    public bool AllowAuthenticatedUsers { get; } = allowAuthenticatedUsers;

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowedNetworkRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var httpContext = context.Resource as HttpContext ?? (context.Resource as Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext)?.HttpContext;
        if ((requirement.AllowAuthenticatedUsers && context.User.Identity?.IsAuthenticated == true)
            || (httpContext?.GetClientIpAddress() is { } address && !httpContext.IsFromKnownProxy() && requirement.Networks.Contains(address)))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
