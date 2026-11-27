using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Lintel.AspNetCore.Http;

/// <summary>
/// Helpers for <see cref="HttpRequest"/>.
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Whether the request was sent by script with the <c>X-Requested-With: XMLHttpRequest</c> header
    /// (jQuery, many AJAX libraries; <c>fetch</c> does not send it by default).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> for an AJAX request.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="request"/> is <see langword="null"/>.</exception>
    public static bool IsAjaxRequest(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the absolute, encoded URL of the request, including the path base.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The URL.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="request"/> is <see langword="null"/>.</exception>
    public static string GetAbsoluteUri(this HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.GetEncodedUrl();
    }

    /// <summary>
    /// Whether <paramref name="url"/> points to this application: a local path (<c>/orders?page=2</c>)
    /// or an absolute URL with the scheme and host of the request.
    /// </summary>
    /// <remarks>Use it before redirecting to a URL that comes from the client, to prevent open redirects.</remarks>
    /// <param name="request">The request.</param>
    /// <param name="url">The URL to check.</param>
    /// <returns><see langword="true"/> if the URL is local.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="request"/> is <see langword="null"/>.</exception>
    public static bool IsLocalUrl(this HttpRequest request, string? url)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // "/path" but not "//host" or "/\host" (browsers treat both as protocol-relative), or "~/path".
        if (url[0] == '/')
        {
            return url.Length == 1 || (url[1] != '/' && url[1] != '\\');
        }
        if (url.Length > 1 && url[0] == '~' && url[1] == '/')
        {
            return url.Length == 2 || (url[2] != '/' && url[2] != '\\');
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.Equals(uri.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }
}
