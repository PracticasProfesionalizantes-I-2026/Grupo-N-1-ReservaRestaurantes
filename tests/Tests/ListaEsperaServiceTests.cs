using BusinessLogic.Notifications.Interfaces;
using BusinessLogic.Reservas.Implementations;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Moq;
using Shared.DTOs.Reservas;
using Shared.Enums;
using Shared.Exceptions;
using Xunit;

namespace Tests;

public class ListaEsperaServiceTests
{
    private readonly Mock<IReservaRepository> _reservaRepoMock;
    private readonly Mock<IMesaRepository> _mesaRepoMock;
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly ListaEsperaService _sut;

    public ListaEsperaServiceTests()
    {
        _reservaRepoMock = new Mock<IReservaRepository>();
        _mesaRepoMock = new Mock<IMesaRepository>();
        _clienteRepoMock = new Mock<IClienteRepository>();
        _notificationMock = new Mock<INotificationService>();

        _sut = new ListaEsperaService(
            _reservaRepoMock.Object,
            _mesaRepoMock.Object,
            _clienteRepoMock.Object,
            _notificationMock.Object);
    }

    private static Cliente CrearCliente(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Nombre = "Agustín",
        Apellido = "Pérez",
        Email = "agustin@example.com",
        Telefono = "+541144556677",
        Rol = "Cliente",
        Activo = true
    };

    private static Mesa CrearMesa(Guid? id = null, int capacidad = 4, int numero = 1, bool activa = true) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Numero = numero,
        Capacidad = capacidad,
        Ubicacion = "Salón Principal",
        Estado = MesaEstado.Libre,
        Activa = activa
    };

    // =========================================================================
    // CU-18 / RN-02: GetSolicitudesOrdenadasAsync
    // =========================================================================

    [Fact]
    public async Task GetSolicitudesOrdenadasAsync_OrdenLlegadaFIFO_RetornaSolicitudesOrdenadas()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        var ahora = DateTime.UtcNow;

        var reserva1 = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = ahora.AddMinutes(-30),
            FechaHora = ahora.AddHours(2)
        };

        var reserva2 = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            Mesa = mesa,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = ahora.AddMinutes(-10),
            FechaHora = ahora.AddHours(2)
        };

        _reservaRepoMock.Setup(r => r.GetAllAsync(null, null, null, ReservaEstado.Pendiente))
            .ReturnsAsync(new List<Reserva> { reserva2, reserva1 });

        // Act
        var resultado = (await _sut.GetSolicitudesOrdenadasAsync()).ToList();

        // Assert: Se respeta el orden FIFO por FechaCreacion
        Assert.Equal(2, resultado.Count);
        Assert.Equal(reserva1.Id, resultado[0].Id);
        Assert.Equal(reserva2.Id, resultado[1].Id);
    }

    [Fact]
    public async Task GetSolicitudesOrdenadasAsync_ListaVacia_RetornaColeccionVacia()
    {
        // Arrange
        _reservaRepoMock.Setup(r => r.GetAllAsync(null, null, null, ReservaEstado.Pendiente))
            .ReturnsAsync(new List<Reserva>());

        // Act
        var resultado = await _sut.GetSolicitudesOrdenadasAsync();

        // Assert
        Assert.Empty(resultado);
    }

    // =========================================================================
    // CU-18 / RF-13 / RN-03: PromoverSolicitudAsync
    // =========================================================================

    [Fact]
    public async Task PromoverSolicitudAsync_WithCompatibleTable_CreatesConfirmedReserva()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 4, numero: 5);
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Mesa = mesa,
            MesaId = mesa.Id,
            CantidadComensales = 4,
            Estado = ReservaEstado.Pendiente,
            FechaHora = DateTime.Today.AddHours(20),
            DuracionEstimadaMinutos = 120
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(
            mesa.Id, reserva.FechaHora, It.IsAny<DateTime>(), reserva.Id))
            .ReturnsAsync(new List<Reserva>());
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.PromoverSolicitudAsync(reserva.Id, mesa.Id);

        // Assert
        Assert.Equal(ReservaEstado.Confirmada, resultado.Estado);
        Assert.Equal(mesa.Id, resultado.MesaId);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(res => res.Estado == ReservaEstado.Confirmada && res.MesaId == mesa.Id)), Times.Once);
        _notificationMock.Verify(n => n.NotificarAsignacionMesaAsync(
            cliente.Id, cliente.Email, It.IsAny<string>(), mesa.Numero, reserva.FechaHora), Times.Once);
    }

    [Fact]
    public async Task PromoverSolicitudAsync_SinMesaId_AsignaMesaOptimaDisponible()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesaChica = CrearMesa(capacidad: 2, numero: 1);
        var mesaOptima = CrearMesa(capacidad: 4, numero: 2);
        var mesaGrande = CrearMesa(capacidad: 8, numero: 3);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            CantidadComensales = 4,
            Estado = ReservaEstado.Pendiente,
            FechaHora = DateTime.Today.AddHours(21),
            DuracionEstimadaMinutos = 120
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetFiltroAsync(null, 4, true))
            .ReturnsAsync(new List<Mesa> { mesaGrande, mesaOptima }); // Vienen desordenadas
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(
            mesaOptima.Id, reserva.FechaHora, It.IsAny<DateTime>(), reserva.Id))
            .ReturnsAsync(new List<Reserva>());
        _reservaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Reserva>())).Returns(Task.CompletedTask);

        // Act
        var resultado = await _sut.PromoverSolicitudAsync(reserva.Id, null);

        // Assert: Asigna mesaOptima (capacidad 4 menor que 8)
        Assert.Equal(ReservaEstado.Confirmada, resultado.Estado);
        Assert.Equal(mesaOptima.Id, resultado.MesaId);
        _reservaRepoMock.Verify(r => r.UpdateAsync(It.Is<Reserva>(res => res.Estado == ReservaEstado.Confirmada && res.MesaId == mesaOptima.Id)), Times.Once);
        _notificationMock.Verify(n => n.NotificarAsignacionMesaAsync(
            cliente.Id, cliente.Email, It.IsAny<string>(), mesaOptima.Numero, reserva.FechaHora), Times.Once);
    }

    [Fact]
    public async Task PromoverSolicitudAsync_WhenNotFound_ThrowsReservaNoEncontradaException()
    {
        // Arrange
        var idInexistente = Guid.NewGuid();
        _reservaRepoMock.Setup(r => r.GetByIdAsync(idInexistente)).ReturnsAsync((Reserva?)null);

        // Act & Assert (Paso 4a)
        await Assert.ThrowsAsync<ReservaNoEncontradaException>(
            () => _sut.PromoverSolicitudAsync(idInexistente));
    }

    [Fact]
    public async Task PromoverSolicitudAsync_ReservaNoPendiente_ThrowsTransicionEstadoInvalidaException()
    {
        // Arrange
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Estado = ReservaEstado.Confirmada
        };
        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => _sut.PromoverSolicitudAsync(reserva.Id));
    }

    [Fact]
    public async Task PromoverSolicitudAsync_WhenIncompatibleTable_ThrowsMesaNoCompatibleException()
    {
        // Arrange: Mesa con capacidad 2 para reserva de 4 comensales
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 2, numero: 1);
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            CantidadComensales = 4,
            Estado = ReservaEstado.Pendiente,
            FechaHora = DateTime.Today.AddHours(20)
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);

        // Act & Assert (Paso 5a)
        await Assert.ThrowsAsync<MesaNoCompatibleException>(
            () => _sut.PromoverSolicitudAsync(reserva.Id, mesa.Id));
    }

    [Fact]
    public async Task PromoverSolicitudAsync_MesaInactiva_ThrowsMesaNoCompatibleException()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 4, numero: 1, activa: false);
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            CantidadComensales = 4,
            Estado = ReservaEstado.Pendiente,
            FechaHora = DateTime.Today.AddHours(20)
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);

        // Act & Assert
        await Assert.ThrowsAsync<MesaNoCompatibleException>(
            () => _sut.PromoverSolicitudAsync(reserva.Id, mesa.Id));
    }

    [Fact]
    public async Task PromoverSolicitudAsync_MesaSolapada_ThrowsMesaNoCompatibleException()
    {
        // Arrange
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 4, numero: 1);
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            CantidadComensales = 4,
            Estado = ReservaEstado.Pendiente,
            FechaHora = DateTime.Today.AddHours(20),
            DuracionEstimadaMinutos = 120
        };

        var reservaSolapada = new Reserva
        {
            Id = Guid.NewGuid(),
            MesaId = mesa.Id,
            FechaHora = DateTime.Today.AddHours(20)
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetByIdAsync(mesa.Id)).ReturnsAsync(mesa);
        _reservaRepoMock.Setup(r => r.GetOverlappingReservationsByMesaAsync(
            mesa.Id, reserva.FechaHora, It.IsAny<DateTime>(), reserva.Id))
            .ReturnsAsync(new List<Reserva> { reservaSolapada });

        // Act & Assert
        await Assert.ThrowsAsync<MesaNoCompatibleException>(
            () => _sut.PromoverSolicitudAsync(reserva.Id, mesa.Id));
    }

    [Fact]
    public async Task PromoverSolicitudAsync_AutomaticoSinMesasDisponibles_ThrowsMesaNoCompatibleException()
    {
        // Arrange
        var cliente = CrearCliente();
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Cliente = cliente,
            CantidadComensales = 6,
            Estado = ReservaEstado.Pendiente,
            FechaHora = DateTime.Today.AddHours(20),
            DuracionEstimadaMinutos = 120
        };

        _reservaRepoMock.Setup(r => r.GetByIdAsync(reserva.Id)).ReturnsAsync(reserva);
        _mesaRepoMock.Setup(m => m.GetFiltroAsync(null, 6, true))
            .ReturnsAsync(new List<Mesa>()); // No hay mesas candidatas

        // Act & Assert
        await Assert.ThrowsAsync<MesaNoCompatibleException>(
            () => _sut.PromoverSolicitudAsync(reserva.Id, null));
    }
}

