using System.ComponentModel.DataAnnotations;

namespace TicketPrime.Web.Shared.Models;

public class LoginRequest
{
    public string Cpf { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public bool Lembrar { get; set; }
}

public class LoginResponse
{
    public string Cpf { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public bool SenhaTemporaria { get; set; } = false;
}

public class ChangePasswordRequest
{
    public string Cpf { get; set; } = string.Empty;
    public string SenhaAtual { get; set; } = string.Empty;
    public string SenhaNova { get; set; } = string.Empty;
}

public class RegistrationRequest
{
    [Required(ErrorMessage = "O CPF é obrigatório")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "CPF deve ter 11 dígitos")]
    public string Cpf { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome é obrigatório")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Nome deve ter entre 3 e 100 caracteres")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório")]
    [EmailAddress(ErrorMessage = "E-mail inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A senha é obrigatória")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Senha deve ter no mínimo 8 caracteres")]
    public string Senha { get; set; } = string.Empty;

    public string Perfil { get; set; } = "CLIENTE";
}

public class ApiErrorResponse
{
    public string Mensagem { get; set; } = string.Empty;
}

public class PasswordResetRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NovaSenha { get; set; } = string.Empty;
}
