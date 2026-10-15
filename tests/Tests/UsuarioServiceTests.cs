using BusinessLogic.Usuarios.Implementations;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Moq;
using Shared.DTOs.Usuarios;
using Shared.Exceptions;

namespace Tests;

/// <summary>
/// Pruebas unitarias de UsuarioService.
/// Patrón AAA (Arrange / Act / Assert) con aislamiento total via Moq.
/// </summary>
public class UsuarioServiceTests
{
    // ─── Fixture común ────────────────────────────────────────────────────────
    private readonly Mock<IClienteRepository> _repoMock;
    private readonly UsuarioService _sut;

    public UsuarioServiceTests()
    {
        _repoMock = new Mock<IClienteRepository>();
        _sut = new UsuarioService(_repoMock.Object);
    }

    // =========================================================================
    // CambiarRolUsuarioAsync
    // =========================================================================

    [Fact]
    public async Task CambiarRolUsuarioAsync_ConRolValido_RetornaUsuarioActualizado()
    {
        // Arrange
        var id = Guid.NewGuid();
        var usuario = new Cliente { Id = id, Nombre = "Ana", Apellido = "García", Email = "ana@mail.com", Rol = "Cliente", Activo = true };
        var dto = new UsuarioCambiarRolDTO { NuevoRol = "Gerente" };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CambiarRolUsuarioAsync(id, dto);

        // Assert
        Assert.Equal("Gerente", resultado.Rol);
        _repoMock.Verify(r => r.UpdateAsync(It.Is<Cliente>(c => c.Rol == "Gerente")), Times.Once);
    }

    [Fact]
    public async Task CambiarRolUsuarioAsync_ConRolInvalido_LanzaValidationException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dto = new UsuarioCambiarRolDTO { NuevoRol = "Superheroe" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _sut.CambiarRolUsuarioAsync(id, dto));
        _repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CambiarRolUsuarioAsync_CuandoUsuarioNoExiste_LanzaNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Cliente?)null);
        var dto = new UsuarioCambiarRolDTO { NuevoRol = "Gerente" };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CambiarRolUsuarioAsync(id, dto));
    }

    [Fact]
    public async Task CambiarRolUsuarioAsync_QuitarAdminUnicoAdmin_LanzaConflictException()
    {
        // Arrange  (RN de salvaguarda)
        var id = Guid.NewGuid();
        var admin = new Cliente { Id = id, Rol = "Admin", Activo = true };
        var dto = new UsuarioCambiarRolDTO { NuevoRol = "Cliente" };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(admin);
        _repoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(1); // solo uno

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _sut.CambiarRolUsuarioAsync(id, dto));
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Cliente>()), Times.Never);
    }

    [Fact]
    public async Task CambiarRolUsuarioAsync_QuitarAdminConVariosAdmins_ActualizaRol()
    {
        // Arrange
        var id = Guid.NewGuid();
        var admin = new Cliente { Id = id, Nombre = "Luis", Apellido = "Pérez", Email = "luis@mail.com", Rol = "Admin", Activo = true };
        var dto = new UsuarioCambiarRolDTO { NuevoRol = "Gerente" };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(admin);
        _repoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(3); // hay más admins
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CambiarRolUsuarioAsync(id, dto);

        // Assert
        Assert.Equal("Gerente", resultado.Rol);
    }

    [Theory]
    [InlineData("admin")]   // case-insensitive: minúsculas
    [InlineData("ADMIN")]   // mayúsculas
    [InlineData("Gerente")]
    [InlineData("Cliente")]
    public async Task CambiarRolUsuarioAsync_RolesValidos_NoLanzaValidationException(string rol)
    {
        // Arrange
        var id = Guid.NewGuid();
        var usuario = new Cliente { Id = id, Nombre = "X", Apellido = "Y", Email = "x@y.com", Rol = "Cliente", Activo = true };
        var dto = new UsuarioCambiarRolDTO { NuevoRol = rol };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        // Act & Assert (no debe lanzar excepción de validación)
        var resultado = await _sut.CambiarRolUsuarioAsync(id, dto);
        Assert.NotNull(resultado);
    }

    // =========================================================================
    // EliminarUsuarioAsync
    // =========================================================================

    [Fact]
    public async Task EliminarUsuarioAsync_AutoEliminacion_LanzaConflictException()
    {
        // Arrange  (RN: no se puede eliminar la cuenta en sesión)
        var id = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _sut.EliminarUsuarioAsync(id, id));
    }

    [Fact]
    public async Task EliminarUsuarioAsync_UsuarioNoExiste_LanzaNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Cliente?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.EliminarUsuarioAsync(id, adminId));
    }

    [Fact]
    public async Task EliminarUsuarioAsync_SinReservas_AplicaBajaFisicaYRetornaTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var usuario = new Cliente { Id = id, Rol = "Cliente", Activo = true };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.HasReservationsAsync(id)).ReturnsAsync(false);
        _repoMock.Setup(r => r.DeleteAsync(id)).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.EliminarUsuarioAsync(id, adminId);

        // Assert
        Assert.True(resultado);
        _repoMock.Verify(r => r.DeleteAsync(id), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Cliente>()), Times.Never);
    }

    [Fact]
    public async Task EliminarUsuarioAsync_ConReservas_AplicaBajaLogicaYRetornaFalse()
    {
        // Arrange  (RN-03: preservar integridad referencial)
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var usuario = new Cliente { Id = id, Rol = "Cliente", Activo = true };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.HasReservationsAsync(id)).ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.EliminarUsuarioAsync(id, adminId);

        // Assert
        Assert.False(resultado);
        Assert.False(usuario.Activo); // se desactivó lógicamente
        _repoMock.Verify(r => r.UpdateAsync(usuario), Times.Once);
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task EliminarUsuarioAsync_UltimoAdmin_LanzaConflictException()
    {
        // Arrange  (salvaguarda: no eliminar al único admin activo)
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var usuario = new Cliente { Id = id, Rol = "Admin", Activo = true };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(1);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _sut.EliminarUsuarioAsync(id, adminId));
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }
}
