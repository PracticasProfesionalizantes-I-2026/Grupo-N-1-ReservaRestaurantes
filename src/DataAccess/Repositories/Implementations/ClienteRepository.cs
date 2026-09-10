using DataAccess.Context;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implementations;

public class ClienteRepository : IClienteRepository
{
    private readonly RestaurantDbContext _context;

    public ClienteRepository(RestaurantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<Cliente>> GetAllAsync()
    {
        return await _context.Clientes
            .AsNoTracking()
            .OrderBy(c => c.Apellido)
            .ThenBy(c => c.Nombre)
            .ToListAsync();
    }

    public async Task<Cliente?> GetByIdAsync(Guid id)
    {
        return await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Cliente?> GetByEmailAsync(string email)
    {
        return await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email.ToLower() == email.ToLower());
    }

    public async Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null)
    {
        var query = _context.Clientes.AsNoTracking().Where(c => c.Email.ToLower() == email.ToLower());
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }
        return await query.AnyAsync();
    }

    public async Task<bool> HasReservationsAsync(Guid clienteId)
    {
        return await _context.Reservas
            .AsNoTracking()
            .AnyAsync(r => r.ClienteId == clienteId);
    }

    public async Task<Cliente> CreateAsync(Cliente cliente)
    {
        cliente.Id = Guid.NewGuid();
        if (cliente.FechaRegistro == default)
        {
            cliente.FechaRegistro = DateTime.UtcNow;
        }

        await _context.Clientes.AddAsync(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    public async Task UpdateAsync(Cliente cliente)
    {
        _context.Clientes.Update(cliente);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente != null)
        {
            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
        }
    }
}
