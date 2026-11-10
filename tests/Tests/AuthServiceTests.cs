using BusinessLogic.Auth.Implementations;
using BusinessLogic.Auth.Interfaces;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Moq;
using Shared.DTOs.Auth;
using Shared.Exceptions;
using Xunit;

namespace Tests;

public class AuthServiceTests
{
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<IJwtTokenGenerator> _jwtMock;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _clienteRepoMock = new Mock<IClienteRepository>();
        _jwtMock = new Mock<IJwtTokenGenerator>();
        _sut = new AuthService(_clienteRepoMock.Object, _jwtMock.Object);
    }

    // =========================================================================
    // CU-04: RegisterAsync
    // =========================================================================

    [Fact]
    public async Task RegisterAsync_ConDatosValidos_CreaUsuarioConRolClienteYHashBCrypt()
    {
        // Arrange
        var dto = new UsuarioRegisterDTO
        {
            Nombre = "Martín",
            Apellido = "González",
            Email = "Martin.Gonzalez@Example.com ",
            Telefono = "+54 11 4455-6677",
            Password = "Password123!"
        };

        _clienteRepoMock.Setup(r => r.ExistsByEmailAsync("martin.gonzalez@example.com", null))
            .ReturnsAsync(false);

        _clienteRepoMock.Setup(r => r.CreateAsync(It.IsAny<Cliente>()))
            .ReturnsAsync((Cliente c) => c);

        // Act
        var resultado = await _sut.RegisterAsync(dto);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Martín", resultado.Nombre);
        Assert.Equal("González", resultado.Apellido);
        Assert.Equal("martin.gonzalez@example.com", resultado.Email);
        Assert.Equal("Cliente", resultado.Rol);
        Assert.True(resultado.Activo);

        _clienteRepoMock.Verify(r => r.CreateAsync(It.Is<Cliente>(c =>
            c.Email == "martin.gonzalez@example.com" &&
            c.Rol == "Cliente" &&
            c.Activo &&
            c.PasswordHash.StartsWith("$2") &&
            BCrypt.Net.BCrypt.Verify("Password123!", c.PasswordHash))), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ConEmailExistente_LanzaEmailDuplicadoException()
    {
        // Arrange
        var dto = new UsuarioRegisterDTO
        {
            Nombre = "Martín",
            Apellido = "González",
            Email = "duplicado@example.com",
            Telefono = "+54 11 4455-6677",
            Password = "Password123!"
        };

        _clienteRepoMock.Setup(r => r.ExistsByEmailAsync("duplicado@example.com", null))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<EmailDuplicadoException>(
            () => _sut.RegisterAsync(dto));

        _clienteRepoMock.Verify(r => r.CreateAsync(It.IsAny<Cliente>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ConTelefonoConLetras_LanzaValidationException()
    {
        // Arrange
        var dto = new UsuarioRegisterDTO
        {
            Nombre = "Martín",
            Apellido = "González",
            Email = "test@example.com",
            Telefono = "1234abcd56",
            Password = "Password123!"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_ConTelefonoVacio_LanzaValidationException()
    {
        // Arrange
        var dto = new UsuarioRegisterDTO
        {
            Nombre = "Martín",
            Apellido = "González",
            Email = "test@example.com",
            Telefono = "   ",
            Password = "Password123!"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RegisterAsync(dto));
    }

    [Fact]
    public async Task RegisterAsync_DtoNulo_LanzaArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.RegisterAsync(null!));
    }

    // =========================================================================
    // CU-05: LoginAsync
    // =========================================================================

    [Fact]
    public async Task LoginAsync_CredencialesValidas_RetornaTokenYDatos()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "Martín",
            Apellido = "González",
            Email = "martin@example.com",
            PasswordHash = passwordHash,
            Rol = "Cliente",
            Activo = true
        };

        _clienteRepoMock.Setup(r => r.GetByEmailAsync("martin@example.com"))
            .ReturnsAsync(cliente);

        _jwtMock.Setup(j => j.GenerateToken(cliente))
            .Returns(("token-jwt-mock", DateTime.UtcNow.AddHours(1)));
        _jwtMock.Setup(j => j.GenerateRefreshToken())
            .Returns("refresh-token-mock-123");

        var dto = new LoginRequestDTO
        {
            Email = "MARTIN@EXAMPLE.COM",
            Password = "Password123!"
        };

        // Act
        var resultado = await _sut.LoginAsync(dto);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("token-jwt-mock", resultado.Token);
        Assert.Equal("refresh-token-mock-123", resultado.RefreshToken);
        Assert.Equal("martin@example.com", resultado.Email);
        Assert.Equal("Cliente", resultado.Role);
    }

    [Fact]
    public async Task LoginAsync_CredencialesInvalidas_LanzaUnauthorizedException()
    {
        // Arrange
        _clienteRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((Cliente?)null);

        var dto = new LoginRequestDTO
        {
            Email = "noexiste@example.com",
            Password = "wrongpassword"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(dto));
    }

    [Fact]
    public async Task LoginAsync_ContrasenaAlmacenadaEnTextoPlano_DeniegaLoginYLanzaUnauthorizedException()
    {
        // Arrange — Usuario con contraseña guardada en texto plano (sin hash BCrypt)
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "Inseguro",
            Apellido = "Usuario",
            Email = "inseguro@test.com",
            PasswordHash = "PasswordPlana123!", // En texto plano, NO es hash BCrypt
            Rol = "Cliente",
            Activo = true
        };

        _clienteRepoMock.Setup(r => r.GetByEmailAsync("inseguro@test.com"))
            .ReturnsAsync(cliente);

        var dto = new LoginRequestDTO
        {
            Email = "inseguro@test.com",
            Password = "PasswordPlana123!" // La misma clave exacta
        };

        // Act & Assert — Debe denegar el login por no tener hash BCrypt válido
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(dto));
    }

    [Fact]
    public async Task LoginAsync_HashNoBCrypt_DeniegaLoginYLanzaUnauthorizedException()
    {
        // Arrange — Usuario con hash MD5 o formato no soportado
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "Hash",
            Apellido = "Invalido",
            Email = "hashinvalido@test.com",
            PasswordHash = "e10adc3949ba59abbe56e057f20f883e", // MD5
            Rol = "Cliente",
            Activo = true
        };

        _clienteRepoMock.Setup(r => r.GetByEmailAsync("hashinvalido@test.com"))
            .ReturnsAsync(cliente);

        var dto = new LoginRequestDTO
        {
            Email = "hashinvalido@test.com",
            Password = "123456"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(dto));
    }

    [Fact]
    public async Task LoginAsync_ContrasenaBCryptIncorrecta_LanzaUnauthorizedException()
    {
        // Arrange — Hash BCrypt legítimo pero contraseña ingresada incorrecta
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("ContrasenaCorrecta123!");
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "Valido",
            Apellido = "Usuario",
            Email = "valido@test.com",
            PasswordHash = passwordHash,
            Rol = "Cliente",
            Activo = true
        };

        _clienteRepoMock.Setup(r => r.GetByEmailAsync("valido@test.com"))
            .ReturnsAsync(cliente);

        var dto = new LoginRequestDTO
        {
            Email = "valido@test.com",
            Password = "ContrasenaErronea999!"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(dto));
    }

    [Fact]
    public async Task LoginAsync_UsuarioInactivoConCredencialesValidas_LanzaCuentaInactivaException()
    {
        // Arrange — Usuario registrado con contraseña correcta pero inactivo/deshabilitado
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("PasswordValida123!");
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario",
            Apellido = "Inactivo",
            Email = "inactivo@test.com",
            PasswordHash = passwordHash,
            Rol = "Cliente",
            Activo = false // Cuenta inactiva
        };

        _clienteRepoMock.Setup(r => r.GetByEmailAsync("inactivo@test.com"))
            .ReturnsAsync(cliente);

        var dto = new LoginRequestDTO
        {
            Email = "inactivo@test.com",
            Password = "PasswordValida123!"
        };

        // Act & Assert — Debe lanzar CuentaInactivaException (HTTP 403)
        await Assert.ThrowsAsync<CuentaInactivaException>(
            () => _sut.LoginAsync(dto));
    }
}

