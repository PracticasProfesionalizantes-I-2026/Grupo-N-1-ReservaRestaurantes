using Shared.DTOs.Reservas;

namespace BusinessLogic.Reservas.Interfaces;

public interface IListaEsperaService
{
    /// <summary>
    /// CU-18 / RN-02: Obtiene todas las solicitudes en lista de espera activas (Pendientes)
    /// ordenadas por fecha de creación en estricto orden de llegada (FIFO).
    /// </summary>
    Task<IEnumerable<ReservaResponseDTO>> GetSolicitudesOrdenadasAsync();

    /// <summary>
    /// CU-18 / RF-13 / RN-03: Promueve una reserva en lista de espera a Confirmada,
    /// validando y asignando una mesa disponible compatible (manual o automática)
    /// y notificando al cliente.
    /// </summary>
    Task<ReservaResponseDTO> PromoverSolicitudAsync(Guid reservaId, Guid? mesaId = null);
}

