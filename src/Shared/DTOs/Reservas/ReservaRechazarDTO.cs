using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Reservas;

/// <summary>
/// DTO para CU-16: Rechazar reserva (Gerente).
/// </summary>
public class ReservaRechazarDTO
{
    /// <summary>
    /// Motivo obligatorio por el que se rechaza la reserva.
    /// Se persiste en el campo Observaciones de la reserva.
    /// </summary>
    [Required(ErrorMessage = "El motivo de rechazo es obligatorio.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 500 caracteres.")]
    public string MotivoRechazo { get; set; } = string.Empty;
}

