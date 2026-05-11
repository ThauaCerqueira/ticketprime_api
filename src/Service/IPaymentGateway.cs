namespace src.Service;

/// <summary>
/// Informações de pagamento enviadas pelo comprador.
/// Nunca armazenar dados de cartão completos — apenas os últimos 4 dígitos para exibição.
/// </summary>
public sealed class PaymentRequest
{
    /// <summary>"pix" | "cartao_credito" | "cartao_debito"</summary>
    public string MetodoPagamento { get; init; } = "pix";

    /// <summary>Valor total a cobrar (ingresso + taxa + seguro).</summary>
    public decimal Valor { get; init; }

    /// <summary>Descrição exibida na fatura/extrato do comprador.</summary>
    public string Descricao { get; init; } = string.Empty;

    /// <summary>Últimos 4 dígitos do cartão — apenas para exibição de recibo. Nunca o número completo.</summary>
    public string? Ultimos4Cartao { get; init; }

    /// <summary>Nome do titular conforme impresso no cartão.</summary>
    public string? NomeTitular { get; init; }

    /// <summary>Validade no formato MM/AA.</summary>
    public string? ValidadeCartao { get; init; }

    /// <summary>E-mail do pagador — obrigatório para Pix no Mercado Pago.</summary>
    public string? PagadorEmail { get; init; }

    /// <summary>Token de cartão gerado pelo SDK Mercado Pago no frontend (nunca dados raw).</summary>
    public string? CardToken { get; init; }
}

/// <summary>
/// Resultado retornado pelo gateway após processar o pagamento.
/// </summary>
public sealed class PaymentResult
{
    public bool Sucesso { get; init; }

    /// <summary>Código único da transação no gateway (para conciliação e estorno).</summary>
    public string? CodigoTransacao { get; init; }

    /// <summary>Chave Pix ou QR Code para pagamento Pix (quando aplicável).</summary>
    public string? ChavePix { get; init; }

    /// <summary>Mensagem de erro legível quando Sucesso = false.</summary>
    public string? MensagemErro { get; init; }

    public static PaymentResult Ok(string codigoTransacao, string? chavePix = null) =>
        new() { Sucesso = true, CodigoTransacao = codigoTransacao, ChavePix = chavePix };

    public static PaymentResult Falha(string mensagem) =>
        new() { Sucesso = false, MensagemErro = mensagem };
}

/// <summary>
/// Resultado da consulta de status de uma transação no gateway.
/// Mapeia os status do gateway (approved, rejected, pending, cancelled, refunded)
/// para um enum padronizado usado internamente.
/// </summary>
public sealed class PaymentStatusResult
{
    public bool Sucesso { get; init; }

    /// <summary>Status padronizado da transação no gateway.</summary>
    public PaymentGatewayStatus Status { get; init; }

    /// <summary>Status bruto retornado pelo gateway (para depuração).</summary>
    public string? StatusGateway { get; init; }

    /// <summary>Mensagem de erro quando Sucesso = false.</summary>
    public string? MensagemErro { get; init; }

    public static PaymentStatusResult Ok(PaymentGatewayStatus status, string? statusGateway) =>
        new() { Sucesso = true, Status = status, StatusGateway = statusGateway };

    public static PaymentStatusResult Falha(string mensagem) =>
        new() { Sucesso = false, MensagemErro = mensagem };
}

/// <summary>
/// Status padronizado de uma transação no gateway de pagamento,
/// independente do gateway utilizado (Mercado Pago, Stripe, etc.).
/// </summary>
public enum PaymentGatewayStatus
{
    /// <summary>Pagamento aprovado e confirmado.</summary>
    Approved,
    /// <summary>Pagamento pendente (ex: aguardando pagamento PIX).</summary>
    Pending,
    /// <summary>Pagamento recusado/rejeitado.</summary>
    Rejected,
    /// <summary>Pagamento cancelado (ex: PIX expirou).</summary>
    Cancelled,
    /// <summary>Pagamento reembolsado/estornado.</summary>
    Refunded,
    /// <summary>Status desconhecido ou não mapeado.</summary>
    Unknown
}

/// <summary>
/// Resultado de um estorno/reembolso solicitado ao gateway.
/// </summary>
public sealed class RefundResult
{
    public bool Sucesso { get; init; }

    /// <summary>ID do estorno no gateway (para rastreio e conciliação).</summary>
    public string? IdEstorno { get; init; }

    /// <summary>Mensagem de erro legível quando Sucesso = false.</summary>
    public string? MensagemErro { get; init; }

    public static RefundResult Ok(string idEstorno) =>
        new() { Sucesso = true, IdEstorno = idEstorno };

    public static RefundResult Falha(string mensagem) =>
        new() { Sucesso = false, MensagemErro = mensagem };
}

/// <summary>
/// Contrato para processamento de pagamentos e estornos.
/// Implemente esta interface para integrar qualquer gateway (Mercado Pago, Stripe, etc.).
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ProcessarAsync(PaymentRequest request);

    /// <summary>
    /// Solicita o estorno total ou parcial de uma transação.
    /// </summary>
    /// <param name="codigoTransacao">ID da transação original retornado por ProcessarAsync.</param>
    /// <param name="valor">Valor a estornar (pode ser menor que o total para estorno parcial).</param>
    /// <param name="motivo">Motivo legível para registro no gateway.</param>
    Task<RefundResult> EstornarAsync(string codigoTransacao, decimal valor, string motivo);

    /// <summary>
    /// Consulta o status atual de uma transação no gateway de pagamento.
    /// Utilizado para reconciliar o estado de pagamentos pendentes (ex: PIX),
    /// verificando se o PIX foi pago, expirou ou foi cancelado.
    /// </summary>
    /// <param name="codigoTransacao">ID da transação retornado por ProcessarAsync.</param>
    /// <returns>Status atual da transação no gateway.</returns>
    Task<PaymentStatusResult> ConsultarStatusAsync(string codigoTransacao);
}
