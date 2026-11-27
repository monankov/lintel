# Lintel.Navigation

Menus and breadcrumbs from a JSON or XML file: a tree of items with localized titles, filtering (for example by user
roles) and the active item of the current page.

This package does not depend on ASP.NET Core. `Lintel.AspNetCore` adds ready-made menu and breadcrumb items for web
applications (`MenuItem`, `RouteBreadcrumb`) and a `<breadcrumbs>` tag helper.

## Install

```
dotnet add package Lintel.Navigation
```

## Define an item type and a file

```csharp
using Lintel.Navigation;

public sealed class SiteMenuItem : NavigationItem<SiteMenuItem>
{
    public string? Url { get; set; }
}
```

```json
[
  {
    "Id": "catalog",
    "Title": "Catalog",
    "Children": [
      { "Id": "books", "Title": "Books", "Url": "/books" },
      { "Id": "music", "Title": "Music", "Url": "/music" }
    ]
  }
]
```

## Register and use

```csharp
builder.Services.AddNavigation()
    .AddJson<SiteMenuItem>(Path.Combine(builder.Environment.ContentRootPath, "menu.json"), options =>
    {
        options.ResourceType = typeof(MenuTitles);   // optional: titles from a .resx, by Id (or ResourceName)
        options.ItemActivator = item => item.Id == "books";
        options.ItemFilter = item => item.Id != "music";
    });
```

The second overload `AddJson<T>(path, (services, options) => ...)` gives you the services of the current request, for
example to find the active item from the request or to check user roles.

```csharp
public sealed class Shell(INavigation<SiteMenuItem> menu, IBreadcrumbNavigation<SiteMenuItem> breadcrumbs)
{
    // menu: the whole filtered tree; menu.Active is the active item
    // breadcrumbs: the path from the top item to the active item
}
```

Every item has `Children`, `Parent`, `Ancestors`, `Descendants`, `IsActive`, `HasActiveChild` and `HasActiveDescendant`
for rendering.

- The file is read once and kept for the application lifetime (`EnableCaching = false` reads it every time).
- Titles are localized in the culture of each request.
- A hidden item hides its children.
- `AddXml<T>` reads the same tree from XML, and `AddProvider<T>` takes your own `INavigationProvider<T>` (for example a
  menu from a database).

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
