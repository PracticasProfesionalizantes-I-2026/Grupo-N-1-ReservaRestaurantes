using DataAccess.Context;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace DataAccess.Repositories.Implementations;

public class ReservaRepository : IReservaRepository
{
    private readonly RestaurantDbContext _context;

    public ReservaRepository(RestaurantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<Reserva>> GetAllAsync(
        DateTime? fecha = null,
        Guid? clienteId = null,
        Guid? mesaId = null,
        ReservaEstado? estado = null)
    {
        var query = _context.Reservas
            .AsNoTracking()
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .AsQueryable();

        if (fecha.HasValue)
        {
            var dateOnly = fecha.Value.Date;
            var nextDay = dateOnly.AddDays(1);
            query = query.Where(r => r.FechaHora >= dateOnly && r.FechaHora < nextDay);
        }

        if (clienteId.HasValue)
        {
            query = query.Where(r => r.ClienteId == clienteId.Value);
        }

        if (mesaId.HasValue)
        {
            query = query.Where(r => r.MesaId == mesaId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(r => r.Estado == estado.Value);
        }

        return await query.OrderBy(r => r.FechaHora).ToListAsync();
    }

    public async Task<Reserva?> GetByIdAsync(Guid id)
    {
        return await _context.Reservas
            .AsNoTracking()
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Reserva>> GetOverlappingReservationsByMesaAsync(
        Guid mesaId,
        DateTime start,
        DateTime end,
        Guid? excludeReservaId = null)
    {
        // Traemos reservas de la mesa que no estén canceladas ni finalizadas para la ventana de fecha
        var windowStart = start.Date;
        var windowEnd = end.Date.AddDays(1);

        var query = _context.Reservas
            .AsNoTracking()
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .Where(r => r.MesaId == mesaId &&
                        r.Estado != ReservaEstado.Cancelada &&
                        r.Estado != ReservaEstado.Finalizada &&
                        r.FechaHora >= windowStart &&
                        r.FechaHora < windowEnd);

        if (excludeReservaId.HasValue)
        {
            query = query.Where(r => r.Id != excludeReservaId.Value);
        }

        var candidates = await query.ToListAsync();

        // En memoria comprobamos el solapamiento exacto [start, end)
        return candidates.Where(r =>
        {
            var rStart = r.FechaHora;
            var rEnd = r.FechaHora.AddMinutes(r.DuracionEstimadaMinutos);
            return rStart < end && rEnd > start;
        }).ToList();
    }

    public async Task<IEnumerable<Reserva>> GetOverlappingReservationsByClienteAsync(
        Guid clienteId,
        DateTime start,
        DateTime end,
        Guid? excludeReservaId = null)
    {
        var windowStart = start.Date;
        var windowEnd = end.Date.AddDays(1);

        var query = _context.Reservas
            .AsNoTracking()
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .Where(r => r.ClienteId == clienteId &&
                        r.Estado != ReservaEstado.Cancelada &&
                        r.Estado != ReservaEstado.Finalizada &&
                        r.FechaHora >= windowStart &&
                        r.FechaHora < windowEnd);

        if (excludeReservaId.HasValue)
        {
            query = query.Where(r => r.Id != excludeReservaId.Value);
        }

        var candidates = await query.ToListAsync();

        return candidates.Where(r =>
        {
            var rStart = r.FechaHora;
            var rEnd = r.FechaHora.AddMinutes(r.DuracionEstimadaMinutos);
            return rStart < end && rEnd > start;
        }).ToList();
    }

    public async Task<Reserva> CreateAsync(Reserva reserva)
    {
        reserva.Id = Guid.NewGuid();
        if (reserva.FechaCreacion == default)
        {
            reserva.FechaCreacion = DateTime.UtcNow;
        }

        await _context.Reservas.AddAsync(reserva);
        await _context.SaveChangesAsync();

        // Cargar navegación si es necesario
        return (await GetByIdAsync(reserva.Id)) ?? reserva;
    }

    public async Task UpdateAsync(Reserva reserva)
    {
        _context.Reservas.Update(reserva);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var reserva = await _context.Reservas.FindAsync(id);
        if (reserva != null)
        {
            _context.Reservas.Remove(reserva);
            await _context.SaveChangesAsync();
        }
    }
}
