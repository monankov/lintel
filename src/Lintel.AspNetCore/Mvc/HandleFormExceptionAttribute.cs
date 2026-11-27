using Lintel.AspNetCore.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DataAnnotationsValidationException = System.ComponentModel.DataAnnotations.ValidationException;

namespace Lintel.AspNetCore.Mvc;

/// <summary>
/// Turns an exception of a form action into <c>ModelState</c> errors. Register your own implementation to show
/// messages of your exceptions: <c>services.AddScoped&lt;IFormExceptionHandler, MyFormExceptionHandler&gt;()</c>.
/// </summary>
public interface IFormExceptionHandler
{
    /// <summary>
    /// Adds errors for <paramref name="exception"/> to <paramref name="modelState"/>.
    /// </summary>
    /// <param name="exception">The exception thrown by the action.</param>
    /// <param name="modelState">The model state of the action.</param>
    /// <returns>
    /// <see langword="true"/> to render the form again; <see langword="false"/> to let the exception propagate
    /// (for example to <c>UseExceptionHandler</c>).
    /// </returns>
    bool Handle(Exception exception, ModelStateDictionary modelState);
}

/// <summary>
/// Options of the default <see cref="IFormExceptionHandler"/>.
/// </summary>
public class FormExceptionOptions
{
    /// <summary>The form error shown for exceptions other than validation ones.</summary>
    public string ErrorMessage { get; set; } = "The form could not be saved. Please try again.";
}

/// <summary>
/// The default <see cref="IFormExceptionHandler"/>: a <see cref="DataAnnotationsValidationException"/> becomes errors of
/// its members (or of the form when it has none); any other exception becomes <see cref="FormExceptionOptions.ErrorMessage"/>,
/// so that technical details are not shown to the user.
/// </summary>
/// <param name="options">The options.</param>
public class FormExceptionHandler(IOptions<FormExceptionOptions> options) : IFormExceptionHandler
{
    private readonly FormExceptionOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public virtual bool Handle(Exception exception, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(modelState);

        if (exception is DataAnnotationsValidationException { ValidationResult: var result })
        {
            var message = result.ErrorMessage ?? exception.Message;
            var members = result.MemberNames.Where(name => !string.IsNullOrEmpty(name)).ToList();
            foreach (var member in members.Count > 0 ? members : [""])
            {
                modelState.AddModelError(member, message);
            }
            return true;
        }

        modelState.AddModelError("", _options.ErrorMessage);
        return true;
    }
}

/// <summary>
/// Renders the form again with an error instead of failing when the action throws:
/// <c>[HttpPost, HandleFormException] public IActionResult Edit(OrderForm form) { ... }</c>.
/// </summary>
/// <remarks>
/// <para>
/// Applies to non-AJAX requests with a form body (<c>application/x-www-form-urlencoded</c> or <c>multipart/form-data</c>)
/// to actions of a <see cref="Controller"/>; other exceptions propagate unchanged. The exception is logged, turned into
/// <c>ModelState</c> errors by <see cref="IFormExceptionHandler"/> and the view (<see cref="ViewName"/> or the view of the
/// action) is rendered with the controller's <c>ViewData</c>, so the entered values and the model are kept.
/// </para>
/// <para>Exceptions thrown while the result executes (for example while a view renders) are not handled.</para>
/// </remarks>
/// <param name="viewName">The view to render; by default the view of the action.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HandleFormExceptionAttribute(string? viewName = null) : Attribute, IFilterFactory
{
    /// <summary>The view to render; <see langword="null"/> means the view of the action.</summary>
    public string? ViewName { get; } = string.IsNullOrWhiteSpace(viewName) ? null : viewName;

    /// <inheritdoc />
    public bool IsReusable => false;

    /// <inheritdoc />
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var handler = serviceProvider.GetService<IFormExceptionHandler>()
            ?? new FormExceptionHandler(serviceProvider.GetService<IOptions<FormExceptionOptions>>() ?? Options.Create(new FormExceptionOptions()));
        var logger = serviceProvider.GetService<ILogger<HandleFormExceptionAttribute>>()
            ?? (ILogger)Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        return new Filter(ViewName, handler, logger);
    }

    private sealed class Filter(string? viewName, IFormExceptionHandler handler, ILogger logger) : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception is not { } exception || context.ExceptionHandled
                || context.Controller is not Controller controller || !IsFormPost(context.HttpContext.Request))
            {
                return;
            }

            if (!handler.Handle(exception, context.ModelState))
            {
                return;
            }

            logger.LogError(exception, "The form action {Action} failed; the form is rendered again.", context.ActionDescriptor.DisplayName);
            context.Result = new ViewResult
            {
                ViewName = viewName,
                ViewData = controller.ViewData,
                TempData = controller.TempData,
            };
            context.ExceptionHandled = true;
        }

        private static bool IsFormPost(HttpRequest request)
            => !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)
                && request.HasFormContentType && !request.IsAjaxRequest();
    }
}
