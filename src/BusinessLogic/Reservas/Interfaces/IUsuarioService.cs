using Shared.DTOs.Usuarios;

namespace BusinessLogic.Usuarios.Interfaces;

public interface IUsuarioService
{
    Task<UsuarioResponseDTO> CambiarRolUsuarioAsync(Guid id, UsuarioCambiarRolDTO dto);

    /// <summary>
    /// CU-20: Eliminar usuario aplicando baja lógica o física según reservas asociadas (RN-03).
    /// </summary>
    Task<bool> EliminarUsuarioAsync(Guid id, Guid adminEnSesionId);

    /// <summary>
    /// CU-09 / RN-01: Obtiene el contexto inicial y resumen operativo adaptado al rol del usuario.
    /// </summary>
    Task<ContextoInicioDTO> GetContextoInicioAsync(Guid usuarioId, string? rol = null);
}
