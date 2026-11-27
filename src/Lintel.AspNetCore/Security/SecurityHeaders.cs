using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lintel.AspNetCore.Security;

/// <summary>
/// Options of <see cref="SecurityHeadersExtensions.UseSecurityHeaders"/>.
/// </summary>
public class SecurityHeadersOptions
{
    /// <summary>
    /// The default policy: only own resources, no inline scripts and no <c>&lt;style&gt;</c> elements; <c>style</c>
    /// attributes for computed values and <c>data:</c> images (used by Bootstrap) are allowed.
    /// </summary>
    public const string DefaultContentSecurityPolicy =
        "default-src 'self'; style-src-attr 'unsafe-inline'; img-src 'self' data:; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";

    /// <summary>
    /// What Hot Reload and Browser Link need in Development: scripts and connections to <c>localhost</c> on other ports.
    /// </summary>
    public const string DefaultDevelopmentAdditions =
        "script-src 'self' http://localhost:*; connect-src 'self' ws://localhost:* wss://localhost:* http://localhost:*";

    /// <summary>The <c>Content-Security-Policy</c>; <see langword="null"/> sends none.</summary>
    public string? ContentSecurityPolicy { get; set; } = DefaultContentSecurityPolicy;

    /// <summary>Appended to the policy in the Development environment; <see langword="null"/> appends nothing.</summary>
    public string? DevelopmentAdditions { get; set; } = DefaultDevelopmentAdditions;

    /// <summary>
    /// The policy of <c>application/pdf</c> responses: the PDF viewers of browsers need their own scripts and styles,
    /// so only embedding is restricted. Default: <c>frame-ancestors 'self'</c>; <see langword="null"/> uses the general policy.
    /// </summary>
    public string? PdfContentSecurityPolicy { get; set; } = "frame-ancestors 'self'";

    /// <summary>The <c>Referrer-Policy</c>. Default: <c>strict-origin-when-cross-origin</c>; <see langword="null"/> sends none.</summary>
    public string? ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>Whether <c>X-Content-Type-Options: nosniff</c> is sent. Default: <see langword="true"/>.</summary>
    public bool NoSniff { get; set; } = true;
}

/// <summary>
/// Security headers of responses.
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds <c>Content-Security-Policy</c>, <c>X-Content-Type-Options: nosniff</c> and <c>Referrer-Policy</c> to responses.
    /// Place it after <c>UseExceptionHandler</c> (error pages get the headers, the developer exception page with its inline
    /// scripts does not) and before <c>UseStaticFiles</c> (styles and scripts get <c>nosniff</c>).
    /// </summary>
    /// <remarks>
    /// The headers are set when the response starts, so the policy of PDF responses can depend on their content type.
    /// A header already set by the endpoint (for example its own policy) is kept. Set the policy here or on the reverse
    /// proxy, not in both: a browser applies all policies at once.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">Changes the options.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, Action<SecurityHeadersOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = new SecurityHeadersOptions();
        configure?.Invoke(options);

        var isDevelopment = app.ApplicationServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true;
        var policy = isDevelopment && !string.IsNullOrWhiteSpace(options.ContentSecurityPolicy) && !string.IsNullOrWhiteSpace(options.DevelopmentAdditions)
            ? $"{options.ContentSecurityPolicy.TrimEnd(' ', ';')}; {options.DevelopmentAdditions}"
            : options.ContentSecurityPolicy;

        return app.Use((context, next) =>
        {
            var response = context.Response;
            response.OnStarting(() =>
            {
                var headers = response.Headers;
                var isPdf = response.ContentType?.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase) == true;
                var responsePolicy = isPdf && options.PdfContentSecurityPolicy is not null ? options.PdfContentSecurityPolicy : policy;
                if (!string.IsNullOrWhiteSpace(responsePolicy) && !headers.ContainsKey("Content-Security-Policy"))
                {
                    headers.ContentSecurityPolicy = responsePolicy;
                }
                if (options.NoSniff)
                {
                    headers.XContentTypeOptions = "nosniff";
                }
                if (!string.IsNullOrWhiteSpace(options.ReferrerPolicy) && !headers.ContainsKey("Referrer-Policy"))
                {
                    headers["Referrer-Policy"] = options.ReferrerPolicy;
                }
                return Task.CompletedTask;
            });
            return next(context);
        });
    }
}
