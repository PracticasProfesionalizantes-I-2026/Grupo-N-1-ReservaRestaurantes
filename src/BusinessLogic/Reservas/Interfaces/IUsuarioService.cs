using Shared.DTOs.Usuarios;

namespace BusinessLogic.Usuarios.Interfaces;

public interface IUsuarioService
{
    Task<UsuarioResponseDTO> CambiarRolUsuarioAsync(Guid id, UsuarioCambiarRolDTO dto);

    /// <summary>
    /// CU-20: Eliminar usuario aplicando baja lógica o física según reservas asociadas (RN-03).
    /// </summary>
    Task<bool> EliminarUsuarioAsync(Guid id, Guid adminEnSesionId);
}
