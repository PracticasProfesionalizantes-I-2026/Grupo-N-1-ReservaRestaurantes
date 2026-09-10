using DataAccess.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IMesaRepository
{
    Task<IEnumerable<Mesa>> GetAllAsync();
    Task<Mesa?> GetByIdAsync(Guid id);
    Task<Mesa?> GetByNumeroAsync(int numero);
    Task<bool> ExistsByNumeroAsync(int numero, Guid? excludeId = null);
    Task<bool> HasReservationsAsync(Guid mesaId);
    Task<IEnumerable<Mesa>> GetActiveTablesAsync();
    Task<Mesa> CreateAsync(Mesa mesa);
    Task UpdateAsync(Mesa mesa);
    Task DeleteAsync(Guid id);
}
