using Shared.Enums;

namespace Shared.DTOs.Mesas;

public class MesaFiltroDTO
{

    public MesaEstado? Estado { get; set; }
    public int? CapacidadMinima { get; set; }
    public bool? SoloActivas { get; set; }
}
