using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lintel.Operations.Tests.TestUtils;

public interface IStringWriter
{
    void Write(string? message);

    Task WriteAsync(string? message);
}

public sealed class MockWriter : IStringWriter
{
    public List<string?> Buffer { get; } = [];

    public void Write(string? message) => Buffer.Add(message);

    public async Task WriteAsync(string? message)
    {
        await Task.Yield();
        Buffer.Add(message);
    }
}

internal static class TestServiceCollectionExtensions
{
    public static IServiceCollection AddMockWriter(this IServiceCollection services, IStringWriter writer)
    {
        services.TryAddSingleton(writer);
        return services;
    }
}
