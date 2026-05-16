using Dapper;
using src.Infrastructure.IRepository;
using src.Models;

namespace src.Infrastructure.Repository;

public class FavoritoRepository : IFavoritoRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public FavoritoRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> IsFavoritoAsync(string usuarioCpf, int eventoId)
    {
        const string sql = "SELECT COUNT(1) FROM Favoritos WHERE UsuarioCpf = @Cpf AND EventoId = @EventoId";
        using var conn = _connectionFactory.CreateConnection();
        var count = await conn.QuerySingleAsync<int>(sql, new { Cpf = usuarioCpf, EventoId = eventoId });
        return count > 0;
    }

    public async Task AdicionarAsync(string usuarioCpf, int eventoId)
    {
        const string sql = "INSERT INTO Favoritos (UsuarioCpf, EventoId) VALUES (@Cpf, @EventoId)";
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(sql, new { Cpf = usuarioCpf, EventoId = eventoId });
    }

    public async Task RemoverAsync(string usuarioCpf, int eventoId)
    {
        const string sql = "DELETE FROM Favoritos WHERE UsuarioCpf = @Cpf AND EventoId = @EventoId";
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(sql, new { Cpf = usuarioCpf, EventoId = eventoId });
    }

    public async Task<IEnumerable<Favorito>> ListarPorUsuarioAsync(string usuarioCpf)
    {
        const string sql = @"
            SELECT f.*, e.Nome AS EventoNome, e.DataEvento AS EventoData,
                   e.Local AS EventoLocal, e.PrecoPadrao AS EventoPreco,
                   e.GeneroMusical AS EventoGenero
            FROM Favoritos f
            INNER JOIN Eventos e ON e.Id = f.EventoId
            WHERE f.UsuarioCpf = @Cpf
            ORDER BY f.DataFavoritado DESC";
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<Favorito>(sql, new { Cpf = usuarioCpf });
    }

    public async Task<int> ContarFavoritosAsync(int eventoId)
    {
        const string sql = "SELECT COUNT(*) FROM Favoritos WHERE EventoId = @EventoId";
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleAsync<int>(sql, new { EventoId = eventoId });
    }
}
