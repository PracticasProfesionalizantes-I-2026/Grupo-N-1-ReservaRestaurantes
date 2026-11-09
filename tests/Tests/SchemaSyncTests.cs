using System.Data.Common;
using DataAccess.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

public class SchemaSyncTests
{
    [Fact]
    public void DatabaseMigrate_ProducesSameSchemaAs_EnsureCreated()
    {
        var ensureDbPath = Path.Combine(Path.GetTempPath(), $"ensure_{Guid.NewGuid()}.db");
        var migrateDbPath = Path.Combine(Path.GetTempPath(), $"migrate_{Guid.NewGuid()}.db");

        try
        {
            // 1. Crear con EnsureCreated
            var ensureOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseSqlite($"Data Source={ensureDbPath}")
                .Options;

            using (var ctx = new RestaurantDbContext(ensureOptions))
            {
                ctx.Database.EnsureCreated();
            }

            // 2. Crear con Migrations (Migrate)
            var migrateOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseSqlite($"Data Source={migrateDbPath}", b => b.MigrationsAssembly("Migrations"))
                .Options;

            using (var ctx = new RestaurantDbContext(migrateOptions))
            {
                ctx.Database.Migrate();
            }

            // 3. Comparar tablas y columnas
            var ensureColumns = GetColumns(ensureDbPath);
            var migrateColumns = GetColumns(migrateDbPath);

            Assert.Equal(ensureColumns.Keys.OrderBy(k => k), migrateColumns.Keys.OrderBy(k => k));

            foreach (var table in ensureColumns.Keys)
            {
                Assert.Equal(
                    ensureColumns[table].OrderBy(c => c),
                    migrateColumns[table].OrderBy(c => c)
                );
            }

            // 4. Comparar índices definidos
            var ensureIndexes = GetIndexes(ensureDbPath);
            var migrateIndexes = GetIndexes(migrateDbPath);

            Assert.Equal(ensureIndexes.OrderBy(i => i), migrateIndexes.OrderBy(i => i));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(ensureDbPath)) File.Delete(ensureDbPath);
            if (File.Exists(migrateDbPath)) File.Delete(migrateDbPath);
        }
    }

    [Fact]
    public async Task DbInitializer_InitializeAsync_AppliesMigrationsAndIsIdempotent()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"init_{Guid.NewGuid()}.db");
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", "AdminTest123!");
        Environment.SetEnvironmentVariable("GERENTE_PASSWORD", "GerenteTest123!");

        try
        {
            var options = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseSqlite($"Data Source={dbPath}", b => b.MigrationsAssembly("Migrations"))
                .Options;

            // Primera inicialización: debe migrar y sembrar
            using (var ctx = new RestaurantDbContext(options))
            {
                await DataAccess.Data.DbInitializer.InitializeAsync(ctx);
            }

            using (var ctx = new RestaurantDbContext(options))
            {
                var clientesCount = await ctx.Clientes.CountAsync();
                var mesasCount = await ctx.Mesas.CountAsync();
                Assert.True(clientesCount > 0);
                Assert.True(mesasCount > 0);
            }

            // Segunda inicialización: debe ser idempotente y no duplicar datos
            using (var ctx = new RestaurantDbContext(options))
            {
                await DataAccess.Data.DbInitializer.InitializeAsync(ctx);
            }

            using (var ctx = new RestaurantDbContext(options))
            {
                var clientesCount = await ctx.Clientes.CountAsync();
                var mesasCount = await ctx.Mesas.CountAsync();
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
