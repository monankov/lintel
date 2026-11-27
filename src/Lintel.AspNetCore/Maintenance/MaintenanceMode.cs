using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.Maintenance;

/// <summary>
/// Options of the maintenance mode, usually bound to configuration (<c>"MaintenanceMode": { "Enabled": true }</c>) so that
/// it can be switched on without a restart.
/// </summary>
public class MaintenanceModeOptions
{
    /// <summary>Whether the application is in maintenance (used by the default <see cref="IMaintenanceModeProvider"/>).</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The page returned during maintenance: an absolute path or a path relative to the content root. When it is not set
    /// or the file does not exist, <see cref="Message"/> is returned as text.
    /// </summary>
    public string? PageFilePath { get; set; }

    /// <summary>The content type of the page. Default: <c>text/html; charset=utf-8</c>.</summary>
    public string PageContentType { get; set; } = "text/html; charset=utf-8";

    /// <summary>The text returned when there is no page.</summary>
    public string Message { get; set; } = "The application is temporarily unavailable. Please try again later.";

    /// <summary>The status code. Default: 503 Service Unavailable.</summary>
    public int StatusCode { get; set; } = StatusCodes.Status503ServiceUnavailable;

    /// <summary>The <c>Retry-After</c> header in seconds; not sent when <see langword="null"/>.</summary>
    public int? RetryAfterSeconds { get; set; }

    /// <summary>Path prefixes that stay available, for example <c>/health</c> or <c>/admin</c>.</summary>
    public IList<string> ExcludedPaths { get; } = [];

    /// <summary>Lets some requests through, for example those of administrators or from the local network.</summary>
    public Func<HttpContext, bool>? Bypass { get; set; }
}

/// <summary>
/// Tells whether the application is in maintenance. The default implementation reads
/// <see cref="MaintenanceModeOptions.Enabled"/>; register your own to ask another source (cache its answer: it is called
/// for every request).
/// </summary>
public interface IMaintenanceModeProvider
{
    /// <summary>Whether the application is in maintenance for this request.</summary>
    /// <param name="context">The request.</param>
    /// <returns><see langword="true"/> during maintenance.</returns>
    ValueTask<bool> IsEnabledAsync(HttpContext context);
}

/// <summary>
/// The default <see cref="IMaintenanceModeProvider"/>: <see cref="MaintenanceModeOptions.Enabled"/> of the current options.
/// </summary>
/// <param name="options">The options monitor.</param>
public class OptionsMaintenanceModeProvider(IOptionsMonitor<MaintenanceModeOptions> options) : IMaintenanceModeProvider
{
    private readonly IOptionsMonitor<MaintenanceModeOptions> _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public ValueTask<bool> IsEnabledAsync(HttpContext context) => ValueTask.FromResult(_options.CurrentValue.Enabled);
}

/// <summary>
/// Answers requests with the maintenance page while the application is in maintenance.
/// </summary>
public class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<MaintenanceModeOptions> _options;
    private readonly IMaintenanceModeProvider _provider;
    private readonly string _contentRoot;

    /// <summary>
    /// Initializes the middleware.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="options">The options monitor.</param>
    /// <param name="provider">The maintenance state.</param>
    /// <param name="environment">The host environment, for relative page paths.</param>
    /// <exception cref="ArgumentNullException">If an argument other than <paramref name="environment"/> is <see langword="null"/>.</exception>
    public MaintenanceModeMiddleware(RequestDelegate next, IOptionsMonitor<MaintenanceModeOptions> options, IMaintenanceModeProvider provider,
        IWebHostEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(provider);

        _next = next;
        _options = options;
        _provider = provider;
        _contentRoot = environment?.ContentRootPath ?? AppContext.BaseDirectory;
    }

    /// <summary>Processes a request.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = _options.CurrentValue;
        if (IsExcluded(context, options) || !await _provider.IsEnabledAsync(context))
        {
            await _next(context);
            return;
        }

        var response = context.Response;
        response.StatusCode = options.StatusCode;
        response.Headers.CacheControl = "no-store";
        if (options.RetryAfterSeconds is { } seconds)
        {
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        var page = string.IsNullOrWhiteSpace(options.PageFilePath) ? null : Path.Combine(_contentRoot, options.PageFilePath);
        if (page is not null && File.Exists(page))
        {
            response.ContentType = options.PageContentType;
            await response.SendFileAsync(page, context.RequestAborted);
        }
        else
        {
            response.ContentType = "text/plain; charset=utf-8";
            await response.WriteAsync(options.Message, context.RequestAborted);
        }
    }

    private static bool IsExcluded(HttpContext context, MaintenanceModeOptions options)
        => options.ExcludedPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase))
            || options.Bypass?.Invoke(context) == true;
}

/// <summary>
/// Registration of the maintenance mode.
/// </summary>
public static class MaintenanceModeExtensions
{
    /// <summary>
    /// Adds the maintenance mode with options bound to <paramref name="configuration"/> (they are reloaded with it).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section, for example <c>Configuration.GetSection("MaintenanceMode")</c>.</param>
    /// <param name="configure">Changes the options after binding (for example <see cref="MaintenanceModeOptions.Bypass"/>).</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMaintenanceMode(this IServiceCollection services, IConfiguration configuration,
        Action<MaintenanceModeOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MaintenanceModeOptions>().Bind(configuration);
        return services.AddMaintenanceMode(configure);
    }

    /// <summary>
    /// Adds the maintenance mode. An <see cref="IMaintenanceModeProvider"/> registered before is kept.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the options.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMaintenanceMode(this IServiceCollection services, Action<MaintenanceModeOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<MaintenanceModeOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }
        services.TryAddSingleton<IMaintenanceModeProvider, OptionsMaintenanceModeProvider>();
        return services;
    }

    /// <summary>
    /// Answers requests with the maintenance page during maintenance. Place it after <c>UseStaticFiles</c> (so that the
    /// page can use styles and images) and before routing. Requires <see cref="AddMaintenanceMode(IServiceCollection, Action{MaintenanceModeOptions}?)"/>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseMaintenanceMode(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<MaintenanceModeMiddleware>();
    }
}
