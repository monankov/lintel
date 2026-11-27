using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.AspNetCore.Mvc;

/// <summary>
/// Rendering Razor views to strings from controllers (for example to return HTML inside JSON).
/// </summary>
public static class PartialViewControllerExtensions
{
    /// <summary>
    /// Renders a partial view (without layout) to a string.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="controller">The controller.</param>
    /// <param name="viewName">The view name or path (<c>~/Views/...</c>); the action name when empty.</param>
    /// <param name="model">The model.</param>
    /// <returns>The HTML.</returns>
    public static Task<string> RenderPartialViewAsync<TModel>(this Controller controller, string? viewName, TModel model)
        => controller.RenderViewAsync(viewName, model, isPartial: true);

    /// <summary>
    /// Renders a view to a string.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="controller">The controller.</param>
    /// <param name="viewName">The view name or path (<c>~/Views/...</c>); the action name when empty.</param>
    /// <param name="model">The model.</param>
    /// <param name="isPartial"><see langword="true"/> to render without layout.</param>
    /// <returns>The HTML.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="controller"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">If the view is not found.</exception>
    public static async Task<string> RenderViewAsync<TModel>(this Controller controller, string? viewName, TModel model, bool isPartial = false)
    {
        ArgumentNullException.ThrowIfNull(controller);

        if (string.IsNullOrEmpty(viewName))
        {
            viewName = controller.ControllerContext.ActionDescriptor.ActionName;
        }

        controller.ViewData.Model = model;

        var viewEngine = controller.HttpContext.RequestServices.GetRequiredService<ICompositeViewEngine>();
        var viewResult = viewName.StartsWith("~/", StringComparison.Ordinal) || viewName.StartsWith('/')
            ? viewEngine.GetView(executingFilePath: null, viewName, isMainPage: !isPartial)
            : viewEngine.FindView(controller.ControllerContext, viewName, isMainPage: !isPartial);

        if (!viewResult.Success)
        {
            throw new InvalidOperationException($"A view with the name \"{viewName}\" could not be found. Searched locations: {string.Join(", ", viewResult.SearchedLocations)}.");
        }

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(controller.ControllerContext, viewResult.View, controller.ViewData, controller.TempData, writer, new HtmlHelperOptions());
        await viewResult.View.RenderAsync(viewContext).ConfigureAwait(false);
        return writer.ToString();
    }
}
