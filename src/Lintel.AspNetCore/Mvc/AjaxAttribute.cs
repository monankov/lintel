using Lintel.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Routing;

namespace Lintel.AspNetCore.Mvc;

/// <summary>
/// Selects the action only for AJAX requests (<c>X-Requested-With: XMLHttpRequest</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AjaxAttribute : ActionMethodSelectorAttribute
{
    /// <inheritdoc />
    public override bool IsValidForRequest(RouteContext routeContext, ActionDescriptor action)
    {
        ArgumentNullException.ThrowIfNull(routeContext);

        return routeContext.HttpContext.Request.IsAjaxRequest();
    }
}
