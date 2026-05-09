namespace src.DTOs;

public class ReservaDetalhadaDTO
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public DateTime DataCompra { get; set; }
    public string? CupomUtilizado { get; set; }
    public decimal ValorFinalPago { get; set; }
    public string Nome { get; set; } = string.Empty;       // Nome do evento
    public DateTime DataEvento { get; set; }               // Data do evento
    public decimal PrecoPadrao { get; set; }               // Preço do evento
    public decimal TaxaServicoPago { get; set; }           // Taxa de serviço paga
    public bool TemSeguro { get; set; }                    // Contratou seguro de devolução?
    public decimal ValorSeguroPago { get; set; }           // Valor pago pelo seguro
    /// <summary>Valor que seria devolvido em caso de cancelamento agora.</summary>
    public decimal ValorDevolvivel => TemSeguro
        ? ValorFinalPago - ValorSeguroPago   // com seguro: devolve ingresso + taxa
        : ValorFinalPago - TaxaServicoPago - ValorSeguroPago; // sem seguro: só ingresso
}
 