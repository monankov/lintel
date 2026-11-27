using Lintel.Operations.Tests.TestUtils;
using Microsoft.Extensions.DependencyInjection;

namespace Lintel.Operations.Tests.Operations;

public class OperationExecutorTests
{
    public interface IQuery<TResult> : IOperation<TResult>;

    public interface ICommand : IOperation;

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

    public class BarAsyncQuery : IQuery<int>
    {
        public int Number1 { get; init; }
        public int Number2 { get; init; }
    }

    public class BarAsyncQueryHandler(IStringWriter writer) : AsyncOperationHandler<BarAsyncQuery, int>
    {
        public override async Task<int> HandleAsync(BarAsyncQuery operation, CancellationToken cancellationToken)
        {
            var multiplication = operation.Number1 * operation.Number2;
            await writer.WriteAsync($"BarAsyncQuery {multiplication}");
            return multiplication;
        }
    }

    public class FooCommand : ICommand
    {
        public string? Name { get; init; }
    }

    public class FooCommandHandler(IStringWriter writer) : OperationHandler<FooCommand>
    {
        protected override void Handle(FooCommand operation) => writer.Write(operation.Name);
    }

    public class FooAsyncCommand : ICommand
    {
        public string? Name { get; init; }
    }

    public class FooAsyncCommandHandler(IStringWriter writer) : AsyncOperationHandler<FooAsyncCommand>
    {
        protected override Task HandleAsync(FooAsyncCommand operation, CancellationToken cancellationToken)
            => writer.WriteAsync(operation.Name);
    }

    public class TokenQuery : IQuery<CancellationToken>;

    public class TokenQueryHandler : AsyncOperationHandler<TokenQuery, CancellationToken>
    {
        public override Task<CancellationToken> HandleAsync(TokenQuery operation, CancellationToken cancellationToken)
            => Task.FromResult(cancellationToken);
    }

    public class BazCommand : ICommand;

    private static (IOperationExecutor Executor, MockWriter Writer) CreateExecutor()
    {
        var writer = new MockWriter();
        var provider = new ServiceCollection()
            .AddMockWriter(writer)
            .AddOperations(typeof(OperationExecutorTests).Assembly)
            .BuildServiceProvider();
        return (provider.GetRequiredService<IOperationExecutor>(), writer);
    }

    [Fact]
    public async Task Command()
    {
        var (executor, writer) = CreateExecutor();

        var result = await executor.ExecuteAsync(new FooCommand { Name = "FooCommandTest" }, TestContext.Current.CancellationToken);

        result.Should().Be(VoidResult.Value);
        writer.Buffer.Should().Equal("FooCommandTest");
    }

    [Fact]
    public async Task AsyncCommand()
    {
        var (executor, writer) = CreateExecutor();

        var result = await executor.ExecuteAsync(new FooAsyncCommand { Name = "FooAsyncCommandTest" }, TestContext.Current.CancellationToken);

        result.Should().Be(VoidResult.Value);
        writer.Buffer.Should().Equal("FooAsyncCommandTest");
    }

    [Fact]
    public async Task Query()
    {
        var (executor, writer) = CreateExecutor();

        var result = await executor.ExecuteAsync(new BarQuery { Number1 = 3, Number2 = 4 }, TestContext.Current.CancellationToken);

        result.Should().Be(7);
        writer.Buffer.Should().Equal("BarQuery 7");
    }

    [Fact]
    public async Task AsyncQuery()
    {
        var (executor, writer) = CreateExecutor();

        var result = await executor.ExecuteAsync(new BarAsyncQuery { Number1 = 3, Number2 = 4 }, TestContext.Current.CancellationToken);

        result.Should().Be(12);
        writer.Buffer.Should().Equal("BarAsyncQuery 12");
    }

    [Fact]
    public async Task Cancellation_token_is_passed_to_handler()
    {
        var (executor, _) = CreateExecutor();
        using var cts = new CancellationTokenSource();

        var result = await executor.ExecuteAsync(new TokenQuery(), cts.Token);

        result.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Null_operation_throws()
    {
        var (executor, _) = CreateExecutor();

        var act = () => executor.ExecuteAsync<int>(null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Throws_when_no_handler_found()
    {
        var (executor, _) = CreateExecutor();

        var act = () => executor.ExecuteAsync(new BazCommand(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Operation handler for operation '{typeof(BazCommand).FullName}' was not found");
    }
}
