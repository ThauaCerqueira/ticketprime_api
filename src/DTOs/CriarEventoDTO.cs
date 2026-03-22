namespace src.DTOs;
public record CriarEventoDTO
{
    public string Nome { get; init; } = string.Empty;
    public int CapacidadeTotal { get; init;}
    public DateTime DataEvento { get; init; }
    public decimal PrecoPadrao { get; init; }
}