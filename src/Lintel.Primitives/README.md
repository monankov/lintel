# Lintel.Primitives

Small helpers that many applications need: sequential GUIDs, trees, hierarchy paths, enum display names, string
conversion and compact numbers.

## Install

```
dotnet add package Lintel.Primitives
```

## Sequential GUIDs

Random GUIDs as primary keys make database indexes slow, because new rows go to random places. Sequential GUIDs grow
over time, like numbers:

```csharp
using Lintel.Primitives;

builder.Services.AddSequentialGuidProvider();

public sealed class OrderService(ISequentialGuidProvider guids)
{
    public Order Create() => new(guids.GetSequentialGuid(SequentialGuidType.Ending));
}
```

Choose the type for your database:

| Database | Type |
|---|---|
| SQL Server (`uniqueidentifier`) | `Ending` |
| PostgreSQL (`uuid`), MySQL as `char(36)`, SQLite as text | `String` (default) |
| Oracle `raw(16)`, MySQL as `binary(16)` | `Binary` |

For PostgreSQL and other databases that sort GUIDs from the start, the built-in `Guid.CreateVersion7()` is also a good
choice for new keys.

## Trees and hierarchy paths

`TreeNode<T>` is a base class for items with a parent and children (`AddChild`, `Parent`, `Children`, `Ancestors`,
`Descendants`).

`HierarchyId` is a path in a tree such as `/1/5/12/`. It is easy to store in one column and to compare:

```csharp
var node = HierarchyId.Parse("/1/5/12/");
node.GetLevel();            // 3
node.GetAncestor(1);        // /1/5/
node.IsDescendantOf(5);     // true
node.GetDescendant(40);     // /1/5/12/40/
```

## Other helpers

```csharp
OrderState.Paid.GetDisplayName();   // the [Display(Name = ...)] of the enum value, or its name
OrderState.Paid.GetDescription();   // the [Description] of the enum value

"42".Cast<int>();                   // 42; returns default for an invalid value unless throwIfInvalidCast is true
"2026-05-01".Cast<DateOnly>(provider: CultureInfo.InvariantCulture);

1_234.ToCompactString();            // "1.2K" (also 12K, 1.2M, 5.5B)

SequentialFileName.NewFileNameWithExtension(".pdf"); // a unique name that sorts by creation time
```

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
