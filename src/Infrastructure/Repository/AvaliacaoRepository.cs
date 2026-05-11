using Dapper;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Models;

namespace src.Infrastructure.Repository;

public class AvaliacaoRepository : IAvaliacaoRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public AvaliacaoRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> UsuarioJaAvaliouAsync(string usuarioCpf, int eventoId)
    {
        const string sql = "SELECT COUNT(1) FROM Avaliacoes WHERE UsuarioCpf = @UsuarioCpf AND EventoId = @EventoId";
        using var conn = _connectionFactory.CreateConnection();
        var count = await conn.QuerySingleAsync<int>(sql, new { UsuarioCpf = usuarioCpf, EventoId = eventoId });
        return count > 0;
    }

    public async Task CriarAsync(Avaliacao avaliacao)
    {
        const string sql = @"
            INSERT INTO Avaliacoes (UsuarioCpf, EventoId, Nota, Comentario, Anonima)
            VALUES (@UsuarioCpf, @EventoId, @Nota, @Comentario, @Anonima)";
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(sql, new
        {
            avaliacao.UsuarioCpf,
            avaliacao.EventoId,
            avaliacao.Nota,
            avaliacao.Comentario,
            avaliacao.Anonima
        });
    }

    public async Task<IEnumerable<Avaliacao>> ListarPorEventoAsync(int eventoId)
    {
        const string sql = @"
            SELECT a.Id, a.UsuarioCpf, a.EventoId, a.Nota, a.Comentario, a.DataAvaliacao,
                   a.Anonima,
                   CASE WHEN a.Anonima = 1 THEN NULL ELSE u.Nome END AS NomeUsuario
            FROM Avaliacoes a
            INNER JOIN Usuarios u ON u.Cpf = a.UsuarioCpf
            WHERE a.EventoId = @EventoId
            ORDER BY a.DataAvaliacao DESC";
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<Avaliacao>(sql, new { EventoId = eventoId });
    }

    public async Task<double?> ObterMediaAsync(int eventoId)
    {
        const string sql = "SELECT AVG(CAST(Nota AS FLOAT)) FROM Avaliacoes WHERE EventoId = @EventoId";
        using var conn = _connectionFactory.CreateConnection();
        var media = await conn.QuerySingleOrDefaultAsync<double?>(sql, new { EventoId = eventoId });
        return media;
    }
}
