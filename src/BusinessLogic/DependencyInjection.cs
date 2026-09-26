using BusinessLogic.Cliente.Implementations;
using BusinessLogic.Cliente.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogic;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        // Registro del servicio del Caso de Uso CU-04 (Cliente)
        services.AddScoped<IClienteService, ClienteService>();

        return services;
    }
}
