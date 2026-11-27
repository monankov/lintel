# Lintel.Domain

Base classes for domain models: entities, value objects, and contracts for auditing and soft delete.
It has no dependencies. `Lintel.EntityFrameworkCore` maps these types to the database.

## Install

```
dotnet add package Lintel.Domain
```

## Entities

An entity is defined by its identity. Two entities are equal when they have the same type and the same key:

```csharp
using Lintel.Domain;

public sealed class Order : Entity<Order, int>
{
    public string Number { get; private set; } = "";

    private Order()
    {
    }

    public Order(string number) => Number = number;
}
```

An entity without a key yet (not saved) is *transient*; transient entities are equal only to themselves.
For an entity with a key made of several values use `CompositeEntity<TEntity>`.

## Value objects

A value object is defined by its values. Two value objects are equal when all their public properties are equal:

```csharp
public sealed class Address : ValueObject<Address>
{
    public string City { get; init; } = "";
    public string Street { get; init; } = "";
}

new Address { City = "Kazan" } == new Address { City = "Kazan" }; // true
```

## Auditing

Mark entities with interfaces, and `Lintel.EntityFrameworkCore` fills the values when it saves changes:

| Interface | Properties |
|---|---|
| `ICreatable` / `ISignedCreatable` | `DateCreated` / `CreatedBy` |
| `IModifiable` / `ISignedModifiable` | `DateModified` / `ModifiedBy` |
| `ISoftDeletable` / `ISignedSoftDeletable` | `DateDeleted` / `DeletedBy`: the row is kept and marked as deleted |
| `IUndeletable` | the row is never deleted: a delete keeps it (`ISoftDeletable` is an `IUndeletable` that also stores the date) |

The base classes `AuditableEntity<TEntity, TKey>` and `SignedAuditableEntity<TEntity, TKey>` already have the
creation and modification properties:

```csharp
public sealed class Customer : SignedAuditableEntity<Customer, int>, ISoftDeletable
{
    public string Name { get; set; } = "";
    public DateTime? DateDeleted { get; private set; }
}
```

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
