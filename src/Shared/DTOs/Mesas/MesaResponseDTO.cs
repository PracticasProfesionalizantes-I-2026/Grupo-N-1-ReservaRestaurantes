using Shared.Enums;

namespace Shared.DTOs.Mesas;

public class MesaResponseDTO
{
    public Guid Id { get; set; }
    public int Numero { get; set; }
    public int Capacidad { get; set; }

    public MesaEstado Estado { get; set; }
    public string EstadoNombre => Estado.ToString();
    public bool Activa { get; set; }
}
