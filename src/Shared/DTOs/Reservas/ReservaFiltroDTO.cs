using Shared.Enums;
namespace Shared.DTOs.Reservas;

public class ReservaFiltroDTO
{

    public DateTime? Fecha { get; set; }
    public ReservaEstado? Estado { get; set; }
    public Guid? MesaId { get; set; }
    public Guid? ClienteId { get; set; }
}