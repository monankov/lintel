using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Lintel.EntityFrameworkCore;

/// <summary>
/// Access to application services from <see cref="DbContextOptions"/>.
/// </summary>
public static class DbContextOptionsExtensions
{
    /// <summary>
    /// Resolves a service from the application service provider the options were built with
    /// (<c>AddDbContext</c> sets it). Useful in a <see cref="DbContext"/> constructor.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="options">The context options.</param>
    /// <returns>The service, or <see langword="null"/> if it is not registered or no application service provider is set.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is <see langword="null"/>.</exception>
    public static T? GetService<T>(this DbContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var serviceProvider = options.FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider;
        return (T?)serviceProvider?.GetService(typeof(T));
    }
}
