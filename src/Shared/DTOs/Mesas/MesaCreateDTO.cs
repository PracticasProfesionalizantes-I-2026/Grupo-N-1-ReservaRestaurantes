namespace Shared.DTOs.Mesas;

public class MesaCreateDTO
{
    public int Numero { get; set; }
    public int Capacidad { get; set; }
    public string Ubicacion { get; set; } = string.Empty;
}
