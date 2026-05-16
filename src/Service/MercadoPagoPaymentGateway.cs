using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace src.Service;

/// <summary>
/// Gateway Mercado Pago — integração real via REST API v1.
/// Configure a variável de ambiente MercadoPago__AccessToken em produção.
/// Documentação: https://www.mercadopago.com.br/developers/pt/reference
/// </summary>
public sealed class MercadoPagoPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly ILogger<MercadoPagoPaymentGateway> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public MercadoPagoPaymentGateway(
        IHttpClientFactory httpClientFactory,
        ILogger<MercadoPagoPaymentGateway> logger)
    {
        _logger = logger;
        _http = httpClientFactory.CreateClient("MercadoPago");
    }

    public async Task<PaymentResult> ProcessarAsync(PaymentRequest request)
    {
        try
        {
            return request.MetodoPagamento switch
            {
                "pix" => await ProcessarPixAsync(request),
                "cartao_credito" => await ProcessarCartaoAsync(request, "credit_card"),
                "cartao_debito" => await ProcessarCartaoAsync(request, "debit_card"),
                _ => PaymentResult.Falha($"Método de pagamento não suportado: {request.MetodoPagamento}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar pagamento Mercado Pago");
            return PaymentResult.Falha("Erro interno ao processar pagamento. Tente novamente.");
        }
    }

    private async Task<PaymentResult> ProcessarPixAsync(PaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PagadorEmail))
            return PaymentResult.Falha("E-mail do pagador é obrigatório para pagamento Pix.");

        var body = new
        {
            transaction_amount = request.Valor,
            description = request.Descricao,
            payment_method_id = "pix",
            payer = new { email = request.PagadorEmail }
        };

        var (sucesso, id, responseBody) = await PostPaymentAsync(body, request.IdempotencyKey);
        if (!sucesso)
        {
            // ═══════════════════════════════════════════════════════════════════
            // SEGURANÇA: NUNCA logar o responseBody completo do gateway.
            //   O body pode conter dados sensíveis como erros de processamento
            //   de cartão, códigos de autorização, ou metadados do comprador.
            //   Logamos apenas status code e uma referência anonimizada.
            // ═══════════════════════════════════════════════════════════════════
            _logger.LogWarning("MP Pix falhou (anônimo). IdempotencyKey={Ik}",
                AnonymizeKey(request.IdempotencyKey));
            return PaymentResult.Falha("Não foi possível gerar o Pix. Tente novamente.");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var status = root.GetProperty("status").GetString();
        if (status != "pending")
            return PaymentResult.Falha($"Status inesperado do Pix: {status}. Aguarde e tente novamente.");

        var qrCode = root
            .GetProperty("point_of_interaction")
            .GetProperty("transaction_data")
            .GetProperty("qr_code")
            .GetString() ?? string.Empty;

        return PaymentResult.Ok(id!, qrCode);
    }

    private async Task<PaymentResult> ProcessarCartaoAsync(PaymentRequest request, string paymentMethodId)
    {
        if (string.IsNullOrWhiteSpace(request.CardToken))
            return PaymentResult.Falha(
                "Token do cartão é obrigatório. Utilize o SDK Mercado Pago no frontend para tokenizar o cartão.");

        if (string.IsNullOrWhiteSpace(request.PagadorEmail))
            return PaymentResult.Falha("E-mail do pagador é obrigatório.");

        var body = new
        {
            transaction_amount = request.Valor,
            token = request.CardToken,
            description = request.Descricao,
            installments = 1,
            payment_method_id = paymentMethodId,
            payer = new { email = request.PagadorEmail }
        };

        var (sucesso, id, responseBody) = await PostPaymentAsync(body, request.IdempotencyKey);
        if (!sucesso)
        {
            // ═══════════════════════════════════════════════════════════════════
            // SEGURANÇA: NUNCA logar responseBody do gateway de cartão.
            //   O body pode conter dados sensíveis como mensagens de recusa
            //   com informações do portador, códigos de autorização parciais,
            //   ou metadados do cartão tokenizado.
            // ═══════════════════════════════════════════════════════════════════
            _logger.LogWarning("MP Cartão falhou (anônimo). Método={Metodo}, Ik={Ik}",
                request.MetodoPagamento,
                AnonymizeKey(request.IdempotencyKey));
            return PaymentResult.Falha("Pagamento recusado. Verifique os dados do cartão e tente novamente.");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var status = doc.RootElement.GetProperty("status").GetString();

        if (status is not ("approved" or "in_process"))
            return PaymentResult.Falha($"Pagamento não aprovado. Status: {status}.");

        return PaymentResult.Ok(id!);
    }

    public async Task<RefundResult> EstornarAsync(string codigoTransacao, decimal valor, string motivo)
    {
        try
        {
            // Estorno total: omite o campo amount. Estorno parcial: envia amount.
            // Para simplificar, sempre enviamos o valor para suportar estorno parcial.
            var body = new { amount = valor };
            var json = JsonSerializer.Serialize(body, _jsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var req = new HttpRequestMessage(HttpMethod.Post, $"v1/payments/{codigoTransacao}/refunds")
            {
                Content = content
            };
            req.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());

            var response = await _http.SendAsync(req);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // ══════════════════════════════════════════════════════════
                // SEGURANÇA: Logar apenas status code, não o body da resposta.
                //   O body de erro do gateway pode conter metadados da
                //   transação original que não devem vazar para logs.
                // ══════════════════════════════════════════════════════════
                _logger.LogWarning(
                    "MP Estorno falhou: StatusCode={Status}, Transacao={Tx}",
                    response.StatusCode,
                    AnonymizeKey(codigoTransacao));
                return RefundResult.Falha("Estorno não processado pelo gateway. Contate o suporte.");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var refundId = doc.RootElement.GetProperty("id").GetRawText();

            _logger.LogInformation("Estorno MP aprovado. TransacaoId={Tx}, EstornoId={Ref}, Valor={Valor}",
                AnonymizeKey(codigoTransacao), AnonymizeKey(refundId), valor);

            return RefundResult.Ok(refundId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao estornar pagamento MP {CodigoTransacao}", codigoTransacao);
            return RefundResult.Falha("Erro interno ao processar estorno.");
        }
    }

    public async Task<PaymentStatusResult> ConsultarStatusAsync(string codigoTransacao)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"v1/payments/{codigoTransacao}");
            var response = await _http.SendAsync(req);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // ══════════════════════════════════════════════════════════
                // SEGURANÇA: Logar apenas status code, não o body da resposta.
                // ══════════════════════════════════════════════════════════
                _logger.LogWarning(
                    "MP ConsultaStatus falhou: StatusCode={Status}, Transacao={Tx}",
                    response.StatusCode,
                    AnonymizeKey(codigoTransacao));
                return PaymentStatusResult.Falha("Não foi possível consultar o status do pagamento no gateway.");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var statusGateway = root.GetProperty("status").GetString() ?? "unknown";

            var statusMapeado = statusGateway switch
            {
                "approved"             => PaymentGatewayStatus.Approved,
                "pending"              => PaymentGatewayStatus.Pending,
                "in_process"           => PaymentGatewayStatus.Pending,
                "in_mediation"         => PaymentGatewayStatus.Pending,
                "rejected"             => PaymentGatewayStatus.Rejected,
                "cancelled"            => PaymentGatewayStatus.Cancelled,
                "refunded"             => PaymentGatewayStatus.Refunded,
                "partially_refunded"   => PaymentGatewayStatus.Refunded,
                "charged_back"         => PaymentGatewayStatus.Refunded,
                _                      => PaymentGatewayStatus.Unknown
            };

            _logger.LogInformation(
                "MP ConsultaStatus: Transacao={Tx}, StatusGateway={StatusGateway}, StatusMapeado={StatusMapeado}",
                AnonymizeKey(codigoTransacao), statusGateway, statusMapeado);

            return PaymentStatusResult.Ok(statusMapeado, statusGateway);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status no Mercado Pago para transação {CodigoTransacao}", codigoTransacao);
            return PaymentStatusResult.Falha("Erro interno ao consultar status do pagamento.");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(bool sucesso, string? id, string responseBody)> PostPaymentAsync(object body, string? idempotencyKey = null)
    {
        var json = JsonSerializer.Serialize(body, _jsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var req = new HttpRequestMessage(HttpMethod.Post, "v1/payments") { Content = content };
        req.Headers.Add("X-Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());

        var response = await _http.SendAsync(req);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return (false, null, responseBody);

        using var doc = JsonDocument.Parse(responseBody);
        var id = doc.RootElement.GetProperty("id").GetRawText();

        return (true, id, responseBody);
    }

    /// <summary>
    /// Anonimiza chaves de transação/idempotência para logging seguro.
    /// Retorna apenas os primeiros 8 caracteres + "...", nunca o valor completo.
    /// Isso previne vazamento de dados sensíveis em logs de erro.
    /// </summary>
    private static string AnonymizeKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return "(empty)";

        return key.Length <= 8
            ? key
            : string.Concat(key.AsSpan(0, 8), "...");
    }
}
