using BusinessLogic.Notifications.Interfaces;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Notifications.Implementations;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task NotificarAsignacionMesaAsync(Guid clienteId, string clienteEmail, string clienteNombre, int mesaNumero, DateTime fechaHora)
    {
        _logger.LogInformation(
            "Notificación enviada a cliente {ClienteNombre} ({ClienteEmail}, Id: {ClienteId}): Asignada Mesa {MesaNumero} para la reserva del {FechaHora:yyyy-MM-dd HH:mm}",
            clienteNombre, clienteEmail, clienteId, mesaNumero, fechaHora);

        return Task.CompletedTask;
    }
}

