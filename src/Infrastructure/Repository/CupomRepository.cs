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

    public async Task<int> CriarAsync(Coupon cupom)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"
            INSERT INTO Cupons (Codigo, TipoDesconto, PorcentagemDesconto, ValorDescontoFixo,
                                ValorMinimoRegra, DataExpiracao, LimiteUsos, TotalUsado,
                                CategoriaEvento, PrimeiroAcesso)
            VALUES (@Codigo, @TipoDesconto, @PorcentagemDesconto, @ValorDescontoFixo,
                    @ValorMinimoRegra, @DataExpiracao, @LimiteUsos, 0,
                    @CategoriaEvento, @PrimeiroAcesso);";
        return await connection.ExecuteAsync(sql, cupom);
    }

    public async Task<Coupon?> ObterPorCodigoAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT Codigo, TipoDesconto, PorcentagemDesconto, ValorDescontoFixo,
                   ValorMinimoRegra, DataExpiracao, LimiteUsos, TotalUsado,
                   CategoriaEvento, PrimeiroAcesso
            FROM Cupons WHERE Codigo = @Codigo;";
        return await connection.QueryFirstOrDefaultAsync<Coupon>(sql, new { Codigo = codigo });
    }

    public async Task<IEnumerable<Coupon>> ListarAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT Codigo, TipoDesconto, PorcentagemDesconto, ValorDescontoFixo,
                   ValorMinimoRegra, DataExpiracao, LimiteUsos, TotalUsado,
                   CategoriaEvento, PrimeiroAcesso
            FROM Cupons ORDER BY Codigo;";
        return await connection.QueryAsync<Coupon>(sql);
    }

    public async Task IncrementarUsoAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Cupons SET TotalUsado = TotalUsado + 1 WHERE Codigo = @Codigo",
            new { Codigo = codigo });
    }

    public async Task<bool> DeletarAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "DELETE FROM Cupons WHERE Codigo = @Codigo;",
            new { Codigo = codigo });
        return rows > 0;
    }
}
