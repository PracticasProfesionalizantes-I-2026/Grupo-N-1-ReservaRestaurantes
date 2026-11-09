using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.DTOs.Mesas;

public class MesaCambiarEstadoDTO
{
    [Required(ErrorMessage = "El nuevo estado es obligatorio.")]
    [EnumDataType(typeof(MesaEstado), ErrorMessage = "El estado especificado no es válido.")]
    public MesaEstado NuevoEstado { get; set; }
}
