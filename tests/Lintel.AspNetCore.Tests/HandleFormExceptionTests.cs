using System.ComponentModel.DataAnnotations;
using Lintel.AspNetCore.Mvc;
using Lintel.AspNetCore.Tests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lintel.AspNetCore.Tests;

public class HandleFormExceptionTests
{
    private sealed class TestController : Controller;

    private sealed class RefusingHandler : IFormExceptionHandler
    {
        public bool Handle(Exception exception, ModelStateDictionary modelState) => false;
    }

    private static DefaultHttpContext FormPost(string method = "POST", string contentType = "application/x-www-form-urlencoded", bool ajax = false)
    {
        var context = TestContextFactory.HttpContext();
        context.Request.Method = method;
        context.Request.ContentType = contentType;
        if (ajax)
        {
            context.Request.Headers.XRequestedWith = "XMLHttpRequest";
        }
        return context;
    }

    private static ActionExecutedContext Execute(Exception? exception, HttpContext? httpContext = null, object? controller = null,
        HandleFormExceptionAttribute? attribute = null, IServiceProvider? services = null)
    {
        httpContext ??= FormPost();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor { DisplayName = "Orders.Edit" });
        controller ??= new TestController { ViewData = new(new EmptyModelMetadataProvider(), actionContext.ModelState) { Model = "model" } };
        var executed = new ActionExecutedContext(actionContext, [], controller) { Exception = exception };

        var filter = (IActionFilter)(attribute ?? new HandleFormExceptionAttribute())
            .CreateInstance(services ?? new ServiceCollection().AddLogging().AddFormExceptionHandling().BuildServiceProvider());
        filter.OnActionExecuting(new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller));
        filter.OnActionExecuted(executed);
        return executed;
    }

    [Fact]
    public void Exception_of_form_post_renders_the_view_again_with_controller_view_data()
    {
        var context = Execute(new InvalidOperationException("Database is down"));

        context.ExceptionHandled.Should().BeTrue();
        var view = context.Result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().BeNull("the view of the action is used");
        view.ViewData!.Model.Should().Be("model");
        context.ModelState[""]!.Errors.Select(e => e.ErrorMessage).Should().Equal("The form could not be saved. Please try again.");
    }

    [Fact]
    public void View_name_and_message_are_configurable()
    {
        var services = new ServiceCollection().AddFormExceptionHandling(o => o.ErrorMessage = "Не удалось сохранить.").BuildServiceProvider();

        var context = Execute(new InvalidOperationException(), attribute: new HandleFormExceptionAttribute("Edit"), services: services);

        context.Result.Should().BeOfType<ViewResult>().Which.ViewName.Should().Be("Edit");
        context.ModelState[""]!.Errors.Single().ErrorMessage.Should().Be("Не удалось сохранить.");
        new HandleFormExceptionAttribute(" ").ViewName.Should().BeNull();
        new HandleFormExceptionAttribute().IsReusable.Should().BeFalse();
    }

    [Fact]
    public void Validation_exception_becomes_errors_of_its_members_or_of_the_form()
    {
        var withMembers = Execute(new ValidationException(new ValidationResult("Name is taken.", ["Name", "Email"]), null, null));
        var withoutMembers = Execute(new ValidationException("Dates overlap."));

        withMembers.ModelState["Name"]!.Errors.Single().ErrorMessage.Should().Be("Name is taken.");
        withMembers.ModelState["Email"]!.Errors.Single().ErrorMessage.Should().Be("Name is taken.");
        withoutMembers.ModelState[""]!.Errors.Single().ErrorMessage.Should().Be("Dates overlap.");
    }

    public static TheoryData<string, HttpContext?, bool> NotHandled() => new()
    {
        { "GET", FormPost("GET"), false },
        { "HEAD", FormPost("HEAD"), false },
        { "JSON body", FormPost(contentType: "application/json"), false },
        { "AJAX", FormPost(ajax: true), false },
        { "multipart PUT", FormPost("PUT", "multipart/form-data; boundary=x"), true },
    };

    [Theory]
    [MemberData(nameof(NotHandled))]
    public void Only_non_ajax_form_submissions_are_handled(string request, HttpContext? httpContext, bool handled)
        => Execute(new InvalidOperationException(), httpContext).ExceptionHandled.Should().Be(handled, request);

    [Fact]
    public void Other_cases_are_left_to_the_pipeline()
    {
        Execute(null).Result.Should().BeNull("there is no exception");
        Execute(new InvalidOperationException(), controller: new object()).ExceptionHandled.Should().BeFalse("not a Controller");

        var refusing = new ServiceCollection().AddScoped<IFormExceptionHandler, RefusingHandler>().BuildServiceProvider();
        Execute(new InvalidOperationException(), services: refusing).ExceptionHandled.Should().BeFalse("the handler refused");
    }

    [Fact]
    public void Already_handled_exception_is_not_handled_again()
    {
        var httpContext = FormPost();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var executed = new ActionExecutedContext(actionContext, [], new TestController())
        {
            Exception = new InvalidOperationException(),
            ExceptionHandled = true,
        };

        var filter = (IActionFilter)new HandleFormExceptionAttribute().CreateInstance(new ServiceCollection().BuildServiceProvider());
        filter.OnActionExecuted(executed);

        executed.Result.Should().BeNull();
    }

    [Fact]
    public void Works_without_registered_services_and_checks_arguments()
    {
        Execute(new InvalidOperationException(), services: new ServiceCollection().BuildServiceProvider()).ExceptionHandled.Should().BeTrue();

        var handler = new FormExceptionHandler(Options.Create(new FormExceptionOptions()));
        var actions = new Action[]
        {
            () => new HandleFormExceptionAttribute().CreateInstance(null!),
            () => _ = new FormExceptionHandler(null!),
            () => handler.Handle(null!, new ModelStateDictionary()),
            () => handler.Handle(new InvalidOperationException(), null!),
        };
        foreach (var action in actions)
        {
            action.Should().Throw<ArgumentNullException>();
        }
    }
}
