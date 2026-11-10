using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Mesas;

public class MesaUpdateDTO
{
    [Required(ErrorMessage = "El número de mesa es obligatorio.")]
    [Range(1, 1000, ErrorMessage = "El número de mesa debe ser mayor a 0.")]
    public int Numero { get; set; }

    [Required(ErrorMessage = "La capacidad es obligatoria.")]
    [Range(1, 30, ErrorMessage = "La capacidad debe ser entre 1 y 30 comensales.")]
    public int Capacidad { get; set; }

    [StringLength(80, ErrorMessage = "La ubicación no puede superar los 80 caracteres.")]
    public string? Ubicacion { get; set; }

    public bool Activa { get; set; } = true;
}
