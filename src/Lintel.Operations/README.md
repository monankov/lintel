# Lintel.Operations

Commands, queries and notices with their handlers. A small in-process mediator for .NET 10.

- An **operation** is a request with one handler: a command (no result) or a query (with a result).
- A **notice** is an event with any number of handlers.
- **Filters** run around operations, for example for logging or validation.

## Install

```
dotnet add package Lintel.Operations
```

## Operations

Describe the operation and write its handler:

```csharp
using Lintel.Operations;

public sealed class GetOrderTotal : IOperation<decimal>
{
    public int OrderId { get; init; }
}

public sealed class GetOrderTotalHandler(ShopDbContext db) : AsyncOperationHandler<GetOrderTotal, decimal>
{
    public override async Task<decimal> HandleAsync(GetOrderTotal operation, CancellationToken cancellationToken)
        => await db.OrderLines.Where(l => l.OrderId == operation.OrderId).SumAsync(l => l.Price, cancellationToken);
}
```

A command has no result: implement `IOperation` and use `AsyncOperationHandler<TOperation>`
(or `OperationHandler<...>` for synchronous code).

Register the handlers of an assembly and execute operations through `IOperationExecutor`:

```csharp
builder.Services.AddOperations(typeof(Program).Assembly);

public sealed class OrdersController(IOperationExecutor executor) : Controller
{
    public async Task<IActionResult> Total(int id, CancellationToken cancellationToken)
        => Json(await executor.ExecuteAsync(new GetOrderTotal { OrderId = id }, cancellationToken));
}
```

## Filters

A filter runs before and after the handler. Call `next()` to continue:

```csharp
public sealed class LoggingFilter<TOperation, TResult>(ILogger<TOperation> logger) : IOperationFilter<TOperation, TResult>
    where TOperation : IOperation<TResult>
{
    public async Task<TResult> FilterAsync(TOperation operation, OperationHandlerDelegate<TResult> next, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing {Operation}", typeof(TOperation).Name);
        return await next();
    }
}

builder.Services.AddOperationFilter(typeof(LoggingFilter<,>));
```

Filters run in the order they are registered.

## Notices

```csharp
using Lintel.Notices;

public sealed class OrderPaid : INotice
{
    public int OrderId { get; init; }
}

public sealed class SendReceipt(IMailer mailer) : NoticeHandler<OrderPaid>
{
    protected override void Handle(OrderPaid notice) => mailer.SendReceipt(notice.OrderId);
}

// Asynchronous handler
public sealed class SaveAuditEntry(AppDbContext db) : AsyncNoticeHandler<OrderPaid>
{
    public override async Task HandleAsync(OrderPaid notice, CancellationToken cancellationToken)
    {
        db.AuditEntries.Add(new AuditEntry($"Order {notice.OrderId} paid"));
        await db.SaveChangesAsync(cancellationToken);
    }
}

builder.Services.AddNotices(typeof(Program).Assembly);

await publisher.PublishAsync(new OrderPaid { OrderId = 42 }, cancellationToken); // INoticePublisher
```

Handlers run one after another by default. To run them at the same time:

```csharp
builder.Services.AddNotices(o => o.PublishStrategy = ParallelNoticePublishStrategy.Instance, typeof(Program).Assembly);
```

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
