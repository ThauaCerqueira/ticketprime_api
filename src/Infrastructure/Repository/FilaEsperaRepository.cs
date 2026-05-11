using Dapper;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Models;

namespace src.Infrastructure.Repository;

public class FilaEsperaRepository : IFilaEsperaRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public FilaEsperaRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> AdicionarAsync(WaitingQueue entrada)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"INSERT INTO FilaEspera (UsuarioCpf, EventoId, DataEntrada, Status)
                    VALUES (@UsuarioCpf, @EventoId, GETUTCDATE(), 'Aguardando');
                    SELECT CAST(SCOPE_IDENTITY() AS INT)";
        var id = await connection.QuerySingleAsync<int>(sql, new
        {
            entrada.UsuarioCpf,
            entrada.EventoId
        });
        return id;
    }

    public async Task<bool> RemoverAsync(int id, string usuarioCpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE FilaEspera
                    SET Status = 'Expirado'
                    WHERE Id = @Id AND UsuarioCpf = @UsuarioCpf AND Status = 'Aguardando'";
        var rows = await connection.ExecuteAsync(sql, new { Id = id, UsuarioCpf = usuarioCpf });
        return rows > 0;
    }

    public async Task<bool> RemoverPorCpfEEventoAsync(string usuarioCpf, int eventoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE FilaEspera
                    SET Status = 'Expirado'
                    WHERE UsuarioCpf = @UsuarioCpf AND EventoId = @EventoId AND Status = 'Aguardando'";
        var rows = await connection.ExecuteAsync(sql, new { UsuarioCpf = usuarioCpf, EventoId = eventoId });
        return rows > 0;
    }

    public async Task<bool> EstaNaFilaAsync(string usuarioCpf, int eventoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT COUNT(1) FROM FilaEspera
                    WHERE UsuarioCpf = @UsuarioCpf AND EventoId = @EventoId AND Status = 'Aguardando'";
        var count = await connection.QuerySingleAsync<int>(sql, new { UsuarioCpf = usuarioCpf, EventoId = eventoId });
        return count > 0;
    }

    public async Task<int> ContarPorEventoAsync(int eventoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT COUNT(*) FROM FilaEspera
                    WHERE EventoId = @EventoId AND Status = 'Aguardando'";
        return await connection.QuerySingleAsync<int>(sql, new { EventoId = eventoId });
    }

    public async Task<int> ObterPosicaoAsync(string usuarioCpf, int eventoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT COUNT(*) FROM FilaEspera
                    WHERE EventoId = @EventoId
                      AND Status = 'Aguardando'
                      AND DataEntrada < (
                          SELECT DataEntrada FROM FilaEspera
                          WHERE UsuarioCpf = @UsuarioCpf AND EventoId = @EventoId AND Status = 'Aguardando'
                      )";
        var posicao = await connection.QuerySingleAsync<int>(sql, new { UsuarioCpf = usuarioCpf, EventoId = eventoId });
        return posicao + 1; // 1-based position
    }

    public async Task<IEnumerable<WaitingQueueDto>> ListarPorEventoAsync(int eventoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT
                        f.Id,
                        f.UsuarioCpf,
                        u.Nome AS UsuarioNome,
                        u.Email AS UsuarioEmail,
                        f.EventoId,
                        e.Nome AS EventoNome,
                        f.DataEntrada,
                        f.Status,
                        f.DataNotificacao,
                        ROW_NUMBER() OVER (ORDER BY f.DataEntrada ASC) AS Posicao
                    FROM FilaEspera f
                    INNER JOIN Usuarios u ON u.Cpf = f.UsuarioCpf
                    INNER JOIN Eventos e ON e.Id = f.EventoId
                    WHERE f.EventoId = @EventoId AND f.Status = 'Aguardando'
                    ORDER BY f.DataEntrada ASC";
        return await connection.QueryAsync<WaitingQueueDto>(sql, new { EventoId = eventoId });
    }

    public async Task<IEnumerable<WaitingQueueDto>> ListarPorUsuarioAsync(string usuarioCpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT
                        f.Id,
                        f.UsuarioCpf,
                        u.Nome AS UsuarioNome,
                        u.Email AS UsuarioEmail,
                        f.EventoId,
                        e.Nome AS EventoNome,
                        f.DataEntrada,
                        f.Status,
                        f.DataNotificacao,
                        ROW_NUMBER() OVER (PARTITION BY f.EventoId ORDER BY f.DataEntrada ASC) AS Posicao
                    FROM FilaEspera f
                    INNER JOIN Usuarios u ON u.Cpf = f.UsuarioCpf
                    INNER JOIN Eventos e ON e.Id = f.EventoId
                    WHERE f.UsuarioCpf = @UsuarioCpf
                    ORDER BY f.DataEntrada DESC";
        return await connection.QueryAsync<WaitingQueueDto>(sql, new { UsuarioCpf = usuarioCpf });
    }

    public async Task<WaitingQueueDto?> ObterProximoDaFilaAsync(int eventoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT TOP 1
                        f.Id,
                        f.UsuarioCpf,
                        u.Nome AS UsuarioNome,
                        u.Email AS UsuarioEmail,
                        f.EventoId,
                        e.Nome AS EventoNome,
                        f.DataEntrada,
                        f.Status,
                        f.DataNotificacao,
                        1 AS Posicao
                    FROM FilaEspera f
                    INNER JOIN Usuarios u ON u.Cpf = f.UsuarioCpf
                    INNER JOIN Eventos e ON e.Id = f.EventoId
                    WHERE f.EventoId = @EventoId AND f.Status = 'Aguardando'
                    ORDER BY f.DataEntrada ASC";
        return await connection.QueryFirstOrDefaultAsync<WaitingQueueDto>(sql, new { EventoId = eventoId });
    }

    public async Task<bool> MarcarComoNotificadoAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE FilaEspera
                    SET Status = 'Notificado', DataNotificacao = GETUTCDATE()
                    WHERE Id = @Id AND Status = 'Aguardando'";
        var rows = await connection.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }

    public async Task<bool> MarcarComoConfirmadoAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE FilaEspera
                    SET Status = 'Confirmado'
                    WHERE Id = @Id AND Status = 'Notificado'";
        var rows = await connection.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }
}
