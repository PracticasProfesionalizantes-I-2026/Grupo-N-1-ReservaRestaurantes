using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Reservas;

public class ReservaUpdateDTO
{
    [Required(ErrorMessage = "La fecha y hora son obligatorias.")]
    public DateTime FechaHora { get; set; }

    [Required(ErrorMessage = "La cantidad de comensales es obligatoria.")]
    [Range(1, 20, ErrorMessage = "La cantidad de comensales debe ser entre 1 y 20.")]
    public int CantidadComensales { get; set; }

    public Guid? MesaId { get; set; }

    [Range(30, 360, ErrorMessage = "La duración estimada debe estar entre 30 y 360 minutos.")]
    public int? DuracionEstimadaMinutos { get; set; } = 120;

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    public string? Observaciones { get; set; }
}
