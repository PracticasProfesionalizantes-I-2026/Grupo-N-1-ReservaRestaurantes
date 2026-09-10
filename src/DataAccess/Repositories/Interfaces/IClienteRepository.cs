using DataAccess.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IClienteRepository
{
    Task<IEnumerable<Cliente>> GetAllAsync();
    Task<Cliente?> GetByIdAsync(Guid id);
    Task<Cliente?> GetByEmailAsync(string email);
    Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null);
    Task<bool> HasReservationsAsync(Guid clienteId);
    Task<Cliente> CreateAsync(Cliente cliente);
    Task UpdateAsync(Cliente cliente);
    Task DeleteAsync(Guid id);
}
