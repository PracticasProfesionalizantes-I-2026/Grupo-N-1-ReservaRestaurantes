namespace Shared.DTOs.Reservas;

public class ReservaCreateDTO
{
    public Guid ClienteId { get; set; }
    public Guid MesaId { get; set; }
    public DateTime FechaHora { get; set; }
    public int CantidadComensales { get; set; }
    public int DuracionEstimadaMinutos { get; set; } = 120;
    public string? Observaciones { get; set; }
}
