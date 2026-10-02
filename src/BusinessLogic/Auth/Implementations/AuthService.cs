using BusinessLogic.Auth.Interfaces;
using DataAccess.Repositories.Interfaces;
using Shared.DTOs.Auth;
using Shared.Exceptions;

namespace BusinessLogic.Auth.Implementations;

public class AuthService : IAuthService
{
    private readonly IClienteRepository _clienteRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(IClienteRepository clienteRepository, IJwtTokenGenerator jwtTokenGenerator)
    {
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
        _jwtTokenGenerator = jwtTokenGenerator ?? throw new ArgumentNullException(nameof(jwtTokenGenerator));
    }

    public async Task<LoginResponseDTO> LoginAsync(LoginRequestDTO dto)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        // 1. Normalizar Email
        var normalizedEmail = dto.Email.Trim().ToLower();

        // 2. Buscar cliente por email
        var cliente = await _clienteRepository.GetByEmailAsync(normalizedEmail);
        if (cliente == null || !cliente.Activo)
        {
            throw new UnauthorizedException("Credenciales inválidas.");
        }

        // 3. Verificar contraseña
        if (!VerifyPassword(dto.Password, cliente.PasswordHash))
        {
            throw new UnauthorizedException("Credenciales inválidas.");
        }

        // 4. Generar Token JWT
        var (token, expiration) = _jwtTokenGenerator.GenerateToken(cliente);

        // 5. Retornar DTO de respuesta
        return new LoginResponseDTO
        {
            Token = token,
            Expiration = expiration,
            UserId = cliente.Id,
            Email = cliente.Email,
            Nombre = $"{cliente.Nombre} {cliente.Apellido}".Trim(),
            Role = string.IsNullOrWhiteSpace(cliente.Rol) ? "Cliente" : cliente.Rol
        };
    }

    public Task LogoutAsync()
    {
        // Cierre de sesión sin estado (Stateless JWT)
        // El token expira o es descartado por el cliente.
        return Task.CompletedTask;
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        // Si la contraseña almacenada empieza con el prefijo BCrypt, la verifica con BCrypt
        if (storedHash.StartsWith("$2a$") || storedHash.StartsWith("$2b$") || storedHash.StartsWith("$2y$"))
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }

        // De lo contrario compara texto plano (por compatibilidad o pruebas)
        return password == storedHash;
    }
}
