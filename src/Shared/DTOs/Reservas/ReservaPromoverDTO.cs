namespace Shared.DTOs.Reservas;

public class ReservaPromoverDTO
{
    /// <summary>
    /// ID de la mesa a asignar a la reserva en espera.
    /// Si es nulo, el sistema buscará y asignará automáticamente la mesa óptima disponible.
    /// </summary>
    public Guid? MesaId { get; set; }
}

