using Shared.Enums;

namespace DataAccess.Entities;

public class Reserva
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid MesaId { get; set; }
    public DateTime FechaHora { get; set; }
    public int CantidadComensales { get; set; }
    public int DuracionEstimadaMinutos { get; set; } = 120;
    public ReservaEstado Estado { get; set; } = ReservaEstado.Pendiente;
    public string? Observaciones { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Propiedades de navegación
    public Cliente Cliente { get; set; } = null!;
    public Mesa Mesa { get; set; } = null!;
}
