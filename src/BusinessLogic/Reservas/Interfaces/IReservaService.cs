using Shared.DTOs.Reservas;

namespace BusinessLogic.Reservas.Interfaces;

public interface IReservaService
{
    Task<ReservaResponseDTO> SolicitarReservaAsync(Guid clienteId, ReservaSolicitudDTO dto);
    Task<ReservaResponseDTO?> GetByIdAsync(Guid id, Guid? usuarioId = null, string? rol = null);
    Task<ReservaResponseDTO> ModificarReservaAsync(Guid reservaId, ReservaUpdateDTO dto, Guid? usuarioId = null, string? rol = null);
    Task<ReservaResponseDTO> CancelarReservaClienteAsync(Guid reservaId, Guid clienteId, ReservaCancelarDTO? dto = null);
    Task<IEnumerable<ReservaResponseDTO>> FiltrarPorFechaAsync(Guid clienteId, DateTime fecha);
    Task<IEnumerable<ReservaResponseDTO>> GetListaEsperaAsync();
    Task<ReservaResponseDTO> PromoverListaEsperaAsync(Guid reservaId, Guid? mesaId = null);
    Task<ReservaResponseDTO> ConfirmarReservaAsync(Guid reservaId);
    Task<ReservaResponseDTO> IniciarReservaAsync(Guid reservaId);
    Task<ReservaResponseDTO> FinalizarReservaAsync(Guid reservaId);
    Task<ReservaResponseDTO> CambiarEstadoAsync(Guid reservaId, ReservaCambiarEstadoDTO dto);
    Task<ReservaResponseDTO> CancelarReservaGerenteAsync(Guid reservaId);
    Task<ReservaResponseDTO> RechazarReservaAsync(Guid reservaId);
    Task<ReservaResponseDTO> AsignarMesaAsync(Guid reservaId, int mesaId);
    Task<IEnumerable<ReservaResponseDTO>> GetTableroOperativoAsync(DateTime? fecha = null);
    Task<IEnumerable<ReservaResponseDTO>> FiltrarReservasAsync(ReservaFiltroDTO filtro);

}

