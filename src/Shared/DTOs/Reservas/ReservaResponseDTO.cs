using Shared.Enums;

namespace Shared.DTOs.Reservas;

public class ReservaResponseDTO
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string ClienteNombreCompleto { get; set; } = string.Empty;
    public string ClienteEmail { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;
    public Guid MesaId { get; set; }
    public int MesaNumero { get; set; }
    public int MesaCapacidad { get; set; }
    public string MesaUbicacion { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public DateTime FechaHoraFin => FechaHora.AddMinutes(DuracionEstimadaMinutos);
    public int CantidadComensales { get; set; }
    public int DuracionEstimadaMinutos { get; set; }
    public ReservaEstado Estado { get; set; }
    public string EstadoNombre => Estado.ToString();
    public string? Observaciones { get; set; }
    public DateTime FechaCreacion { get; set; }
}
