namespace src.Models;

public class Reservation
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }

    /// <summary>
    /// ID do tipo de ingresso (setor) adquirido. Referencia a tabela TiposIngresso.
    /// Necessário para suporte a múltiplos setores/lotes por evento.
    /// </summary>
    public int TicketTypeId { get; set; }

    /// <summary>
    /// ID do lote progressivo utilizado na compra (se aplicável).
    /// Null se não foi usado lote (preço base do tipo de ingresso).
    /// </summary>
    public int? LoteId { get; set; }

    public DateTime DataCompra { get; set; }
    public string? CupomUtilizado { get; set; }
    public decimal ValorFinalPago { get; set; }
    /// <summary>Taxa de serviço cobrada na compra. Não é devolvida sem seguro.</summary>
    public decimal TaxaServicoPago { get; set; }
    /// <summary>Indica que o usuário contratou o seguro de devolução integral.</summary>
    public bool TemSeguro { get; set; }
    /// <summary>Valor pago pelo seguro (15% do preço do ingresso). Não é devolvido.</summary>
    public decimal ValorSeguroPago { get; set; }
    /// <summary>Código único do ingresso para validação na entrada do evento.</summary>
    /// <remarks>
    /// ═══════════════════════════════════════════════════════════════════
    /// SEGURANÇA: GUID gerado no service/repository no momento da
    ///   persistência (ReservaService.ComprarIngressoAsync). O construtor
    ///   do modelo não gera GUIDs para evitar desperdício quando o objeto
    ///   é instanciado mas não persistido (ex: validações que falham).
    /// ═══════════════════════════════════════════════════════════════════
    /// </remarks>
    public string CodigoIngresso { get; set; } = string.Empty;
    /// <summary>Status da reserva: Ativa, Usada, Cancelada.</summary>
    public string Status { get; set; } = "Ativa";
    /// <summary>Data do cancelamento (se cancelada).</summary>
    public DateTime? DataCancelamento { get; set; }
    /// <summary>Motivo do cancelamento.</summary>
    public string? MotivoCancelamento { get; set; }
    /// <summary>
    /// Indica se esta reserva foi comprada como meia-entrada (Lei 12.933/2013).
    /// Quando true, o ValorFinalPago é calculado com base em 50% do preço do tipo de ingresso.
    /// </summary>
    public bool EhMeiaEntrada { get; set; }

    /// <summary>Data/hora do check-in na entrada do evento. Null = não utilizado.</summary>
    public DateTime? DataCheckin { get; set; }

    // ── Gateway de pagamento ────────────────────────────────────────────────

    /// <summary>
    /// ID da transação retornado pelo gateway de pagamento (Mercado Pago, Stripe, etc.).
    /// Necessário para solicitar estornos via EstornarAsync.
    /// </summary>
    public string? CodigoTransacaoGateway { get; set; }

    /// <summary>ID do estorno no gateway (preenchido após cancelamento com reembolso bem-sucedido).</summary>
    public string? IdEstornoGateway { get; set; }

    /// <summary>Data/hora do estorno processado pelo gateway.</summary>
    public DateTime? DataEstorno { get; set; }

    /// <summary>
    /// Status do pagamento conforme retornado pelo gateway (ex: 'approved', 'pending', 'rejected', 'refunded').
    /// Atualizado via webhook de confirmação do MercadoPago.
    /// </summary>
    public string? StatusPagamento { get; set; }

    /// <summary>
    /// Chave de idempotência enviada ao gateway de pagamento para evitar cobrança duplicada.
    /// Gerada uma única vez por tentativa de compra e reutilizada em retentativas.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    // ── PIX ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cópia do QR Code Pix (texto "copia e cola") retornado pelo gateway
    /// quando o método de pagamento é PIX. Usado para exibir o QR Code
    /// ao comprador imediatamente após a compra.
    /// </summary>
    public string? ChavePix { get; set; }
}
