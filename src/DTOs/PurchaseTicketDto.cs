using System.ComponentModel.DataAnnotations;

namespace src.DTOs;

public class PurchaseTicketDto : IValidatableObject
{
    /// <summary>ID do evento. Obrigatório.</summary>
    [Required(ErrorMessage = "O ID do evento é obrigatório.")]
    [Range(1, int.MaxValue, ErrorMessage = "O ID do evento deve ser um valor positivo.")]
    public int EventoId { get; set; }

    /// <summary>
    /// ID do tipo de ingresso (setor) escolhido pelo comprador.
    /// Obrigatório quando o evento possui múltiplos tipos de ingresso.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "O ID do tipo de ingresso deve ser um valor válido.")]
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
    [Required(ErrorMessage = "O método de pagamento é obrigatório.")]
    [RegularExpression("^(pix|cartao_credito|cartao_debito)$", ErrorMessage = "Método de pagamento inválido. Use pix, cartao_credito ou cartao_debito.")]
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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // ── Validação condicional: cartão exige token + nome do titular ──
        if (MetodoPagamento is "cartao_credito" or "cartao_debito")
        {
            if (string.IsNullOrWhiteSpace(CardToken))
                yield return new ValidationResult(
                    "O token do cartão é obrigatório para pagamento com cartão. Utilize o SDK Mercado Pago no frontend.",
                    [nameof(CardToken)]);

            if (string.IsNullOrWhiteSpace(NomeTitular))
                yield return new ValidationResult(
                    "O nome do titular do cartão é obrigatório.",
                    [nameof(NomeTitular)]);

            if (string.IsNullOrWhiteSpace(Ultimos4Cartao))
                yield return new ValidationResult(
                    "Os últimos 4 dígitos do cartão são obrigatórios.",
                    [nameof(Ultimos4Cartao)]);
        }

        // ── Validação condicional: meia-entrada exige documento ─────────
        if (EhMeiaEntrada && string.IsNullOrWhiteSpace(DocumentoBase64))
        {
            yield return new ValidationResult(
                "O documento comprobatório é obrigatório para meia-entrada (Lei 12.933/2013).",
                [nameof(DocumentoBase64)]);
        }
    }
}

