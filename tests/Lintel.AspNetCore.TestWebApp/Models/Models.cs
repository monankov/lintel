using System.ComponentModel.DataAnnotations;
using Lintel.DataAnnotations;

namespace Lintel.AspNetCore.TestWebApp.Models;

public sealed record Product(int Id, string Name, decimal Price);

public static class Catalog
{
    /// <summary>25 products: "Product 01" (price 25) … "Product 25" (price 1).</summary>
    public static IReadOnlyList<Product> Products { get; } =
        Enumerable.Range(1, 25).Select(i => new Product(i, $"Product {i:00}", 26 - i)).ToList();
}

public sealed class ChecksForm
{
    [Required]
    [Display(Name = "I agree")]
    public bool Agree { get; set; }

    [Display(Name = "Send news")]
    public bool Newsletter { get; set; }

    public string Kind { get; set; } = "person";
}

public sealed class BookingForm
{
    [Required]
    [Display(Name = "Guest name")]
    public string? Name { get; set; }

    [Display(Name = "Arrival date")]
    public DateOnly From { get; set; }

    [Display(Name = "Departure date")]
    [GreaterThan(nameof(From))]
    public DateOnly To { get; set; }

    [Display(Name = "Has pets")]
    public bool HasPets { get; set; }

    [Display(Name = "Pet name")]
    [RequiredIfTrue(nameof(HasPets))]
    public string? PetName { get; set; }
}
