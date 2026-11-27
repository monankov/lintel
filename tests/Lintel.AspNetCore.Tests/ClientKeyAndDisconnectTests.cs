using System.Net;
using Lintel.AspNetCore.Http;
using Lintel.AspNetCore.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Tests;

public class ClientKeyAndDisconnectTests
{
    private static DefaultHttpContext Request(string? address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);
        return context;
    }

    [Theory]
    [InlineData("192.168.0.10", 64, "192.168.0.10")]
    [InlineData("::ffff:10.0.0.1", 64, "10.0.0.1")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", 64, "2001:db8:1:2::")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", 48, "2001:db8:1::")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", 60, "2001:db8:1::")]
    [InlineData("2001:db8:1:12:aaaa::", 60, "2001:db8:1:10::")]
    [InlineData("2001:db8::1", 128, "2001:db8::1")]
    [InlineData("2001:db8::1", 0, "::")]
    [InlineData(null, 64, "unknown")]
    public void Rate_limit_key_groups_ipv6_by_network(string? address, int prefix, string expected)
        => Request(address).GetClientRateLimitKey(prefix).Should().Be(expected);

    [Fact]
    public void Failed_attempt_key_uses_the_ipv6_network()
        => Request("2001:db8:1:2::77").ClientKey().Should().Be("ip:2001:db8:1:2::");

    [Fact]
    public void Rate_limit_key_checks_arguments()
    {
        var withoutContext = () => ((HttpContext)null!).GetClientRateLimitKey();
        var negative = () => Request("::1").GetClientRateLimitKey(-1);
        var tooLong = () => Request("::1").GetClientRateLimitKey(129);

        withoutContext.Should().Throw<ArgumentNullException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
        tooLong.Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private static async Task<HttpContext> RunAsync(RequestDelegate endpoint, bool aborted, bool started = false)
    {
        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        app.UseClientDisconnectHandling();
        app.Run(endpoint);
        var pipeline = app.Build();

        var context = new DefaultHttpContext();
        if (started)
        {
            context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature { StatusCode = 200 });
        }
        using var cancellation = new CancellationTokenSource();
        if (aborted)
        {
            await cancellation.CancelAsync();
        }
        context.RequestAborted = cancellation.Token;

        await pipeline(context);
        return context;
    }

    [Fact]
    public async Task Request_closed_by_the_client_ends_with_499()
    {
        var context = await RunAsync(c => throw new OperationCanceledException(c.RequestAborted), aborted: true);

        context.Response.StatusCode.Should().Be(ClientDisconnectExtensions.ClientClosedRequestStatusCode);
    }

    [Fact]
    public async Task Started_response_keeps_its_status()
    {
        var context = await RunAsync(_ => throw new TaskCanceledException(), aborted: true, started: true);

        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Other_cancellations_and_errors_stay_errors()
    {
        var timeout = () => RunAsync(_ => throw new OperationCanceledException("own timeout"), aborted: false);
        var error = () => RunAsync(_ => throw new InvalidOperationException(), aborted: true);

        await timeout.Should().ThrowAsync<OperationCanceledException>();
        await error.Should().ThrowAsync<InvalidOperationException>();
        (await RunAsync(_ => Task.CompletedTask, aborted: false)).Response.StatusCode.Should().Be(200);

        var withoutApp = () => ((IApplicationBuilder)null!).UseClientDisconnectHandling();
        withoutApp.Should().Throw<ArgumentNullException>();
    }
}
