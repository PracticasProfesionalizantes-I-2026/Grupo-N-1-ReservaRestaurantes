using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.DTOs.Reservas;

public class ReservaCambiarEstadoDTO
{
    [Required(ErrorMessage = "El nuevo estado es obligatorio.")]
    [EnumDataType(typeof(ReservaEstado), ErrorMessage = "El estado especificado no es válido.")]
    public ReservaEstado NuevoEstado { get; set; }
}
