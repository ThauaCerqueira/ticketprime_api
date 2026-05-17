using src.Models;

namespace src.Infrastructure.IRepository;

public interface IUsuarioRepository
{
    Task<User?> ObterPorCpf(string cpf);
    Task<string> CriarUsuario(User usuario);
    Task<IEnumerable<User>> ListarUsuarios();
    Task<int> ContarUsuariosAsync();
    Task AtualizarSenha(string cpf, string senhaHash);

    /// <summary>
    /// Busca um usuário pelo email.
    /// </summary>
    Task<User?> ObterPorEmail(string email);

    /// <summary>
    /// Marca o email do usuário como verificado.
    /// </summary>
    Task ConfirmarEmail(string email);

    /// <summary>
    /// Salva o token de verificação de email e sua expiração.
    /// </summary>
    Task SalvarTokenVerificacaoEmail(string email, string token, DateTime expiracao);

    // ── Password Recovery ───────────────────────────────────────────

    /// <summary>
    /// Salva o token de redefinição de senha e sua expiração.
    /// </summary>
    Task SalvarResetToken(string email, string token, DateTime expiracao);

    /// <summary>
    /// Limpa o token de redefinição de senha após uso bem-sucedido.
    /// </summary>
    Task LimparResetToken(string email);

    /// <summary>
    /// Atualiza o telefone de contato do usuário.
    /// </summary>
    Task AtualizarTelefone(string cpf, string? telefone);

    /// <summary>
    /// Busca um usuário pelo slug público (opaco).
    /// </summary>
    Task<User?> ObterPorSlug(string slug);

    /// <summary>
    /// Atualiza (ou gera) o slug público de um usuário.
    /// </summary>
    Task AtualizarSlug(string cpf, string slug);

    /// <summary>
    /// Atualiza a URL da foto de perfil do organizador.
    /// </summary>
    Task AtualizarFotoUrl(string cpf, string url);

    /// <summary>
    /// Atualiza a URL do banner do organizador.
    /// </summary>
    Task AtualizarBannerUrl(string cpf, string url);

    /// <summary>
    /// Atualiza a biografia do organizador.
    /// </summary>
    Task AtualizarBio(string cpf, string bio);
}
