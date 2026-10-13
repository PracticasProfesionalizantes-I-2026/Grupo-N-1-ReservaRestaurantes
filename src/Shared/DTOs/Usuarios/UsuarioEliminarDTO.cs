namespace Shared.DTOs.Usuarios;

public class UsuarioEliminarDTO
{
    /// <summary>
    /// Flag de confirmación explícita requerido para eliminar la cuenta (RN-02).
    /// </summary>
    public bool Confirmado { get; set; } = false;
}
