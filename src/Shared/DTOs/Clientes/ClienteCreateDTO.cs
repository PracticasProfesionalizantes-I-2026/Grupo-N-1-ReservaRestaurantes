using System.ComponentModel.DataAnnotations;
namespace Shared.DTOs.Clientes;

public class ClienteCreateDTO
{
    //Atributos de validacion + propiedades del cliente

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no es válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(15, ErrorMessage = "El teléfono no puede superar los 15 caracteres.")]
    public string Telefono { get; set; } = string.Empty;
}
