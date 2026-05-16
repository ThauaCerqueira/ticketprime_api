namespace src.Models;

public class Favorito
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public DateTime DataFavoritado { get; set; }

    // Join fields
    public string? EventoNome { get; set; }
    public DateTime? EventoData { get; set; }
    public string? EventoLocal { get; set; }
    public decimal? EventoPreco { get; set; }
    public string? EventoGenero { get; set; }
    public string? FotoThumbnailBase64 { get; set; }
}
