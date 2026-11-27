using Lintel.Operations.Tests.TestUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Operations.Tests.Operations;

public class FilterTests
{
    public interface IQuery<TResult> : IOperation<TResult>;

    public class BarQuery : IQuery<int>
    {
        public int Number1 { get; init; }
        public int Number2 { get; init; }
    }

    public class BarQueryHandler(IStringWriter writer) : OperationHandler<BarQuery, int>
    {
        protected override int Handle(BarQuery operation)
        {
            var sum = operation.Number1 + operation.Number2;
            writer.Write($"BarQuery {sum}");
            return sum;
        }
    }

    public class FooCommand : IOperation
    {
        public string? Name { get; init; }
    }

    public class FooCommandHandler(IStringWriter writer) : OperationHandler<FooCommand>
    {
        protected override void Handle(FooCommand operation) => writer.Write(operation.Name);
    }

    public class UniversalFilter<TOperation, TResult>(IStringWriter writer) : IOperationFilter<TOperation, TResult>
        where TOperation : IOperation<TResult>
    {
        public async Task<TResult> FilterAsync(TOperation operation, OperationHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            await writer.WriteAsync("UniversalFilter");
            return await next();
        }
    }

    public class QueryFilter<TQuery, TResult>(IStringWriter writer) : IOperationFilter<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        public async Task<TResult> FilterAsync(TQuery operation, OperationHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            await writer.WriteAsync("QueryFilter");
            return await next();
        }
    }

    public class AfterFilter<TOperation, TResult>(IStringWriter writer) : IOperationFilter<TOperation, TResult>
        where TOperation : IOperation<TResult>
    {
        public async Task<TResult> FilterAsync(TOperation operation, OperationHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            var result = await next();
            await writer.WriteAsync("AfterFilter");
            return result;
        }
    }

    public class BarQueryOnlyFilter(IStringWriter writer) : IOperationFilter<BarQuery, int>
    {
        public async Task<int> FilterAsync(BarQuery operation, OperationHandlerDelegate<int> next, CancellationToken cancellationToken)
        {
            var result = await next();
            writer.Write("BarQueryOnlyFilter");
            return result * 10;
        }
    }

    public class ShortCircuitFilter : IOperationFilter<BarQuery, int>
    {
        public Task<int> FilterAsync(BarQuery operation, OperationHandlerDelegate<int> next, CancellationToken cancellationToken)
            => Task.FromResult(-1);
    }

    private static (IOperationExecutor Executor, MockWriter Writer) CreateExecutor(Action<IServiceCollection> configure)
    {
        var writer = new MockWriter();
        var services = new ServiceCollection().AddMockWriter(writer);
        configure(services);
        var provider = services
            .AddOperations(typeof(FilterTests).Assembly)
            .BuildServiceProvider();
        return (provider.GetRequiredService<IOperationExecutor>(), writer);
    }

    [Fact]
    public async Task Multiple_filters_are_applied_in_registration_order()
    {
        var (executor, writer) = CreateExecutor(services => services
            .AddOperationFilter(typeof(UniversalFilter<,>))
            .AddOperationFilter(typeof(QueryFilter<,>)));

        await executor.ExecuteAsync(new BarQuery { Number1 = 3, Number2 = 4 }, TestContext.Current.CancellationToken);

        writer.Buffer.Should().Equal("UniversalFilter", "QueryFilter", "BarQuery 7");
    }

    [Fact]
    public async Task Filter_can_run_after_handler()
    {
        var (executor, writer) = CreateExecutor(services => services
            .AddOperationFilter(typeof(AfterFilter<,>)));

        await executor.ExecuteAsync(new BarQuery { Number1 = 3, Number2 = 4 }, TestContext.Current.CancellationToken);

        writer.Buffer.Should().Equal("BarQuery 7", "AfterFilter");
    }

    [Fact]
    public async Task Constrained_filter_is_applied_only_to_matching_operations()
    {
        var (executor, writer) = CreateExecutor(services => services
            .AddOperationFilter(typeof(UniversalFilter<,>))
            .AddOperationFilter(typeof(QueryFilter<,>)));

        await executor.ExecuteAsync(new FooCommand { Name = "SomeName" }, TestContext.Current.CancellationToken);

        writer.Buffer.Should().Equal("UniversalFilter", "SomeName");
    }

    [Fact]
    public async Task Closed_filter_is_applied_only_to_its_operation_and_can_change_result()
    {
        var (executor, writer) = CreateExecutor(services => services
            .AddOperationFilter(typeof(BarQueryOnlyFilter)));

        var result = await executor.ExecuteAsync(new BarQuery { Number1 = 3, Number2 = 4 }, TestContext.Current.CancellationToken);
        await executor.ExecuteAsync(new FooCommand { Name = "SomeName" }, TestContext.Current.CancellationToken);

        result.Should().Be(70);
        writer.Buffer.Should().Equal("BarQuery 7", "BarQueryOnlyFilter", "SomeName");
    }

    [Fact]
    public async Task Filter_can_short_circuit_the_handler()
    {
        var (executor, writer) = CreateExecutor(services => services
            .AddOperationFilter(typeof(ShortCircuitFilter)));

        var result = await executor.ExecuteAsync(new BarQuery { Number1 = 3, Number2 = 4 }, TestContext.Current.CancellationToken);

        result.Should().Be(-1);
        writer.Buffer.Should().BeEmpty();
    }

    [Fact]
    public async Task Filters_registered_directly_in_container_are_still_supported()
    {
        var (executor, writer) = CreateExecutor(services => services
            .AddTransient(typeof(IOperationFilter<,>), typeof(UniversalFilter<,>)));

        await executor.ExecuteAsync(new BarQuery { Number1 = 1, Number2 = 1 }, TestContext.Current.CancellationToken);

        writer.Buffer.Should().Equal("UniversalFilter", "BarQuery 2");
    }
}
