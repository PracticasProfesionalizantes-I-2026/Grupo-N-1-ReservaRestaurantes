using DataAccess.Context;
using DataAccess.Entities;
using DataAccess.Repositories.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Enums;
using Shared.Exceptions;

namespace Tests;

/// <summary>
/// RNF-06: Tests de condición de carrera al reservar.
/// Verifican que el índice único parcial (MesaId, FechaHora) y la transacción
/// serializable impiden la doble asignación de una mesa en el mismo horario.
/// Se usa SQLite con archivo en disco para activar la restricción de índice real
/// (el proveedor InMemory no valida índices únicos).
/// </summary>
public class ReservaConcurrenciaTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContextOptions<RestaurantDbContext> _options;

    public ReservaConcurrenciaTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"concurrencia_{Guid.NewGuid()}.db");
        _options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        // EnsureCreated incluye el índice único parcial definido en OnModelCreating.
        // No se usa Migrate() para evitar cargar Migrations.dll (bloqueado por CI policy en este entorno).
        using var ctx = new RestaurantDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Cliente CrearCliente(string email = "test@test.com") => new()
    {
        Id = Guid.NewGuid(),
        Nombre = "Test",
        Apellido = "Usuario",
        Email = email,
        Telefono = "1234567890",
        PasswordHash = "$2a$11$placeholder_hash_for_test_only",
        Rol = "Cliente",
        Activo = true,
        FechaRegistro = DateTime.UtcNow
    };

    private static Mesa CrearMesa(int numero = 99) => new()
    {
        Id = Guid.NewGuid(),
        Numero = numero,
        Capacidad = 4,
        Estado = MesaEstado.Libre,
        Activa = true,
        Ubicacion = "Salon Test"
    };

    private static DateTime HorarioValido() =>
        DateTime.Today.AddDays(1).AddHours(13); // Mañana 13:00 — turno almuerzo

    private RestaurantDbContext NuevoContexto() => new(_options);

    // ─── Tests ───────────────────────────────────────────────────────────────

    /// <summary>
    /// RNF-06 (happy path): una sola solicitud en un horario libre
    /// debe persistirse sin errores.
    /// </summary>
    [Fact]
    public async Task InsertarReserva_MesaLibre_PersisteSinError()
    {
        // Arrange
        using var ctx = NuevoContexto();
        var cliente = CrearCliente("libre@test.com");
        var mesa = CrearMesa(101);
        ctx.Clientes.Add(cliente);
        ctx.Mesas.Add(mesa);
        await ctx.SaveChangesAsync();

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente.Id,
            MesaId = mesa.Id,
            FechaHora = HorarioValido(),
            CantidadComensales = 2,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = DateTime.UtcNow
        };

        // Act
        ctx.Reservas.Add(reserva);
        await ctx.SaveChangesAsync();

        // Assert
        using var ctxVerif = NuevoContexto();
        var guardada = await ctxVerif.Reservas.FindAsync(reserva.Id);
        Assert.NotNull(guardada);
        Assert.Equal(mesa.Id, guardada.MesaId);
        Assert.Equal(HorarioValido(), guardada.FechaHora);
    }

    /// <summary>
    /// RNF-06 (núcleo del issue): dos solicitudes que intentan insertar
    /// la misma mesa en el mismo horario deben resultar en que solo una
    /// tenga éxito; la segunda debe lanzar DbUpdateException debido al
    /// índice único parcial (MesaId, FechaHora) WHERE Estado NOT IN (4,5,6).
    /// </summary>
    [Fact]
    public async Task InsertarReserva_MismoMesaYHorario_SegundaFallaConDbUpdateException()
    {
        // Arrange — sembrar cliente y mesa compartidos
        var mesaId = Guid.Empty;
        var clienteId = Guid.Empty;

        using (var ctx = NuevoContexto())
        {
            var cliente = CrearCliente("carrera@test.com");
            var mesa = CrearMesa(102);
            ctx.Clientes.Add(cliente);
            ctx.Mesas.Add(mesa);
            await ctx.SaveChangesAsync();
            mesaId = mesa.Id;
            clienteId = cliente.Id;
        }

        var fechaHora = HorarioValido();

        // Primera reserva — debe tener éxito
        using (var ctx1 = NuevoContexto())
        {
            ctx1.Reservas.Add(new Reserva
            {
                Id = Guid.NewGuid(),
                ClienteId = clienteId,
                MesaId = mesaId,
                FechaHora = fechaHora,
                CantidadComensales = 2,
                DuracionEstimadaMinutos = 120,
                Estado = ReservaEstado.Pendiente,
                FechaCreacion = DateTime.UtcNow
            });
            await ctx1.SaveChangesAsync();
        }

        // Segunda reserva con misma (MesaId, FechaHora) y estado activo —
        // el índice único parcial debe rechazarla con DbUpdateException
        using var ctx2 = NuevoContexto();
        ctx2.Reservas.Add(new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            MesaId = mesaId,
            FechaHora = fechaHora,
            CantidadComensales = 3,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,  // estado activo → entra al índice
            FechaCreacion = DateTime.UtcNow
        });

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(
            () => ctx2.SaveChangesAsync());
    }

    /// <summary>
    /// RNF-06: una reserva cancelada (estado=5) no entra en el índice único,
    /// por lo que insertar otra reserva activa para la misma mesa/horario
    /// debe permitirse.
    /// </summary>
    [Fact]
    public async Task InsertarReserva_MismoMesaYHorarioConCancelada_PermiteNuevaReservaActiva()
    {
        // Arrange
        var mesaId = Guid.Empty;
        var clienteId = Guid.Empty;

        using (var ctx = NuevoContexto())
        {
            var cliente = CrearCliente("cancelada@test.com");
            var mesa = CrearMesa(103);
            ctx.Clientes.Add(cliente);
            ctx.Mesas.Add(mesa);
            await ctx.SaveChangesAsync();
            mesaId = mesa.Id;
            clienteId = cliente.Id;
        }

        var fechaHora = HorarioValido();

        // Insertar una reserva cancelada (excluida del índice por WHERE Estado NOT IN (4,5,6))
        using (var ctx1 = NuevoContexto())
        {
            ctx1.Reservas.Add(new Reserva
            {
                Id = Guid.NewGuid(),
                ClienteId = clienteId,
                MesaId = mesaId,
                FechaHora = fechaHora,
                CantidadComensales = 2,
                DuracionEstimadaMinutos = 120,
                Estado = ReservaEstado.Cancelada,  // fuera del índice → no bloquea
                FechaCreacion = DateTime.UtcNow
            });
            await ctx1.SaveChangesAsync();
        }

        // Insertar una reserva activa para el mismo slot — debe poder hacerse
        using var ctx2 = NuevoContexto();
        ctx2.Reservas.Add(new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            MesaId = mesaId,
            FechaHora = fechaHora,
            CantidadComensales = 3,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,
            FechaCreacion = DateTime.UtcNow
        });

        // Act & Assert — no debe lanzar excepción
        var exception = await Record.ExceptionAsync(() => ctx2.SaveChangesAsync());
        Assert.Null(exception);
    }

    /// <summary>
    /// RNF-06: ReservaService.SolicitarReservaAsync captura DbUpdateException
    /// producida por el índice único y la convierte en ConflictException.
    /// Se simula el caso donde ExecuteInTransactionAsync no está disponible
    /// (proveedor InMemory) pero el repositorio lanza DbUpdateException
    /// directamente desde CreateAsync.
    /// </summary>
    [Fact]
    public async Task SolicitarReservaAsync_DbUpdateExceptionEnCreate_LanzaConflictException()
    {
        // Arrange
        var reservaRepoMock = new Moq.Mock<DataAccess.Repositories.Interfaces.IReservaRepository>();
        var mesaRepoMock = new Moq.Mock<DataAccess.Repositories.Interfaces.IMesaRepository>();
        var clienteRepoMock = new Moq.Mock<DataAccess.Repositories.Interfaces.IClienteRepository>();

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "Ana",
            Apellido = "García",
            Email = "ana@test.com",
            Telefono = "1234567890",
            Rol = "Cliente",
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };
        var mesa = new Mesa
        {
            Id = Guid.NewGuid(),
            Numero = 5,
            Capacidad = 4,
            Estado = MesaEstado.Libre,
            Activa = true,
            Ubicacion = "Sala"
        };

        var fechaHora = HorarioValido();
        var dto = new Shared.DTOs.Reservas.ReservaSolicitudDTO
        {
            FechaHora = fechaHora,
            CantidadComensales = 2
        };

        // ExecuteInTransactionAsync ejecuta directamente la función (sin transacción real)
        reservaRepoMock
            .Setup(r => r.ExecuteInTransactionAsync(
                Moq.It.IsAny<Func<Task<Shared.DTOs.Reservas.ReservaResponseDTO>>>(),
                Moq.It.IsAny<System.Data.IsolationLevel>()))
            .Returns<Func<Task<Shared.DTOs.Reservas.ReservaResponseDTO>>, System.Data.IsolationLevel>(
                (op, _) => op());

        clienteRepoMock.Setup(r => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);

        reservaRepoMock
            .Setup(r => r.GetOverlappingReservationsByClienteAsync(
                cliente.Id, fechaHora, Moq.It.IsAny<DateTime>(), null))
            .ReturnsAsync(Enumerable.Empty<Reserva>());

        mesaRepoMock
            .Setup(r => r.GetFiltroAsync(null, 2, true))
            .ReturnsAsync(new[] { mesa });

        reservaRepoMock
            .Setup(r => r.GetOverlappingReservationsByMesaAsync(
                mesa.Id, fechaHora, Moq.It.IsAny<DateTime>(), null))
            .ReturnsAsync(Enumerable.Empty<Reserva>());

        // CreateAsync lanza DbUpdateException (simula violación de índice único)
        reservaRepoMock
            .Setup(r => r.CreateAsync(Moq.It.IsAny<Reserva>()))
            .ThrowsAsync(new DbUpdateException(
                "UNIQUE constraint failed: Reservas.MesaId, Reservas.FechaHora",
                new Microsoft.Data.Sqlite.SqliteException("UNIQUE constraint failed", 19)));

        var sut = new BusinessLogic.Reservas.Implementations.ReservaService(
            reservaRepoMock.Object,
            mesaRepoMock.Object,
            clienteRepoMock.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => sut.SolicitarReservaAsync(cliente.Id, dto));

        Assert.Contains("concurrente", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

