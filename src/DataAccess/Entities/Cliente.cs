namespace DataAccess.Entities;

public class Cliente
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Relación 1 a N con Reservas (DeleteBehavior.Restrict)
    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
