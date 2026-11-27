# Lintel.DataAnnotations

Validation attributes that depend on another property of the same model: "required if", "greater than", "in" and more.
They work with any code that uses `System.ComponentModel.DataAnnotations` (`Validator`, ASP.NET Core model validation).
The package has no dependencies.

## Install

```
dotnet add package Lintel.DataAnnotations
```

## Required if

```csharp
using Lintel.DataAnnotations;

public sealed class BookingForm
{
    public bool HasPets { get; set; }

    [RequiredIfTrue(nameof(HasPets))]
    public string? PetName { get; set; }

    public string? Kind { get; set; }

    [RequiredIf(nameof(Kind), "company")]
    public string? CompanyName { get; set; }
}
```

| Attribute | The property is required when the related property... |
|---|---|
| `RequiredIf(name, value)` / `RequiredIfNot(name, value)` | equals / does not equal `value` |
| `RequiredIf(name, Operator.GreaterThan, value)` | compares to `value` with any operator |
| `RequiredIfTrue(name)` / `RequiredIfFalse(name)` | is `true` / `false` |
| `RequiredIfEmpty(name)` / `RequiredIfNotEmpty(name)` | is empty / is not empty |
| `RequiredIfRegExMatch(name, pattern)` / `RequiredIfNotRegExMatch(name, pattern)` | matches / does not match the pattern |

`RegularExpressionIf(pattern, name, value)` checks the format of the property only when the condition is met.

## Compare with another property

```csharp
public DateOnly From { get; set; }

[GreaterThan(nameof(From))]
public DateOnly To { get; set; }
```

`EqualTo`, `NotEqualTo`, `GreaterThan`, `GreaterThanOrEqualTo`, `LessThan`, `LessThanOrEqualTo`, `In` and `NotIn`
(the related property is a collection) are available, and `Is(Operator, name)` takes any operator. Set
`PassOnNull = true` to skip the check when only one of the two values is empty.

## Messages

Messages use the display names of both properties: "Departure date must be greater than Arrival date." Set
`ErrorMessage` (or `ErrorMessageResourceType` and `ErrorMessageResourceName`) to use your own text.

## Client-side validation

In ASP.NET Core MVC, `Lintel.AspNetCore` adds the `data-val-*` attributes for these attributes:

```csharp
builder.Services.AddAspNetCoreValidation();
```

A script for jQuery Validation Unobtrusive is up to the application.

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
