namespace Lintel.Stateflows;

/// <summary>
/// A state machine bound to an entity: its state is read from and written to the entity.
/// </summary>
/// <typeparam name="TState">The type of states.</typeparam>
/// <typeparam name="TTrigger">The type of triggers.</typeparam>
public interface IStateMachine<TState, TTrigger>
{
    /// <summary>The current state.</summary>
    TState State { get; }

    /// <summary>The triggers that can be fired in the current state (guards are evaluated).</summary>
    IEnumerable<TTrigger> PermittedTriggers { get; }

    /// <summary>Whether <paramref name="trigger"/> can be fired in the current state.</summary>
    /// <param name="trigger">The trigger.</param>
    /// <returns><see langword="true"/> if it can be fired.</returns>
    bool CanFire(TTrigger trigger);

    /// <summary>Fires <paramref name="trigger"/>.</summary>
    /// <param name="trigger">The trigger.</param>
    /// <exception cref="InvalidOperationException">If the trigger is not permitted in the current state.</exception>
    void Fire(TTrigger trigger);

    /// <summary>Fires <paramref name="trigger"/>, awaiting asynchronous entry/exit actions.</summary>
    /// <param name="trigger">The trigger.</param>
    /// <returns>A task that completes when the transition is done.</returns>
    /// <exception cref="InvalidOperationException">If the trigger is not permitted in the current state.</exception>
    Task FireAsync(TTrigger trigger);
}

/// <summary>
/// Default <see cref="IStateMachine{TState, TTrigger}"/> over a <see cref="Stateless.StateMachine{TState, TTrigger}"/>.
/// </summary>
/// <typeparam name="TState">The type of states.</typeparam>
/// <typeparam name="TTrigger">The type of triggers.</typeparam>
public class StateMachine<TState, TTrigger> : IStateMachine<TState, TTrigger>
{
    private readonly Stateless.StateMachine<TState, TTrigger> _stateMachine;

    /// <summary>
    /// Initializes the wrapper.
    /// </summary>
    /// <param name="stateMachine">The configured Stateless machine.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="stateMachine"/> is <see langword="null"/>.</exception>
    public StateMachine(Stateless.StateMachine<TState, TTrigger> stateMachine)
    {
        ArgumentNullException.ThrowIfNull(stateMachine);

        _stateMachine = stateMachine;
    }

    /// <inheritdoc />
    public TState State => _stateMachine.State;

    /// <inheritdoc />
    public IEnumerable<TTrigger> PermittedTriggers => _stateMachine.PermittedTriggers;

    /// <inheritdoc />
    public bool CanFire(TTrigger trigger) => _stateMachine.CanFire(trigger);

    /// <inheritdoc />
    public void Fire(TTrigger trigger) => _stateMachine.Fire(trigger);

    /// <inheritdoc />
    public Task FireAsync(TTrigger trigger) => _stateMachine.FireAsync(trigger);
}

/// <summary>
/// An entity together with its state machine.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TState">The type of states.</typeparam>
/// <typeparam name="TTrigger">The type of triggers.</typeparam>
public class Stateflow<TEntity, TState, TTrigger>
{
    /// <summary>
    /// Initializes the stateflow.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="stateMachine">Its state machine.</param>
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public Stateflow(TEntity entity, IStateMachine<TState, TTrigger> stateMachine)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(stateMachine);

        Entity = entity;
        StateMachine = stateMachine;
    }

    /// <summary>The entity.</summary>
    public TEntity Entity { get; }

    /// <summary>The state machine of the entity.</summary>
    public IStateMachine<TState, TTrigger> StateMachine { get; }
}

/// <summary>
/// Loads an entity by its key and creates its <see cref="Stateflow{TEntity, TState, TTrigger}"/>.
/// Implement it in the application (usually with <see cref="IStateMachineProvider"/> and a repository).
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TState">The type of states.</typeparam>
/// <typeparam name="TTrigger">The type of triggers.</typeparam>
public interface IStateflowProvider<TEntity, TKey, TState, TTrigger>
{
    /// <summary>Returns the stateflow of the entity with key <paramref name="entityId"/>.</summary>
    /// <param name="entityId">The key.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The stateflow.</returns>
    Task<Stateflow<TEntity, TState, TTrigger>> GetStateflowAsync(TKey entityId, CancellationToken cancellationToken = default);
}
