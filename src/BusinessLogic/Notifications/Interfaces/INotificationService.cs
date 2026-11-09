namespace BusinessLogic.Notifications.Interfaces;

public interface INotificationService
{
    Task NotificarAsignacionMesaAsync(Guid clienteId, string clienteEmail, string clienteNombre, int mesaNumero, DateTime fechaHora);
}

