using BusinessLogic.Auth.Interfaces;
using DataAccess.Repositories.Interfaces;
using Shared.DTOs.Auth;
using Shared.DTOs.Usuarios;
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

    public async Task<UsuarioResponseDTO> RegisterAsync(UsuarioRegisterDTO dto)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        // 1. Normalización de datos
        var emailNormalized = dto.Email.Trim().ToLowerInvariant();

        // 2. Validación de formato de teléfono (RN-02)
        if (string.IsNullOrWhiteSpace(dto.Telefono))
        {
            throw new ValidationException(nameof(dto.Telefono), "El teléfono es obligatorio.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Telefono.Trim(), @"^\+?(?=(?:.*\d){7,})[\d\s\-\(\)]+$"))
        {
            throw new ValidationException(nameof(dto.Telefono), "El formato del teléfono es inválido. Solo se permiten números y los símbolos '+', '-', '()', y no se permiten letras.");
        }

        // 3. Verificación de Regla de Negocio RN-01 (Email Único)
        bool emailExiste = await _clienteRepository.ExistsByEmailAsync(emailNormalized);
        if (emailExiste)
        {
            throw new EmailDuplicadoException(dto.Email);
        }

        // 4. Mapeo y hashing de contraseña con BCrypt (RN-02) y rol Cliente por defecto (RN-03)
        var nuevoUsuario = new DataAccess.Entities.Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Email = emailNormalized,
            Telefono = dto.Telefono.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Rol = "Cliente",
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };

        var usuarioCreado = await _clienteRepository.CreateAsync(nuevoUsuario);

        return new UsuarioResponseDTO
        {
            Id = usuarioCreado.Id,
            Nombre = usuarioCreado.Nombre,
            Apellido = usuarioCreado.Apellido,
            Email = usuarioCreado.Email,
            Rol = usuarioCreado.Rol,
            Activo = usuarioCreado.Activo,
            FechaRegistro = usuarioCreado.FechaRegistro
        };
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
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        // Verifica que la contraseña almacenada sea un hash válido de BCrypt
        if (storedHash.StartsWith("$2a$") || storedHash.StartsWith("$2b$") || storedHash.StartsWith("$2y$"))
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }

        // Rechaza cualquier contraseña que no esté hasheada adecuadamente
        return false;
    }
}
