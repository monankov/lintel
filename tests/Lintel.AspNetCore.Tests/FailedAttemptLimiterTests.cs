using System.Net;
using Lintel.AspNetCore.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.Tests;

public class FailedAttemptLimiterTests
{
    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (FailedAttemptLimiter Limiter, ManualTime Time) Create(int maxFailures = 3, int windowMinutes = 10, long? sizeLimit = null)
    {
        var time = new ManualTime(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(time)
            .AddFailedAttemptLimiter(o => { o.MaxFailures = maxFailures; o.Window = TimeSpan.FromMinutes(windowMinutes); });
        if (sizeLimit is not null)
        {
            services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit }));
        }
        return (services.BuildServiceProvider().GetRequiredService<FailedAttemptLimiter>(), time);
    }

    [Fact]
    public void Key_is_blocked_after_max_failures_until_the_oldest_leaves_the_window()
    {
        var (limiter, time) = Create();

        limiter.RegisterFailure("ip:1.2.3.4");
        time.Now = time.Now.AddMinutes(2);
        limiter.RegisterFailure("ip:1.2.3.4");
        limiter.IsBlocked("ip:1.2.3.4", out var notYet).Should().BeFalse();
        notYet.Should().Be(TimeSpan.Zero);
        time.Now = time.Now.AddMinutes(2);
        limiter.RegisterFailure("ip:1.2.3.4");

        limiter.IsBlocked("ip:1.2.3.4", out var retryAfter).Should().BeTrue();
        retryAfter.Should().Be(TimeSpan.FromMinutes(6), "the first failure was 4 minutes ago in a 10-minute window");
        limiter.IsBlocked("ip:5.6.7.8", out _).Should().BeFalse("keys are independent");

        time.Now = time.Now.AddMinutes(6);
        limiter.IsBlocked("ip:1.2.3.4", out _).Should().BeFalse("the oldest failure left the window");
    }

    [Fact]
    public void Retry_after_counts_from_the_failure_that_reached_the_limit()
    {
        var (limiter, time) = Create(maxFailures: 2);

        for (var i = 0; i < 4; i++)
        {
            limiter.RegisterFailure("user:a@b.test");
            time.Now = time.Now.AddMinutes(1);
        }

        limiter.IsBlocked("user:a@b.test", out var retryAfter).Should().BeTrue();
        retryAfter.Should().Be(TimeSpan.FromMinutes(8), "the two newest failures decide: the older of them was 2 minutes ago");
    }

    [Fact]
    public void Reset_forgets_failures_and_works_with_a_size_limited_cache()
    {
        var (limiter, _) = Create(maxFailures: 1, sizeLimit: 100);

        limiter.RegisterFailure("k");
        limiter.IsBlocked("k", out _).Should().BeTrue();

        limiter.Reset("k");
        limiter.IsBlocked("k", out _).Should().BeFalse();
    }

    [Fact]
    public void Options_come_from_configuration_and_are_validated()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new("MaxFailures", "7"), new("Window", "00:30:00")]).Build();
        var options = new ServiceCollection().AddFailedAttemptLimiter(configuration).BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<FailedAttemptOptions>>().CurrentValue;

        options.MaxFailures.Should().Be(7);
        options.Window.Should().Be(TimeSpan.FromMinutes(30));

        var invalid = () => new ServiceCollection().AddFailedAttemptLimiter(o => o.MaxFailures = 0).BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<FailedAttemptOptions>>().CurrentValue;
        invalid.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Client_key_and_too_many_requests_response()
    {
        var context = new DefaultHttpContext();
        context.ClientKey().Should().Be("ip:unknown");
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:10.0.0.1");
        context.ClientKey().Should().Be("ip:10.0.0.1");

        context.Response.SetTooManyRequests(TimeSpan.FromSeconds(90.2));
        context.Response.StatusCode.Should().Be(429);
        context.Response.Headers.RetryAfter.ToString().Should().Be("91");
        context.Response.SetTooManyRequests(TimeSpan.Zero);
        context.Response.Headers.RetryAfter.ToString().Should().Be("1");
    }

    [Fact]
    public void Arguments_are_checked()
    {
        var (limiter, _) = Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var monitor = new ServiceCollection().AddOptions().BuildServiceProvider().GetRequiredService<IOptionsMonitor<FailedAttemptOptions>>();
        var actions = new Action[]
        {
            () => limiter.IsBlocked(" ", out _),
            () => limiter.RegisterFailure(""),
            () => limiter.Reset(null!),
            () => _ = new FailedAttemptLimiter(null!, TimeProvider.System, monitor),
            () => _ = new FailedAttemptLimiter(cache, null!, monitor),
            () => _ = new FailedAttemptLimiter(cache, TimeProvider.System, null!),
            () => ((IServiceCollection)null!).AddFailedAttemptLimiter(),
            () => new ServiceCollection().AddFailedAttemptLimiter((IConfiguration)null!),
            () => ((HttpContext)null!).ClientKey(),
            () => ((HttpResponse)null!).SetTooManyRequests(TimeSpan.Zero),
        };
        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentException>();
        }
    }
}
