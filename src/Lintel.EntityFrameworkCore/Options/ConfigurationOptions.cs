using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lintel.EntityFrameworkCore.Options;

/// <summary>
/// Options of <see cref="ModelBuilderExtensions.ApplyEntityConfiguration(Microsoft.EntityFrameworkCore.ModelBuilder, Action{EntityConfigurationOptions}?)"/>.
/// </summary>
public class EntityConfigurationOptions
{
    /// <summary>
    /// Additional configuration applied to every entity type derived from <c>Entity&lt;TEntity&gt;</c>.
    /// </summary>
    public Action<IMutableEntityType, EntityTypeBuilder>? Configure { get; set; }
}

/// <summary>
/// Options of <see cref="ModelBuilderExtensions.ApplyValueObjectConfiguration(Microsoft.EntityFrameworkCore.ModelBuilder, Action{ValueObjectConfigurationOptions}?)"/>.
/// </summary>
public class ValueObjectConfigurationOptions
{
    /// <summary>
    /// Configuration applied to every owned value object: the owned entity type, the navigation name and its builder.
    /// </summary>
    public Action<IMutableEntityType, string, OwnedNavigationBuilder>? Configure { get; set; }
}

/// <summary>
/// Options of <see cref="ModelBuilderExtensions.ApplyValueObjectsAsComplexTypes(Microsoft.EntityFrameworkCore.ModelBuilder, Action{ComplexValueObjectConfigurationOptions}?)"/>.
/// </summary>
public class ComplexValueObjectConfigurationOptions
{
    /// <summary>
    /// Configuration applied to every value object mapped as a complex type:
    /// the declaring entity type, the property name and the complex property builder.
    /// Called for nested value objects too, with the root entity type.
    /// </summary>
    public Action<IMutableEntityType, string, ComplexPropertyBuilder>? Configure { get; set; }
}

/// <summary>
/// Options of <see cref="ModelBuilderExtensions.ApplyAuditableConfiguration(Microsoft.EntityFrameworkCore.ModelBuilder, Action{AuditableConfigurationOptions}?)"/>.
/// </summary>
public class AuditableConfigurationOptions
{
    /// <summary>The maximum length of <c>CreatedBy</c>, <c>ModifiedBy</c> and <c>DeletedBy</c>. Defaults to 50.</summary>
    public int SignedColumnMaxLength { get; set; } = 50;

    /// <summary>Whether <c>CreatedBy</c> and <c>ModifiedBy</c> are required. Defaults to <see langword="false"/>.</summary>
    public bool SignedColumnIsRequired { get; set; }

    /// <summary>
    /// Whether to add the <see cref="SoftDeleteQueryFilter"/> to soft-deletable entity types. Defaults to <see langword="true"/>.
    /// </summary>
    public bool AddSoftDeleteQueryFilter { get; set; } = true;
}
