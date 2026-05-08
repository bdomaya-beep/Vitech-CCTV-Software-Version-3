using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelVMS.Application.Abstractions.Authentication;
using SentinelVMS.Application.Abstractions.Devices;
using SentinelVMS.Application.Abstractions.Events;
using SentinelVMS.Application.Abstractions.Persistence;
using SentinelVMS.Application.Configuration;
using SentinelVMS.Infrastructure.Authentication;
using SentinelVMS.Infrastructure.Data;
using SentinelVMS.Infrastructure.Devices;
using SentinelVMS.Infrastructure.Events;
using SentinelVMS.Infrastructure.Persistence;

namespace SentinelVMS.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, AppDatabaseOptions databaseOptions)
    {
        services.AddDbContext<SentinelDbContext>(options =>
        {
            if (databaseOptions.Provider == DatabaseProvider.PostgreSql)
            {
                options.UseNpgsql(databaseOptions.ConnectionString);
            }
            else
            {
                options.UseSqlite(databaseOptions.ConnectionString);
            }
        });

        services.AddSingleton<ISessionService, SessionService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDeviceDiscoveryService, DeviceDiscoveryService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
