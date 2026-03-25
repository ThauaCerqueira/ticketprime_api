namespace src.DTOs;
public record CriarEventoDTO
{
    public string Nome { get; set; } = string.Empty;
    public int CapacidadeTotal { get; set;}
    public DateTime DataEvento { get; set; }
    public decimal PrecoPadrao { get; set; }
}