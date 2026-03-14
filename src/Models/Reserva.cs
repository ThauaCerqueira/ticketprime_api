namespace src.Models;

public class Reserva
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; }
    public int EventoId { get; set; }
    public string CupomUtilizado { get; set; }
    public decimal ValorFinalPago { get; set; }
}