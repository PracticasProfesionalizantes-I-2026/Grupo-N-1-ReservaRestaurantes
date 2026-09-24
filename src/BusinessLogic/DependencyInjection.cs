using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogic;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        // Aquí se registrarán los servicios a medida que se implementen los Casos de Uso
        // ej: services.AddScoped<IReservaService, ReservaService>();
        return services;
    }
}
