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

        _reservaRepoMock
            .Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<ReservaResponseDTO>>>(), It.IsAny<System.Data.IsolationLevel>()))
            .Returns<Func<Task<ReservaResponseDTO>>, System.Data.IsolationLevel>((op, _) => op());

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
        var clienteId = Guid.NewGuid();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            Estado = ReservaEstado.EnCurso,
            FechaHora = FechaHoraValida()
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.CancelarReservaClienteAsync(reserva.Id, clienteId));
    }

    [Fact]
    public async Task CancelarReservaClienteAsync_ReservaDeOtroCliente_LanzaAccesoDenegadoException()
    {
        // Arrange (Prevención de IDOR: un cliente no puede cancelar la reserva de otro)
        var propietarioId = Guid.NewGuid();
        var atacanteId = Guid.NewGuid();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = propietarioId,
            Estado = ReservaEstado.Confirmada,
            FechaHora = FechaHoraValida()
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(
            () => _sut.CancelarReservaClienteAsync(reserva.Id, atacanteId));

        Assert.Contains("permisos para cancelar una reserva ajena", ex.Message);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Reserva>()), Times.Never);
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

    [Fact]
    public async Task GetByIdAsync_ClientePropietario_RetornaDTO()
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
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            Estado = ReservaEstado.Confirmada
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act
        var resultado = await _sut.GetByIdAsync(reserva.Id, cliente.Id, "Cliente");

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(reserva.Id, resultado.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ClienteAjeno_LanzaAccesoDenegadoException()
    {
        // Arrange (Prevención de IDOR en consulta de reserva)
        var propietario = CrearCliente();
        var atacanteId = Guid.NewGuid();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = propietario.Id,
            Cliente = propietario,
            Mesa = CrearMesa(),
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            Estado = ReservaEstado.Confirmada
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(
            () => _sut.GetByIdAsync(reserva.Id, atacanteId, "Cliente"));

        Assert.Contains("permisos para consultar una reserva ajena", ex.Message);
    }

    [Theory]
    [InlineData("Gerente")]
    [InlineData("Admin")]
    public async Task GetByIdAsync_GerenteOAdmin_RetornaReservaDeCualquierCliente(string rol)
    {
        // Arrange (Personal autorizado puede consultar cualquier reserva)
        var propietario = CrearCliente();
        var personalId = Guid.NewGuid();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = propietario.Id,
            Cliente = propietario,
            Mesa = CrearMesa(),
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            Estado = ReservaEstado.Confirmada
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act
        var resultado = await _sut.GetByIdAsync(reserva.Id, personalId, rol);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(reserva.Id, resultado.Id);
    }

    // =========================================================================
    // CancelarReservaGerenteAsync (CU-15: cancelación por gerente con motivo obligatorio)
    // =========================================================================

    [Fact]
    public async Task CancelarReservaGerenteAsync_SinMotivo_LanzaValidationException()
    {
        // Arrange
        var reserva = new Reserva { Id = Guid.NewGuid(), Estado = ReservaEstado.Confirmada };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.CancelarReservaGerenteAsync(reserva.Id, new ReservaCancelarDTO { Motivo = "" }));
    }

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
            () => _sut.CancelarReservaGerenteAsync(reserva.Id, new ReservaCancelarDTO { Motivo = "Cierre imprevisto" }));
    }

    [Fact]
    public async Task CancelarReservaGerenteAsync_ReservaRechazada_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Estado = ReservaEstado.Rechazada,
            FechaHora = FechaHoraValida()
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.CancelarReservaGerenteAsync(reserva.Id, new ReservaCancelarDTO { Motivo = "Cierre imprevisto" }));
    }

    [Fact]
    public async Task CancelarReservaGerenteAsync_ReservaEnCurso_CancelaYLiberaMesaYPersisteMotivo()
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
        var resultado = await _sut.CancelarReservaGerenteAsync(reserva.Id, new ReservaCancelarDTO { Motivo = "Emergencia en cocina" });

        // Assert
        Assert.Equal(ReservaEstado.Cancelada, resultado.Estado);
        Assert.Equal(MesaEstado.Libre, mesa.Estado);
        Assert.Contains("Emergencia en cocina", reserva.Observaciones);
        _mesaRepoMock.Verify(r => r.UpdateAsync(It.Is<Mesa>(m => m.Estado == MesaEstado.Libre)), Times.Once);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(r => r.Estado == ReservaEstado.Cancelada)), Times.Once);
    }

    // =========================================================================
    // RechazarReservaAsync (CU-16: motivo obligatorio y estado Rechazada)
    // =========================================================================

    [Fact]
    public async Task RechazarReservaAsync_SinMotivo_LanzaValidationException()
    {
        // Arrange
        var reserva = new Reserva { Id = Guid.NewGuid(), Estado = ReservaEstado.Pendiente };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RechazarReservaAsync(reserva.Id, new ReservaRechazarDTO { MotivoRechazo = "" }));
    }

    [Fact]
    public async Task RechazarReservaAsync_ReservaPendiente_CambiaARechazadaYLiberaMesaYPersisteMotivo()
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
        var dto = new ReservaRechazarDTO { MotivoRechazo = "Sin mesas disponibles en el turno solicitado" };
        var resultado = await _sut.RechazarReservaAsync(reserva.Id, dto);

        // Assert
        Assert.Equal(ReservaEstado.Rechazada, resultado.Estado);
        Assert.Contains("Sin mesas disponibles", reserva.Observaciones);
        _mesaRepoMock.Verify(r => r.UpdateAsync(It.Is<Mesa>(m => m.Estado == MesaEstado.Libre)), Times.Once);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(r => r.Estado == ReservaEstado.Rechazada)), Times.Once);
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
            () => _sut.RechazarReservaAsync(reserva.Id, new ReservaRechazarDTO { MotivoRechazo = "Ya confirmada" }));
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

    // =========================================================================
    // RF-07: ModificarReservaAsync
    // =========================================================================

    [Fact]
    public async Task ModificarReservaAsync_ReservaPendientePropia_ModificaExitosamente()
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
            Estado = ReservaEstado.Pendiente,
            DuracionEstimadaMinutos = 120
        };

        var nuevoHorario = FechaHoraValida().AddHours(1);
        var dto = new ReservaUpdateDTO
        {
            FechaHora = nuevoHorario,
            CantidadComensales = 4,
            Observaciones = "Cambiamos a 4 comensales"
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(mesa.Id, dto.FechaHora, It.IsAny<DateTime>(), reserva.Id))
            .ReturnsAsync(Enumerable.Empty<Reserva>());
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.ModificarReservaAsync(reserva.Id, dto, cliente.Id, "Cliente");

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(nuevoHorario, resultado.FechaHora);
        Assert.Equal(4, resultado.CantidadComensales);
        Assert.Equal("Cambiamos a 4 comensales", resultado.Observaciones);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Reserva>()), Times.Once);
    }

    [Fact]
    public async Task ModificarReservaAsync_ReservaAjenaPorCliente_LanzaAccesoDenegadoException()
    {
        // Arrange (IDOR check)
        var propietario = CrearCliente();
        var atacanteId = Guid.NewGuid();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = propietario.Id,
            Cliente = propietario,
            Mesa = CrearMesa(),
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            Estado = ReservaEstado.Confirmada
        };

        var dto = new ReservaUpdateDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<AccesoDenegadoException>(
            () => _sut.ModificarReservaAsync(reserva.Id, dto, atacanteId, "Cliente"));
    }

    [Fact]
    public async Task ModificarReservaAsync_ReservaFinalizada_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange
        var cliente = CrearCliente();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Mesa = CrearMesa(),
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            Estado = ReservaEstado.Finalizada
        };

        var dto = new ReservaUpdateDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.ModificarReservaAsync(reserva.Id, dto, cliente.Id, "Cliente"));
    }

    [Fact]
    public async Task ModificarReservaAsync_MesaConSolapamiento_LanzaMesaSolapadaException()
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
            Estado = ReservaEstado.Confirmada
        };

        var dto = new ReservaUpdateDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            MesaId = mesa.Id
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(mesa.Id, dto.FechaHora, It.IsAny<DateTime>(), reserva.Id))
            .ReturnsAsync(new List<Reserva> { new() { Id = Guid.NewGuid() } });

        // Act & Assert
        await Assert.ThrowsAsync<MesaSolapadaException>(
            () => _sut.ModificarReservaAsync(reserva.Id, dto, cliente.Id, "Cliente"));
    }

    [Fact]
    public async Task ModificarReservaAsync_GerenteOAdmin_ModificaCualquierReserva()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var gerenteId = Guid.NewGuid();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            MesaId = mesa.Id,
            Mesa = mesa,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            Estado = ReservaEstado.Confirmada,
            DuracionEstimadaMinutos = 120
        };

        var nuevoHorario = FechaHoraValida().AddHours(1);
        var dto = new ReservaUpdateDTO
        {
            FechaHora = nuevoHorario,
            CantidadComensales = 3
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(mesa.Id, dto.FechaHora, It.IsAny<DateTime>(), reserva.Id))
            .ReturnsAsync(Enumerable.Empty<Reserva>());
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.ModificarReservaAsync(reserva.Id, dto, gerenteId, "Gerente");

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(nuevoHorario, resultado.FechaHora);
        Assert.Equal(3, resultado.CantidadComensales);
    }

    // =========================================================================
    // RF-10: CambiarEstadoAsync (Máquina de estados de reservas)
    // =========================================================================

    [Fact]
    public async Task CambiarEstadoAsync_TransicionValidaPendienteAConfirmada_CambiaEstado()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            MesaId = mesa.Id,
            Estado = ReservaEstado.Pendiente,
            FechaHora = FechaHoraValida()
        };

        var dto = new ReservaCambiarEstadoDTO { NuevoEstado = ReservaEstado.Confirmada };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CambiarEstadoAsync(reserva.Id, dto);

        // Assert
        Assert.Equal(ReservaEstado.Confirmada, resultado.Estado);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(res => res.Estado == ReservaEstado.Confirmada)), Times.Once);
    }

    [Fact]
    public async Task CambiarEstadoAsync_TransicionValidaConfirmadaAEnCurso_OcupaMesa()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Estado = MesaEstado.Reservada;
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            MesaId = mesa.Id,
            Estado = ReservaEstado.Confirmada,
            FechaHora = FechaHoraValida()
        };

        var dto = new ReservaCambiarEstadoDTO { NuevoEstado = ReservaEstado.EnCurso };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CambiarEstadoAsync(reserva.Id, dto);

        // Assert
        Assert.Equal(ReservaEstado.EnCurso, resultado.Estado);
        _mesaRepoMock.Verify(m => m.UpdateAsync(It.Is<Mesa>(mesaUpdated => mesaUpdated.Estado == MesaEstado.Ocupada)), Times.Once);
    }

    [Fact]
    public async Task CambiarEstadoAsync_TransicionValidaEnCursoAFinalizada_LiberaMesa()
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
            MesaId = mesa.Id,
            Estado = ReservaEstado.EnCurso,
            FechaHora = FechaHoraValida()
        };

        var dto = new ReservaCambiarEstadoDTO { NuevoEstado = ReservaEstado.Finalizada };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CambiarEstadoAsync(reserva.Id, dto);

        // Assert
        Assert.Equal(ReservaEstado.Finalizada, resultado.Estado);
        _mesaRepoMock.Verify(m => m.UpdateAsync(It.Is<Mesa>(mesaUpdated => mesaUpdated.Estado == MesaEstado.Libre)), Times.Once);
    }

    [Fact]
    public async Task CambiarEstadoAsync_TransicionInvalidaFinalizadaACancelada_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Estado = ReservaEstado.Finalizada,
            FechaHora = FechaHoraValida()
        };

        var dto = new ReservaCambiarEstadoDTO { NuevoEstado = ReservaEstado.Cancelada };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.CambiarEstadoAsync(reserva.Id, dto));
    }

    [Fact]
    public async Task CambiarEstadoAsync_MismoEstado_NoOpYRetornaDTO()
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

        var dto = new ReservaCambiarEstadoDTO { NuevoEstado = ReservaEstado.Confirmada };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act
        var resultado = await _sut.CambiarEstadoAsync(reserva.Id, dto);

        // Assert
        Assert.Equal(ReservaEstado.Confirmada, resultado.Estado);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Reserva>()), Times.Never);
    }

    [Fact]
    public async Task CambiarEstadoAsync_ReservaInexistente_LanzaReservaNoEncontradaException()
    {
        // Arrange
        var reservaId = Guid.NewGuid();
        var dto = new ReservaCambiarEstadoDTO { NuevoEstado = ReservaEstado.Confirmada };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reservaId)).ReturnsAsync((Reserva?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ReservaNoEncontradaException>(
            () => _sut.CambiarEstadoAsync(reservaId, dto));
    }

    [Fact]
    public async Task CambiarEstadoAsync_TransicionAConfirmada_MesaInactiva_LanzaValidationException()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Activa = false;
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            MesaId = mesa.Id,
            Estado = ReservaEstado.Pendiente,
            FechaHora = FechaHoraValida()
        };

        var dto = new ReservaCambiarEstadoDTO { NuevoEstado = ReservaEstado.Confirmada };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.CambiarEstadoAsync(reserva.Id, dto));
    }

    [Fact]
    public async Task CambiarEstadoAsync_TransicionConfirmadaACancelada_LiberaMesa()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Estado = MesaEstado.Reservada;
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            MesaId = mesa.Id,
            Estado = ReservaEstado.Confirmada,
            FechaHora = FechaHoraValida()
        };

        var dto = new ReservaCambiarEstadoDTO { NuevoEstado = ReservaEstado.Cancelada };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CambiarEstadoAsync(reserva.Id, dto);

        // Assert
        Assert.Equal(ReservaEstado.Cancelada, resultado.Estado);
        _mesaRepoMock.Verify(m => m.UpdateAsync(It.Is<Mesa>(mesaUpdated => mesaUpdated.Estado == MesaEstado.Libre)), Times.Once);
    }

    [Fact]
    public async Task CambiarEstadoAsync_TransicionConfirmadaANoShow_LiberaMesa()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Estado = MesaEstado.Reservada;
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            MesaId = mesa.Id,
            Estado = ReservaEstado.Confirmada,
            FechaHora = FechaHoraValida()
        };

        var dto = new ReservaCambiarEstadoDTO { NuevoEstado = ReservaEstado.NoShow };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.UpdateAsync(It.IsAny<Mesa>())).Returns(Task.CompletedTask);
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.CambiarEstadoAsync(reserva.Id, dto);

        // Assert
        Assert.Equal(ReservaEstado.NoShow, resultado.Estado);
        _mesaRepoMock.Verify(m => m.UpdateAsync(It.Is<Mesa>(mesaUpdated => mesaUpdated.Estado == MesaEstado.Libre)), Times.Once);
    }

    // ─── Tests AsignarMesaAsync ──────────────────────────────────────────────
    [Fact]
    public async Task AsignarMesaAsync_MesaValida_AsignaCorrectamente()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa(numero: 5, capacidad: 4);
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            ClienteId = cliente.Id,
            CantidadComensales = 2,
            FechaHora = FechaHoraValida(),
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(
            mesa.Id, reserva.FechaHora, It.IsAny<DateTime>(), reserva.Id))
            .ReturnsAsync(new List<Reserva>());
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.AsignarMesaAsync(reserva.Id, mesa.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(mesa.Id, reserva.MesaId);
        Assert.Equal(mesa.Numero, result.MesaNumero);
        _mesaRepoMock.Verify(m => m.GetByIdAsync(mesa.Id), Times.Once);
        _mesaRepoMock.Verify(m => m.GetByNumeroAsync(It.IsAny<int>()), Times.Never);
        _reservaRepoMock.Verify(r => r.UpdateAsync(reserva), Times.Once);
    }

    [Fact]
    public async Task AsignarMesaAsync_ReservaInexistente_LanzaNotFoundException()
    {
        var reservaId = Guid.NewGuid();
        var mesaId = Guid.NewGuid();
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reservaId)).ReturnsAsync((Reserva?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AsignarMesaAsync(reservaId, mesaId));
    }

    [Fact]
    public async Task AsignarMesaAsync_MesaInexistente_LanzaNotFoundException()
    {
        var cliente = CrearCliente();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Estado = ReservaEstado.Pendiente
        };
        var mesaId = Guid.NewGuid();

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesaId)).ReturnsAsync((Mesa?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AsignarMesaAsync(reserva.Id, mesaId));
        _mesaRepoMock.Verify(m => m.GetByIdAsync(mesaId), Times.Once);
        _mesaRepoMock.Verify(m => m.GetByNumeroAsync(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(ReservaEstado.Finalizada)]
    [InlineData(ReservaEstado.Cancelada)]
    public async Task AsignarMesaAsync_ReservaFinalizadaOCancelada_LanzaTransicionEstadoInvalidaException(ReservaEstado estadoInvalido)
    {
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Estado = estadoInvalido
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);

        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(() => _sut.AsignarMesaAsync(reserva.Id, mesa.Id));
    }

    [Fact]
    public async Task AsignarMesaAsync_MesaInactiva_LanzaValidationException()
    {
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Activa = false;
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            CantidadComensales = 2,
            Estado = ReservaEstado.Pendiente
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);

        await Assert.ThrowsAsync<ValidationException>(() => _sut.AsignarMesaAsync(reserva.Id, mesa.Id));
    }

    [Fact]
    public async Task AsignarMesaAsync_MesaEnMantenimiento_LanzaValidationException()
    {
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Estado = MesaEstado.EnMantenimiento;
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            CantidadComensales = 2,
            Estado = ReservaEstado.Pendiente
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);

        await Assert.ThrowsAsync<ValidationException>(() => _sut.AsignarMesaAsync(reserva.Id, mesa.Id));
    }

    [Fact]
    public async Task AsignarMesaAsync_CapacidadInsuficiente_LanzaCapacidadInsuficienteException()
    {
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 2);
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            CantidadComensales = 4,
            Estado = ReservaEstado.Pendiente
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);

        await Assert.ThrowsAsync<CapacidadInsuficienteException>(() => _sut.AsignarMesaAsync(reserva.Id, mesa.Id));
    }

    [Fact]
    public async Task AsignarMesaAsync_MesaSolapada_LanzaMesaSolapadaException()
    {
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 4);
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            CantidadComensales = 2,
            FechaHora = FechaHoraValida(),
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(
            mesa.Id, reserva.FechaHora, It.IsAny<DateTime>(), reserva.Id))
            .ReturnsAsync(new List<Reserva> { new Reserva { Id = Guid.NewGuid() } });

        await Assert.ThrowsAsync<MesaSolapadaException>(() => _sut.AsignarMesaAsync(reserva.Id, mesa.Id));
    }
}
