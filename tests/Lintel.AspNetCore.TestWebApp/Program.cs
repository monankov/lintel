using Lintel.AspNetCore.Localization;
using Lintel.AspNetCore.ModelBinding;
using Lintel.AspNetCore.Navigation;
using Lintel.AspNetCore.Rewrite;
using Lintel.AspNetCore.Routing;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews(options => options.AddSlugifyRouting().UseDateModelBinding("dd.MM.yyyy"))
    .AddLocalizedValidationMessages()
    .Services
    .AddRazorPages().Services
    .AddAspNetCore()
    .AddAspNetCoreValidation()
    .AddRouting(options =>
    {
        options.LowercaseUrls = true;
        options.AddSlugifyRouting();
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administrators", policy => policy.RequireRole("Administrator"));
    options.AddPolicy("Everyone", policy => policy.RequireAssertion(_ => true));
});
builder.Services.AddNavigation()
    .AddJson<RouteBreadcrumb>(Path.Combine(builder.Environment.ContentRootPath, "Breadcrumbs.json"),
        (services, options) => options.ActivateByRoute(services))
    .AddJson<MenuItem>(Path.Combine(builder.Environment.ContentRootPath, "Menu.json"),
        (services, options) => options.ActivateMenu(services).FilterMenuByPolicy(services));

var app = builder.Build();

// Old links under /legacy are redirected to lower case with a trailing slash; other URLs are left alone.
app.UseRewriter(new RewriteOptions().AddRedirectToLowercaseTrailingSlash(
    request => request.Path.StartsWithSegments("/legacy", StringComparison.OrdinalIgnoreCase),
    StatusCodes.Status301MovedPermanently));

// English by default; Russian with "Accept-Language: ru".
app.UseRequestLocalization(options => options.SetDefaultCulture("en").AddSupportedCultures("en", "ru").AddSupportedUICultures("en", "ru"));
app.UseRouting();
app.MapControllerRoute("default", "{controller:slugify=Home}/{action:slugify=Index}/{id?}");
app.MapRazorPages();
app.MapGet("/legacy/{**path}", (string? path) => $"legacy:{path}");

app.Run();

/// <summary>The entry point, visible to <c>WebApplicationFactory</c>.</summary>
public partial class Program;
