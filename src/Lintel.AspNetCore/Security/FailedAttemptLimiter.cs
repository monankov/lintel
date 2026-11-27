using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.Security;

/// <summary>
/// Options of <see cref="FailedAttemptLimiter"/>, usually from configuration:
/// <c>"FailedAttempts": { "MaxFailures": 5, "Window": "01:00:00" }</c>.
/// </summary>
public class FailedAttemptOptions
{
    /// <summary>How many failures within <see cref="Window"/> block further attempts. Default: 5.</summary>
    public int MaxFailures { get; set; } = 5;

    /// <summary>The sliding window. Default: one hour.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Counts failed attempts (for example wrong passwords) per key in a sliding window and blocks the key while the limit is
/// reached. Keys are up to the application: the client address (<see cref="FailedAttemptLimiterExtensions.ClientKey"/>),
/// an account, or both, so that guessing from many addresses is limited too.
/// </summary>
/// <remarks>
/// <para>
/// While a key is blocked, do not check the password at all, even a correct one: otherwise guessing would continue.
/// A key is unblocked when its oldest counted failure leaves the window.
/// </para>
/// <para>
/// The counters live in <see cref="IMemoryCache"/> of the process: they are reset by a restart and are not shared between
/// servers. Entries have a size of 1, so the limiter works with a cache that has a <c>SizeLimit</c>.
/// </para>
/// </remarks>
public class FailedAttemptLimiter
{
    private const string KeyPrefix = "Lintel.FailedAttempts:";

    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;
    private readonly IOptionsMonitor<FailedAttemptOptions> _options;
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes the limiter.
    /// </summary>
    /// <param name="cache">The memory cache.</param>
    /// <param name="time">The clock.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public FailedAttemptLimiter(IMemoryCache cache, TimeProvider time, IOptionsMonitor<FailedAttemptOptions> options)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);

        _cache = cache;
        _time = time;
        _options = options;
    }

    /// <summary>Whether <paramref name="key"/> is blocked, and for how long.</summary>
    /// <param name="key">The key.</param>
    /// <param name="retryAfter">The time until the next attempt is allowed; zero when not blocked.</param>
    /// <returns><see langword="true"/> while the limit is reached.</returns>
    /// <exception cref="ArgumentException">If <paramref name="key"/> is empty.</exception>
    public bool IsBlocked(string key, out TimeSpan retryAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var options = _options.CurrentValue;
        lock (_lock)
        {
            var failures = Actual(key, options);
            if (failures is null || failures.Count < options.MaxFailures)
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            retryAfter = failures[^options.MaxFailures] + options.Window - _time.GetUtcNow();
            return true;
        }
    }

    /// <summary>Counts a failed attempt of <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <exception cref="ArgumentException">If <paramref name="key"/> is empty.</exception>
    public void RegisterFailure(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var options = _options.CurrentValue;
        lock (_lock)
        {
            var failures = Actual(key, options) ?? [];
            failures.Add(_time.GetUtcNow());
            _cache.Set(KeyPrefix + key, failures, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = options.Window });
        }
    }

    /// <summary>Forgets the failures of <paramref name="key"/>, for example after a successful sign-in.</summary>
    /// <param name="key">The key.</param>
    /// <exception cref="ArgumentException">If <paramref name="key"/> is empty.</exception>
    public void Reset(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_lock)
        {
            _cache.Remove(KeyPrefix + key);
        }
    }

    private List<DateTimeOffset>? Actual(string key, FailedAttemptOptions options)
    {
        if (!_cache.TryGetValue(KeyPrefix + key, out List<DateTimeOffset>? failures) || failures is null)
        {
            return null;
        }

        var windowStart = _time.GetUtcNow() - options.Window;
        failures.RemoveAll(failure => failure <= windowStart);
        return failures;
    }
}

/// <summary>
/// Registration and request helpers of <see cref="FailedAttemptLimiter"/>.
/// </summary>
public static class FailedAttemptLimiterExtensions
{
    /// <summary>Adds <see cref="FailedAttemptLimiter"/> with options from <paramref name="configuration"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section of <see cref="FailedAttemptOptions"/>.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public static IServiceCollection AddFailedAttemptLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<FailedAttemptOptions>().Bind(configuration);
        return services.AddFailedAttemptLimiter();
    }

    /// <summary>Adds <see cref="FailedAttemptLimiter"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the options.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddFailedAttemptLimiter(this IServiceCollection services, Action<FailedAttemptOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<FailedAttemptOptions>()
            .Validate(o => o.MaxFailures > 0 && o.Window > TimeSpan.Zero, "FailedAttempts: MaxFailures and Window must be positive.")
            .ValidateOnStart();
        if (configure is not null)
        {
            options.Configure(configure);
        }
        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<FailedAttemptLimiter>();
        return services;
    }

    /// <summary>
    /// The key of the client address of the request (<c>ip:192.168.0.10</c>; an IPv6 address by its <c>/64</c> network;
    /// <c>ip:unknown</c> without an address), see <see cref="IpAddressExtensions.GetClientRateLimitKey"/>.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="context"/> is <see langword="null"/>.</exception>
    public static string ClientKey(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return "ip:" + context.GetClientRateLimitKey();
    }

    /// <summary>
    /// Sets the response to <c>429 Too Many Requests</c> with <c>Retry-After</c> in whole seconds (at least 1).
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="retryAfter">The time until the next attempt.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="response"/> is <see langword="null"/>.</exception>
    public static void SetTooManyRequests(this HttpResponse response, TimeSpan retryAfter)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    }
}
