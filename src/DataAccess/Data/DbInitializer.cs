using DataAccess.Context;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace DataAccess.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(RestaurantDbContext context)
    {
        // Aplica migraciones pendientes en la base de datos SQLite
        await context.Database.MigrateAsync();

        // Si ya hay clientes cargados, la base ya fue inicializada
        if (await context.Clientes.AnyAsync())
        {
            return;
        }

        // 1. Clientes semilla
        var cliente1 = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "Juan",
            Apellido = "Pérez",
            Email = "juan.perez@example.com",
            Telefono = "+54 11 4455-6677",
            FechaRegistro = DateTime.UtcNow.AddDays(-30)
        };

        var cliente2 = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "María",
            Apellido = "González",
            Email = "maria.gonzalez@example.com",
            Telefono = "+54 11 5566-7788",
            FechaRegistro = DateTime.UtcNow.AddDays(-20)
        };

        var cliente3 = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "Carlos",
            Apellido = "Rodríguez",
            Email = "carlos.rodriguez@example.com",
            Telefono = "+54 11 6677-8899",
            FechaRegistro = DateTime.UtcNow.AddDays(-10)
        };

        var cliente4 = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = "Laura",
            Apellido = "Fernández",
            Email = "laura.fernandez@example.com",
            Telefono = "+54 11 7788-9900",
            FechaRegistro = DateTime.UtcNow.AddDays(-5)
        };

        await context.Clientes.AddRangeAsync(cliente1, cliente2, cliente3, cliente4);

        // 2. Mesas semilla
        var mesa1 = new Mesa
        {
            Id = Guid.NewGuid(),
            Numero = 1,
            Capacidad = 2,
            Ubicacion = "Salón Principal",
            Estado = MesaEstado.Libre,
            Activa = true
        };

        var mesa2 = new Mesa
        {
            Id = Guid.NewGuid(),
            Numero = 2,
            Capacidad = 4,
            Ubicacion = "Salón Principal",
            Estado = MesaEstado.Libre,
            Activa = true
        };

        var mesa3 = new Mesa
        {
            Id = Guid.NewGuid(),
            Numero = 3,
            Capacidad = 4,
            Ubicacion = "Salón Principal",
            Estado = MesaEstado.Libre,
            Activa = true
        };

        var mesa4 = new Mesa
        {
            Id = Guid.NewGuid(),
            Numero = 4,
            Capacidad = 6,
            Ubicacion = "Terraza",
            Estado = MesaEstado.Libre,
            Activa = true
        };

        var mesa5 = new Mesa
        {
            Id = Guid.NewGuid(),
            Numero = 5,
            Capacidad = 8,
            Ubicacion = "Salón VIP",
            Estado = MesaEstado.Libre,
            Activa = true
        };

        var mesa6 = new Mesa
        {
            Id = Guid.NewGuid(),
            Numero = 6,
            Capacidad = 2,
            Ubicacion = "Terraza",
            Estado = MesaEstado.Libre,
            Activa = true
        };

        var mesa7 = new Mesa
        {
            Id = Guid.NewGuid(),
            Numero = 7,
            Capacidad = 4,
            Ubicacion = "Terraza",
            Estado = MesaEstado.EnMantenimiento,
            Activa = false
        };

        await context.Mesas.AddRangeAsync(mesa1, mesa2, mesa3, mesa4, mesa5, mesa6, mesa7);

        // 3. Reservas semilla
        var reserva1 = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente1.Id,
            MesaId = mesa2.Id,
            FechaHora = DateTime.UtcNow.Date.AddDays(1).AddHours(21), // Mañana a las 21:00 UTC
            CantidadComensales = 3,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Confirmada,
            Observaciones = "Cena de cumpleaños cerca de la ventana",
            FechaCreacion = DateTime.UtcNow.AddDays(-1)
        };

        var reserva2 = new Reserva
        {
            Id = Guid.NewGuid(),
            ClienteId = cliente2.Id,
            MesaId = mesa4.Id,
            FechaHora = DateTime.UtcNow.Date.AddDays(2).AddHours(13), // Pasado mañana a las 13:00 UTC
            CantidadComensales = 5,
            DuracionEstimadaMinutos = 120,
            Estado = ReservaEstado.Pendiente,
            Observaciones = "Almuerzo familiar con trona para niño",
            FechaCreacion = DateTime.UtcNow.AddHours(-12)
        };

        await context.Reservas.AddRangeAsync(reserva1, reserva2);

        await context.SaveChangesAsync();
    }
}
