namespace src.DTOs;

/// <summary>
/// DTO para solicitar redefinição de senha via email.
/// </summary>
public class PasswordResetRequestDto
{
    /// <summary>
    /// Email do usuário que deseja redefinir a senha.
    /// </summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// DTO para confirmar a redefinição de senha com o token recebido por email.
/// </summary>
public class PasswordResetDto
{
    /// <summary>
    /// Email do usuário.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Token de redefinição recebido por email.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Nova senha (deve atender aos requisitos de força).
    /// </summary>
    public string NovaSenha { get; set; } = string.Empty;
}
