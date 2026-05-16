using System.ComponentModel.DataAnnotations;

namespace src.DTOs;

/// <summary>
/// DTO para solicitar reenvio do token de verificação de email.
/// </summary>
public class EmailVerificationRequestDto
{
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
    public string Email { get; set; } = string.Empty;
}
