namespace Lintel.Stateflows;

/// <summary>
/// Configures the states and transitions of the machines of <typeparamref name="TEntity"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TState">The type of states.</typeparam>
/// <typeparam name="TTrigger">The type of triggers.</typeparam>
public interface IStateMachineConfiguration<TEntity, TState, TTrigger>
{
    /// <summary>
    /// Configures <paramref name="stateMachine"/> for <paramref name="entity"/>.
    /// </summary>
    /// <param name="stateMachine">The machine to configure.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that completes when the machine is configured.</returns>
    Task ConfigureAsync(Stateless.StateMachine<TState, TTrigger> stateMachine, TEntity entity, CancellationToken cancellationToken = default);
}

/// <summary>
/// The same as <see cref="IStateMachineConfiguration{TEntity, TState, TTrigger}"/>; kept for compatibility with Structr.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TState">The type of states.</typeparam>
/// <typeparam name="TTrigger">The type of triggers.</typeparam>
public interface IStateMachineConfigurator<TEntity, TState, TTrigger> : IStateMachineConfiguration<TEntity, TState, TTrigger>;

/// <summary>
/// Base class for synchronous state machine configurations.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TState">The type of states.</typeparam>
/// <typeparam name="TTrigger">The type of triggers.</typeparam>
public abstract class StateMachineConfiguration<TEntity, TState, TTrigger> : IStateMachineConfiguration<TEntity, TState, TTrigger>
{
    Task IStateMachineConfiguration<TEntity, TState, TTrigger>.ConfigureAsync(Stateless.StateMachine<TState, TTrigger> stateMachine,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        Configure(stateMachine, entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Configures <paramref name="stateMachine"/> for <paramref name="entity"/>.
    /// </summary>
    /// <param name="stateMachine">The machine to configure.</param>
    /// <param name="entity">The entity.</param>
    protected abstract void Configure(Stateless.StateMachine<TState, TTrigger> stateMachine, TEntity entity);
}

/// <summary>
/// Base class for synchronous state machine configurators; the same as
/// <see cref="StateMachineConfiguration{TEntity, TState, TTrigger}"/>, kept for compatibility with Structr.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TState">The type of states.</typeparam>
/// <typeparam name="TTrigger">The type of triggers.</typeparam>
public abstract class StateMachineConfigurator<TEntity, TState, TTrigger> : IStateMachineConfigurator<TEntity, TState, TTrigger>
{
    Task IStateMachineConfiguration<TEntity, TState, TTrigger>.ConfigureAsync(Stateless.StateMachine<TState, TTrigger> stateMachine,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        Configure(stateMachine, entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Configures <paramref name="stateMachine"/> for <paramref name="entity"/>.
    /// </summary>
    /// <param name="stateMachine">The machine to configure.</param>
    /// <param name="entity">The entity.</param>
    protected abstract void Configure(Stateless.StateMachine<TState, TTrigger> stateMachine, TEntity entity);
}

/// <summary>
/// Shortcuts for Stateless configurations.
/// </summary>
public static class StateMachineExtensions
{
    /// <summary>Allows <paramref name="trigger"/> without leaving the state and without an action.</summary>
    /// <typeparam name="TState">The type of states.</typeparam>
    /// <typeparam name="TTrigger">The type of triggers.</typeparam>
    /// <param name="configuration">The state configuration.</param>
    /// <param name="trigger">The trigger.</param>
    /// <returns>The same configuration.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="configuration"/> is <see langword="null"/>.</exception>
    public static Stateless.StateMachine<TState, TTrigger>.StateConfiguration InternalTransition<TState, TTrigger>(
        this Stateless.StateMachine<TState, TTrigger>.StateConfiguration configuration,
        TTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.InternalTransition(trigger, static () => { });
    }

    /// <summary>Allows <paramref name="trigger"/> without leaving the state when <paramref name="guard"/> is true.</summary>
    /// <typeparam name="TState">The type of states.</typeparam>
    /// <typeparam name="TTrigger">The type of triggers.</typeparam>
    /// <param name="configuration">The state configuration.</param>
    /// <param name="trigger">The trigger.</param>
    /// <param name="guard">The condition.</param>
    /// <param name="guardDescription">The description of the condition (for diagrams and errors).</param>
    /// <returns>The same configuration.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="configuration"/> or <paramref name="guard"/> is <see langword="null"/>.</exception>
    public static Stateless.StateMachine<TState, TTrigger>.StateConfiguration InternalTransitionIf<TState, TTrigger>(
        this Stateless.StateMachine<TState, TTrigger>.StateConfiguration configuration,
        TTrigger trigger,
        Func<bool> guard,
        string? guardDescription = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(guard);

        return configuration.InternalTransitionIf(trigger, _ => guard(), static () => { }, guardDescription);
    }
}
