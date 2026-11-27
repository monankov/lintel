using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Http;

/// <summary>
/// Helpers for <see cref="HttpContext"/>.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Returns the IP address of the client as a string: the text form of
    /// <see cref="Security.IpAddressExtensions.GetClientIpAddress"/> (an IPv4 address mapped to IPv6 is returned as IPv4).
    /// </summary>
    /// <remarks>
    /// Behind a reverse proxy or load balancer, configure it with <c>AddForwardedHeaders</c> and enable
    /// <c>app.UseForwardedHeaders()</c> so that this is the client address, not the proxy one.
    /// </remarks>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The address, or <see langword="null"/> if unknown (for example in tests or over a Unix socket).</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="context"/> is <see langword="null"/>.</exception>
    public static string? GetIpAddress(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Security.IpAddressExtensions.GetClientIpAddress(context)?.ToString();
    }

    /// <summary>
    /// Returns all registered authentication schemes.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The schemes.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="context"/> is <see langword="null"/>.</exception>
    public static Task<IEnumerable<AuthenticationScheme>> GetAuthenticationSchemesAsync(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
    }

    /// <summary>
    /// Whether an authentication scheme with the given name (case-insensitive) is registered.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="scheme">The scheme name.</param>
    /// <returns><see langword="true"/> if the scheme is registered.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="context"/> is <see langword="null"/>.</exception>
    public static async Task<bool> IsSupportedAuthenticationSchemeAsync(this HttpContext context, string scheme)
    {
        var schemes = await context.GetAuthenticationSchemesAsync().ConfigureAwait(false);
        return schemes.Any(x => string.Equals(x.Name, scheme, StringComparison.OrdinalIgnoreCase));
    }
}
