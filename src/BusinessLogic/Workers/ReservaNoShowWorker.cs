using DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Enums;

namespace BusinessLogic.Workers;

public class ReservaNoShowWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservaNoShowWorker> _logger;

    public ReservaNoShowWorker(IServiceProvider serviceProvider, ILogger<ReservaNoShowWorker> logger)
    {
        //Un BackgroundService es "Singleton" (vive siempre) y el DbContext es "Scoped" (vive por petición).
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando tarea automática de detección de No-Show...");

        // El bucle se ejecutará indefinidamente mientras la app esté viva
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();

                // Calculamos cuál es la fecha/hora límite (Hace 15 minutos atrás)
                var limiteTolerancia = DateTime.UtcNow.AddMinutes(-15);

                // Buscamos reservas "Confirmadas" cuya fecha/hora programada sea anterior al límite
                var reservasExpiradas = await context.Reservas
                    .Include(r => r.Mesa)
                    .Where(r => r.Estado == ReservaEstado.Confirmada && r.FechaHora <= limiteTolerancia)
                    .ToListAsync(stoppingToken);

                if (reservasExpiradas.Any())
                {
                    foreach (var reserva in reservasExpiradas)
                    {
                        // Regla: Se cancela automáticamente y se marca como Ausente
                        reserva.Estado = ReservaEstado.NoShow;
                        
                        // Regla: Se libera la mesa
                        if (reserva.Mesa != null)
                        {
                            reserva.Mesa.Estado = MesaEstado.Libre;
                        }

                        _logger.LogInformation("Reserva {Id} declarada No-Show automáticamente. Mesa liberada.", reserva.Id);
                    }

                    // Guardamos todos los cambios de una vez
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error en el Worker de No-Show.");
            }

            // Le decimos al Worker que espere 1 minuto antes de volver a revisar todo
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
