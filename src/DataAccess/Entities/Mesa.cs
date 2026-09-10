using Shared.Enums;

namespace DataAccess.Entities;

public class Mesa
{
    public Guid Id { get; set; }
    public int Numero { get; set; }
    public int Capacidad { get; set; }
    public string Ubicacion { get; set; } = string.Empty;
    public MesaEstado Estado { get; set; } = MesaEstado.Libre;
    public bool Activa { get; set; } = true;

    // Relación 1 a N con Reservas (DeleteBehavior.Restrict)
    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
