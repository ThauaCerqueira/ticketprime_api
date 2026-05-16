using System.ComponentModel.DataAnnotations;

namespace src.DTOs;

public class LoginDTO
{
    [Required(ErrorMessage = "O CPF é obrigatório.")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "O CPF deve ter exatamente 11 dígitos.")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "O CPF deve conter apenas números (11 dígitos).")]
    public string Cpf { get; set; } = string.Empty;

    [Required(ErrorMessage = "A senha é obrigatória.")]
    public string Senha { get; set; } = string.Empty;

    /// <summary>
    /// Quando true, estende a validade do JWT e do cookie de 30 minutos para 7 dias.
    /// Útil para dispositivos confiáveis (computador pessoal, celular próprio).
    /// </summary>
    public bool Lembrar { get; set; }
}
  
public class LoginResponseDTO
{
    public string Cpf { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;

    /// <summary>
    /// Token JWT de acesso.
    /// ══════════════════════════════════════════════════════
    /// SEGURANÇA: O token também é definido como cookie httpOnly
    /// (ticketprime_token) para cenários onde o header Authorization
    /// não é enviado automaticamente (ex: refresh de página no Blazor).
    /// ══════════════════════════════════════════════════════
    /// </summary>
    public string Token { get; set; } = string.Empty;
    public bool SenhaTemporaria { get; set; } = false;

    /// <summary>
    /// Refresh token — NÃO é mais retornado no body da resposta.
    /// ══════════════════════════════════════════════════════
    /// SEGURANÇA: O refresh token agora é DEFINIDO EXCLUSIVAMENTE
    /// como cookie httpOnly (ticketprime_refresh) com Path restrito
    /// a /api/auth/refresh. Isso elimina o risco de XSS roubar o
    /// refresh token do localStorage.
    /// ══════════════════════════════════════════════════════
    /// Motivação: O refresh token dá acesso a renovar a sessão
    /// indefinidamente por até 30 dias. Mantê-lo em localStorage
    /// (como estava antes) significa que um XSS dá ao atacante
    /// acesso persistente à conta mesmo depois da senha ser trocada.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? RefreshToken { get; set; }
}

public class TrocarSenhaDTO
{
    [Required(ErrorMessage = "O CPF é obrigatório.")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "O CPF deve ter exatamente 11 dígitos.")]
    public string Cpf { get; set; } = string.Empty;

    [Required(ErrorMessage = "A senha atual é obrigatória.")]
    public string SenhaAtual { get; set; } = string.Empty;

    [Required(ErrorMessage = "A nova senha é obrigatória.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "A nova senha deve ter no mínimo 8 caracteres.")]
    public string SenhaNova { get; set; } = string.Empty;
}

public class RefreshTokenRequestDTO
{
    [Required(ErrorMessage = "O refresh token é obrigatório.")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponseDTO
{
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token — NÃO é mais retornado no body da resposta.
    /// ══════════════════════════════════════════════════════
    /// SEGURANÇA: O refresh token rotacionado é definido como
    /// cookie httpOnly no próprio endpoint /api/auth/refresh.
    /// O body da resposta NÃO contém o refresh token.
    /// ══════════════════════════════════════════════════════
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
    public bool SenhaTemporaria { get; set; } = false;
}
