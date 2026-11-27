using Lintel.Operations.Tests.TestUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Operations.Tests.Operations;

public class OperationsServiceCollectionExtensionsTests
{
    public class BarQuery : IOperation<int>;

    public class BarQueryHandler : OperationHandler<BarQuery, int>
    {
        protected override int Handle(BarQuery operation) => 0;
    }

    public class FooCommand : IOperation;

    public class FooCommandHandler : OperationHandler<FooCommand>
    {
        protected override void Handle(FooCommand operation) { }
    }

    public abstract class AbstractHandler : OperationHandler<FooCommand>;

    public class FakeExecutor : IOperationExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(IOperation<TResult> operation, CancellationToken cancellationToken = default)
            => Task.FromResult(default(TResult)!);
    }

    public class NotAFilter;

    [Fact]
    public void AddOperations_registers_executor_and_handlers()
    {
        var provider = new ServiceCollection()
            .AddMockWriter(new MockWriter())
            .AddOperations(GetType().Assembly)
            .BuildServiceProvider();

        provider.GetService<IOperationHandler<FooCommand>>().Should().BeOfType<FooCommandHandler>();
        provider.GetService<IOperationHandler<FooCommand, VoidResult>>().Should().BeOfType<FooCommandHandler>();
        provider.GetService<IOperationHandler<BarQuery, int>>().Should().BeOfType<BarQueryHandler>();
        provider.GetService<IOperationExecutor>().Should().BeOfType<OperationExecutor>();
    }

    [Fact]
    public void AddOperations_without_assemblies_registers_only_executor()
    {
        var services = new ServiceCollection().AddOperations();

        services.Should().ContainSingle()
            .Which.ServiceType.Should().Be<IOperationExecutor>();
    }

    [Fact]
    public void AddOperations_called_twice_does_not_duplicate_registrations()
    {
        var services = new ServiceCollection()
            .AddOperations(GetType().Assembly)
            .AddOperations(GetType().Assembly);

        services.Count(d => d.ServiceType == typeof(IOperationHandler<FooCommand>)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IOperationExecutor)).Should().Be(1);
    }

    [Fact]
    public void AddOperations_skips_abstract_handlers()
    {
        var services = new ServiceCollection().AddOperations(GetType().Assembly);

        services.Should().NotContain(d => d.ImplementationType == typeof(AbstractHandler));
    }

    [Fact]
    public void AddOperations_applies_options()
    {
        var services = new ServiceCollection().AddOperations(options =>
        {
            options.ExecutorServiceType = typeof(FakeExecutor);
            options.ExecutorServiceLifetime = ServiceLifetime.Singleton;
            options.HandlerServiceLifetime = ServiceLifetime.Scoped;
        }, GetType().Assembly);

        services.Single(d => d.ServiceType == typeof(IOperationExecutor))
            .Should().Match<ServiceDescriptor>(d => d.ImplementationType == typeof(FakeExecutor) && d.Lifetime == ServiceLifetime.Singleton);
        services.Single(d => d.ServiceType == typeof(IOperationHandler<FooCommand>))
            .Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(IOperationExecutor))]
    public void ExecutorServiceType_rejects_invalid_types(Type type)
    {
        var options = new OperationServiceOptions();

        var act = () => options.ExecutorServiceType = type;

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddOperationFilter_rejects_types_that_are_not_filters()
    {
        var act = () => new ServiceCollection().AddOperationFilter(typeof(NotAFilter));

        act.Should().Throw<ArgumentException>().WithParameterName("filterType");
    }

    [Fact]
    public void AddOperationFilter_does_not_register_same_filter_twice()
    {
        var services = new ServiceCollection()
            .AddOperationFilter(typeof(FilterTests.UniversalFilter<,>))
            .AddOperationFilter(typeof(FilterTests.UniversalFilter<,>));

        services.Should().ContainSingle(d => d.ServiceType == typeof(IOperationFilter<,>));
    }
}
