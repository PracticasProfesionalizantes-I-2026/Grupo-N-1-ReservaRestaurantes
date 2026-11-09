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

    // =========================================================================
    // CU-09: GetContextoInicioAsync
    // =========================================================================

    [Fact]
    public async Task GetContextoInicioAsync_Cliente_RetornaResumenClienteYAccesosRapidos()
    {
        // Arrange
        var id = Guid.NewGuid();
        var usuario = new Cliente
        {
            Id = id,
            Nombre = "Juan",
            Apellido = "Pérez",
            Email = "juan@example.com",
            Rol = "Cliente",
            Activo = true
        };

        var reservaMock = new Mock<IReservaRepository>();
        var sut = new UsuarioService(_repoMock.Object, reservaMock.Object);

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        reservaMock.Setup(r => r.GetAllAsync(null, id, null, null))
            .ReturnsAsync(new List<Reserva>
            {
                new Reserva
                {
                    Id = Guid.NewGuid(),
                    ClienteId = id,
                    Estado = Shared.Enums.ReservaEstado.Confirmada,
                    FechaHora = DateTime.Today.AddHours(20)
                }
            });

        // Act
        var resultado = await sut.GetContextoInicioAsync(id, "Cliente");

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Juan Pérez", resultado.NombreCompleto);
        Assert.Equal("Cliente", resultado.Rol);
        Assert.Contains("Mis Reservas", resultado.AccesosRapidos);
        Assert.NotNull(resultado.ResumenCliente);
        Assert.Equal(1, resultado.ResumenCliente.TotalReservasActivas);
        Assert.Single(resultado.ResumenCliente.ProximasReservas);
    }

    [Fact]
    public async Task GetContextoInicioAsync_Gerente_RetornaMetricasDelSalonYAccesosRapidos()
    {
        // Arrange
        var id = Guid.NewGuid();
        var usuario = new Cliente
        {
            Id = id,
            Nombre = "Carlos",
            Apellido = "Gerente",
            Email = "gerente@example.com",
            Rol = "Gerente",
            Activo = true
        };

        var reservaMock = new Mock<IReservaRepository>();
        var mesaMock = new Mock<IMesaRepository>();
        var sut = new UsuarioService(_repoMock.Object, reservaMock.Object, mesaMock.Object);

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        mesaMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync(new List<Mesa>
            {
                new Mesa { Id = Guid.NewGuid(), Numero = 1, Estado = Shared.Enums.MesaEstado.Libre, Activa = true },
                new Mesa { Id = Guid.NewGuid(), Numero = 2, Estado = Shared.Enums.MesaEstado.Ocupada, Activa = true }
            });

        reservaMock.Setup(r => r.GetAllAsync(It.IsAny<DateTime>(), null, null, null))
            .ReturnsAsync(new List<Reserva> { new Reserva { Id = Guid.NewGuid() } });

        reservaMock.Setup(r => r.GetAllAsync(null, null, null, Shared.Enums.ReservaEstado.Pendiente))
            .ReturnsAsync(new List<Reserva> { new Reserva { Id = Guid.NewGuid() } });

        // Act
        var resultado = await sut.GetContextoInicioAsync(id, "Gerente");

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Gerente", resultado.Rol);
        Assert.Contains("Tablero Operativo", resultado.AccesosRapidos);
        Assert.NotNull(resultado.ResumenGerente);
        Assert.Equal(2, resultado.ResumenGerente.TotalMesas);
        Assert.Equal(1, resultado.ResumenGerente.MesasLibres);
        Assert.Equal(1, resultado.ResumenGerente.MesasOcupadas);
        Assert.Equal(1, resultado.ResumenGerente.ReservasHoy);
        Assert.Equal(1, resultado.ResumenGerente.ReservasPendientesListaEspera);
    }

    [Fact]
    public async Task GetContextoInicioAsync_Admin_RetornaMetricasGenerales()
    {
        // Arrange
        var id = Guid.NewGuid();
        var usuario = new Cliente
        {
            Id = id,
            Nombre = "Admin",
            Apellido = "Principal",
            Email = "admin@example.com",
            Rol = "Admin",
            Activo = true
        };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);
        _repoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<Cliente> { usuario, new Cliente { Id = Guid.NewGuid(), Activo = true } });
        _repoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(1);

        // Act
        var resultado = await _sut.GetContextoInicioAsync(id, "Admin");

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Admin", resultado.Rol);
        Assert.Contains("Gestión de Usuarios", resultado.AccesosRapidos);
        Assert.NotNull(resultado.ResumenAdmin);
        Assert.Equal(2, resultado.ResumenAdmin.TotalUsuarios);
        Assert.Equal(1, resultado.ResumenAdmin.AdminsActivos);
    }

    [Fact]
    public async Task GetContextoInicioAsync_UsuarioInexistente_LanzaNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Cliente?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetContextoInicioAsync(id));
    }

    [Fact]
    public async Task GetContextoInicioAsync_UsuarioInactivo_LanzaAccesoDenegadoException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var usuario = new Cliente { Id = id, Activo = false, Rol = "Cliente" };
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);

        // Act & Assert
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => _sut.GetContextoInicioAsync(id));
    }

    [Fact]
    public async Task GetContextoInicioAsync_RolInvalido_LanzaAccesoDenegadoException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var usuario = new Cliente { Id = id, Activo = true, Rol = "Invitado" };
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(usuario);

        // Act & Assert
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => _sut.GetContextoInicioAsync(id, "Invitado"));
    }
}
