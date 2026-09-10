using DataAccess.Entities;
using Shared.Enums;

namespace DataAccess.Repositories.Interfaces;

public interface IReservaRepository
{
    Task<IEnumerable<Reserva>> GetAllAsync(DateTime? fecha = null, Guid? clienteId = null, Guid? mesaId = null, ReservaEstado? estado = null);
    Task<Reserva?> GetByIdAsync(Guid id);
    Task<IEnumerable<Reserva>> GetOverlappingReservationsByMesaAsync(Guid mesaId, DateTime start, DateTime end, Guid? excludeReservaId = null);
    Task<IEnumerable<Reserva>> GetOverlappingReservationsByClienteAsync(Guid clienteId, DateTime start, DateTime end, Guid? excludeReservaId = null);
    Task<Reserva> CreateAsync(Reserva reserva);
    Task UpdateAsync(Reserva reserva);
    Task DeleteAsync(Guid id);
}
