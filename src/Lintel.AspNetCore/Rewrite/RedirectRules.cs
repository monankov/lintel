using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Net.Http.Headers;

namespace Lintel.AspNetCore.Rewrite;

/// <summary>
/// Patterns of the redirect rules.
/// </summary>
public static partial class RedirectDefaults
{
    /// <summary>
    /// Matches paths whose last segment is not a file name and that do not end with a slash: <c>/orders</c>, but not
    /// <c>/orders/</c> or <c>/site.css</c>.
    /// </summary>
    public const string TrailingSlashMatchPattern = "^(((.*/)|(/?))[^/.]+(?!/$))$";

    [GeneratedRegex(TrailingSlashMatchPattern, RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    internal static partial Regex TrailingSlashRegex();
}

/// <summary>
/// Redirects GET and HEAD requests whose scheme, host or path contain upper-case letters to the lower-case URL.
/// The query string is kept as is.
/// </summary>
/// <param name="filter">Which requests the rule applies to.</param>
/// <param name="statusCode">The redirect status code, usually 301.</param>
public sealed class RedirectToLowercaseRule(Func<HttpRequest, bool> filter, int statusCode)
    : RedirectRule(filter, statusCode, lowercase: true, trailingSlash: false);

/// <summary>
/// Redirects GET and HEAD requests to the same URL with a trailing slash (except file paths).
/// </summary>
/// <param name="filter">Which requests the rule applies to.</param>
/// <param name="statusCode">The redirect status code, usually 301.</param>
public sealed class RedirectToTrailingSlashRule(Func<HttpRequest, bool> filter, int statusCode)
    : RedirectRule(filter, statusCode, lowercase: false, trailingSlash: true);

/// <summary>
/// Redirects GET and HEAD requests to the lower-case URL with a trailing slash in a single redirect.
/// </summary>
/// <param name="filter">Which requests the rule applies to.</param>
/// <param name="statusCode">The redirect status code, usually 301.</param>
public sealed class RedirectToLowercaseTrailingSlashRule(Func<HttpRequest, bool> filter, int statusCode)
    : RedirectRule(filter, statusCode, lowercase: true, trailingSlash: true);

/// <summary>
/// Base class of the redirect rules.
/// </summary>
public abstract class RedirectRule : IRule
{
    private readonly Func<HttpRequest, bool> _filter;
    private readonly int _statusCode;
    private readonly bool _lowercase;
    private readonly bool _trailingSlash;

    private protected RedirectRule(Func<HttpRequest, bool> filter, int statusCode, bool lowercase, bool trailingSlash)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _filter = filter;
        _statusCode = statusCode;
        _lowercase = lowercase;
        _trailingSlash = trailingSlash;
    }

    /// <inheritdoc />
    public void ApplyRule(RewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Result = RuleResult.ContinueRules;

        var request = context.HttpContext.Request;
        if (!(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) || !_filter(request))
        {
            return;
        }

        var scheme = request.Scheme;
        var host = request.Host.Value ?? "";
        var pathBase = request.PathBase.Value ?? "";
        var path = request.Path.Value ?? "";

        var toLowercase = _lowercase && (HasUpper(scheme) || HasUpper(host) || HasUpper(pathBase) || HasUpper(path));
        var addSlash = _trailingSlash && NeedsTrailingSlash(path);
        if (!toLowercase && !addSlash)
        {
            return;
        }

        if (_lowercase)
        {
            scheme = scheme.ToLowerInvariant();
            host = host.ToLowerInvariant();
            pathBase = pathBase.ToLowerInvariant();
            path = path.ToLowerInvariant();
        }
        if (addSlash)
        {
            path += "/";
        }

        var response = context.HttpContext.Response;
        response.StatusCode = _statusCode;
        response.Headers[HeaderNames.Location] = UriHelper.BuildAbsolute(scheme, new HostString(host), pathBase, path, request.QueryString);
        context.Result = RuleResult.EndResponse;
    }

    private static bool HasUpper(string value) => value.Any(char.IsUpper);

    private static bool NeedsTrailingSlash(string path)
    {
        try
        {
            return RedirectDefaults.TrailingSlashRegex().IsMatch(path);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}

/// <summary>
/// Registration of the redirect rules: <c>app.UseRewriter(new RewriteOptions().AddRedirectToLowercase())</c>.
/// </summary>
public static class RewriteOptionsExtensions
{
    /// <summary>Adds <see cref="RedirectToLowercaseRule"/> for all requests with status 301.</summary>
    /// <param name="options">The rewrite options.</param>
    /// <returns>The same options.</returns>
    public static RewriteOptions AddRedirectToLowercase(this RewriteOptions options)
        => options.AddRedirectToLowercase(static _ => true, StatusCodes.Status301MovedPermanently);

    /// <summary>Adds <see cref="RedirectToLowercaseRule"/>.</summary>
    /// <param name="options">The rewrite options.</param>
    /// <param name="filter">Which requests the rule applies to.</param>
    /// <param name="statusCode">The redirect status code.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> or <paramref name="filter"/> is <see langword="null"/>.</exception>
    public static RewriteOptions AddRedirectToLowercase(this RewriteOptions options, Func<HttpRequest, bool> filter, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Add(new RedirectToLowercaseRule(filter, statusCode));
    }

    /// <summary>Adds <see cref="RedirectToTrailingSlashRule"/> for all requests with status 301.</summary>
    /// <param name="options">The rewrite options.</param>
    /// <returns>The same options.</returns>
    public static RewriteOptions AddRedirectToTrailingSlash(this RewriteOptions options)
        => options.AddRedirectToTrailingSlash(static _ => true, StatusCodes.Status301MovedPermanently);

    /// <summary>Adds <see cref="RedirectToTrailingSlashRule"/>.</summary>
    /// <param name="options">The rewrite options.</param>
    /// <param name="filter">Which requests the rule applies to.</param>
    /// <param name="statusCode">The redirect status code.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> or <paramref name="filter"/> is <see langword="null"/>.</exception>
    public static RewriteOptions AddRedirectToTrailingSlash(this RewriteOptions options, Func<HttpRequest, bool> filter, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Add(new RedirectToTrailingSlashRule(filter, statusCode));
    }

    /// <summary>Adds <see cref="RedirectToLowercaseTrailingSlashRule"/> for all requests with status 301.</summary>
    /// <param name="options">The rewrite options.</param>
    /// <returns>The same options.</returns>
    public static RewriteOptions AddRedirectToLowercaseTrailingSlash(this RewriteOptions options)
        => options.AddRedirectToLowercaseTrailingSlash(static _ => true, StatusCodes.Status301MovedPermanently);

    /// <summary>Adds <see cref="RedirectToLowercaseTrailingSlashRule"/>.</summary>
    /// <param name="options">The rewrite options.</param>
    /// <param name="filter">Which requests the rule applies to.</param>
    /// <param name="statusCode">The redirect status code.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> or <paramref name="filter"/> is <see langword="null"/>.</exception>
    public static RewriteOptions AddRedirectToLowercaseTrailingSlash(this RewriteOptions options, Func<HttpRequest, bool> filter, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Add(new RedirectToLowercaseTrailingSlashRule(filter, statusCode));
    }
}
