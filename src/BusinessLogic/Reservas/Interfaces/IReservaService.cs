using Shared.DTOs.Reservas;

namespace BusinessLogic.Reservas.Interfaces;

public interface IReservaService
{
    Task<ReservaResponseDTO> SolicitarReservaAsync(Guid clienteId, ReservaSolicitudDTO dto);
    Task<ReservaResponseDTO?> GetByIdAsync(Guid id);
}
