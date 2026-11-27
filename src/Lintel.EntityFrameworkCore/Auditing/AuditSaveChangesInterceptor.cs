using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lintel.EntityFrameworkCore;

/// <summary>
/// Calls <see cref="DbContextExtensions.Audit(DbContext, AuditTimestampProvider?, AuditSignProvider?)"/>
/// before every <c>SaveChanges</c> / <c>SaveChangesAsync</c>.
/// </summary>
/// <example>
/// <code>
/// services.AddDbContext&lt;AppDbContext&gt;((sp, options) =&gt; options
///     .UseSqlServer(connectionString)
///     .AddInterceptors(new AuditSaveChangesInterceptor(
///         TimeProvider.System,
///         () =&gt; sp.GetRequiredService&lt;IHttpContextAccessor&gt;().HttpContext?.User.Identity?.Name)));
/// </code>
/// </example>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly AuditTimestampProvider? _timestampProvider;
    private readonly AuditSignProvider? _signProvider;

    /// <summary>
    /// Initializes an interceptor with custom providers.
    /// </summary>
    /// <param name="timestampProvider">Supplies the timestamp; <see cref="DateTime.Now"/> (local time) by default, as in Structr.</param>
    /// <param name="signProvider">Supplies the user name; <see langword="null"/> by default.</param>
    public AuditSaveChangesInterceptor(AuditTimestampProvider? timestampProvider = null, AuditSignProvider? signProvider = null)
    {
        _timestampProvider = timestampProvider;
        _signProvider = signProvider;
    }

    /// <summary>
    /// Initializes an interceptor that writes UTC timestamps from <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="signProvider">Supplies the user name; <see langword="null"/> by default.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public AuditSaveChangesInterceptor(TimeProvider timeProvider, AuditSignProvider? signProvider = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timestampProvider = () => timeProvider.GetUtcNow().UtcDateTime;
        _signProvider = signProvider;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        eventData.Context?.Audit(_timestampProvider, _signProvider);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        eventData.Context?.Audit(_timestampProvider, _signProvider);
        return ValueTask.FromResult(result);
    }
}
