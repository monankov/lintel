using Lintel.AspNetCore.Client.Alerts;
using Lintel.AspNetCore.Client.Options;
using Lintel.AspNetCore.Mvc;
using Lintel.AspNetCore.Validation;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration of Lintel.AspNetCore services.
/// </summary>
public static class AspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="AspNetCore.Http.IHttpContextAccessor"/>, client alerts, client options and form exception handling.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAspNetCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddClientAlerts();
        services.AddClientOptions();
        services.AddFormExceptionHandling();
        return services;
    }

    /// <summary>
    /// Adds the default <see cref="IFormExceptionHandler"/> used by <see cref="HandleFormExceptionAttribute"/>.
    /// An <see cref="IFormExceptionHandler"/> registered before is kept.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Changes the options, for example the general error message.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddFormExceptionHandling(this IServiceCollection services, Action<FormExceptionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<FormExceptionOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }
        services.TryAddScoped<IFormExceptionHandler, FormExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Adds <see cref="IClientAlertProvider"/> (stored in TempData) and the options of <c>&lt;client-alerts&gt;</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Changes how alerts are rendered as HTML.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddClientAlerts(this IServiceCollection services, Action<ClientAlertOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<ClientAlertOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }
        services.AddHttpContextAccessor();
        services.TryAddTransient<IClientAlertProvider, ClientAlertProvider>();
        return services;
    }

    /// <summary>
    /// Adds <see cref="IClientOptionProvider"/> (stored in <c>HttpContext.Items</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddClientOptions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddTransient<IClientOptionProvider, ClientOptionProvider>();
        return services;
    }

    /// <summary>
    /// Enables unobtrusive client-side validation for the attributes of <c>Lintel.DataAnnotations</c>
    /// (<c>RequiredIf</c>, <c>GreaterThan</c>, ...). Standard attributes keep their adapters.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAspNetCoreValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IValidationAttributeAdapterProvider>();
        services.AddSingleton<IValidationAttributeAdapterProvider, ModelAwareValidationAttributeAdapterProvider>();
        return services;
    }
}
