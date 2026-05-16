using Dapper;
using src.Models;
using src.Infrastructure.IRepository;

namespace src.Infrastructure.Repository;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public UsuarioRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> ObterPorCpf(string cpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT * FROM Usuarios WHERE Cpf = @Cpf";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Cpf = cpf });
    }

    public async Task<string> CriarUsuario(User usuario)
    {
        using var connection = _connectionFactory.CreateConnection();

        // ═══════════════════════════════════════════════════════════════════
        // ANTES: SELECT COUNT(*) + INSERT (2 queries, race condition possível)
        //   Entre o SELECT e o INSERT, outro request podia inserir o mesmo
        //   email → violação de UNIQUE constraint.
        //
        // AGORA: INSERT direto com tratamento de exceção de UNIQUE constraint.
        //   O UNIQUE INDEX no banco (UX_Usuarios_Email) já garante unicidade.
        //   Se violar, capturamos SqlException.Number == 2601.
        // ═══════════════════════════════════════════════════════════════════
        try
        {
            var sql = @"INSERT INTO Usuarios (Cpf, Nome, Email, Senha, Perfil, Telefone, Slug)
                        VALUES (@Cpf, @Nome, @Email, @Senha, @Perfil, @Telefone, @Slug)";
            await connection.ExecuteAsync(sql, usuario);
            return usuario.Cpf;
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2601)
        {
            // 2601 = Cannot insert duplicate key row in object with unique index
            throw new InvalidOperationException("Este e-mail ou CPF já está cadastrado.");
        }
    }

    public async Task<IEnumerable<User>> ListarUsuarios()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT * FROM Usuarios";
        return await connection.QueryAsync<User>(sql);
    }

    public async Task<int> ContarUsuariosAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM Usuarios");
    }

    public async Task AtualizarSenha(string cpf, string senhaHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE Usuarios SET Senha = @SenhaHash, SenhaTemporaria = 0 WHERE Cpf = @Cpf";
        await connection.ExecuteAsync(sql, new { Cpf = cpf, SenhaHash = senhaHash });
    }

    public async Task<User?> ObterPorEmail(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT * FROM Usuarios WHERE Email = @Email";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task ConfirmarEmail(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE Usuarios SET EmailVerificado = 1, TokenVerificacaoEmail = NULL, TokenExpiracaoEmail = NULL WHERE Email = @Email";
        await connection.ExecuteAsync(sql, new { Email = email });
    }

    public async Task SalvarTokenVerificacaoEmail(string email, string token, DateTime expiracao)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE Usuarios SET TokenVerificacaoEmail = @Token, TokenExpiracaoEmail = @Expiracao WHERE Email = @Email";
        await connection.ExecuteAsync(sql, new { Email = email, Token = token, Expiracao = expiracao });
    }

    // ── Password Recovery ───────────────────────────────────────────

    public async Task SalvarResetToken(string email, string token, DateTime expiracao)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE Usuarios SET ResetToken = @Token, ResetTokenExpiracao = @Expiracao WHERE Email = @Email";
        await connection.ExecuteAsync(sql, new { Email = email, Token = token, Expiracao = expiracao });
    }

    public async Task LimparResetToken(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE Usuarios SET ResetToken = NULL, ResetTokenExpiracao = NULL WHERE Email = @Email";
        await connection.ExecuteAsync(sql, new { Email = email });
    }

    public async Task AtualizarTelefone(string cpf, string? telefone)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Usuarios SET Telefone = @Telefone WHERE Cpf = @Cpf",
            new { Cpf = cpf, Telefone = telefone });
    }

    public async Task<User?> ObterPorSlug(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT * FROM Usuarios WHERE Slug = @Slug";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Slug = slug });
    }

    public async Task AtualizarSlug(string cpf, string slug)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE Usuarios SET Slug = @Slug WHERE Cpf = @Cpf";
        await connection.ExecuteAsync(sql, new { Cpf = cpf, Slug = slug });
    }
}
