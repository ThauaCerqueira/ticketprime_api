using System.ComponentModel.DataAnnotations;

namespace src.DTOs;

/// <summary>
/// DTO para confirmar verificação de email com token.
/// </summary>
public class EmailConfirmationDto
{
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "O token de verificação é obrigatório.")]
    public string Token { get; set; } = string.Empty;
}
