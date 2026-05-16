namespace src.DTOs;
  
public class LoginDTO
{
    public string Cpf { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
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
    public string Cpf { get; set; } = string.Empty;
    public string SenhaAtual { get; set; } = string.Empty;
    public string SenhaNova { get; set; } = string.Empty;
}

public class RefreshTokenRequestDTO
{
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
