using System.Linq.Expressions;
using System.Reflection;
using Lintel.Domain;
using Lintel.EntityFrameworkCore.Utilities;
using Lintel.EntityFrameworkCore.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lintel.EntityFrameworkCore;

/// <summary>
/// Conventions for <see cref="Lintel.Domain"/> types. Call them in <c>OnModelCreating</c> after the entity configurations.
/// </summary>
public static class ModelBuilderExtensions
{
    private static readonly MethodInfo _efProperty = typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(DateTime?));

    /// <summary>
    /// Configures <c>Id</c> as the key of every root entity type derived from <c>Entity&lt;TEntity, TKey&gt;</c>.
    /// </summary>
    /// <param name="builder">The model builder.</param>
    /// <param name="configureOptions">Additional configuration of every <c>Entity&lt;TEntity&gt;</c> type.</param>
    /// <returns>The same model builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ModelBuilder ApplyEntityConfiguration(this ModelBuilder builder, Action<EntityConfigurationOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new EntityConfigurationOptions();
        configureOptions?.Invoke(options);

        // A key can only be defined on the root of an inheritance hierarchy.
        foreach (var entityType in builder.GetEntityTypes(typeof(Entity<,>), type => type.BaseType is null && !type.IsOwned()))
        {
            builder.Entity(entityType.ClrType).HasKey(nameof(Entity<,>.Id));
        }

        if (options.Configure is not null)
        {
            foreach (var entityType in builder.GetEntityTypes(typeof(Entity<>)))
            {
                builder.Entity(entityType.ClrType, entityTypeBuilder => options.Configure(entityType, entityTypeBuilder));
            }
        }

        return builder;
    }

    /// <summary>
    /// Applies <see cref="ValueObjectConfigurationOptions.Configure"/> to every value object mapped as an owned type
    /// (<c>OwnsOne</c>).
    /// </summary>
    /// <remarks>For new models prefer <see cref="ApplyValueObjectsAsComplexTypes"/>.</remarks>
    /// <param name="builder">The model builder.</param>
    /// <param name="configureOptions">The configuration.</param>
    /// <returns>The same model builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ModelBuilder ApplyValueObjectConfiguration(this ModelBuilder builder, Action<ValueObjectConfigurationOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new ValueObjectConfigurationOptions();
        configureOptions?.Invoke(options);

        if (options.Configure is null)
        {
            return builder;
        }

        foreach (var entityType in builder.GetEntityTypes(typeof(ValueObject<>), type => type.IsOwned()))
        {
            var navigation = entityType.FindOwnership()!.PrincipalToDependent!;
            var principalType = navigation.DeclaringEntityType;

            builder.Entity(principalType.ClrType)
                .OwnsOne(entityType.ClrType, navigation.Name, owned => options.Configure(entityType, navigation.Name, owned));
        }

        return builder;
    }

    /// <summary>
    /// Maps every property of a value object type (derived from <c>ValueObject&lt;TValueObject&gt;</c>) on entity types
    /// as an EF Core complex type: the value is stored in the columns of the entity, without a key, and changes of its
    /// properties mark the entity as modified (so auditing updates <c>DateModified</c>). Nested value objects are mapped too.
    /// </summary>
    /// <remarks>
    /// Properties already mapped as owned navigations, complex properties or ignored are left as they are.
    /// Collections of value objects are not mapped.
    /// </remarks>
    /// <param name="builder">The model builder.</param>
    /// <param name="configureOptions">Additional configuration of every complex property.</param>
    /// <returns>The same model builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ModelBuilder ApplyValueObjectsAsComplexTypes(this ModelBuilder builder, Action<ComplexValueObjectConfigurationOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new ComplexValueObjectConfigurationOptions();
        configureOptions?.Invoke(options);

        var valueObjectType = typeof(ValueObject<>);

        // Value objects used by entities were discovered by convention as entity types, with navigations to the
        // value objects they contain. Turn those inner navigations into complex properties first, while the outer
        // types are still in the model: EF Core leaves dangling foreign keys if the outer type is removed first.
        foreach (var conventionType in builder.Model.GetEntityTypes()
            .Where(type => !type.IsOwned() && valueObjectType.IsAssignableFromGenericType(type.ClrType))
            .ToList())
        {
            foreach (var property in ValueObjectProperties(conventionType.ClrType))
            {
                if (property.PropertyType != conventionType.ClrType && conventionType.FindNavigation(property.Name) is not null)
                {
                    builder.Entity(conventionType.ClrType).ComplexProperty(property.PropertyType, property.Name);
                }
            }
        }

        var entityTypes = builder.Model.GetEntityTypes()
            .Where(type => !type.IsOwned() && !valueObjectType.IsAssignableFromGenericType(type.ClrType))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            foreach (var property in ValueObjectProperties(entityType.ClrType))
            {
                if (property.DeclaringType is { } declaringType
                    && entityType.BaseType is { } baseType
                    && declaringType.IsAssignableFrom(baseType.ClrType))
                {
                    continue; // mapped on the base entity type
                }
                if (entityType.IsIgnored(property.Name)
                    || entityType.FindNavigation(property.Name)?.TargetEntityType.IsOwned() == true
                    || entityType.FindComplexProperty(property.Name) is not null)
                {
                    continue;
                }

                // An explicitly configured complex property replaces the entity type discovered by convention.
                builder.Entity(entityType.ClrType).ComplexProperty(property.PropertyType, property.Name,
                    complex => ConfigureComplex(entityType, property, complex, options, [property.PropertyType]));
            }
        }

        RemoveUnreferencedValueObjectEntityTypes(builder.Model);

        return builder;
    }

    /// <summary>
    /// Configures audit properties of <see cref="IAuditable"/> entity types: required dates, the length of user names
    /// and the <see cref="SoftDeleteQueryFilter"/> for <see cref="ISoftDeletable"/> entities.
    /// </summary>
    /// <param name="builder">The model builder.</param>
    /// <param name="configureOptions">The configuration.</param>
    /// <returns>The same model builder.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ModelBuilder ApplyAuditableConfiguration(this ModelBuilder builder, Action<AuditableConfigurationOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new AuditableConfigurationOptions();
        configureOptions?.Invoke(options);

        var entityTypes = builder.GetEntityTypes(typeof(IAuditable), type =>
            !typeof(ValueObject<>).IsAssignableFromGenericType(type.ClrType)
            // Properties of an inheritance hierarchy are configured where the audit interface first appears.
            && (type.BaseType is null || !typeof(IAuditable).IsAssignableFrom(type.BaseType.ClrType)));

        foreach (var entityType in entityTypes)
        {
            var clrType = entityType.ClrType;
            var entityTypeBuilder = builder.Entity(clrType);

            if (typeof(ICreatable).IsAssignableFrom(clrType))
            {
                entityTypeBuilder.Property(AuditableProperties.DateCreated).IsRequired();
                if (typeof(ISignedCreatable).IsAssignableFrom(clrType))
                {
                    entityTypeBuilder.Property(AuditableProperties.CreatedBy)
                        .IsRequired(options.SignedColumnIsRequired)
                        .HasMaxLength(options.SignedColumnMaxLength);
                }
            }

            if (typeof(IModifiable).IsAssignableFrom(clrType))
            {
                entityTypeBuilder.Property(AuditableProperties.DateModified).IsRequired();
                if (typeof(ISignedModifiable).IsAssignableFrom(clrType))
                {
                    entityTypeBuilder.Property(AuditableProperties.ModifiedBy)
                        .IsRequired(options.SignedColumnIsRequired)
                        .HasMaxLength(options.SignedColumnMaxLength);
                }
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                entityTypeBuilder.Property(AuditableProperties.DateDeleted).IsRequired(false);
                if (typeof(ISignedSoftDeletable).IsAssignableFrom(clrType))
                {
                    entityTypeBuilder.Property(AuditableProperties.DeletedBy)
                        .IsRequired(false)
                        .HasMaxLength(options.SignedColumnMaxLength);
                }

                // Query filters can only be defined on the root of an inheritance hierarchy.
                if (options.AddSoftDeleteQueryFilter && entityType.BaseType is null && !entityType.IsOwned())
                {
                    entityTypeBuilder.HasQueryFilter(SoftDeleteQueryFilter.Name, SoftDeleteFilter(clrType));
                }
            }
        }

        return builder;
    }

    /// <summary>
    /// Returns the entity types of the model assignable to <paramref name="type"/>, which may be an open generic type
    /// such as <c>typeof(Entity&lt;&gt;)</c>.
    /// </summary>
    /// <param name="builder">The model builder.</param>
    /// <param name="type">The base type or interface.</param>
    /// <param name="filter">An additional filter.</param>
    /// <returns>The matching entity types.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="builder"/> or <paramref name="type"/> is <see langword="null"/>.</exception>
    public static List<IMutableEntityType> GetEntityTypes(this ModelBuilder builder, Type type, Func<IMutableEntityType, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(type);

        var types = builder.Model.GetEntityTypes()
            .Where(entityType => type.IsGenericTypeDefinition
                ? type.IsAssignableFromGenericType(entityType.ClrType)
                : type.IsAssignableFrom(entityType.ClrType));

        if (filter is not null)
        {
            types = types.Where(filter);
        }

        return types.ToList();
    }

    // Value object types used by entities were discovered by convention as entity types. After their usages
    // became complex properties nothing references them; nested ones are referenced only by such leftovers.
    // Remove unreferenced ones until none is left, so that leftovers referencing each other go too.
    private static void RemoveUnreferencedValueObjectEntityTypes(IMutableModel model)
    {
        var valueObjectType = typeof(ValueObject<>);

        bool removed;
        do
        {
            var unreferenced = model.GetEntityTypes()
                .FirstOrDefault(type => !type.IsOwned()
                    && type.BaseType is null
                    && !type.GetDirectlyDerivedTypes().Any()
                    && valueObjectType.IsAssignableFromGenericType(type.ClrType)
                    && !type.GetReferencingForeignKeys().Any());

            removed = unreferenced is not null;
            if (removed)
            {
                model.RemoveEntityType(unreferenced!);
            }
        }
        while (removed);
    }

    private static LambdaExpression SoftDeleteFilter(Type clrType)
    {
        // entity => EF.Property<DateTime?>(entity, "DateDeleted") == null
        var parameter = Expression.Parameter(clrType, "entity");
        var dateDeleted = Expression.Call(_efProperty, parameter, Expression.Constant(AuditableProperties.DateDeleted));
        return Expression.Lambda(Expression.Equal(dateDeleted, Expression.Constant(null, typeof(DateTime?))), parameter);
    }

    private static void ConfigureComplex(IMutableEntityType entityType,
        PropertyInfo property,
        ComplexPropertyBuilder builder,
        ComplexValueObjectConfigurationOptions options,
        HashSet<Type> path)
    {
        foreach (var nested in ValueObjectProperties(property.PropertyType))
        {
            if (!path.Add(nested.PropertyType))
            {
                continue; // a value object containing itself cannot be flattened
            }

            builder.ComplexProperty(nested.PropertyType, nested.Name,
                complex => ConfigureComplex(entityType, nested, complex, options, path));
            path.Remove(nested.PropertyType);
        }

        options.Configure?.Invoke(entityType, property.Name, builder);
    }

    private static IEnumerable<PropertyInfo> ValueObjectProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0
                && typeof(ValueObject<>).IsAssignableFromGenericType(property.PropertyType));
}
