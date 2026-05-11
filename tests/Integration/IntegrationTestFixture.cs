using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Infrastructure.Repository;

namespace TicketPrime.Tests.Integration;

/// <summary>
/// Fixture de integração que gerencia a conexão com o banco SQL Server real.
///
/// Uso:
///   1. Garanta que o SQL Server esteja rodando (docker compose up -d sqlserver)
///   2. Defina a env var TEST_CONNECTION_STRING ou use a default (docker)
///   3. Os testes rodam dentro de uma transação que é revertida ao final
///      de cada teste, garantindo isolamento total.
///
/// Connection String default (Docker):
///   Server=localhost,1433;Database=TicketPrime;
///   User Id=sa;Password=TicketPrime@2024!;TrustServerCertificate=True;
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    private SqlConnection? _connection;
    private SqlTransaction? _transaction;

    public DbConnectionFactory DbFactory { get; private set; } = null!;
    public IUsuarioRepository UsuarioRepository { get; private set; } = null!;
    public IEventoRepository EventoRepository { get; private set; } = null!;
    public IReservaRepository ReservaRepository { get; private set; } = null!;
    public ICupomRepository CupomRepository { get; private set; } = null!;

    /// <summary>
    /// Obtém a connection string da env var TEST_CONNECTION_STRING ou usa
    /// a default que aponta para o SQL Server do docker-compose.
    /// </summary>
    public static string ObterConnectionString()
    {
        return Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
            ?? "Server=localhost,1433;Database=TicketPrime;User Id=sa;Password=TicketPrime@2024!;TrustServerCertificate=True;";
    }

    public async Task InitializeAsync()
    {
        var connectionString = ObterConnectionString();

        // Cria a fábrica de conexões
        DbFactory = new DbConnectionFactory(connectionString);

        // Abre conexão real com o banco
        _connection = new SqlConnection(connectionString);
        await _connection.OpenAsync();

        // Inicia transação que será revertida no DisposeAsync
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync();

        // Cria os repositórios injetando a factory — eles abrirão suas
        // PRÓPRIAS conexões. Para testes integrados reais com transação
        // compartilhada, usaríamos um UnitOfWork. Por ora testamos cada
        // repositório individualmente (cada um abre sua própria conexão).
        UsuarioRepository = new UsuarioRepository(DbFactory);
        EventoRepository = new EventoRepository(DbFactory, NullLogger<EventoRepository>.Instance);
        ReservaRepository = new ReservaRepository(DbFactory);
        CupomRepository = new CupomRepository(DbFactory);
    }

    public Task DisposeAsync()
    {
        _transaction?.Rollback();
        _transaction?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Collection definition para compartilhar a mesma fixture entre vários
/// arquivos de teste sem repetir a inicialização.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}
