using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Reservas;

public class ReservaCancelarDTO
{
    [StringLength(250, ErrorMessage = "El motivo de cancelación no puede superar los 250 caracteres.")]
    public string? Motivo { get; set; }
}
