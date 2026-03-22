using src.Models;
using src.Infrastructure.IRepository;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using Microsoft.Identity.Client;


namespace src.Infrastructure.Repository;

public class EventoRepository : IEventoRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public EventoRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AdicionarAsync(Evento evento)
    {
        const string sql = @"
            INSERT INTO Eventos (Nome, CapacidadeTotal, DataEvento, PrecoPadrao)
            VALUES (@Nome, @CapacidadeTotal, @DataEvento, @PrecoPadrao)";

        using var connection = _connectionFactory.CreateConnection();
        
        await connection.ExecuteAsync(sql, evento);
    }
}