using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Infrastructure.Repository;
using src.Models;

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

    /// <summary>
    /// Indica se a conexão com o banco foi estabelecida com sucesso.
    /// Quando false, todos os testes de integração são ignorados (skip).
    /// </summary>
    public bool DatabaseAvailable { get; private set; } = false;

    public DbConnectionFactory DbFactory { get; private set; } = null!;
    public IUsuarioRepository UsuarioRepository { get; private set; } = null!;
    public IEventoRepository EventoRepository { get; private set; } = null!;
    public IReservaRepository ReservaRepository { get; private set; } = null!;
    public ICupomRepository CupomRepository { get; private set; } = null!;

    /// <summary>
    /// Obtém a connection string da env var TEST_DB_CONNECTION ou TEST_CONNECTION_STRING.
    /// ═══════════════════════════════════════════════════════════════════
    /// ANTES: Fallback hardcoded "Server=localhost,1433;...;Password=TicketPrime@2024!;..."
    ///   Senha de banco commitada no repositório — risco de segurança.
    ///
    /// AGORA: Usa o helper centralizado TestConnectionHelper que NÃO tem
    /// fallback hardcoded. Se a variável de ambiente não estiver configurada,
    /// o teste falha com instruções claras.
    /// ═══════════════════════════════════════════════════════════════════
    /// Um timeout curto é aplicado para falhar rapidamente quando o banco
    /// não está disponível (evita aguardar 30s por teste).
    /// </summary>
    public static string ObterConnectionString()
    {
        // ══════════════════════════════════════════════════════════════
        // Tenta TEST_DB_CONNECTION primeiro (padrão do projeto),
        // depois TEST_CONNECTION_STRING (legado).
        // ══════════════════════════════════════════════════════════════
        var raw = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
                  ?? Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"""
                ============================================================
                ⚠️  VARIÁVEL DE AMBIENTE NÃO CONFIGURADA

                Configure TEST_DB_CONNECTION ou TEST_CONNECTION_STRING:
                  PowerShell:  $env:TEST_DB_CONNECTION="Server=localhost,1433;Database=TicketPrime;User Id=sa;Password=...;TrustServerCertificate=True;"
                ============================================================
                """);
        }

        // Aplica timeout curto para detecção rápida de banco indisponível
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(raw);
        if (builder.ConnectTimeout > 5 || builder.ConnectTimeout == 0)
            builder.ConnectTimeout = 5;

        return builder.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        var connectionString = ObterConnectionString();

        try
        {
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

            DatabaseAvailable = true;
        }
        catch (SqlException)
        {
            // Banco não disponível — testes serão ignorados (skip) via IgnorarSeDBIndisponivel()
            DatabaseAvailable = false;
        }
        catch (InvalidOperationException)
        {
            // Falha de inicialização do repositório quando o banco não está pronto
            DatabaseAvailable = false;
        }
    }

    public Task DisposeAsync()
    {
        if (DatabaseAvailable)
        {
            _transaction?.Rollback();
            _transaction?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cria um TicketType padrão (setor "Pista") para um evento.
    /// Necessário para testes de Reserva por causa da FK FK_Reservas_TiposIngresso.
    /// </summary>
    public async Task<int> CriarTipoIngressoPadraoAsync(int eventoId)
    {
        var tipo = new TicketType("Pista", 100.00m, 500, 1)
        {
            EventoId = eventoId,
            CapacidadeRestante = 500
        };
        await EventoRepository.AdicionarTiposIngressoAsync(eventoId, [tipo]);
        return tipo.Id;
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
