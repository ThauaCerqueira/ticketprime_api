using System.Security.Cryptography;
using System.Text.RegularExpressions;
using src.Models;
using src.Infrastructure.IRepository;
 
namespace src.Service;
  
public class UserService
{
    private static readonly Regex SenhaForteRegex = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex de validação de email — permite emails reais (RFC 5322 simplificado)
    /// e REJEITA caracteres de injeção (SQL/XSS).
    /// </summary>
    private static readonly Regex EmailValidoRegex = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IUsuarioRepository _repository;
    private readonly IEmailService _emailService;
    private readonly int _bcryptWorkFactor;
    private readonly AuditLogService? _auditLog;

    public UserService(
        IUsuarioRepository repository,
        IEmailService emailService,
        IConfiguration? configuration = null,
        AuditLogService? auditLog = null)
    {
        _repository = repository;
        _emailService = emailService;
        _bcryptWorkFactor = configuration?.GetValue<int>("Bcrypt:WorkFactor", 11) ?? 11;
        _auditLog = auditLog;
    }
 
    /// <summary>
    /// Remove ou neutraliza caracteres perigosos do nome (XSS prevention).
    /// </summary>
    private static string SanitizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return nome;

        // Remove tags HTML/XML completas (qualquer <...>)
        nome = Regex.Replace(nome, @"<[^>]*>", string.Empty);

        // Remove caracteres de controle (exceto espaços e acentos)
        nome = Regex.Replace(nome, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", string.Empty);

        // Remove aspas duplas e simples para evitar injeção em atributos HTML
        nome = nome.Replace("\"", string.Empty).Replace("'", string.Empty);

        return nome.Trim();
    }

    /// <summary>
    /// Gera um slug público único e opaco de 16 caracteres hexadecimais.
    /// Usado em URLs públicas no lugar do CPF para evitar enumeração de usuários.
    /// </summary>
    private static string GerarSlug()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>
    /// Valida o formato do email e rejeita caracteres de injeção (SQL/XSS).
    /// </summary>
    private static void ValidarEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("O email é obrigatório.");

        // Rejeita caracteres de injeção SQL/XSS
        if (email.Any(c => c is '<' or '>' or '"' or ';' or '\'' or '\\') ||
            email.Contains("--") || email.Contains("/*") || email.Contains("*/") ||
            email.Contains("@@") || email.Contains("char(") ||
            email.Contains("exec ") || email.Contains("drop ") || email.Contains("select "))
            throw new ArgumentException("O email informado contém caracteres inválidos.");

        if (!EmailValidoRegex.IsMatch(email))
            throw new ArgumentException("O email informado não possui um formato válido.");
    }

    public async Task<User> CadastrarUsuario(
        User novoUsuario,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (!ValidarCpf(novoUsuario.Cpf))
            throw new InvalidOperationException("CPF inválido. Verifique os dígitos informados.");

        // Valida email contra injeção e formato
        ValidarEmail(novoUsuario.Email);

        // Valida força da senha
        if (string.IsNullOrWhiteSpace(novoUsuario.Senha) || !SenhaForteRegex.IsMatch(novoUsuario.Senha))
            throw new ArgumentException(
                "A senha deve ter no mínimo 8 caracteres, incluindo: " +
                "1 letra maiúscula, 1 letra minúscula, 1 dígito e 1 caractere especial (!@#$%^&* etc.).");

        // Sanitiza nome contra XSS
        novoUsuario.Nome = SanitizarNome(novoUsuario.Nome);
        if (string.IsNullOrWhiteSpace(novoUsuario.Nome))
            throw new ArgumentException("O nome informado é inválido após a verificação de segurança.");

        var usuarioExistente = await _repository.ObterPorCpf(novoUsuario.Cpf);

        if (usuarioExistente != null)
            throw new InvalidOperationException("Erro: O CPF informado já está cadastrado.");

        // Gera slug público único (opaco) — nunca expõe o CPF na URL
        if (string.IsNullOrWhiteSpace(novoUsuario.Slug))
            novoUsuario.Slug = GerarSlug();

        // Hash bcrypt com work factor configurável
        novoUsuario.Senha = BCrypt.Net.BCrypt.HashPassword(novoUsuario.Senha, workFactor: _bcryptWorkFactor);

        await _repository.CriarUsuario(novoUsuario);

        // Gera token de verificação de email para o novo usuário
        var token = GerarTokenVerificacao();
        var expiracao = DateTime.UtcNow.AddHours(24);
        await _repository.SalvarTokenVerificacaoEmail(novoUsuario.Email, token, expiracao);

        // Envia email com o token de verificação
        var assuntoVerificacao = "TicketPrime — Confirme seu email";
        var corpoVerificacao = $@"
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <h2 style='color: #7c3aed;'>🎫 TicketPrime</h2>
    <p>Olá <strong>{novoUsuario.Nome}</strong>,</p>
    <p>Bem-vindo ao TicketPrime! Use o código abaixo para verificar seu email:</p>
    <div style='background: #f3f4f6; padding: 20px; text-align: center; border-radius: 8px; margin: 20px 0;'>
        <span style='font-size: 24px; font-weight: bold; letter-spacing: 4px; color: #7c3aed;'>{token}</span>
    </div>
    <p>Este código expira em <strong>24 horas</strong>.</p>
    <p>Se você não criou uma conta no TicketPrime, ignore este email.</p>
    <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;' />
    <p style='color: #9ca3af; font-size: 12px;'>TicketPrime — Sua plataforma de eventos</p>
</body>
</html>";
        await _emailService.SendAsync(novoUsuario.Email, assuntoVerificacao, corpoVerificacao);

        // Registra cadastro de usuário na auditoria
        if (_auditLog != null)
            await _auditLog.LogCadastroUsuarioAsync(
                novoUsuario.Cpf, ipAddress ?? "unknown", userAgent);

        return novoUsuario;
    }
  
    public async Task<User?> BuscarPorCpf(string cpf)
    {
        return await _repository.ObterPorCpf(cpf);
    }

    /// <summary>
    /// Gera e persiste um novo token de verificação de email e o envia por email.
    /// </summary>
    public async Task<string> GerarTokenVerificacaoEmail(string email)
    {
        ValidarEmail(email);

        var token = GerarTokenVerificacao();
        var expiracao = DateTime.UtcNow.AddHours(24);
        await _repository.SalvarTokenVerificacaoEmail(email, token, expiracao);

        var assunto = "TicketPrime — Código de verificação de email";
        var corpo = $@"
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <h2 style='color: #7c3aed;'>🎫 TicketPrime</h2>
    <p>Use o código abaixo para verificar seu email:</p>
    <div style='background: #f3f4f6; padding: 20px; text-align: center; border-radius: 8px; margin: 20px 0;'>
        <span style='font-size: 24px; font-weight: bold; letter-spacing: 4px; color: #7c3aed;'>{token}</span>
    </div>
    <p>Este código expira em <strong>24 horas</strong>.</p>
    <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;' />
    <p style='color: #9ca3af; font-size: 12px;'>TicketPrime — Sua plataforma de eventos</p>
</body>
</html>";
        await _emailService.SendAsync(email, assunto, corpo);

        return token;
    }

    // ── Password Recovery ───────────────────────────────────────────

    /// <summary>
    /// Gera um token de redefinição de senha, persiste no banco e envia por email.
    /// </summary>
    public async Task<string> GerarResetSenhaToken(string email)
    {
        ValidarEmail(email);

        var token = GerarTokenVerificacao(); // mesmo gerador de 32 bytes
        var expiracao = DateTime.UtcNow.AddHours(1); // expira em 1 hora
        await _repository.SalvarResetToken(email, token, expiracao);

        var assunto = "TicketPrime — Redefinição de senha";
        var corpo = $@"
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <h2 style='color: #7c3aed;'>🎫 TicketPrime</h2>
    <p>Recebemos uma solicitação de redefinição de senha para sua conta.</p>
    <p>Use o código abaixo para criar uma nova senha:</p>
    <div style='background: #f3f4f6; padding: 20px; text-align: center; border-radius: 8px; margin: 20px 0;'>
        <span style='font-size: 24px; font-weight: bold; letter-spacing: 4px; color: #7c3aed;'>{token}</span>
    </div>
    <p>Este código expira em <strong>1 hora</strong>.</p>
    <p>Se você não solicitou a redefinição de senha, ignore este email.</p>
    <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;' />
    <p style='color: #9ca3af; font-size: 12px;'>TicketPrime — Sua plataforma de eventos</p>
</body>
</html>";
        await _emailService.SendAsync(email, assunto, corpo);

        return token;
    }

    /// <summary>
    /// Redefine a senha do usuário usando o token de redefinição.
    /// </summary>
    public async Task<bool> RedefinirSenha(string email, string token, string novaSenha)
    {
        ValidarEmail(email);

        var usuario = await _repository.ObterPorEmail(email);
        if (usuario == null)
            return false;

        // Verifica token
        if (usuario.ResetToken != token)
            return false;

        // Verifica expiração
        if (usuario.ResetTokenExpiracao.HasValue && usuario.ResetTokenExpiracao.Value < DateTime.UtcNow)
            return false;

        // Valida força da nova senha
        if (string.IsNullOrWhiteSpace(novaSenha) || !SenhaForteRegex.IsMatch(novaSenha))
            throw new ArgumentException(
                "A senha deve ter no mínimo 8 caracteres, incluindo: " +
                "1 letra maiúscula, 1 letra minúscula, 1 dígito e 1 caractere especial.");

        var senhaHash = BCrypt.Net.BCrypt.HashPassword(novaSenha, workFactor: _bcryptWorkFactor);
        await _repository.AtualizarSenha(usuario.Cpf, senhaHash);
        await _repository.LimparResetToken(email);

        return true;
    }

    /// <summary>
    /// Gera um token criptograficamente aleatório de 32 bytes (64 chars hex).
    /// </summary>
    private static string GerarTokenVerificacao()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool ValidarCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11 || !cpf.All(char.IsDigit))
            return false;

        // Rejeita CPFs com todos os dígitos iguais (ex: 00000000000)
        if (cpf.Distinct().Count() == 1) return false;

        int[] digits = cpf.Select(c => c - '0').ToArray();

        // Primeiro dígito verificador
        int sum = 0;
        for (int i = 0; i < 9; i++) sum += digits[i] * (10 - i);
        int remainder = sum % 11;
        if (digits[9] != (remainder < 2 ? 0 : 11 - remainder)) return false;

        // Segundo dígito verificador
        sum = 0;
        for (int i = 0; i < 10; i++) sum += digits[i] * (11 - i);
        remainder = sum % 11;
        return digits[10] == (remainder < 2 ? 0 : 11 - remainder);
    }
}