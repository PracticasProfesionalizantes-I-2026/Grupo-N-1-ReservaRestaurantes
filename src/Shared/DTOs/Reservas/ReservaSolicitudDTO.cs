using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Reservas;

public class ReservaSolicitudDTO
{
    [Required(ErrorMessage = "La fecha y hora son obligatorias.")]
    public DateTime FechaHora { get; set; }

    [Required(ErrorMessage = "La cantidad de comensales es obligatoria.")]
    [Range(1, 20, ErrorMessage = "La cantidad de comensales debe ser entre 1 y 20.")]
    public int CantidadComensales { get; set; }

    public string? Observaciones { get; set; }
}
