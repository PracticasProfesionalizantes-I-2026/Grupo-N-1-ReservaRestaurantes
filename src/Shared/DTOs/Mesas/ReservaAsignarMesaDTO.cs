using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Reservas;

public class ReservaAsignarMesaDTO
{
    [Required(ErrorMessage = "El ID de mesa es obligatorio.")]
    public Guid MesaId { get; set; }
}