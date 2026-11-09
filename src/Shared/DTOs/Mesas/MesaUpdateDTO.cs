using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Mesas;

public class MesaUpdateDTO
{
    [Required(ErrorMessage = "La capacidad es obligatoria.")]
    [Range(1, 50, ErrorMessage = "La capacidad debe ser entre 1 y 50 comensales.")]
    public int Capacidad { get; set; }

    [StringLength(80, ErrorMessage = "La ubicación no puede superar los 80 caracteres.")]
    public string? Ubicacion { get; set; }

    public bool Activa { get; set; } = true;
}
