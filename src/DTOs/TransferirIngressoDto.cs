using System.ComponentModel.DataAnnotations;

namespace src.DTOs;

/// <summary>
/// Payload para transferência de ingresso a outro usuário.
/// </summary>
public sealed class TransferirIngressoDto
{
    /// <summary>CPF do usuário que receberá o ingresso (11 dígitos, somente números).</summary>
    [Required]
    [StringLength(11, MinimumLength = 11)]
    public string CpfDestinatario { get; set; } = string.Empty;
}
