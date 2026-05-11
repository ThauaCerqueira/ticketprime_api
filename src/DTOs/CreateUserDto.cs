using System.ComponentModel.DataAnnotations;

namespace src.DTOs;

public class CreateUserDto
{
    [Required]
    [StringLength(11, MinimumLength = 11)]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "O CPF deve conter exatamente 11 números")]
    public required string Cpf { get; set; }
}
