namespace Lintel.EntityFrameworkCore;

/// <summary>
/// Supplies the moment written to <c>DateCreated</c>, <c>DateModified</c> and <c>DateDeleted</c>.
/// </summary>
/// <returns>The audit timestamp.</returns>
public delegate DateTime AuditTimestampProvider();

/// <summary>
/// Supplies the user name written to <c>CreatedBy</c>, <c>ModifiedBy</c> and <c>DeletedBy</c>.
/// </summary>
/// <returns>The user name, or <see langword="null"/> if unknown.</returns>
public delegate string? AuditSignProvider();
