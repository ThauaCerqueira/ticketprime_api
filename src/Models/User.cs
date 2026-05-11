using System.ComponentModel.DataAnnotations;
namespace src.Models;
 
public class User
{
    [Required(ErrorMessage = "O CPF é obrigatório")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "O CPF deve ter exatamente 11 dígitos")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "O CPF deve conter apenas números")]
    public string Cpf { get; set; } = string.Empty;
 
    [Required(ErrorMessage = "O Nome é obrigatório")]
    public string Nome { get; set; } = string.Empty;
 
    [Required(ErrorMessage = "O Email é obrigatório")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;
 
    [Required(ErrorMessage = "A Senha é obrigatória")]
    [MinLength(8, ErrorMessage = "A senha deve ter no mínimo 8 caracteres")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "A senha deve conter pelo menos uma letra maiúscula, uma minúscula e um número")]
    public string Senha { get; set; } = string.Empty;
 
    public string Perfil { get; set; } = "CLIENTE";
    public bool SenhaTemporaria { get; set; } = false;

    // ── Email Verification ──────────────────────────────────────────
    /// <summary>
    /// Indica se o email do usuário foi verificado.
    /// </summary>
    public bool EmailVerificado { get; set; } = false;

    /// <summary>
    /// Token de verificação de email (gerado no cadastro ou reenviado).
    /// </summary>
    public string? TokenVerificacaoEmail { get; set; }

    /// <summary>
    /// Data de expiração do token de verificação de email.
    /// </summary>
    public DateTime? TokenExpiracaoEmail { get; set; }

    // ── Contact ─────────────────────────────────────────────────────
    /// <summary>
    /// Telefone de contato do usuário (opcional). Formato livre — ex.: (11) 91234-5678.
    /// </summary>
    public string? Telefone { get; set; }

    // ── Password Recovery ───────────────────────────────────────────
    /// <summary>
    /// Token para redefinição de senha (recuperação de senha).
    /// </summary>
    public string? ResetToken { get; set; }

    /// <summary>
    /// Data de expiração do token de redefinição de senha.
    /// </summary>
    public DateTime? ResetTokenExpiracao { get; set; }

    // ── Public Slug (opaque identifier) ──────────────────────────────
    /// <summary>
    /// Slug público único (opaco) usado em URLs públicas no lugar do CPF.
    /// Gerado automaticamente no cadastro. Ex.: "a1b2c3d4e5f6".
    /// Nunca expõe o CPF do usuário.
    /// </summary>
    public string? Slug { get; set; }
}
