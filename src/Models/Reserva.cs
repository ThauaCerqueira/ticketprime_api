namespace src.Models;

public class Reserva
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public DateTime DataCompra { get; set; }
    public string? CupomUtilizado { get; set; }
    public decimal ValorFinalPago { get; set; }
    /// <summary>Taxa de serviço cobrada na compra. Não é devolvida sem seguro.</summary>
    public decimal TaxaServicoPago { get; set; }
    /// <summary>Indica que o usuário contratou o seguro de devolução integral.</summary>
    public bool TemSeguro { get; set; }
    /// <summary>Valor pago pelo seguro (15% do preço do ingresso). Não é devolvido.</summary>
    public decimal ValorSeguroPago { get; set; }
}
