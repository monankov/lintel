using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lintel.EntityFrameworkCore.Migrations;

/// <summary>
/// Names new migrations with a running number instead of a timestamp: <c>0001_InitialCreate</c>,
/// <c>0002_AddOrders</c> instead of <c>20260920170115_InitialCreate</c>. Enable it with
/// <see cref="SequentialMigrationsDbContextOptionsBuilderExtensions.UseSequentialMigrationIds"/>.
/// </summary>
/// <remarks>
/// <para>
/// The next number is one more than the largest number among the migrations of the context in the migrations assembly
/// (<c>MigrationsAssembly(...)</c>, or the assembly of the context); migrations of other contexts in the same assembly
/// are not counted. Numbers have four digits, so that migrations sort in order as strings; EF Core applies them in that order.
/// </para>
/// <para>
/// A project that already has migrations with timestamp ids cannot switch: a new <c>0001_…</c> would sort before them and
/// be applied first. Generating an id then fails with an explanation.
/// </para>
/// </remarks>
/// <param name="currentContext">The context the migrations are generated for.</param>
/// <param name="options">The options of the context.</param>
public sealed partial class SequentialMigrationsIdGenerator(ICurrentDbContext currentContext, IDbContextOptions options) : IMigrationsIdGenerator
{
    /// <summary>The number of digits of a migration number.</summary>
    public const int Digits = 4;

    private readonly ICurrentDbContext _currentContext = currentContext ?? throw new ArgumentNullException(nameof(currentContext));
    private readonly IDbContextOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">If existing migrations do not use running numbers, or the numbers run out.</exception>
    public string GenerateId(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // The type of the context itself: a context configured in OnConfiguring has options of DbContext.
        var contextType = _currentContext.Context.GetType();
        var ids = ExistingIds(GetMigrationsAssembly(contextType), contextType).ToList();
        var foreign = ids.Find(id => !NumberedId().IsMatch(id));
        if (foreign is not null)
        {
            throw new InvalidOperationException(
                $"The migration '{foreign}' does not start with a {Digits}-digit number. Sequential migration ids can only be used "
                + "from the first migration: a numbered migration would sort before it and be applied first.");
        }

        var next = ids.Select(id => int.Parse(id[..Digits], CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max() + 1;
        if (next >= Math.Pow(10, Digits))
        {
            throw new InvalidOperationException($"Migration numbers ran out of {Digits} digits.");
        }

        return next.ToString("D" + Digits, CultureInfo.InvariantCulture) + "_" + name;
    }

    /// <inheritdoc />
    public string GetName(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        var separator = id.IndexOf('_', StringComparison.Ordinal);
        return separator < 0 ? id : id[(separator + 1)..];
    }

    /// <inheritdoc />
    public bool IsValidId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return AnyId().IsMatch(value);
    }

    private Assembly GetMigrationsAssembly(Type contextType)
    {
        var relational = _options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault();
        return relational?.MigrationsAssemblyObject
            ?? (relational?.MigrationsAssembly is { Length: > 0 } name ? Assembly.Load(new AssemblyName(name)) : contextType.Assembly);
    }

    private static IEnumerable<string> ExistingIds(Assembly assembly, Type contextType)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = [.. exception.Types.OfType<Type>()];
        }

        return types
            .Where(type => type.IsSubclassOf(typeof(Migration))
                && type.GetCustomAttribute<DbContextAttribute>()?.ContextType == contextType)
            .Select(type => type.GetCustomAttribute<MigrationAttribute>()?.Id)
            .OfType<string>();
    }

    [GeneratedRegex(@"^\d{4}_.+$")]
    private static partial Regex NumberedId();

    // Timestamps of EF Core are ids too: commands must recognize them when the project still has such migrations.
    [GeneratedRegex(@"^\d+_.+$")]
    private static partial Regex AnyId();
}

/// <summary>
/// The options extension that replaces the migration id generator of EF Core.
/// </summary>
/// <remarks>
/// The generator is registered as scoped (EF Core registers its own as a singleton): it needs the current context, and
/// the services that use it (<c>MigrationsAssembly</c>, <c>Migrator</c>, <c>MigrationsScaffolder</c>) are scoped too.
/// </remarks>
public sealed class SequentialMigrationsOptionsExtension : IDbContextOptionsExtension
{
    /// <inheritdoc />
    public DbContextOptionsExtensionInfo Info => field ??= new ExtensionInfo(this);

    /// <inheritdoc />
    public void ApplyServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Scoped<IMigrationsIdGenerator, SequentialMigrationsIdGenerator>());
    }

    /// <inheritdoc />
    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(SequentialMigrationsOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => "SequentialMigrationIds ";

        public override int GetServiceProviderHashCode() => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) => debugInfo["Lintel:SequentialMigrationIds"] = "1";
    }
}

/// <summary>
/// Enabling <see cref="SequentialMigrationsIdGenerator"/>.
/// </summary>
public static class SequentialMigrationsDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Names new migrations <c>0001_Name</c>, <c>0002_Name</c>, ... instead of timestamps. The setting matters at design time
    /// (<c>dotnet ef migrations add</c>), so add it to the options used by the tools, for example in <c>AddDbContext</c>.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static DbContextOptionsBuilder UseSequentialMigrationIds(this DbContextOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new SequentialMigrationsOptionsExtension());
        return builder;
    }
}
