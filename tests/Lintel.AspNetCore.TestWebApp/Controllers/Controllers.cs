using Lintel.AspNetCore.Client.Alerts;
using Lintel.AspNetCore.Client.Options;
using Lintel.AspNetCore.Mvc;
using Lintel.AspNetCore.Referrer;
using Lintel.AspNetCore.TestWebApp.Models;
using Lintel.Collections;
using Lintel.Querying;
using Microsoft.AspNetCore.Mvc;

namespace Lintel.AspNetCore.TestWebApp.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        this.AddClientOptions(new { pageSize = 10, theme = "dark" });
        return View();
    }

    [HttpGet]
    public IActionResult Checks() => View(new ChecksForm { Newsletter = true });

    // Shows the posted values to check model binding of the generated inputs.
    [HttpPost]
    public IActionResult Checks(ChecksForm form) => Content($"{form.Agree}|{form.Newsletter}|{form.Kind}");
}

/// <summary>Conventional routing: /products?page=2&amp;pagesize=5&amp;sort=Price&amp;order=Desc.</summary>
public sealed class ProductsController : Controller
{
    public IActionResult Index(int page = 1, int pagesize = 10, string? sort = null, SortOrder order = SortOrder.Asc)
    {
        IEnumerable<Product> products = Catalog.Products;
        if (!string.IsNullOrEmpty(sort))
        {
            products = products.OrderBy(sort, order);
        }
        return View(products.ToPagedList(page, pagesize));
    }

    /// <summary>Returns a rendered partial view inside JSON, as AJAX endpoints do.</summary>
    public async Task<IActionResult> Row(int id)
        => Json(new { html = await this.RenderPartialViewAsync("_ProductRow", Catalog.Products.Single(p => p.Id == id)) });

    [Ajax]
    public IActionResult Count() => Content(Catalog.Products.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>Attribute routing with the page number in the path: /catalog/page/2.</summary>
[Route("catalog")]
[Lintel.AspNetCore.Navigation.Menu("side-catalog")]
public sealed class CatalogController : Controller
{
    [HttpGet("page/{page:int}")]
    [HttpGet("")]
    public IActionResult Index(int page = 1) => View(Catalog.Products.ToPagedList(page, 10));
}

/// <summary>Slugified attribute route: /order-items/recent-list.</summary>
[Route("[controller]/[action]")]
public sealed class OrderItemsController : Controller
{
    public IActionResult RecentList() => Content(Url.Action(nameof(RecentList))!);
}

public sealed class BookingsController : Controller
{
    [HttpGet]
    public IActionResult Create() => View(new BookingForm { From = new DateOnly(2026, 5, 1), To = new DateOnly(2026, 5, 2) });

    [HttpPost]
    [HandleFormException]
    public IActionResult Create(BookingForm form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        // Errors of the business layer: the form is rendered again by [HandleFormException].
        switch (form.Name)
        {
            case "Taken":
                throw new System.ComponentModel.DataAnnotations.ValidationException(
                    new System.ComponentModel.DataAnnotations.ValidationResult("This name is taken.", [nameof(BookingForm.Name)]), null, null);
            case "Crash":
                throw new InvalidOperationException("The database is not available.");
        }

        return RedirectToAction(nameof(Index)).Success($"Booking for {form.Name} saved");
    }

    public IActionResult Index() => View();

    // Renders the form without a model, so that the view reads the posted values from ModelState.
    [HttpPost]
    public IActionResult Check(BookingForm form)
    {
        _ = form;
        return View(nameof(Create));
    }

    [HttpGet]
    public IActionResult Edit(int id) => View(id);

    [HttpPost]
    public IActionResult Edit(int id, string? name) => this.RedirectToReferrer("/bookings");
}
