using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Stateflows;

/// <summary>
/// Creates state machines bound to entities and configured by the registered configurations.
/// </summary>
public interface IStateMachineProvider
{
    /// <summary>
    /// Creates a state machine for <paramref name="entity"/>.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TState">The type of states.</typeparam>
    /// <typeparam name="TTrigger">The type of triggers.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <param name="stateAccessor">Reads the state from the entity.</param>
    /// <param name="stateMutator">Writes the state to the entity.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The configured machine.</returns>
    Task<IStateMachine<TState, TTrigger>> GetStateMachineAsync<TEntity, TState, TTrigger>(
        TEntity entity,
        Func<TEntity, TState> stateAccessor,
        Action<TEntity, TState> stateMutator,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IStateMachineProvider"/>: applies every registered
/// <see cref="IStateMachineConfiguration{TEntity, TState, TTrigger}"/> (configurators included) in registration order.
/// </summary>
public class StateMachineProvider : IStateMachineProvider
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve configurations from.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="serviceProvider"/> is <see langword="null"/>.</exception>
    public StateMachineProvider(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">If any argument is <see langword="null"/>.</exception>
    public async Task<IStateMachine<TState, TTrigger>> GetStateMachineAsync<TEntity, TState, TTrigger>(
        TEntity entity,
        Func<TEntity, TState> stateAccessor,
        Action<TEntity, TState> stateMutator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(stateAccessor);
        ArgumentNullException.ThrowIfNull(stateMutator);

        var stateMachine = new Stateless.StateMachine<TState, TTrigger>(() => stateAccessor(entity), state => stateMutator(entity, state));

        IEnumerable<IStateMachineConfiguration<TEntity, TState, TTrigger>> configurations;
        try
        {
            configurations = [.. _serviceProvider.GetServices<IStateMachineConfiguration<TEntity, TState, TTrigger>>()];
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"Error constructing state machine configurations of type {typeof(IStateMachineConfiguration<TEntity, TState, TTrigger>)}", ex);
        }

        foreach (var configuration in configurations)
        {
            await configuration.ConfigureAsync(stateMachine, entity, cancellationToken).ConfigureAwait(false);
        }

        return new StateMachine<TState, TTrigger>(stateMachine);
    }
}
