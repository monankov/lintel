# Lintel.EntityFrameworkCore

EF Core 10 support for the types of `Lintel.Domain`: model conventions, automatic auditing, soft delete,
paged queries and numbered migrations.

## Install

```
dotnet add package Lintel.EntityFrameworkCore
```

## Model conventions

Call the conventions at the end of `OnModelCreating`:

```csharp
using Lintel.EntityFrameworkCore;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // your own configuration first

    modelBuilder.ApplyEntityConfiguration();          // keys of Entity<TEntity, TKey>
    modelBuilder.ApplyValueObjectsAsComplexTypes();   // ValueObject<T> properties stored in the columns of the entity
    modelBuilder.ApplyAuditableConfiguration();       // audit columns and the soft-delete filter
}
```

`ApplyValueObjectsAsComplexTypes` stores a value object such as `Address` in the table of its entity
(`Address_City`, `Address_Street`); nested value objects work too. Properties that you already mapped yourself
(for example with `OwnsOne`) are not changed, and collections of value objects are not mapped.
If you map value objects as owned types, `ApplyValueObjectConfiguration(o => o.Configure = ...)` applies your
settings to all of them.

## Auditing

Add the interceptor. It fills `DateCreated`, `DateModified`, `DateDeleted` and the `...By` properties when you save:

```csharp
builder.Services.AddDbContext<ShopDbContext>((services, options) => options
    .UseNpgsql(connectionString)
    .AddInterceptors(new AuditSaveChangesInterceptor(
        TimeProvider.System,
        () => services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User.Identity?.Name)));
```

The constructor with `TimeProvider` writes UTC time. Use it for PostgreSQL: Npgsql refuses local time in
`timestamp with time zone` columns. The constructor without `TimeProvider` writes `DateTime.Now`, as Structr did.

## Soft delete

When you delete an `ISoftDeletable` entity, the row is kept and `DateDeleted` is set. Queries hide such rows with a
named query filter:

```csharp
var orders = await db.Orders.ToListAsync();                         // without deleted orders
var all = await db.Orders.IncludeSoftDeleted().ToListAsync();       // with deleted orders, other filters still work
var everything = await db.Orders.IgnoreQueryFilters().ToListAsync(); // without any filter
```

Owned entities of a soft-deleted entity are kept too.

## Paged queries

```csharp
PagedList<Order> page = await db.Orders.OrderBy(o => o.Number).ToPagedListAsync(pageNumber: 3, pageSize: 20, cancellationToken);
```

It runs a `COUNT` query and a query with `OFFSET` / `LIMIT`.

## Numbered migrations

```csharp
options.UseNpgsql(connectionString).UseSequentialMigrationIds();
```

`dotnet ef migrations add` then creates `0001_InitialCreate`, `0002_AddPrice`, ... instead of names with a timestamp.
Use it from the first migration: a project that already has timestamp migrations gets an error, because a new
`0001_...` would run before them.

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
