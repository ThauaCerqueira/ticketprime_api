using System.ComponentModel.DataAnnotations;
namespace src.Models;
 
public class Usuario
{
    [Required(ErrorMessage = "O CPF é obrigatório")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "O CPF deve ter exatamente 11 dígitos")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "O CPF deve conter apenas números")]
    public string Cpf { get; set; } = string.Empty;
 
    [Required(ErrorMessage = "O Nome é obrigatório")]
    public string Nome { get; set; } = string.Empty;
 
    [Required(ErrorMessage = "O Email é obrigatório")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;
 
    [Required(ErrorMessage = "A Senha é obrigatória")]
    [MinLength(6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres")]
    public string Senha { get; set; } = string.Empty;
 
    public string Perfil { get; set; } = "CLIENTE"; 
}