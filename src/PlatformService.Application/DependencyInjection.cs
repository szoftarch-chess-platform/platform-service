using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlatformService.Application.Users;

namespace PlatformService.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use case services and every FluentValidation validator in this assembly.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Time-dependent logic takes a TimeProvider so tests can control the clock.
        services.TryAddSingleton(TimeProvider.System);

        // Error messages are always in English, whatever the culture of the server. Localization is turned off
        // instead of setting an English culture, because the container runs in invariant globalization mode,
        // where creating that culture throws.
        ValidatorOptions.Global.LanguageManager.Enabled = false;
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
