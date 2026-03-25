using Dapper;
using src.Models;

namespace src.Infrastructure;

public class CupomRepository
{
    private readonly DbConnectionFactory _factory;

    public CupomRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task CriarCupom(Cupom cupom)
    {
        using var connection = _factory.CreateConnection();

        var sql = @"INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra) 
                    VALUES (@Codigo, @PorcentagemDesconto, @ValorMinimoRegra)";
        
        await connection.ExecuteAsync(sql, cupom);
    }
}