using BusinessLogic.Auth.Implementations;
using BusinessLogic.Auth.Interfaces;
using BusinessLogic.Cliente.Implementations;
using BusinessLogic.Cliente.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using BusinessLogic.Reservas.Implementations;
using BusinessLogic.Reservas.Interfaces;

namespace BusinessLogic;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        // Registro del servicio del Caso de Uso CU-04 (Cliente)
        services.AddScoped<IClienteService, ClienteService>();

        // Registro de servicios de Autenticación / JWT (CU-05 y CU-08)
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();

        // Registro del servicio del Caso de Uso CU-01 (Reserva)
        services.AddScoped<IReservaService, ReservaService>();

        return services;

        
    }
}
