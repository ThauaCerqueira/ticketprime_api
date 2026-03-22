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

    // NOVO MÉTODO: Essencial para a validação do Erro 400
    public async Task<Usuario?> ObterPorCpf(string cpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT * FROM Usuarios WHERE Cpf = @cpf";
        
        // Retorna o usuário se encontrar, ou null se não existir
        return await connection.QueryFirstOrDefaultAsync<Usuario>(sql, new { cpf });
    }

    public async Task CriarUsuario(Usuario usuario)
{
    // Verifique se o nome é _connectionFactory ou _factory conforme seu construtor
    using var connection = _connectionFactory.CreateConnection();

    // Organizado para bater com a estrutura da sua imagem
    var sql = @"INSERT INTO Usuarios (Cpf, Nome, Email, Senha)
                VALUES (@Cpf, @Nome, @Email, @Senha)";

    await connection.ExecuteAsync(sql, usuario);
}

    public async Task<IEnumerable<Usuario>> ListarUsuarios()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT * FROM Usuarios";

        return await connection.QueryAsync<Usuario>(sql);
    }
}