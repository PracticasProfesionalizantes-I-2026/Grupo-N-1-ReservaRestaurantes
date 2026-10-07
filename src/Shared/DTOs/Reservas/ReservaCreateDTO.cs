namespace Shared.DTOs.Reservas;

public class ReservaCreateDTO
{
    public Guid ClienteId { get; set; }
    public Guid MesaId { get; set; }
    public DateTime FechaHora { get; set; }
    public int CantidadComensales { get; set; }

    public string? Observaciones { get; set; }
}
