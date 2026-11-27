using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Stateflows.Tests;

public enum DocumentState
{
    Draft,
    Sent,
    Accepted,
    Rejected,
}

public enum DocumentAction
{
    Edit,
    Send,
    Accept,
    Reject,
    Archive,
}

public sealed class Document
{
    public int Id { get; init; }
    public string? Content { get; set; }
    public DocumentState State { get; set; }
    public List<string> Log { get; } = [];
}

/// <summary>Draft → Sent (only with content) → Accepted / Rejected.</summary>
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

/// <summary>A second configuration of the same machine: adds an async entry action. Ignored by Structr.</summary>
public sealed class DocumentNotifications : IStateMachineConfiguration<Document, DocumentState, DocumentAction>
{
    public Task ConfigureAsync(Stateless.StateMachine<DocumentState, DocumentAction> stateMachine, Document entity, CancellationToken cancellationToken = default)
    {
        stateMachine.Configure(DocumentState.Accepted)
            .OnEntryAsync(async () =>
            {
                await Task.Yield();
                entity.Log.Add("accepted");
            });
        return Task.CompletedTask;
    }
}

/// <summary>A configuration through the other base class, which Structr never registered.</summary>
public sealed class DocumentArchiving : StateMachineConfiguration<Document, DocumentState, DocumentAction>
{
    protected override void Configure(Stateless.StateMachine<DocumentState, DocumentAction> stateMachine, Document entity)
        => stateMachine.Configure(DocumentState.Rejected).InternalTransitionIf(DocumentAction.Archive, () => entity.Log.Count == 0, "nothing logged");
}

public sealed class DocumentStateflowProvider(IStateMachineProvider stateMachines) : IStateflowProvider<Document, int, DocumentState, DocumentAction>
{
    public async Task<Stateflow<Document, DocumentState, DocumentAction>> GetStateflowAsync(int entityId, CancellationToken cancellationToken = default)
    {
        var document = new Document { Id = entityId, Content = "text" };
        var machine = await stateMachines.GetStateMachineAsync<Document, DocumentState, DocumentAction>(
            document, d => d.State, (d, s) => d.State = s, cancellationToken);
        return new Stateflow<Document, DocumentState, DocumentAction>(document, machine);
    }
}

public sealed class CustomStateMachineProvider(IServiceProvider services) : StateMachineProvider(services);

public sealed class Invoice
{
    public DocumentState State { get; set; }
}

public interface IUnregisteredService;

/// <summary>Depends on a service that is not registered, so it cannot be constructed.</summary>
public sealed class InvoiceWorkflow(IUnregisteredService service) : StateMachineConfigurator<Invoice, DocumentState, DocumentAction>
{
    protected override void Configure(Stateless.StateMachine<DocumentState, DocumentAction> stateMachine, Invoice entity)
        => _ = service;
}

public class StateflowsTests
{
    private static IServiceProvider Services(Action<StateflowServiceOptions>? configure = null)
        => new ServiceCollection()
            .AddStateflows(configure, typeof(StateflowsTests).Assembly)
            .AddStateflows(typeof(StateflowsTests).Assembly)
            .BuildServiceProvider();

    private static Task<IStateMachine<DocumentState, DocumentAction>> Machine(Document document)
        => Services().GetRequiredService<IStateMachineProvider>()
            .GetStateMachineAsync<Document, DocumentState, DocumentAction>(document, d => d.State, (d, s) => d.State = s, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Machine_reads_and_writes_entity_state()
    {
        var document = new Document { Content = "text" };
        var machine = await Machine(document);

        machine.Fire(DocumentAction.Send);

        machine.State.Should().Be(DocumentState.Sent);
        document.State.Should().Be(DocumentState.Sent);
        machine.PermittedTriggers.Should().BeEquivalentTo([DocumentAction.Accept, DocumentAction.Reject]);
    }

    [Fact]
    public async Task Configuration_that_cannot_be_constructed_gives_clear_error()
    {
        var provider = Services().GetRequiredService<IStateMachineProvider>();

        var act = () => provider.GetStateMachineAsync<Invoice, DocumentState, DocumentAction>(
            new Invoice(), i => i.State, (i, s) => i.State = s, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Error constructing state machine configurations of type *IStateMachineConfiguration*Invoice*")
            .WithInnerException(typeof(InvalidOperationException));
    }

    [Fact]
    public async Task Guards_are_evaluated()
    {
        var document = new Document();
        var machine = await Machine(document);

        machine.CanFire(DocumentAction.Send).Should().BeFalse();
        machine.CanFire(DocumentAction.Edit).Should().BeTrue();
        var act = () => machine.Fire(DocumentAction.Send);
        act.Should().Throw<InvalidOperationException>();

        document.Content = "now with content";
        machine.CanFire(DocumentAction.Send).Should().BeTrue();
    }

    [Fact]
    public async Task Internal_transition_keeps_state()
    {
        var document = new Document();
        var machine = await Machine(document);

        machine.Fire(DocumentAction.Edit);

        machine.State.Should().Be(DocumentState.Draft);
    }

    [Fact]
    public async Task All_configurations_of_an_entity_are_applied_including_async_actions()
    {
        var document = new Document { Content = "text", State = DocumentState.Sent };
        var machine = await Machine(document);

        await machine.FireAsync(DocumentAction.Accept);

        document.State.Should().Be(DocumentState.Accepted);
        document.Log.Should().Equal("accepted");
    }

    [Fact]
    public async Task StateMachineConfiguration_base_class_is_registered_and_guarded_internal_transition_works()
    {
        var document = new Document { State = DocumentState.Rejected };
        var machine = await Machine(document);

        machine.CanFire(DocumentAction.Archive).Should().BeTrue();
        document.Log.Add("something");
        machine.CanFire(DocumentAction.Archive).Should().BeFalse();
    }

    [Fact]
    public async Task Stateflow_provider_is_registered()
    {
        var stateflow = await Services().GetRequiredService<IStateflowProvider<Document, int, DocumentState, DocumentAction>>()
            .GetStateflowAsync(42, TestContext.Current.CancellationToken);

        stateflow.Entity.Id.Should().Be(42);
        stateflow.StateMachine.CanFire(DocumentAction.Send).Should().BeTrue();
    }

    [Fact]
    public void Configurations_are_not_duplicated_by_repeated_registration()
        => Services().GetServices<IStateMachineConfiguration<Document, DocumentState, DocumentAction>>()
            .Select(c => c.GetType()).Should().BeEquivalentTo([typeof(DocumentWorkflow), typeof(DocumentNotifications), typeof(DocumentArchiving)]);

    [Fact]
    public void Options_select_provider_type_and_lifetime()
    {
        var services = new ServiceCollection().AddStateflows(o =>
        {
            o.ProviderServiceType = typeof(CustomStateMachineProvider);
            o.ProviderServiceLifetime = ServiceLifetime.Singleton;
        });

        services.Should().ContainSingle(d => d.ServiceType == typeof(IStateMachineProvider))
            .Which.Should().Match<ServiceDescriptor>(d => d.ImplementationType == typeof(CustomStateMachineProvider) && d.Lifetime == ServiceLifetime.Singleton);

        var invalid = () => new StateflowServiceOptions { ProviderServiceType = typeof(string) };
        invalid.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Arguments_are_validated()
    {
        var provider = Services().GetRequiredService<IStateMachineProvider>();

        var withoutEntity = () => provider.GetStateMachineAsync<Document, DocumentState, DocumentAction>(null!, d => d.State, (d, s) => d.State = s);
        var withoutAccessor = () => provider.GetStateMachineAsync<Document, DocumentState, DocumentAction>(new Document(), null!, (d, s) => d.State = s);
        var withoutStateflowArguments = () => new Stateflow<Document, DocumentState, DocumentAction>(null!, null!);
        var withoutGuard = () => new Stateless.StateMachine<DocumentState, DocumentAction>(DocumentState.Draft)
            .Configure(DocumentState.Draft).InternalTransitionIf(DocumentAction.Edit, null!);

        await withoutEntity.Should().ThrowAsync<ArgumentNullException>();
        await withoutAccessor.Should().ThrowAsync<ArgumentNullException>();
        withoutStateflowArguments.Should().Throw<ArgumentNullException>();
        withoutGuard.Should().Throw<ArgumentNullException>();
    }
}
