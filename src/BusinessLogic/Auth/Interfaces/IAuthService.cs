using Shared.DTOs.Auth;

namespace BusinessLogic.Auth.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDTO> LoginAsync(LoginRequestDTO dto);
    Task LogoutAsync();
}
