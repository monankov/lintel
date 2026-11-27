using Lintel.AspNetCore.TestWebApp.Models;
using Lintel.Collections;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Lintel.AspNetCore.TestWebApp.Pages.Reports;

/// <summary>
/// A Razor Page with pagination. The page number is read from the query: in Razor Pages the "page" route value
/// is the page path, so it cannot be bound as a handler parameter named "page".
/// </summary>
public sealed class IndexModel : PageModel
{
    public PagedList<Product> Products { get; private set; } = PagedList.Empty<Product>();

    public void OnGet()
    {
        var page = int.TryParse(Request.Query["page"], out var number) && number > 0 ? number : 1;
        Products = Catalog.Products.ToPagedList(page, 10);
    }
}
