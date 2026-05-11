namespace src.DTOs;

/// <summary>
/// DTO para solicitar reenvio do token de verificação de email.
/// </summary>
public class EmailVerificationRequestDto
{
    public string Email { get; set; } = string.Empty;
}
