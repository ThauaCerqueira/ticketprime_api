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
    public string Token { get; set; } = string.Empty;
    public bool SenhaTemporaria { get; set; } = false;
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
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
}
