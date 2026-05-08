using Dapper;
using src.Models;
using src.Infrastructure;
using src.Infrastructure.IRepository;

namespace src.Infrastructure.Repository;

public class CupomRepository : ICupomRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public CupomRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CriarAsync(Cupom cupom)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra)
            VALUES (@Codigo, @PorcentagemDesconto, @ValorMinimoRegra);
        ";

        return await connection.ExecuteAsync(sql, cupom);
    }

    public async Task<Cupom?> ObterPorCodigoAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT Codigo, PorcentagemDesconto, ValorMinimoRegra
            FROM Cupons
            WHERE Codigo = @Codigo;
        ";

        return await connection.QueryFirstOrDefaultAsync<Cupom>(sql, new { Codigo = codigo });
    }

    public async Task<IEnumerable<Cupom>> ListarAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT Codigo, PorcentagemDesconto, ValorMinimoRegra
            FROM Cupons;
        ";

        return await connection.QueryAsync<Cupom>(sql);
    }

    public async Task<bool> DeletarAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            DELETE FROM Cupons
            WHERE Codigo = @Codigo;
        ";

        var rows = await connection.ExecuteAsync(sql, new { Codigo = codigo });

        return rows > 0;
    }
}
