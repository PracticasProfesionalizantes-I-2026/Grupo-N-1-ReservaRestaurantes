using Shared.DTOs.Usuarios;

namespace BusinessLogic.Usuarios.Interfaces;

public interface IUsuarioService
{
    Task<UsuarioResponseDTO> CambiarRolUsuarioAsync(Guid id, UsuarioCambiarRolDTO dto);
}
