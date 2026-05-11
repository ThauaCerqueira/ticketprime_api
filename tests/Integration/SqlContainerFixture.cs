using Microsoft.Data.SqlClient;
using src.Infrastructure;
using Testcontainers.MsSql;

namespace TicketPrime.Tests.Integration;

/// <summary>
/// Fixture que inicializa um SQL Server real dentro de um Docker container via
/// Testcontainers.MsSql. O container é criado uma vez por sessão de testes e
/// destruído ao final.
///
/// Para usar: referencia esta fixture com [Collection("SqlContainer")]
/// em vez de IntegrationTestFixture.
/// </summary>
public sealed class SqlContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("TicketPrime@TestContainers!")
        .Build();

    public DbConnectionFactory DbFactory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();
        DbFactory = new DbConnectionFactory(ConnectionString);

        // Apply database schema from the main script
        await ApplySchemaAsync();
    }

    private async Task ApplySchemaAsync()
    {
        var scriptPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "db", "script.sql");

        if (!File.Exists(scriptPath))
            return; // Schema application is optional in CI — skip if not found

        var script = await File.ReadAllTextAsync(scriptPath);

        // Split on GO statements (T-SQL batch separator)
        var batches = script.Split(
            new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO\n", "\nGO\r\n", "\r\nGO", "\nGO", "GO\r\n", "GO\n" },
            StringSplitOptions.RemoveEmptyEntries);

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = trimmed;
            cmd.CommandTimeout = 60;
            try { await cmd.ExecuteNonQueryAsync(); }
            catch { /* Ignore individual batch errors (e.g. already-exists) */ }
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

/// <summary>
/// xUnit collection definition so tests can share the same SQL container.
/// </summary>
[CollectionDefinition("SqlContainer")]
public class SqlContainerCollection : ICollectionFixture<SqlContainerFixture> { }
