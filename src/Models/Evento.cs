namespace src.Models;

public class Evento
{
    public int Id { get; set; }
    
    public required string Nome { get; set; } 

    public int CapacidadeTotal { get; set; }

    public DateTime DataEvento { get; set; }

    public decimal PrecoPadrao { get; set; } = 0;
}