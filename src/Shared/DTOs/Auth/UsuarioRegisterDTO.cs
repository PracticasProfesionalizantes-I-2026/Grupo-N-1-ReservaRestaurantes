using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Auth;

public class UsuarioRegisterDTO
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato de correo electrónico es inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(25, MinimumLength = 7, ErrorMessage = "El teléfono debe tener entre 7 y 25 caracteres.")]
    [RegularExpression(@"^\+?(?=(?:.*\d){7,})[\d\s\-\(\)]+$", ErrorMessage = "El formato del teléfono es inválido. Solo se permiten números y los símbolos '+', '-', '()', y no se permiten letras.")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe contener al menos 8 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

