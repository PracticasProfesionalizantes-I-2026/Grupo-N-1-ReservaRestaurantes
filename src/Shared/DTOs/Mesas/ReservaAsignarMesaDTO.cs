using System.ComponentModel.DataAnnotations;
namespace Shared.DTOs.Reservas;
public class ReservaAsignarMesaDTO
{
    [Required(ErrorMessage = "El ID de mesa es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El ID de mesa es obligatorio y debe ser mayor a cero.")]
    public int MesaId { get; set; }
}