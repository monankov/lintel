using Lintel.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Client.Alerts;

/// <summary>
/// A message for the user shown on the next rendered page (for example "Saved" after a redirect).
/// </summary>
public class ClientAlert
{
    /// <summary>
    /// Initializes an alert.
    /// </summary>
    /// <param name="type">The kind of alert, for example one of <see cref="ClientAlertTypes"/>.</param>
    /// <param name="message">The text.</param>
    /// <exception cref="ArgumentException">If <paramref name="type"/> or <paramref name="message"/> is empty.</exception>
    public ClientAlert(string type, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Type = type;
        Message = message;
    }

    /// <summary>The kind of alert.</summary>
    public string Type { get; }

    /// <summary>The text.</summary>
    public string Message { get; }
}

/// <summary>
/// Standard alert types. Any other string can be used as well.
/// </summary>
public static class ClientAlertTypes
{
    /// <summary>An operation succeeded.</summary>
    public const string Success = "success";

    /// <summary>Neutral information.</summary>
    public const string Info = "info";

    /// <summary>Something needs attention.</summary>
    public const string Warning = "warning";

    /// <summary>An operation failed.</summary>
    public const string Error = "error";
}

/// <summary>
/// How <c>&lt;client-alerts&gt;</c> renders alerts as HTML. The defaults produce Bootstrap 5 markup.
/// </summary>
public class ClientAlertOptions
{
    /// <summary>The class of every alert element. Default: <c>alert</c>.</summary>
    public string CssClass { get; set; } = "alert";

    /// <summary>
    /// The class added for an alert type. Types that are not listed get <c>alert-{type}</c>.
    /// Default: <see cref="ClientAlertTypes.Error"/> is rendered as <c>alert-danger</c>.
    /// </summary>
    public IDictionary<string, string> TypeCssClasses { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [ClientAlertTypes.Error] = "alert-danger",
    };

    /// <summary>The classes added to a dismissible alert. Default: <c>alert-dismissible fade show</c>.</summary>
    public string DismissibleCssClass { get; set; } = "alert-dismissible fade show";

    /// <summary>The accessible label of the close button. Default: <c>Close</c>.</summary>
    public string CloseButtonLabel { get; set; } = "Close";

    /// <summary>Returns the class added for <paramref name="type"/>.</summary>
    /// <param name="type">The alert type.</param>
    /// <returns>The CSS class.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="type"/> is <see langword="null"/>.</exception>
    public string GetTypeCssClass(string type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return TypeCssClasses.TryGetValue(type, out var cssClass) ? cssClass : "alert-" + type.ToLowerInvariant();
    }
}

/// <summary>
/// Stores alerts for the next rendered page.
/// </summary>
public interface IClientAlertProvider
{
    /// <summary>Adds an alert.</summary>
    /// <param name="alert">The alert.</param>
    void AddClientAlert(ClientAlert alert);

    /// <summary>
    /// Returns the stored alerts to show them. They are removed at the end of the request, so each alert is shown once;
    /// calling this method again in the same request returns them again.
    /// </summary>
    /// <returns>The alerts.</returns>
    IEnumerable<ClientAlert> GetAllClientAlerts();
}

/// <summary>
/// Default <see cref="IClientAlertProvider"/> that keeps alerts in TempData, so they survive a redirect.
/// </summary>
public class ClientAlertProvider : IClientAlertProvider
{
    private static readonly string _key = typeof(IClientAlertProvider).FullName!;

    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="contextAccessor">The HTTP context accessor.</param>
    /// <param name="tempDataDictionaryFactory">The TempData factory.</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public ClientAlertProvider(IHttpContextAccessor contextAccessor, ITempDataDictionaryFactory tempDataDictionaryFactory)
    {
        ArgumentNullException.ThrowIfNull(contextAccessor);
        ArgumentNullException.ThrowIfNull(tempDataDictionaryFactory);

        _contextAccessor = contextAccessor;
        _tempDataDictionaryFactory = tempDataDictionaryFactory;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">If <paramref name="alert"/> is <see langword="null"/>.</exception>
    public void AddClientAlert(ClientAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);

        var tempData = GetTempData();
        List<ClientAlert> alerts = [.. Read(tempData), alert];
        tempData.Put(_key, alerts);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Structr read alerts with <c>Peek</c>, so they were never marked as read and appeared on every following page.
    /// </remarks>
    public IEnumerable<ClientAlert> GetAllClientAlerts() => GetTempData().Get<List<ClientAlert>>(_key) ?? [];

    // Adding does not mark the alerts as read: they must survive the redirect.
    private static List<ClientAlert> Read(ITempDataDictionary tempData) => tempData.Peek<List<ClientAlert>>(_key) ?? [];

    private ITempDataDictionary GetTempData()
        => _tempDataDictionaryFactory.GetTempData(_contextAccessor.HttpContext
            ?? throw new InvalidOperationException("Client alerts are available only during an HTTP request."));
}

/// <summary>
/// Adds an alert and then executes the wrapped result.
/// </summary>
public class ClientAlertResult : IActionResult
{
    private readonly IActionResult _result;
    private readonly ClientAlert _alert;

    /// <summary>
    /// Initializes the result.
    /// </summary>
    /// <param name="result">The wrapped result.</param>
    /// <param name="alert">The alert.</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public ClientAlertResult(IActionResult result, ClientAlert alert)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(alert);

        _result = result;
        _alert = alert;
    }

    /// <inheritdoc />
    public Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.HttpContext.RequestServices.GetRequiredService<IClientAlertProvider>().AddClientAlert(_alert);
        return _result.ExecuteResultAsync(context);
    }
}

/// <summary>
/// Adding alerts to action results: <c>return RedirectToAction("Index").Success("Saved");</c>
/// </summary>
public static class ClientAlertActionResultExtensions
{
    /// <summary>
    /// Wraps the result so that the alert is stored when it executes.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="alert">The alert.</param>
    /// <returns>The wrapped result.</returns>
    public static IActionResult AddClientAlert(this IActionResult result, ClientAlert alert) => new ClientAlertResult(result, alert);

    /// <summary>Adds an alert of type <see cref="ClientAlertTypes.Success"/>.</summary>
    /// <param name="result">The result.</param>
    /// <param name="message">The text.</param>
    /// <returns>The wrapped result.</returns>
    public static IActionResult Success(this IActionResult result, string message)
        => result.AddClientAlert(new ClientAlert(ClientAlertTypes.Success, message));

    /// <summary>Adds an alert of type <see cref="ClientAlertTypes.Info"/>.</summary>
    /// <param name="result">The result.</param>
    /// <param name="message">The text.</param>
    /// <returns>The wrapped result.</returns>
    public static IActionResult Info(this IActionResult result, string message)
        => result.AddClientAlert(new ClientAlert(ClientAlertTypes.Info, message));

    /// <summary>Adds an alert of type <see cref="ClientAlertTypes.Warning"/>.</summary>
    /// <param name="result">The result.</param>
    /// <param name="message">The text.</param>
    /// <returns>The wrapped result.</returns>
    public static IActionResult Warning(this IActionResult result, string message)
        => result.AddClientAlert(new ClientAlert(ClientAlertTypes.Warning, message));

    /// <summary>Adds an alert of type <see cref="ClientAlertTypes.Error"/>.</summary>
    /// <param name="result">The result.</param>
    /// <param name="message">The text.</param>
    /// <returns>The wrapped result.</returns>
    public static IActionResult Error(this IActionResult result, string message)
        => result.AddClientAlert(new ClientAlert(ClientAlertTypes.Error, message));
}
