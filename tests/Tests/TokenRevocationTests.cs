using BusinessLogic.Auth.Implementations;
using BusinessLogic.Auth.Interfaces;
using BusinessLogic.Usuarios.Implementations;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Moq;
using Shared.DTOs.Usuarios;
using Xunit;

namespace Tests;

/// <summary>
/// SEC-07 / RN-03 de CU-19 / CU-08:
/// Pruebas para la lista de revocación de tokens JWT e invalidación de sesiones
/// al cambiar rol, dar de baja usuario o cerrar sesión.
/// </summary>
public class TokenRevocationTests
{
    // =========================================================================
    // TokenRevocationService
    // =========================================================================

    [Fact]
    public void RevokeToken_MarcaTokenComoRevocado()
    {
        // Arrange
        var sut = new TokenRevocationService();
        var token = "token.jwt.prueba.123";

        // Act
        sut.RevokeToken(token);

        // Assert
        Assert.True(sut.IsTokenRevoked(token));
        Assert.True(sut.IsRevoked(token, null, null));
    }

    [Fact]
    public void IsTokenRevoked_TokenNoRevocado_RetornaFalse()
    {
        // Arrange
        var sut = new TokenRevocationService();

        // Act & Assert
        Assert.False(sut.IsTokenRevoked("token.desconocido"));
        Assert.False(sut.IsRevoked("token.desconocido", null, null));
    }

    [Fact]
    public void RevokeUserTokens_InvalidaTokenEmitidoAntesDeLaRevocacion()
    {
        // Arrange
        var sut = new TokenRevocationService();
        var userId = Guid.NewGuid();

        var tokenIssuedAt = DateTime.UtcNow.AddMinutes(-5);

        // Act — se revoca al usuario (ej. por cambio de rol a las 17:00)
        sut.RevokeUserTokens(userId);

        // Assert — el token previo a la revocación debe ser rechazado
        Assert.True(sut.IsUserRevoked(userId, tokenIssuedAt));
        Assert.True(sut.IsRevoked("token.valido", userId, tokenIssuedAt));
    }

    [Fact]
    public void RevokeUserTokens_PermiteTokenEmitidoDespuesDeLaRevocacion()
    {
        // Arrange
        var sut = new TokenRevocationService();
        var userId = Guid.NewGuid();

        // Act — se revoca al usuario a las 17:00
        sut.RevokeUserTokens(userId);

        // Token emitido después de la revocación (nuevo login)
        var tokenPosterior = DateTime.UtcNow.AddSeconds(5);

        // Assert — no debe estar revocado
        Assert.False(sut.IsUserRevoked(userId, tokenPosterior));
        Assert.False(sut.IsRevoked("token.nuevo", userId, tokenPosterior));
    }

    // =========================================================================
    // UsuarioService con ITokenRevocationService
    // =========================================================================

    [Fact]
    public async Task CambiarRolUsuarioAsync_ConRevocacion_InvocaRevokeUserTokens()
    {
        // Arrange
        var repoMock = new Mock<IClienteRepository>();
        var revocationMock = new Mock<ITokenRevocationService>();
        var sut = new UsuarioService(repoMock.Object, tokenRevocationService: revocationMock.Object);

        var id = Guid.NewGuid();
        var usuario = new Cliente
        {
            Id = id,
            Nombre = "Carlos",
            Apellido = "López",
            Email = "carlos@test.com",
            Rol = "Cliente",
            Activo = true
        };
        var dto = new UsuarioCambiarRolDTO { NuevoRol = "Gerente" };

        repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        repoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await sut.CambiarRolUsuarioAsync(id, dto);

        // Assert
        Assert.Equal("Gerente", resultado.Rol);
        revocationMock.Verify(r => r.RevokeUserTokens(id), Times.Once);
    }

    [Fact]
    public async Task EliminarUsuarioAsync_BajaLogica_InvocaRevokeUserTokens()
    {
        // Arrange
        var repoMock = new Mock<IClienteRepository>();
        var revocationMock = new Mock<ITokenRevocationService>();
        var sut = new UsuarioService(repoMock.Object, tokenRevocationService: revocationMock.Object);

        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var usuario = new Cliente
        {
            Id = id,
            Nombre = "Juan",
            Apellido = "Pérez",
            Rol = "Cliente",
            Activo = true
        };

        repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        repoMock.Setup(r => r.HasReservationsAsync(id)).ReturnsAsync(true); // Tiene reservas -> baja lógica
        repoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await sut.EliminarUsuarioAsync(id, adminId);

        // Assert (retorna false por ser baja lógica y revoca tokens)
        Assert.False(resultado);
        revocationMock.Verify(r => r.RevokeUserTokens(id), Times.Once);
    }

    [Fact]
    public async Task EliminarUsuarioAsync_BajaFisica_InvocaRevokeUserTokens()
    {
        // Arrange
        var repoMock = new Mock<IClienteRepository>();
        var revocationMock = new Mock<ITokenRevocationService>();
        var sut = new UsuarioService(repoMock.Object, tokenRevocationService: revocationMock.Object);

        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var usuario = new Cliente
        {
            Id = id,
            Nombre = "Juan",
            Apellido = "Pérez",
            Rol = "Cliente",
            Activo = true
        };

        repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        repoMock.Setup(r => r.HasReservationsAsync(id)).ReturnsAsync(false); // Sin reservas -> baja física
        repoMock.Setup(r => r.DeleteAsync(id)).Returns(Task.CompletedTask);

        // Act
        var resultado = await sut.EliminarUsuarioAsync(id, adminId);

        // Assert (retorna true por ser baja física y revoca tokens)
        Assert.True(resultado);
        revocationMock.Verify(r => r.RevokeUserTokens(id), Times.Once);
    }

    // =========================================================================
    // AuthService.LogoutAsync con ITokenRevocationService
    // =========================================================================

    [Fact]
    public async Task LogoutAsync_ConTokenYUsuario_InvocaRevocacionDeTokenYUsuario()
    {
        // Arrange
        var repoMock = new Mock<IClienteRepository>();
        var jwtMock = new Mock<IJwtTokenGenerator>();
        var revocationMock = new Mock<ITokenRevocationService>();

        var sut = new AuthService(repoMock.Object, jwtMock.Object, revocationMock.Object);
        var token = "jwt.token.cerrar.sesion";
        var userId = Guid.NewGuid();

        // Act
        await sut.LogoutAsync(token, userId);

        // Assert
        revocationMock.Verify(r => r.RevokeToken(token), Times.Once);
        revocationMock.Verify(r => r.RevokeUserTokens(userId), Times.Once);
    }
}

