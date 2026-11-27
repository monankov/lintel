# Lintel.Stateflows

State machines for entities, on top of [Stateless](https://github.com/dotnet-state-machine/stateless). The state is
stored in the entity (for example in a database column), and the machine is created for one entity when you need it.

## Install

```
dotnet add package Lintel.Stateflows
```

## Describe the machine

Write the states and transitions with the Stateless API. You get the entity, so conditions can use its data:

```csharp
using Lintel.Stateflows;

public enum DocumentState { Draft, Sent, Accepted, Rejected }

public enum DocumentAction { Edit, Send, Accept, Reject }

public sealed class DocumentWorkflow : StateMachineConfigurator<Document, DocumentState, DocumentAction>
{
    protected override void Configure(Stateless.StateMachine<DocumentState, DocumentAction> stateMachine, Document entity)
    {
        stateMachine.Configure(DocumentState.Draft)
            .InternalTransition(DocumentAction.Edit)
            .PermitIf(DocumentAction.Send, DocumentState.Sent, () => !string.IsNullOrEmpty(entity.Content));

        stateMachine.Configure(DocumentState.Sent)
            .Permit(DocumentAction.Accept, DocumentState.Accepted)
            .Permit(DocumentAction.Reject, DocumentState.Rejected);
    }
}
```

Register all configurations of an assembly:

```csharp
builder.Services.AddStateflows(typeof(Program).Assembly);
```

One machine can have several configurations (for example the transitions in one class and notifications in another).
For configuration that needs `await`, implement `IStateMachineConfiguration<TEntity, TState, TTrigger>`:
load the data in `ConfigureAsync` and use it in the conditions, because Stateless conditions are synchronous.

## Use the machine

```csharp
public sealed class DocumentService(IStateMachineProvider machines, ShopDbContext db)
{
    public async Task SendAsync(Document document, CancellationToken cancellationToken)
    {
        var machine = await machines.GetStateMachineAsync<Document, DocumentState, DocumentAction>(
            document, d => d.State, (d, state) => d.State = state, cancellationToken);

        if (!machine.CanFire(DocumentAction.Send))
        {
            throw new InvalidOperationException("The document cannot be sent.");
        }

        await machine.FireAsync(DocumentAction.Send); // changes document.State
        await db.SaveChangesAsync(cancellationToken);
    }
}
```

`machine.PermittedTriggers` lists the actions that are possible now, for example to show only the buttons that work.

To load an entity by its key together with its machine, implement `IStateflowProvider<TEntity, TKey, TState, TTrigger>`;
it returns a `Stateflow` with the entity and the machine. `AddStateflows` registers such providers from the same
assemblies.

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
