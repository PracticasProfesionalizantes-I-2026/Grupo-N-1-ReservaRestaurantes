using BusinessLogic.Reservas.Implementations;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Moq;
using Shared.DTOs.Reservas;
using Shared.Enums;
using Shared.Exceptions;

namespace Tests;

/// <summary>
/// Pruebas unitarias de ReservaService.
/// Patrón AAA (Arrange / Act / Assert) con aislamiento total via Moq.
/// </summary>
public class ReservaServiceTests
{
    // ─── Fixture común ────────────────────────────────────────────────────────
    private readonly Mock<IReservaRepository> _reservaRepoMock;
    private readonly Mock<IMesaRepository> _mesaRepoMock;
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly ReservaService _sut;

    public ReservaServiceTests()
    {
        _reservaRepoMock = new Mock<IReservaRepository>();
        _mesaRepoMock = new Mock<IMesaRepository>();
        _clienteRepoMock = new Mock<IClienteRepository>();
        _sut = new ReservaService(_reservaRepoMock.Object, _mesaRepoMock.Object, _clienteRepoMock.Object);
    }

    // ─── Helpers de datos de prueba ───────────────────────────────────────────
    private static Cliente CrearCliente() => new()
    {
        Id = Guid.NewGuid(),
        Nombre = "Juan",
        Apellido = "Pérez",
        Email = "juan@test.com",
        Telefono = "1234567890",
        Rol = "Cliente",
        Activo = true,
        FechaRegistro = DateTime.UtcNow
    };

    private static Mesa CrearMesa(int numero = 1, int capacidad = 4) => new()
    {
        Id = Guid.NewGuid(),
        Numero = numero,
        Capacidad = capacidad,
        Estado = MesaEstado.Libre,
        Activa = true,
        Ubicacion = "Salon principal"
    };

    private static DateTime FechaHoraValida() =>
        DateTime.Today.AddDays(1).AddHours(13); // Mañana al mediodía (turno almuerzo 12:00-15:30)

    // =========================================================================
    // SolicitarReservaAsync
    // =========================================================================

    [Fact]
    public async Task SolicitarReservaAsync_ConDatosValidos_CreaReservaYRetornaDTO()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 4);
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 3,
            Observaciones = "Sin mariscos"
        };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByClienteAsync(cliente.Id, dto.FechaHora, It.IsAny<DateTime>(), null))
            .ReturnsAsync(Enumerable.Empty<Reserva>());
        _mesaRepoMock.Setup(r => r.GetFiltroAsync(null, dto.CantidadComensales, true))
            .ReturnsAsync(new[] { mesa });
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(mesa.Id, dto.FechaHora, It.IsAny<DateTime>(), null))
            .ReturnsAsync(Enumerable.Empty<Reserva>());
        _reservaRepoMock.Setup(r => r.CreateAsync(It.IsAny<Reserva>()))
            .ReturnsAsync((Reserva r) =>
            {
                r.Cliente = cliente;
                r.Mesa = mesa;
                return r;
            });

        // Act
        var resultado = await _sut.SolicitarReservaAsync(cliente.Id, dto);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(cliente.Id, resultado.ClienteId);
        Assert.Equal(3, resultado.CantidadComensales);
        Assert.Equal(ReservaEstado.Pendiente, resultado.Estado);
        Assert.Equal(120, resultado.DuracionEstimadaMinutos);
        _reservaRepoMock.Verify(r => r.CreateAsync(It.IsAny<Reserva>()), Times.Once);
    }

    [Fact]
    public async Task SolicitarReservaAsync_ClienteNoExiste_LanzaNotFoundException()
    {
        // Arrange
        var clienteId = Guid.NewGuid();
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2
        };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(clienteId)).ReturnsAsync((Cliente?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.SolicitarReservaAsync(clienteId, dto));
        _reservaRepoMock.Verify(r => r.CreateAsync(It.IsAny<Reserva>()), Times.Never);
    }

    [Fact]
    public async Task SolicitarReservaAsync_HorarioFueraDeServicio_LanzaHorarioNoHabilitadoException()
    {
        // Arrange (RN-03: solo 12:00-15:30 o 20:00-23:30)
        var clienteId = Guid.NewGuid();
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = DateTime.Today.AddDays(1).AddHours(17), // 17:00 — fuera de servicio
            CantidadComensales = 2
        };

        // Act & Assert
        await Assert.ThrowsAsync<HorarioNoHabilitadoException>(
            () => _sut.SolicitarReservaAsync(clienteId, dto));
        _clienteRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SolicitarReservaAsync_SinMesasConCapacidadSuficiente_LanzaSinDisponibilidadException()
    {
        // Arrange
        var cliente = CrearCliente();
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 5
        };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByClienteAsync(cliente.Id, dto.FechaHora, It.IsAny<DateTime>(), null))
            .ReturnsAsync(Enumerable.Empty<Reserva>());
        _mesaRepoMock.Setup(r => r.GetFiltroAsync(null, 5, true))
            .ReturnsAsync(Enumerable.Empty<Mesa>()); // Sin mesas de esa capacidad

        // Act & Assert
        await Assert.ThrowsAsync<SinDisponibilidadException>(
            () => _sut.SolicitarReservaAsync(cliente.Id, dto));
    }

    [Fact]
    public async Task SolicitarReservaAsync_MesasSolapadas_LanzaSinDisponibilidadException()
    {
        // Arrange (RN-02: no solapamiento de horarios en la misma mesa)
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 4);
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2
        };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByClienteAsync(cliente.Id, dto.FechaHora, It.IsAny<DateTime>(), null))
            .ReturnsAsync(Enumerable.Empty<Reserva>());
        _mesaRepoMock.Setup(r => r.GetFiltroAsync(null, 2, true))
            .ReturnsAsync(new[] { mesa });
        // Simular que la mesa ya tiene una reserva en ese horario
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(mesa.Id, dto.FechaHora, It.IsAny<DateTime>(), null))
            .ReturnsAsync(new[] { new Reserva { Id = Guid.NewGuid(), MesaId = mesa.Id } });

        // Act & Assert
        await Assert.ThrowsAsync<SinDisponibilidadException>(
            () => _sut.SolicitarReservaAsync(cliente.Id, dto));
    }

    [Fact]
    public async Task SolicitarReservaAsync_ClienteConReservaActivaEnMismoTurno_LanzaReservaDuplicadaClienteException()
    {
        // Arrange
        var cliente = CrearCliente();
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2
        };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByClienteAsync(cliente.Id, dto.FechaHora, It.IsAny<DateTime>(), null))
            .ReturnsAsync(new[] { new Reserva { Id = Guid.NewGuid(), ClienteId = cliente.Id } });

        // Act & Assert
        await Assert.ThrowsAsync<ReservaDuplicadaClienteException>(
            () => _sut.SolicitarReservaAsync(cliente.Id, dto));
    }

    // =========================================================================
    // ConfirmarReservaAsync
    // =========================================================================

    [Fact]
    public async Task ConfirmarReservaAsync_ReservaPendiente_CambiaAConfirmada()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            MesaId = mesa.Id,
            Mesa = mesa,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = DateTime.UtcNow
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.ConfirmarReservaAsync(reserva.Id);

        // Assert
        Assert.Equal(ReservaEstado.Confirmada, resultado.Estado);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(res => res.Estado == ReservaEstado.Confirmada)), Times.Once);
    }

    [Fact]
    public async Task ConfirmarReservaAsync_ReservaNoExiste_LanzaReservaNoEncontradaException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _reservaRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Reserva?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ReservaNoEncontradaException>(
            () => _sut.ConfirmarReservaAsync(id));
    }

    [Fact]
    public async Task ConfirmarReservaAsync_ReservaYaConfirmada_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange (RN: solo Pendiente puede confirmarse)
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Estado = ReservaEstado.Confirmada,
            FechaHora = FechaHoraValida()
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.ConfirmarReservaAsync(reserva.Id));
    }

    // =========================================================================
    // IniciarReservaAsync
    // =========================================================================

    [Fact]
    public async Task IniciarReservaAsync_ReservaConfirmada_CambiaAEnCursoYMesaAOcupada()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            Estado = ReservaEstado.Confirmada,
            FechaHora = FechaHoraValida()
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.IniciarReservaAsync(reserva.Id);

        // Assert (RN-03: ambas entidades se actualizan)
        Assert.Equal(ReservaEstado.EnCurso, resultado.Estado);
        Assert.Equal(MesaEstado.Ocupada, mesa.Estado);
        _mesaRepoMock.Verify(r => r.UpdateAsync(It.Is<Mesa>(m => m.Estado == MesaEstado.Ocupada)), Times.Once);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(r => r.Estado == ReservaEstado.EnCurso)), Times.Once);
    }

    [Fact]
    public async Task IniciarReservaAsync_ReservaPendiente_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange (RN-01: solo Confirmada puede iniciarse)
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Estado = ReservaEstado.Pendiente,
            FechaHora = FechaHoraValida()
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.IniciarReservaAsync(reserva.Id));
    }

    // =========================================================================
    // FinalizarReservaAsync
    // =========================================================================

    [Fact]
    public async Task FinalizarReservaAsync_ReservaEnCurso_CambiaAFinalizadaYLiberaMesa()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Estado = MesaEstado.Ocupada;
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            Estado = ReservaEstado.EnCurso,
            FechaHora = FechaHoraValida()
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.FinalizarReservaAsync(reserva.Id);

        // Assert (RN-02: liberar la mesa)
        Assert.Equal(ReservaEstado.Finalizada, resultado.Estado);
        Assert.Equal(MesaEstado.Libre, mesa.Estado);
        _mesaRepoMock.Verify(r => r.UpdateAsync(It.Is<Mesa>(m => m.Estado == MesaEstado.Libre)), Times.Once);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(r => r.Estado == ReservaEstado.Finalizada)), Times.Once);
    }

    [Fact]
    public async Task FinalizarReservaAsync_ReservaPendiente_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Estado = ReservaEstado.Pendiente,
            FechaHora = FechaHoraValida()
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.FinalizarReservaAsync(reserva.Id));
    }

    // =========================================================================
    // CancelarReservaClienteAsync
    // =========================================================================

    [Fact]
    public async Task CancelarReservaClienteAsync_ReservaPendiente_CambiaACancelada()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Mesa = mesa,
            Estado = ReservaEstado.Pendiente,
            FechaHora = FechaHoraValida()
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CancelarReservaClienteAsync(reserva.Id, cliente.Id);

        // Assert
        Assert.Equal(ReservaEstado.Cancelada, resultado.Estado);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(r => r.Estado == ReservaEstado.Cancelada)), Times.Once);
    }

    [Fact]
    public async Task CancelarReservaClienteAsync_ConMotivo_AgregaMotivoenObservaciones()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Mesa = mesa,
            Estado = ReservaEstado.Confirmada,
            FechaHora = FechaHoraValida()
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);
        var dto = new ReservaCancelarDTO { Motivo = "Imprevisto familiar" };

        // Act
        var resultado = await _sut.CancelarReservaClienteAsync(reserva.Id, cliente.Id, dto);

        // Assert
        Assert.Equal(ReservaEstado.Cancelada, resultado.Estado);
        Assert.Contains("Imprevisto familiar", resultado.Observaciones);
    }

    [Fact]
    public async Task CancelarReservaClienteAsync_ReservaEnCurso_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange (RN-02: no cancelar EnCurso, Finalizada ni Cancelada)
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Estado = ReservaEstado.EnCurso,
            FechaHora = FechaHoraValida()
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.CancelarReservaClienteAsync(reserva.Id, Guid.NewGuid()));
    }

    // =========================================================================
    // GetByIdAsync
    // =========================================================================

    [Fact]
    public async Task GetByIdAsync_ReservaExiste_RetornaDTO()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reservaId = Guid.NewGuid();
        var reserva = new Reserva
        {
            Id = reservaId,
            Cliente = cliente,
            Mesa = mesa,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            Estado = ReservaEstado.Pendiente,
            DuracionEstimadaMinutos = 120,
            FechaCreacion = DateTime.UtcNow
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reservaId)).ReturnsAsync(reserva);

        // Act
        var resultado = await _sut.GetByIdAsync(reservaId);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(reservaId, resultado.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReservaNoExiste_RetornaNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _reservaRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Reserva?)null);

        // Act
        var resultado = await _sut.GetByIdAsync(id);

        // Assert
        Assert.Null(resultado);
    }

    // =========================================================================
    // CancelarReservaGerenteAsync
    // =========================================================================

    [Fact]
    public async Task CancelarReservaGerenteAsync_ReservaFinalizada_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Estado = ReservaEstado.Finalizada,
            FechaHora = FechaHoraValida()
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.CancelarReservaGerenteAsync(reserva.Id));
    }

    [Fact]
    public async Task CancelarReservaGerenteAsync_ReservaEnCurso_CancelajYLiberaMesa()
    {
        // Arrange (RN-03: liberar mesa)
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Estado = MesaEstado.Ocupada;
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            Estado = ReservaEstado.EnCurso,
            FechaHora = FechaHoraValida()
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CancelarReservaGerenteAsync(reserva.Id);

        // Assert
        Assert.Equal(ReservaEstado.Cancelada, resultado.Estado);
        Assert.Equal(MesaEstado.Libre, mesa.Estado);
        _mesaRepoMock.Verify(r => r.UpdateAsync(It.Is<Mesa>(m => m.Estado == MesaEstado.Libre)), Times.Once);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(r => r.Estado == ReservaEstado.Cancelada)), Times.Once);
    }

    // =========================================================================
    // RechazarReservaAsync
    // =========================================================================

    [Fact]
    public async Task RechazarReservaAsync_ReservaPendiente_CambiaACanceladaYLiberaMesa()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            Estado = ReservaEstado.Pendiente,
            FechaHora = FechaHoraValida()
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.RechazarReservaAsync(reserva.Id);

        // Assert
        Assert.Equal(ReservaEstado.Cancelada, resultado.Estado);
        _mesaRepoMock.Verify(r => r.UpdateAsync(It.Is<Mesa>(m => m.Estado == MesaEstado.Libre)), Times.Once);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(r => r.Estado == ReservaEstado.Cancelada)), Times.Once);
    }

    [Fact]
    public async Task RechazarReservaAsync_ReservaNoPendiente_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Estado = ReservaEstado.Confirmada,
            FechaHora = FechaHoraValida()
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.RechazarReservaAsync(reserva.Id));
    }

    // =========================================================================
    // GetListaEsperaAsync
    // =========================================================================

    [Fact]
    public async Task GetListaEsperaAsync_ConReservasPendientes_RetornaLista()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reservasPendientes = new List<Reserva>
        {
            new() { Id = Guid.NewGuid(), Cliente = cliente, Mesa = mesa, Estado = ReservaEstado.Pendiente, FechaHora = FechaHoraValida() }
        };

        _reservaRepoMock.Setup(r => r.GetAllAsync(null, null, null, ReservaEstado.Pendiente))
            .ReturnsAsync(reservasPendientes);

        // Act
        var resultado = await _sut.GetListaEsperaAsync();

        // Assert
        Assert.NotEmpty(resultado);
        Assert.Single(resultado);
    }

    [Fact]
    public async Task GetListaEsperaAsync_SinReservas_LanzaListaEsperaVaciaException()
    {
        // Arrange
        _reservaRepoMock.Setup(r => r.GetAllAsync(null, null, null, ReservaEstado.Pendiente))
            .ReturnsAsync(Enumerable.Empty<Reserva>());

        // Act & Assert
        await Assert.ThrowsAsync<ListaEsperaVaciaException>(
            () => _sut.GetListaEsperaAsync());
    }

    // =========================================================================
    // GetTableroOperativoAsync
    // =========================================================================

    [Fact]
    public async Task GetTableroOperativoAsync_ConReservasOperativas_RetornaDTOs()
    {
        // Arrange
        var fecha = DateTime.Today;
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reservas = new List<Reserva>
        {
            new() { Id = Guid.NewGuid(), Cliente = cliente, Mesa = mesa, FechaHora = fecha.AddHours(13), Estado = ReservaEstado.Confirmada }
        };

        _reservaRepoMock.Setup(r => r.GetReservasOperativasAsync(fecha))
            .ReturnsAsync(reservas);

        // Act
        var resultado = await _sut.GetTableroOperativoAsync(fecha);

        // Assert
        Assert.NotEmpty(resultado);
        Assert.Single(resultado);
    }

    [Fact]
    public async Task GetTableroOperativoAsync_SinReservas_LanzaSinReservasOperativasException()
    {
        // Arrange
        var fecha = DateTime.Today;
        _reservaRepoMock.Setup(r => r.GetReservasOperativasAsync(fecha))
            .ReturnsAsync(Enumerable.Empty<Reserva>());

        // Act & Assert
        await Assert.ThrowsAsync<SinReservasOperativasException>(
            () => _sut.GetTableroOperativoAsync(fecha));
    }
}
