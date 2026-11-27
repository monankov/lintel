using System.Net;
using System.Security.Claims;
using Lintel.AspNetCore.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.Tests;

public class IpNetworkTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value))).Build();

    [Theory]
    [InlineData("192.168.1.10", true)]
    [InlineData("::ffff:192.168.1.10", true)]
    [InlineData("10.0.0.7", true)]
    [InlineData("10.0.0.8", false)]
    [InlineData("::1", true)]
    [InlineData("2001:db8::5", true)]
    [InlineData("172.16.0.1", false)]
    public void List_matches_addresses_and_networks(string address, bool expected)
    {
        var list = IpNetworkList.Parse(["192.168.0.0/16", " 10.0.0.7 ", "::1", "2001:db8::/32", "::ffff:10.0.0.7"]);

        list.Contains(IPAddress.Parse(address)).Should().Be(expected);
        list.Should().HaveCount(5);
        IpNetworkList.Empty.Contains(IPAddress.Loopback).Should().BeFalse();
    }

    [Theory]
    [InlineData("10.0.0.300")]
    [InlineData("10.0.0.0/33")]
    [InlineData("localhost")]
    [InlineData("")]
    public void Invalid_entries_are_reported(string entry)
    {
        var parse = () => IpNetworkList.Parse(["127.0.0.1", entry]);

        parse.Should().Throw<FormatException>().WithMessage($"*'{entry}'*");
    }

    [Fact]
    public void Forwarded_headers_are_configured_from_settings_without_default_loopback_trust()
    {
        var services = new ServiceCollection().AddForwardedHeaders(Config(
            ("Enabled", "true"), ("ForwardedForHeaderName", "X-Real-IP"), ("KnownProxies:0", "10.0.0.5"),
            ("KnownNetworks:0", "172.20.0.0/16"), ("ForwardLimit", "2"))).BuildServiceProvider();

        var options = services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.ForwardedForHeaderName.Should().Be("X-Real-IP");
        options.ForwardedProtoHeaderName.Should().Be("X-Forwarded-Proto");
        options.ForwardLimit.Should().Be(2);
        options.KnownProxies.Should().Equal(IPAddress.Parse("10.0.0.5"));
        options.KnownIPNetworks.Should().Equal(System.Net.IPNetwork.Parse("172.20.0.0/16"));
    }

    [Fact]
    public void Disabled_forwarded_headers_trust_nobody()
    {
        var options = new ServiceCollection().AddForwardedHeaders(Config(("Enabled", "false"), ("KnownProxies:0", "not an address")))
            .BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.None);
        options.KnownProxies.Should().BeEmpty();
        options.KnownIPNetworks.Should().BeEmpty();
    }

    [Theory]
    [InlineData("KnownProxies:0", "10.0.0.300", "*KnownProxies*'10.0.0.300'*")]
    [InlineData("KnownNetworks:0", "10.0.0.0", "*KnownNetworks*'10.0.0.0'*")]
    [InlineData("Other", "x", "*KnownProxies or KnownNetworks*")]
    public void Invalid_forwarded_headers_settings_stop_startup(string key, string value, string message)
    {
        var add = () => new ServiceCollection().AddForwardedHeaders(Config(("Enabled", "true"), (key, value)));

        add.Should().Throw<InvalidOperationException>().WithMessage(message);
    }

    private static async Task<bool> AuthorizeAsync(string? clientIp, bool authenticated = false, bool allowAuthenticatedUsers = true,
        bool behindProxy = false)
    {
        var services = new ServiceCollection().AddLogging()
            .AddForwardedHeaders(Config(("Enabled", behindProxy ? "true" : "false"), ("KnownProxies:0", "10.0.0.5"), ("KnownNetworks:0", "10.1.0.0/16")))
            .AddAuthorization(o => o.AddPolicy("Intranet", p => p.RequireAllowedNetwork(IpNetworkList.Parse(["192.168.0.0/16", "10.0.0.5"]), allowAuthenticatedUsers)))
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Connection.RemoteIpAddress = clientIp is null ? null : IPAddress.Parse(clientIp);
        var user = new ClaimsPrincipal(authenticated ? new ClaimsIdentity([new Claim(ClaimTypes.Name, "u")], "test") : new ClaimsIdentity());

        return (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, context, "Intranet")).Succeeded;
    }

    [Fact]
    public async Task Requirement_allows_networks_and_signed_in_users()
    {
        (await AuthorizeAsync("192.168.5.5")).Should().BeTrue();
        (await AuthorizeAsync("8.8.8.8")).Should().BeFalse();
        (await AuthorizeAsync(null)).Should().BeFalse();
        (await AuthorizeAsync("8.8.8.8", authenticated: true)).Should().BeTrue();
        (await AuthorizeAsync("8.8.8.8", authenticated: true, allowAuthenticatedUsers: false)).Should().BeFalse();
    }

    [Fact]
    public async Task Address_of_a_known_proxy_is_not_allowed_even_if_listed()
    {
        (await AuthorizeAsync("10.0.0.5", behindProxy: false)).Should().BeTrue("without a proxy the address is a client");
        (await AuthorizeAsync("10.0.0.5", behindProxy: true)).Should().BeFalse("the proxy did not pass the client address");
        (await AuthorizeAsync("::ffff:10.1.2.3", behindProxy: true)).Should().BeFalse("a known proxy network");
    }

    [Fact]
    public void Client_ip_and_known_proxy_checks_handle_missing_services()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:1.2.3.4");

        context.GetClientIpAddress().Should().Be(IPAddress.Parse("1.2.3.4"));
        context.IsFromKnownProxy().Should().BeFalse("no forwarded headers options");
        IPAddress.IPv6Loopback.Normalize().Should().Be(IPAddress.IPv6Loopback);
    }

    [Fact]
    public void Arguments_are_checked()
    {
        var actions = new Action[]
        {
            () => IpNetworkList.Parse(null!),
            () => IpNetworkList.Empty.Contains(null!),
            () => ((IPAddress)null!).Normalize(),
            () => ((HttpContext)null!).GetClientIpAddress(),
            () => ((HttpContext)null!).IsFromKnownProxy(),
            () => ((IServiceCollection)null!).AddForwardedHeaders(Config()),
            () => new ServiceCollection().AddForwardedHeaders(null!),
            () => ((AuthorizationPolicyBuilder)null!).RequireAllowedNetwork(IpNetworkList.Empty),
            () => new AuthorizationPolicyBuilder().RequireAllowedNetwork(null!),
            () => _ = new AllowedNetworkRequirement(null!, true),
        };
        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentNullException>();
        }
        ((System.Collections.IEnumerable)IpNetworkList.Parse(["::1"])).Cast<object>().Should().ContainSingle();
    }
}
