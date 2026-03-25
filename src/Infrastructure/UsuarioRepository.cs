using Dapper;
using src.Models;

namespace src.Infrastructure;

public class UsuarioRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public UsuarioRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Usuario?> ObterPorCpf(string cpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT * FROM Usuarios WHERE Cpf = @Cpf";

        return await connection.QueryFirstOrDefaultAsync<Usuario>(sql, new { Cpf = cpf });
    }

    public async Task CriarUsuario(Usuario usuario)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"INSERT INTO Usuarios (Cpf, Nome, Email)
                    VALUES (@Cpf, @Nome, @Email)";

        await connection.ExecuteAsync(sql, usuario);
    }

    public async Task<IEnumerable<Usuario>> ListarUsuarios()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT * FROM Usuarios";

        return await connection.QueryAsync<Usuario>(sql);
    }
}