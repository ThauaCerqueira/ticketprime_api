namespace src.Models;

public class Usuario
{
    public required string Cpf { get; set; } 
    public required string Nome { get; set; }
    public required string Email { get; set; }
    public required string Senha { get; set; }
}