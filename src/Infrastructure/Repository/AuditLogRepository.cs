using Dapper;
using src.Infrastructure.IRepository;
using src.Models;

namespace src.Infrastructure.Repository;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public AuditLogRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> InserirAsync(AuditLogEntry entry)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Obtém o hash da última entrada para encadeamento
        var ultimo = await ObterUltimoInternalAsync(connection);
        entry.PreviousHash = ultimo?.Hash ?? "0";

        // Calcula o hash desta entrada (incluindo PreviousHash)
        entry.Hash = entry.ComputeHash();

        const string sql = @"
            INSERT INTO AuditLog (
                Timestamp, ActionType, UsuarioCpf, EventoId, ReservaId,
                ValorTransacionado, IpAddress, UserAgent, Detalhes,
                PreviousHash, Hash
            ) VALUES (
                @Timestamp, @ActionType, @UsuarioCpf, @EventoId, @ReservaId,
                @ValorTransacionado, @IpAddress, @UserAgent, @Detalhes,
                @PreviousHash, @Hash
            );
            SELECT CAST(SCOPE_IDENTITY() AS INT)";

        var id = await connection.QuerySingleAsync<int>(sql, entry);
        entry.Id = id;
        return id;
    }

    public async Task<AuditLogEntry?> ObterUltimoAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await ObterUltimoInternalAsync(connection);
    }

    private static async Task<AuditLogEntry?> ObterUltimoInternalAsync(System.Data.IDbConnection connection)
    {
        return await connection.QueryFirstOrDefaultAsync<AuditLogEntry>(
            "SELECT TOP 1 * FROM AuditLog ORDER BY Id DESC");
    }

    public async Task<IEnumerable<AuditLogEntry>> ListarPorPeriodoAsync(DateTime inicio, DateTime fim)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<AuditLogEntry>(
            @"SELECT * FROM AuditLog
              WHERE Timestamp >= @Inicio AND Timestamp <= @Fim
              ORDER BY Id ASC",
            new { Inicio = inicio, Fim = fim });
    }

    public async Task<IEnumerable<AuditLogEntry>> ListarPorUsuarioAsync(string cpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<AuditLogEntry>(
            @"SELECT * FROM AuditLog
              WHERE UsuarioCpf = @Cpf
              ORDER BY Id DESC",
            new { Cpf = cpf });
    }

    public async Task<AuditLogEntry?> ObterPorIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<AuditLogEntry>(
            "SELECT * FROM AuditLog WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<AuditLogEntry>> ListarPorTipoAcaoAsync(string actionType, int limite = 100)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<AuditLogEntry>(
            @"SELECT TOP(@Limite) * FROM AuditLog
              WHERE ActionType = @ActionType
              ORDER BY Id DESC",
            new { ActionType = actionType, Limite = limite });
    }

    public async Task<bool> VerificarIntegridadeAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var entries = (await connection.QueryAsync<AuditLogEntry>(
            "SELECT * FROM AuditLog ORDER BY Id ASC")).AsList();

        if (entries.Count == 0)
            return true;

        string expectedPreviousHash = "0";

        foreach (var entry in entries)
        {
            // Verifica se o PreviousHash corresponde ao hash da entrada anterior
            if (entry.PreviousHash != expectedPreviousHash)
                return false;

            // Recalcula o hash e verifica se corresponde ao armazenado
            var computedHash = AuditLogEntry.ComputeHash(
                entry.Timestamp,
                entry.ActionType,
                entry.UsuarioCpf,
                entry.EventoId,
                entry.ReservaId,
                entry.ValorTransacionado,
                entry.IpAddress,
                entry.UserAgent,
                entry.Detalhes,
                entry.PreviousHash);

            if (computedHash != entry.Hash)
                return false;

            expectedPreviousHash = entry.Hash;
        }

        return true;
    }

    public async Task<long> ContarTotalAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<long>(
            "SELECT COUNT(*) FROM AuditLog");
    }
}
