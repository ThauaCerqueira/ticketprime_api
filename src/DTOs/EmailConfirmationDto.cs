namespace src.DTOs;

/// <summary>
/// DTO para confirmar verificação de email com token.
/// </summary>
public class EmailConfirmationDto
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
