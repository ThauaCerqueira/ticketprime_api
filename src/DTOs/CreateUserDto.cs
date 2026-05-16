using System.ComponentModel.DataAnnotations;

namespace src.DTOs;

public class CreateUserDto
{
    [Required(ErrorMessage = "O CPF é obrigatório")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "O CPF deve ter exatamente 11 dígitos")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "O CPF deve conter apenas números")]
    public required string Cpf { get; set; }

    [Required(ErrorMessage = "O Nome é obrigatório")]
    [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres")]
    public required string Nome { get; set; }

    [Required(ErrorMessage = "O Email é obrigatório")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "A Senha é obrigatória")]
    [MinLength(8, ErrorMessage = "A senha deve ter no mínimo 8 caracteres")]
    public required string Senha { get; set; }
}
