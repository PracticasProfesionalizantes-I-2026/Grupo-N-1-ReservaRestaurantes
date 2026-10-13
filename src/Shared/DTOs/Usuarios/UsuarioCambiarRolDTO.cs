using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.Usuarios;

public class UsuarioCambiarRolDTO
{
    [Required(ErrorMessage = "El nuevo rol es obligatorio.")]
    [RegularExpression("^(Admin|Gerente|Cliente)$", ErrorMessage = "El rol especificado no es válido en el sistema. Roles permitidos: Admin, Gerente, Cliente.")]
    public string NuevoRol { get; set; } = string.Empty;
}
