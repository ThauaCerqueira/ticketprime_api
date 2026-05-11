namespace src.Models;

public class Avaliacao
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }

    /// <summary>Nota de 1 a 5.</summary>
    public byte Nota { get; set; }
    public string? Comentario { get; set; }
    public DateTime DataAvaliacao { get; set; } = DateTime.UtcNow;

    /// <summary>Se true, o nome do usuário não será exibido publicamente.</summary>
    public bool Anonima { get; set; }

    // Joined fields for display
    public string? NomeUsuario { get; set; }
}
