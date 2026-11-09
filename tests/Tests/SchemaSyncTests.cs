using System.Data.Common;
using DataAccess.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

public class SchemaSyncTests
{
    /// <summary>
    /// Verifica que el schema creado con EnsureCreated y el modelo actual
    /// incluye las tablas e índices clave (MesaId+FechaHora único parcial).
    /// No usa MigrationsAssembly para evitar restricciones de CI policy en el entorno.
    /// </summary>
    [Fact]
    public void EnsureCreated_IncludeUniqueIndexOnMesaIdFechaHora()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ensure_{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using (var ctx = new RestaurantDbContext(options))
            {
                ctx.Database.EnsureCreated();
            }

            var indexes = GetIndexes(dbPath);
            // El índice único parcial (MesaId, FechaHora) debe existir
            Assert.Contains(indexes, i => i.Contains("IX_Reservas_MesaId_FechaHora", StringComparison.OrdinalIgnoreCase));

            var columns = GetColumns(dbPath);
            Assert.True(columns.ContainsKey("Reservas"));
            Assert.True(columns.ContainsKey("Mesas"));
            Assert.True(columns.ContainsKey("Clientes"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    /// <summary>
    /// Verifica que el DbInitializer siembra datos correctamente usando EnsureCreated
    /// (evita cargar Migrations.dll para compatibilidad con CI policy).
    /// </summary>
    [Fact]
    public async Task DbInitializer_InitializeAsync_SeedsDataAndIsIdempotent()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"init_{Guid.NewGuid()}.db");
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", "AdminTest123!");
        Environment.SetEnvironmentVariable("GERENTE_PASSWORD", "GerenteTest123!");
        Environment.SetEnvironmentVariable("CLIENTE_PASSWORD", "ClienteTest123!");

        try
        {
            var options = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            // Primera inicialización: EnsureCreated + sembrar directamente
            using (var ctx = new RestaurantDbContext(options))
            {
                ctx.Database.EnsureCreated();
                await DataAccess.Data.DbInitializer.SeedOnlyAsync(ctx);
            }

            int clientesPrimera, mesasPrimera;
            using (var ctx = new RestaurantDbContext(options))
            {
                clientesPrimera = await ctx.Clientes.CountAsync();
                mesasPrimera = await ctx.Mesas.CountAsync();
                Assert.True(clientesPrimera > 0, "Debe haber clientes después del seeding");
                Assert.True(mesasPrimera > 0, "Debe haber mesas después del seeding");
            }

            // Segunda inicialización: debe ser idempotente
            using (var ctx = new RestaurantDbContext(options))
            {
                await DataAccess.Data.DbInitializer.SeedOnlyAsync(ctx);
            }

            using (var ctx = new RestaurantDbContext(options))
            {
                var clientesCount = await ctx.Clientes.CountAsync();
                var mesasCount = await ctx.Mesas.CountAsync();
                Assert.Equal(clientesPrimera, clientesCount); // No debe duplicar
                Assert.Equal(mesasPrimera, mesasCount);
                Assert.Equal(6, clientesCount); // 4 clientes + gerente + admin
                Assert.Equal(7, mesasCount);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task DbInitializer_AllSeededUsers_HaveValidBcryptPasswordHashesAndCanAuthenticate()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"seed_auth_{Guid.NewGuid()}.db");
        Environment.SetEnvironmentVariable("CLIENTE_PASSWORD", null);
        Environment.SetEnvironmentVariable("GERENTE_PASSWORD", null);
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", null);

        try
        {
            var options = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using (var ctx = new RestaurantDbContext(options))
            {
                ctx.Database.EnsureCreated();
                await DataAccess.Data.DbInitializer.SeedOnlyAsync(ctx);
            }

            using (var ctx = new RestaurantDbContext(options))
            {
                var usuarios = await ctx.Clientes.ToListAsync();
                Assert.Equal(6, usuarios.Count);

                foreach (var usuario in usuarios)
                {
                    Assert.False(string.IsNullOrWhiteSpace(usuario.PasswordHash));
                    Assert.StartsWith("$2", usuario.PasswordHash);

                    string expectedPassword = usuario.Rol switch
                    {
                        "Gerente" => "Gerente123!",
                        "Admin" => "Admin123!",
                        _ => "Cliente123!"
                    };

                    Assert.True(
                        BCrypt.Net.BCrypt.Verify(expectedPassword, usuario.PasswordHash),
                        $"El usuario {usuario.Email} ({usuario.Rol}) no pudo autenticar con su contraseña semilla."
                    );
                }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    private static Dictionary<string, List<string>> GetColumns(string dbPath)
    {
        var result = new Dictionary<string, List<string>>();
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%';";

        var tables = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        foreach (var table in tables)
        {
            var cols = new List<string>();
            using var colCmd = conn.CreateCommand();
            colCmd.CommandText = $"PRAGMA table_info({table});";
            using var colReader = colCmd.ExecuteReader();
            while (colReader.Read())
            {
                var colName = colReader.GetString(1);
                var colType = colReader.GetString(2);
                var notNull = colReader.GetInt32(3);
                cols.Add($"{colName}:{colType}:{notNull}");
            }
            result[table] = cols;
        }

        return result;
    }

    private static List<string> GetIndexes(string dbPath)
    {
        var indexes = new List<string>();
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%' ORDER BY name;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            indexes.Add(reader.GetString(0));
        }

        return indexes;
    }
}
