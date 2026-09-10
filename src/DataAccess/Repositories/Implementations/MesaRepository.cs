using DataAccess.Context;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implementations;

public class MesaRepository : IMesaRepository
{
    private readonly RestaurantDbContext _context;

    public MesaRepository(RestaurantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<Mesa>> GetAllAsync()
    {
        return await _context.Mesas
            .AsNoTracking()
            .OrderBy(m => m.Numero)
            .ToListAsync();
    }

    public async Task<Mesa?> GetByIdAsync(Guid id)
    {
        return await _context.Mesas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<Mesa?> GetByNumeroAsync(int numero)
    {
        return await _context.Mesas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Numero == numero);
    }

    public async Task<bool> ExistsByNumeroAsync(int numero, Guid? excludeId = null)
    {
        var query = _context.Mesas.AsNoTracking().Where(m => m.Numero == numero);
        if (excludeId.HasValue)
        {
            query = query.Where(m => m.Id != excludeId.Value);
        }
        return await query.AnyAsync();
    }

    public async Task<bool> HasReservationsAsync(Guid mesaId)
    {
        return await _context.Reservas
            .AsNoTracking()
            .AnyAsync(r => r.MesaId == mesaId);
    }

    public async Task<IEnumerable<Mesa>> GetActiveTablesAsync()
    {
        return await _context.Mesas
            .AsNoTracking()
            .Where(m => m.Activa)
            .OrderBy(m => m.Numero)
            .ToListAsync();
    }

    public async Task<Mesa> CreateAsync(Mesa mesa)
    {
        mesa.Id = Guid.NewGuid();
        await _context.Mesas.AddAsync(mesa);
        await _context.SaveChangesAsync();
        return mesa;
    }

    public async Task UpdateAsync(Mesa mesa)
    {
        _context.Mesas.Update(mesa);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var mesa = await _context.Mesas.FindAsync(id);
        if (mesa != null)
        {
            _context.Mesas.Remove(mesa);
            await _context.SaveChangesAsync();
        }
    }
}
