using DataAccess.Context;
using DataAccess.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tests.IntegrationTests;

/// <summary>
/// Factory personalizada para tests de integración.
/// Reemplaza la DB real (SQLite en disco) por SQLite en memoria y
/// configura variables de entorno para JWT y contraseñas de seed.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Conexión que mantiene viva la base SQLite en memoria durante toda la vida del factory.
    // Si se cerrara al terminar el scope de configuración, el schema se perdería.
    private SqliteConnection? _keepAliveConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Variables de entorno para JWT y datos semilla
        Environment.SetEnvironmentVariable("JWT_SECRET", "IntegrationTestSecretKeyQueTieneMasDe32Caracteres!");
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", "Admin123!");
        Environment.SetEnvironmentVariable("GERENTE_PASSWORD", "Gerente123!");
        Environment.SetEnvironmentVariable("CLIENTE_PASSWORD", "Cliente123!");

        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remover el DbContext registrado con SQLite real
            services.RemoveAll(typeof(DbContextOptions<RestaurantDbContext>));
            services.RemoveAll(typeof(RestaurantDbContext));

            // Registrar DbContext con SQLite en memoria (conexión compartida)
            var connectionString = $"Data Source=IntegrationTest_{Guid.NewGuid()};Mode=Memory;Cache=Shared";

            // Abrir y mantener la conexión durante toda la vida del factory
            _keepAliveConnection = new SqliteConnection(connectionString);
            _keepAliveConnection.Open();

            services.AddDbContext<RestaurantDbContext>(options =>
                options.UseSqlite(connectionString, b => b.MigrationsAssembly("Migrations"))
                       .ConfigureWarnings(w => w.Ignore(
                           Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

            // Crear el schema y sembrar datos
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();

            // Usar migraciones (mismo camino que Program.cs) para que el arranque del host
            // no intente crear tablas que ya existen.
            db.Database.Migrate();

            // Sembrar datos iniciales
            DbInitializer.SeedOnlyAsync(db).GetAwaiter().GetResult();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection?.Dispose();
            _keepAliveConnection = null;
        }
    }
}

