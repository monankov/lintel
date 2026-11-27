# Lintel.AspNetCore

Helpers for ASP.NET Core MVC and Razor Pages applications: tag helpers for lists and forms, messages for the user,
breadcrumbs and menus, form errors, date binding, localized validation messages, maintenance mode and security.
The default markup is Bootstrap 5; classes can be changed.

## Install

```
dotnet add package Lintel.AspNetCore
```

```csharp
builder.Services.AddControllersWithViews(o => o.UseDateModelBinding("dd.MM.yyyy"))
    .AddLocalizedValidationMessages();
builder.Services.AddAspNetCore();            // client alerts and options, form exception handling
builder.Services.AddAspNetCoreValidation();  // client-side validation of Lintel.DataAnnotations
```

In `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, Lintel.AspNetCore
```

## Lists

```cshtml
@model PagedList<Product>

<th asp-sort="Name">Name</th>                  @* a link that sorts by Name, then toggles the order *@

<pagination asp-options="new PaginationOptions { PagedEnumerable = Model }" />
<page-info asp-options="new PageInfoOptions { PagedEnumerable = Model }" />
<page-size asp-options="new PageSizeOptions { ItemsPerPage = [10, 25, 50, 0], DefaultPageSize = Model.PageSize }" />
```

The links keep the other query values. A new page size starts again from the first page; `0` means "all items".

## Forms

These tag helpers work on their own once they are imported:

- `<label asp-for>` gets an asterisk for `[Required]` and for `[RequiredIf...]` when its condition is met
  (`asp-required="false"` turns it off).
- `<input>`, `<textarea>` and `<select>` with `asp-for` get `form-control`, `form-select` or `form-check-input`,
  and `is-invalid` when the field has an error.

```cshtml
<validation-summary />                                  @* form errors in an alert; nothing when there are none *@
<checkbox asp-for="Agree" />                            @* input and label in a form-check *@
<radio asp-for="Kind" value="company" asp-label="Company" />
<button type="submit" asp-enabled="Model.CanSave">Save</button>   @* disabled when false *@
```

### Show the form again after an error

```csharp
[HttpPost, HandleFormException]
public IActionResult Edit(OrderForm form) { ... }
```

When the action throws, the form is shown again with the entered values and an error, instead of an error page.
Register your own `IFormExceptionHandler` to turn your exceptions into messages.

### Dates and messages

- `UseDateModelBinding("dd.MM.yyyy")` reads `DateTime` and `DateOnly` in the given formats and in ISO
  (`<input type="date">`). Show the dates in the same format: `<input asp-for="From" asp-format="{0:dd.MM.yyyy}" />`.
- `AddLocalizedValidationMessages()` translates model binding messages and the default messages of standard
  attributes (`[Required]`, `[StringLength]`, `[Range]`, ...) to the language of the request. English and Russian are
  included; add other languages with `ValidationMessageCatalog.Default.Set(...)`. Enable `app.UseRequestLocalization(...)`.

## Messages for the user

```csharp
return RedirectToAction("Index").Success("The order is saved.");   // also Info, Warning, Error
this.AddClientOptions(new { pageSize = 20 });                       // data for the page script
```

```cshtml
<client-alerts />                         @* Bootstrap alerts; format="Json" for your own script *@
<client-options id="client-options" />    @* <script type="application/json"> *@
<tab-badge value="Model.TotalOrders" />   @* a counter badge: 1.2K *@
```

## Breadcrumbs and menus

```csharp
builder.Services.AddNavigation()
    .AddJson<RouteBreadcrumb>(breadcrumbsPath, (s, o) => o.ActivateByRoute(s))
    .AddJson<MenuItem>(menuPath, (s, o) => o.ActivateMenu(s).FilterMenuByPolicy(s));
```

```cshtml
@inject IBreadcrumbNavigation<RouteBreadcrumb> Breadcrumbs
<breadcrumbs asp-items="Breadcrumbs" />
```

- A breadcrumb is active when its area, controller and action (or Razor page) match the request. Names can be
  patterns such as `(.+)Orders`.
- A menu item is active by `[Menu("id")]` on the controller or action, or else by the longest matching `Url`.
- `Policy` hides items from users who do not pass that authorization policy, and empty groups are hidden.

## Security

```csharp
builder.Services.AddForwardedHeaders(builder.Configuration.GetSection("ForwardedHeaders")); // trusted proxies
builder.Services.AddFailedAttemptLimiter(builder.Configuration.GetSection("FailedAttempts"));

var intranet = IpNetworkList.Parse(builder.Configuration.GetSection("AllowedIp").Get<string[]>()!);
builder.Services.AddAuthorization(o => o.AddPolicy("Intranet", p => p.RequireAllowedNetwork(intranet)));

app.UseForwardedHeaders();
app.UseExceptionHandler("/error");
app.UseClientDisconnectHandling();   // requests closed by the client end quietly with 499
app.UseSecurityHeaders();            // Content-Security-Policy, nosniff, Referrer-Policy
app.UseStaticFiles();
app.UseMaintenanceMode();            // 503 with a page while "MaintenanceMode:Enabled" is true
```

- `FailedAttemptLimiter` counts wrong passwords per key, for example `HttpContext.ClientKey()`, and blocks the key
  for a time. Do not check the password while the key is blocked.
- `HttpContext.GetClientRateLimitKey()` is a partition key for the built-in rate limiter. It takes the client address
  after the proxy; IPv6 addresses are grouped by `/64` network.

Register `AddMaintenanceMode(builder.Configuration.GetSection("MaintenanceMode"))` for the maintenance mode.

## Other helpers

| Helper | What it does |
|---|---|
| `options.AddSlugifyRouting()` | URLs like `/order-items/edit-draft` for `OrderItemsController.EditDraft` |
| `AddRedirectToLowercaseTrailingSlash()` | rewrite rule that redirects old links |
| `<a asp-referrer="/orders">` and `this.RedirectToReferrer("/orders")` | "Back" link and return after a post |
| `[Ajax]`, `Request.IsAjaxRequest()` | actions only for AJAX requests |
| `this.RenderViewAsync(...)`, `this.RenderPartialViewAsync(...)` | a view as a string, for example for e-mail |
| `<a asp-match-class="active">`, `asp-append-if` | CSS classes by condition |

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
