using Shared.DTOs.Reservas;

namespace BusinessLogic.Reservas.Interfaces;

public interface IReservaService
{
    Task<ReservaResponseDTO> SolicitarReservaAsync(Guid clienteId, ReservaSolicitudDTO dto);
    Task<ReservaResponseDTO?> GetByIdAsync(Guid id);
    Task<ReservaResponseDTO> CancelarReservaClienteAsync(Guid reservaId, Guid clienteId, ReservaCancelarDTO? dto = null);
    Task<IEnumerable<ReservaResponseDTO>> FiltrarPorFechaAsync(Guid clienteId, DateTime fecha);
    Task<ReservaResponseDTO> ConfirmarReservaAsync(Guid reservaId);
}
