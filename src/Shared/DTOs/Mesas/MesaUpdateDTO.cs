namespace Shared.DTOs.Mesas;

public class MesaUpdateDTO
{
    public int Capacidad { get; set; }
    public string Ubicacion { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
}
