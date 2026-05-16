namespace src.DTOs;

public class ReservationDetailDto
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public DateTime DataCompra { get; set; }
    public string? CupomUtilizado { get; set; }
    public decimal ValorFinalPago { get; set; }
    public string Nome { get; set; } = string.Empty;       // Nome do evento
    public string Local { get; set; } = string.Empty;       // Local do evento
    public DateTime DataEvento { get; set; }               // Data do evento

    /// <summary>Data/hora de término do evento (quando nulo, fallback para DataEvento.AddHours(4)).</summary>
    public DateTime? DataTermino { get; set; }
    public decimal PrecoPadrao { get; set; }               // Preço do evento
    public decimal TaxaServicoPago { get; set; }           // Taxa de serviço paga
    public bool TemSeguro { get; set; }                    // Contratou seguro de devolução?
    public decimal ValorSeguroPago { get; set; }           // Valor pago pelo seguro
    public string CodigoIngresso { get; set; } = string.Empty; // Código único do ticket
    public string Status { get; set; } = "Ativa";          // Status da reserva
    /// <summary>Indica se esta reserva foi comprada como meia-entrada.</summary>
    public bool EhMeiaEntrada { get; set; }
    /// <summary>Data/hora do check-in na entrada do evento. Null = não utilizado.</summary>
    public DateTime? DataCheckin { get; set; }
    /// <summary>Valor que seria devolvido em caso de cancelamento agora.</summary>
    public decimal ValorDevolvivel => TemSeguro
        ? ValorFinalPago - ValorSeguroPago   // com seguro: devolve ingresso + taxa
        : ValorFinalPago - TaxaServicoPago;  // sem seguro: só ingresso (sem seguro = 0)

    // ═══ NOVO: Dados do tipo de ingresso e lote ═══

    /// <summary>ID do tipo de ingresso (setor) adquirido.</summary>
    public int TicketTypeId { get; set; }

    /// <summary>Nome do tipo de ingresso (setor) adquirido (ex: "Pista", "VIP").</summary>
    public string TicketTypeNome { get; set; } = string.Empty;

    /// <summary>ID do lote progressivo utilizado (se aplicável).</summary>
    public int? LoteId { get; set; }

    /// <summary>Nome do lote utilizado (ex: "1º Lote").</summary>
    public string? LoteNome { get; set; }

    /// <summary>ID da transação no gateway de pagamento (usado para estornos).</summary>
    public string? CodigoTransacaoGateway { get; set; }

    /// <summary>ID do estorno no gateway (preenchido após cancelamento reembolsado).</summary>
    public string? IdEstornoGateway { get; set; }

    /// <summary>Data/hora do estorno processado.</summary>
    public DateTime? DataEstorno { get; set; }

    /// <summary>
    /// Chave PIX para pagamento (criptografada em repouso, decriptada ao ser retornada).
    /// Presente apenas quando MetodoPagamento = "pix" e pagamento ainda não confirmado.
    /// </summary>
    public string? ChavePix { get; set; }
}
