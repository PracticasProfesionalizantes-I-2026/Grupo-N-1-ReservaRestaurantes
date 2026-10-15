using BusinessLogic.Reservas.Implementations;
using DataAccess.Context;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Reservas;
using Shared.Enums;
using Shared.Exceptions;

namespace Tests;

/// <summary>
/// Pruebas unitarias de ReservaService.
/// Dado que el servicio consume RestaurantDbContext directamente,
/// se usa EF Core InMemory como sustituto de la persistencia real.
/// Patrón AAA (Arrange / Act / Assert).
/// </summary>
public class ReservaServiceTests
{
    // ─── Factory: base de datos en memoria aislada por test ──────────────────
    private static RestaurantDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // BD única por test
            .Options;
        return new RestaurantDbContext(options);
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
        DateTime.Today.AddDays(1).AddHours(13); // Mañana al mediodía (turno almuerzo)

    // =========================================================================
    // SolicitarReservaAsync
    // =========================================================================

    [Fact]
    public async Task SolicitarReservaAsync_ConDatosValidos_CreaReservaYRetornaDTO()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 4);
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 3,
            Observaciones = "Sin mariscos"
        };

        // Act
        var resultado = await sut.SolicitarReservaAsync(cliente.Id, dto);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(cliente.Id, resultado.ClienteId);
        Assert.Equal(3, resultado.CantidadComensales);
        Assert.Equal(ReservaEstado.Pendiente, resultado.Estado);
        Assert.Equal(120, resultado.DuracionEstimadaMinutos);
    }

    [Fact]
    public async Task SolicitarReservaAsync_ClienteNoExiste_LanzaNotFoundException()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var sut = new ReservaService(ctx);
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.SolicitarReservaAsync(Guid.NewGuid(), dto));
    }

    [Fact]
    public async Task SolicitarReservaAsync_HorarioFueraDeServicio_LanzaHorarioNoHabilitadoException()
    {
        // Arrange  (RN-03: solo 12:00-15:30 o 20:00-23:30)
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        ctx.Clientes.Add(cliente);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = DateTime.Today.AddDays(1).AddHours(17), // 17:00 — fuera de servicio
            CantidadComensales = 2
        };

        // Act & Assert
        await Assert.ThrowsAsync<HorarioNoHabilitadoException>(
            () => sut.SolicitarReservaAsync(cliente.Id, dto));
    }

    [Fact]
    public async Task SolicitarReservaAsync_SinMesasConCapacidadSuficiente_LanzaSinDisponibilidadException()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 2); // capacidad insuficiente para 5 personas
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = FechaHoraValida(),
            CantidadComensales = 5
        };

        // Act & Assert
        await Assert.ThrowsAsync<SinDisponibilidadException>(
            () => sut.SolicitarReservaAsync(cliente.Id, dto));
    }

    [Fact]
    public async Task SolicitarReservaAsync_MesasSolapadas_LanzaSinDisponibilidadException()
    {
        // Arrange  (RN-02: no solapamiento de horarios en la misma mesa)
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa(capacidad: 4);
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var fechaHora = FechaHoraValida();
        // Agregar reserva existente que ocupa la misma franja
        var reservaExistente = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = fechaHora.AddMinutes(-30), // 30 min antes → se solapa
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Confirmada,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reservaExistente);
        await ctx.SaveChangesAsync();

        // Necesitamos un segundo cliente para la nueva reserva
        var cliente2 = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "María",
            Apellido = "López",
            Email = "maria@test.com",
            Telefono = "9876543210",
            Rol = "Cliente",
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };
        ctx.Clientes.Add(cliente2);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = fechaHora,
            CantidadComensales = 2
        };

        // Act & Assert
        await Assert.ThrowsAsync<SinDisponibilidadException>(
            () => sut.SolicitarReservaAsync(cliente2.Id, dto));
    }

    [Fact]
    public async Task SolicitarReservaAsync_ClienteConReservaActivaEnMismoTurno_LanzaReservaDuplicadaClienteException()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa1 = CrearMesa(numero: 1, capacidad: 4);
        var mesa2 = CrearMesa(numero: 2, capacidad: 4);
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa1);
        ctx.Mesas.Add(mesa2);

        var fechaHora = FechaHoraValida();
        var reservaActiva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa1.Id,
            FechaHora = fechaHora,
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Confirmada,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reservaActiva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);
        var dto = new ReservaSolicitudDTO
        {
            FechaHora = fechaHora.AddMinutes(30), // dentro del solapamiento
            CantidadComensales = 2
        };

        // Act & Assert
        await Assert.ThrowsAsync<ReservaDuplicadaClienteException>(
            () => sut.SolicitarReservaAsync(cliente.Id, dto));
    }

    // =========================================================================
    // ConfirmarReservaAsync
    // =========================================================================

    [Fact]
    public async Task ConfirmarReservaAsync_ReservaPendiente_CambiaAConfirmada()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act
        var resultado = await sut.ConfirmarReservaAsync(reserva.Id);

        // Assert
        Assert.Equal(ReservaEstado.Confirmada, resultado.Estado);
    }

    [Fact]
    public async Task ConfirmarReservaAsync_ReservaNoExiste_LanzaReservaNoEncontradaException()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var sut = new ReservaService(ctx);

        // Act & Assert
        await Assert.ThrowsAsync<ReservaNoEncontradaException>(
            () => sut.ConfirmarReservaAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ConfirmarReservaAsync_ReservaYaConfirmada_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange  (RN: solo Pendiente puede confirmarse)
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Confirmada, // ya confirmada
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => sut.ConfirmarReservaAsync(reserva.Id));
    }

    // =========================================================================
    // IniciarReservaAsync
    // =========================================================================

    [Fact]
    public async Task IniciarReservaAsync_ReservaConfirmada_CambiaAEnCursoYMesaAOcupada()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Confirmada,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act
        var resultado = await sut.IniciarReservaAsync(reserva.Id);

        // Assert  (RN-03: ambas entidades se actualizan)
        Assert.Equal(ReservaEstado.EnCurso, resultado.Estado);
        var mesaActualizada = await ctx.Mesas.FindAsync(mesa.Id);
        Assert.Equal(MesaEstado.Ocupada, mesaActualizada!.Estado);
    }

    [Fact]
    public async Task IniciarReservaAsync_ReservaPendiente_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange  (RN-01: solo Confirmada puede iniciarse)
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => sut.IniciarReservaAsync(reserva.Id));
    }

    // =========================================================================
    // FinalizarReservaAsync
    // =========================================================================

    [Fact]
    public async Task FinalizarReservaAsync_ReservaEnCurso_CambiaAFinalizadaYLiberaMesa()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Estado = MesaEstado.Ocupada;
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.EnCurso,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act
        var resultado = await sut.FinalizarReservaAsync(reserva.Id);

        // Assert  (RN-02: liberar la mesa)
        Assert.Equal(ReservaEstado.Finalizada, resultado.Estado);
        var mesaActualizada = await ctx.Mesas.FindAsync(mesa.Id);
        Assert.Equal(MesaEstado.Libre, mesaActualizada!.Estado);
    }

    [Fact]
    public async Task FinalizarReservaAsync_ReservaPendiente_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => sut.FinalizarReservaAsync(reserva.Id));
    }

    // =========================================================================
    // CancelarReservaClienteAsync
    // =========================================================================

    [Fact]
    public async Task CancelarReservaClienteAsync_ReservaPendiente_CambiaACancelada()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act
        var resultado = await sut.CancelarReservaClienteAsync(reserva.Id, cliente.Id);

        // Assert
        Assert.Equal(ReservaEstado.Cancelada, resultado.Estado);
    }

    [Fact]
    public async Task CancelarReservaClienteAsync_ConMotivo_AgregaMotivoenObservaciones()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Confirmada,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);
        var dto = new ReservaCancelarDTO { Motivo = "Imprevisto familiar" };

        // Act
        var resultado = await sut.CancelarReservaClienteAsync(reserva.Id, cliente.Id, dto);

        // Assert
        Assert.Equal(ReservaEstado.Cancelada, resultado.Estado);
        Assert.Contains("Imprevisto familiar", resultado.Observaciones);
    }

    [Fact]
    public async Task CancelarReservaClienteAsync_ReservaEnCurso_LanzaTransicionEstadoInvalidaException()
    {
        // Arrange  (RN-02: no cancelar EnCurso, Finalizada ni Cancelada)
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.EnCurso,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => sut.CancelarReservaClienteAsync(reserva.Id, cliente.Id));
    }

    // =========================================================================
    // GetByIdAsync
    // =========================================================================

    [Fact]
    public async Task GetByIdAsync_ReservaExiste_RetornaDTO()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reservaId = Guid.NewGuid();
        ctx.Reservas.Add(new Reserva
        {
            Id = reservaId,
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act
        var resultado = await sut.GetByIdAsync(reservaId);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(reservaId, resultado.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReservaNoExiste_RetornaNull()
    {
        // Arrange
        await using var ctx = CrearContexto();
        var sut = new ReservaService(ctx);

        // Act
        var resultado = await sut.GetByIdAsync(Guid.NewGuid());

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
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Finalizada,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act & Assert
        await Assert.ThrowsAsync<TransicionEstadoInvalidaException>(
            () => sut.CancelarReservaGerenteAsync(reserva.Id));
    }

    [Fact]
    public async Task CancelarReservaGerenteAsync_ReservaEnCurso_CancelajYLiberaMesa()
    {
        // Arrange  (RN-03: liberar mesa)
        await using var ctx = CrearContexto();
        var cliente = CrearCliente();
        var mesa = CrearMesa();
        mesa.Estado = MesaEstado.Ocupada;
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = FechaHoraValida(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.EnCurso,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        var sut = new ReservaService(ctx);

        // Act
        var resultado = await sut.CancelarReservaGerenteAsync(reserva.Id);

        // Assert
        Assert.Equal(ReservaEstado.Cancelada, resultado.Estado);
        var mesaActualizada = await ctx.Mesas.FindAsync(mesa.Id);
        Assert.Equal(MesaEstado.Libre, mesaActualizada!.Estado);
    }
}
