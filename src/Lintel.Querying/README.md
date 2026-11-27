# Lintel.Querying

Building blocks for queries: specifications, paged lists and sorting by property name.
It has no dependencies and works with LINQ to Objects and with `IQueryable` (for example EF Core).

## Install

```
dotnet add package Lintel.Querying
```

## Specifications

A specification is a named, reusable condition. Combine specifications with `&`, `|` and `!`:

```csharp
using Lintel.Specifications;

public sealed class ActiveCustomers : Specification<Customer>
{
    public override Expression<Func<Customer, bool>> ToExpression() => c => !c.IsBlocked;
}

public sealed class LivesIn(string city) : Specification<Customer>
{
    public override Expression<Func<Customer, bool>> ToExpression() => c => c.Address.City == city;
}

var specification = new ActiveCustomers() & !new LivesIn("Moscow");

var customers = await db.Customers.Where(specification).ToListAsync();   // translated to SQL
bool ok = specification.IsSatisfiedBy(customer);                         // checked in memory
```

`AnySpecification<T>` and `NoneSpecification<T>` are useful starting values when you build a condition step by step.

## Paged lists

```csharp
using Lintel.Collections;

PagedList<Customer> page = customers.OrderBy(c => c.Name).ToPagedList(pageNumber: 2, pageSize: 20);

page.TotalItems;      // all items
page.TotalPages;
page.HasNextPage;
page.FirstItemOnPage; // 21
```

`PagedList<T>` implements `IPagedList<out T>`. Use the interface in view models and APIs: it is covariant, so an
`IPagedList<OrderVm>` can be passed where an `IPagedList<object>` is expected, and the paging data is kept.

`ToPagedList` works on `IEnumerable<T>` and `IQueryable<T>`. For EF Core use `ToPagedListAsync` from
`Lintel.EntityFrameworkCore`. `Lintel.AspNetCore` has tag helpers that show the pages.

## Sorting by property name

Sort by a name that comes from the user, for example from the query string. Names are not case-sensitive, and nested
properties use a dot:

```csharp
using Lintel.Querying;

var sorted = db.Customers
    .OrderBy("address.city")
    .ThenBy("Name", SortOrder.Desc);

var bySeveral = db.Customers.OrderBy(new Dictionary<string, SortOrder>
{
    ["Rating"] = SortOrder.Desc,
    ["Name"] = SortOrder.Asc,
});
```

An unknown property name throws `InvalidOperationException`, so check names that come from users against a list
you allow.

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
