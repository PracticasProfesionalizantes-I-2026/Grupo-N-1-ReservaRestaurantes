using BusinessLogic.Mesas.Implementations;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Moq;
using Shared.DTOs.Mesas;
using Shared.Enums;
using Shared.Exceptions;

namespace Tests;

/// <summary>
/// Pruebas unitarias de MesaService.
/// Patrón AAA (Arrange / Act / Assert) con aislamiento total via Moq.
/// </summary>
public class MesaServiceTests
{
    // ─── Fixture común ────────────────────────────────────────────────────────
    private readonly Mock<IMesaRepository> _repoMock;
    private readonly MesaService _sut; // System Under Test

    public MesaServiceTests()
    {
        _repoMock = new Mock<IMesaRepository>();
        _sut = new MesaService(_repoMock.Object);
    }

    // =========================================================================
    // CreateAsync
    // =========================================================================

    [Fact]
    public async Task CreateAsync_CuandoNumeroNoExiste_RetornaDTO()
    {
        // Arrange
        var dto = new MesaCreateDTO { Numero = 5, Capacidad = 4 };
        var mesaCreada = new Mesa { Id = Guid.NewGuid(), Numero = 5, Capacidad = 4, Estado = MesaEstado.Libre, Activa = true };

        _repoMock.Setup(r => r.ExistsByNumeroAsync(5, null)).ReturnsAsync(false);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<Mesa>())).ReturnsAsync(mesaCreada);

        // Act
        var resultado = await _sut.CreateAsync(dto);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(5, resultado.Numero);
        Assert.Equal(4, resultado.Capacidad);
        Assert.True(resultado.Activa);
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<Mesa>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_CuandoNumeroYaExiste_LanzaConflictException()
    {
        // Arrange
        var dto = new MesaCreateDTO { Numero = 5, Capacidad = 4 };
        _repoMock.Setup(r => r.ExistsByNumeroAsync(5, null)).ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateAsync(dto));
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<Mesa>()), Times.Never);
    }

    // =========================================================================
    // ModificarMesaAsync
    // =========================================================================

    [Fact]
    public async Task ModificarMesaAsync_CuandoMesaExiste_ActualizaYRetornaTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mesaExistente = new Mesa { Id = id, Numero = 1, Capacidad = 2, Activa = true };
        var dto = new MesaUpdateDTO { Capacidad = 6, Activa = false };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(mesaExistente);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.ModificarMesaAsync(id, dto);

        // Assert
        Assert.True(resultado);
        Assert.Equal(6, mesaExistente.Capacidad);
        Assert.False(mesaExistente.Activa);
        _repoMock.Verify(r => r.UpdateAsync(mesaExistente), Times.Once);
    }

    [Fact]
    public async Task ModificarMesaAsync_CuandoMesaNoExiste_RetornaFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Mesa?)null);

        // Act
        var resultado = await _sut.ModificarMesaAsync(id, new MesaUpdateDTO { Capacidad = 4, Activa = true });

        // Assert
        Assert.False(resultado);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Mesa>()), Times.Never);
    }

    // =========================================================================
    // EliminarMesaAsync
    // =========================================================================

    [Fact]
    public async Task EliminarMesaAsync_CuandoMesaExisteSinReservas_EliminaYRetornaTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(new Mesa { Id = id });
        _repoMock.Setup(r => r.HasReservationsAsync(id)).ReturnsAsync(false);
        _repoMock.Setup(r => r.DeleteAsync(id)).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.EliminarMesaAsync(id);

        // Assert
        Assert.True(resultado);
        _repoMock.Verify(r => r.DeleteAsync(id), Times.Once);
    }

    [Fact]
    public async Task EliminarMesaAsync_CuandoMesaNoExiste_RetornaFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Mesa?)null);

        // Act
        var resultado = await _sut.EliminarMesaAsync(id);

        // Assert
        Assert.False(resultado);
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task EliminarMesaAsync_CuandoTieneReservas_LanzaConflictException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(new Mesa { Id = id });
        _repoMock.Setup(r => r.HasReservationsAsync(id)).ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _sut.EliminarMesaAsync(id));
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    // =========================================================================
    // CambiarEstadoMesaAsync
    // =========================================================================

    [Fact]
    public async Task CambiarEstadoMesaAsync_CuandoMesaNoExiste_RetornaNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Mesa?)null);

        // Act
        var resultado = await _sut.CambiarEstadoMesaAsync(id, MesaEstado.Ocupada);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task CambiarEstadoMesaAsync_ALibreSinReservaEnCurso_ActualizaEstado()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mesa = new Mesa { Id = id, Numero = 3, Capacidad = 4, Estado = MesaEstado.Ocupada, Activa = true };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(mesa);
        _repoMock.Setup(r => r.HasReservaEnCursoAsync(id)).ReturnsAsync(false);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CambiarEstadoMesaAsync(id, MesaEstado.Libre);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(MesaEstado.Libre, resultado.Estado);
        _repoMock.Verify(r => r.UpdateAsync(mesa), Times.Once);
    }

    [Fact]
    public async Task CambiarEstadoMesaAsync_ALibreConReservaEnCurso_LanzaMesaConReservaActivaException()
    {
        // Arrange  (RN-03)
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(new Mesa { Id = id });
        _repoMock.Setup(r => r.HasReservaEnCursoAsync(id)).ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<MesaConReservaActivaException>(
            () => _sut.CambiarEstadoMesaAsync(id, MesaEstado.Libre));
    }

    [Fact]
    public async Task CambiarEstadoMesaAsync_AOcupada_NoConsultaReservaEnCurso()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mesa = new Mesa { Id = id, Numero = 2, Capacidad = 2, Estado = MesaEstado.Libre, Activa = true };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(mesa);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CambiarEstadoMesaAsync(id, MesaEstado.Ocupada);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(MesaEstado.Ocupada, resultado.Estado);
        _repoMock.Verify(r => r.HasReservaEnCursoAsync(It.IsAny<Guid>()), Times.Never);
    }

    // =========================================================================
    // FiltrarMapaMesasAsync
    // =========================================================================

    [Fact]
    public async Task FiltrarMapaMesasAsync_CuandoHayMesas_RetornaLista()
    {
        // Arrange
        var filtro = new MesaFiltroDTO { Estado = MesaEstado.Libre, SoloActivas = true };
        var mesas = new List<Mesa>
        {
            new() { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2, Estado = MesaEstado.Libre, Activa = true },
            new() { Id = Guid.NewGuid(), Numero = 2, Capacidad = 4, Estado = MesaEstado.Libre, Activa = true }
        };

        _repoMock.Setup(r => r.GetFiltroAsync(filtro.Estado, filtro.CapacidadMinima, filtro.SoloActivas))
                 .ReturnsAsync(mesas);

        // Act
        var resultado = (await _sut.FiltrarMapaMesasAsync(filtro)).ToList();

        // Assert
        Assert.Equal(2, resultado.Count);
        Assert.All(resultado, m => Assert.Equal(MesaEstado.Libre, m.Estado));
    }

    [Fact]
    public async Task FiltrarMapaMesasAsync_CuandoNoHayMesas_LanzaSinMesasDisponiblesException()
    {
        // Arrange
        var filtro = new MesaFiltroDTO { Estado = MesaEstado.Ocupada };
        _repoMock.Setup(r => r.GetFiltroAsync(filtro.Estado, filtro.CapacidadMinima, filtro.SoloActivas))
                 .ReturnsAsync(new List<Mesa>());

        // Act & Assert
        await Assert.ThrowsAsync<SinMesasDisponiblesException>(() => _sut.FiltrarMapaMesasAsync(filtro));
    }
}
