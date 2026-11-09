using Shared.DTOs.Auth;
using Shared.DTOs.Usuarios;

namespace BusinessLogic.Auth.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDTO> LoginAsync(LoginRequestDTO dto);
    Task<UsuarioResponseDTO> RegisterAsync(UsuarioRegisterDTO dto);
    Task LogoutAsync(string? token = null, Guid? userId = null);
}
