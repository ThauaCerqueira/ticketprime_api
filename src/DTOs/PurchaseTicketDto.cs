namespace src.DTOs;

public class PurchaseTicketDto
{
    public int EventoId { get; set; }

    /// <summary>
    /// ID do tipo de ingresso (setor) escolhido pelo comprador.
    /// Obrigatório quando o evento possui múltiplos tipos de ingresso.
    /// </summary>
    public int TicketTypeId { get; set; }

    public string? CupomUtilizado { get; set; }
    /// <summary>
    /// Quando true, cobra 15% do preço do ingresso como seguro de devolução integral.
    /// Sem seguro, a devolução retorna apenas o valor do ingresso (sem a taxa de serviço).
    /// </summary>
    public bool ContratarSeguro { get; set; }

    /// <summary>
    /// Indica que o comprador optou pela meia-entrada (50% do preço do tipo de ingresso).
    /// O evento deve ter TemMeiaEntrada = true para esta opção ser válida.
    /// Quando true, o campo DocumentoBase64 é OBRIGATÓRIO (Lei 12.933/2013).
    /// </summary>
    public bool EhMeiaEntrada { get; set; }

    // ── Documento comprobatório de meia-entrada (Lei 12.933/2013) ──────────

    /// <summary>
    /// Arquivo do documento comprobatório (carteirinha estudantil, identidade de idoso,
    /// laudo médico, etc.) codificado em Base64.
    /// OBRIGATÓRIO quando EhMeiaEntrada = true.
    /// Tamanho máximo: 10 MB. Formatos aceitos: JPEG, PNG, WebP, PDF.
    /// </summary>
    public string? DocumentoBase64 { get; set; }

    /// <summary>
    /// Nome original do arquivo enviado (ex: "carteirinha_estudantil.jpg").
    /// OBRIGATÓRIO quando DocumentoBase64 é informado.
    /// </summary>
    public string? DocumentoNome { get; set; }

    /// <summary>
    /// MIME type do arquivo (ex: "image/jpeg", "application/pdf").
    /// OBRIGATÓRIO quando DocumentoBase64 é informado.
    /// </summary>
    public string? DocumentoContentType { get; set; }

    // ── Dados de pagamento ────────────────────────────────────────────────

    /// <summary>"pix" | "cartao_credito" | "cartao_debito". Padrão: "pix".</summary>
    public string MetodoPagamento { get; set; } = "pix";

    /// <summary>
    /// Token de cartão gerado pelo SDK Mercado Pago (MercadoPago.js v2) no navegador.
    /// Obrigatório quando MetodoPagamento é cartao_credito ou cartao_debito.
    /// NUNCA os dados completos do cartão transitam pelo servidor — apenas o token.
    /// </summary>
    public string? CardToken { get; set; }

    /// <summary>
    /// Últimos 4 dígitos do cartão — apenas para exibição no recibo.
    /// NUNCA envie o número completo do cartão para o backend.
    /// Obrigatório quando MetodoPagamento é cartao_credito ou cartao_debito.
    /// </summary>
    public string? Ultimos4Cartao { get; set; }

    /// <summary>Nome do titular conforme impresso no cartão.</summary>
    public string? NomeTitular { get; set; }

    /// <summary>Validade do cartão no formato MM/AA (opcional, apenas para exibição no recibo).</summary>
    public string? ValidadeCartao { get; set; }
}

