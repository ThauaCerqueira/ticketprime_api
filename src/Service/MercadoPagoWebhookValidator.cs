using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace src.Service;

/// <summary>
/// Validador de assinatura HMAC-SHA256 para webhooks do MercadoPago.
///
/// O MercadoPago envia um header X-Signature contendo ts e v1 separados por vírgula.
/// O HMAC é calculado sobre o payload do body combinado com o timestamp usando
/// o webhook secret configurado em MercadoPago:WebhookSecret.
///
/// Referência: https://www.mercadopago.com.br/developers/pt/docs/your-integrations/notifications/webhooks
/// </summary>
public class MercadoPagoWebhookValidator
{
    private readonly string _webhookSecret;
    private readonly ILogger<MercadoPagoWebhookValidator> _logger;

    // Tolerância de 5 minutos para clock skew entre servidores
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    public MercadoPagoWebhookValidator(
        IConfiguration configuration,
        ILogger<MercadoPagoWebhookValidator> logger)
    {
        _logger = logger;
        _webhookSecret = configuration["MercadoPago:WebhookSecret"]
            ?? configuration["MercadoPago__WebhookSecret"]
            ?? string.Empty;
    }

    /// <summary>
    /// Valida a assinatura do webhook. Retorna true se a assinatura for válida.
    /// </summary>
    public virtual bool IsValid(string? signatureHeader, string requestBody)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret))
        {
            _logger.LogWarning(
                "MercadoPago:WebhookSecret não configurado. " +
                "Webhook validation BYPASSED — aceitando requisição sem assinatura. " +
                "Risco de segurança! Configure MercadoPago__WebhookSecret em produção.");

            // Se for Produção, loga com Critical — admin precisa agir
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
            {
                _logger.LogCritical(
                    "🚨 PRODUÇÃO: Webhook validation DESABILITADA! " +
                    "Pagamentos podem ser falsificados. Configure MercadoPago__WebhookSecret IMEDIATAMENTE.");
            }

            return true; // Permite em dev; em prod, avisa mas não bloqueia
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("Webhook rejeitado: header X-Signature ausente.");
            return false;
        }

        // Formato esperado: ts=1234567890,v1=abcdef123456...
        var parts = signatureHeader
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        long? timestamp = null;
        string? signatureV1 = null;

        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;

            switch (kv[0])
            {
                case "ts":
                    if (long.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts))
                        timestamp = ts;
                    break;
                case "v1":
                    signatureV1 = kv[1];
                    break;
            }
        }

        if (timestamp == null || signatureV1 == null)
        {
            _logger.LogWarning(
                "Webhook rejeitado: formato X-Signature inválido. Header: {Header}",
                signatureHeader);
            return false;
        }

        // Verifica timestamp (anti-replay attack)
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var age = Math.Abs(now - timestamp.Value);

        if (age > MaxAge.TotalSeconds)
        {
            _logger.LogWarning(
                "Webhook rejeitado: timestamp muito antigo/futuro. " +
                "Timestamp={Timestamp}, Age={Age}s, MaxAge={MaxAge}s",
                timestamp, age, MaxAge.TotalSeconds);
            return false;
        }

        // Calcula o HMAC esperado
        // Template: "id:{data.id};transaction-amount:{valor};status:{status};"
        // Se não conseguir extrair do body, usa fallback com body inteiro
        var expectedSignature = ComputeHmac(signatureV1, requestBody, timestamp.Value);

        // Comparação timing-safe
        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureV1),
            Encoding.UTF8.GetBytes(expectedSignature));

        if (!isValid)
        {
            _logger.LogWarning(
                "Webhook rejeitado: assinatura inválida. " +
                "Esperada={Expected}, Recebida={Received}",
                expectedSignature[..Math.Min(expectedSignature.Length, 16)],
                signatureV1[..Math.Min(signatureV1.Length, 16)]);
        }

        return isValid;
    }

    /// <summary>
    /// Tenta validar usando o formato template-based primeiro,
    /// e fallback para body inteiro.
    /// </summary>
    private string ComputeHmac(string receivedSignature, string requestBody, long timestamp)
    {
        // Tenta extrair data.id, transaction_amount, status do body JSON
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            var root = doc.RootElement;

            var dataId = root.TryGetProperty("data", out var data)
                && data.TryGetProperty("id", out var idProp)
                ? idProp.GetString() ?? ""
                : "";

            var amount = root.TryGetProperty("transaction_amount", out var amtProp)
                ? amtProp.GetRawText()
                : "";

            var status = root.TryGetProperty("status", out var stProp)
                ? stProp.GetString() ?? ""
                : "";

            // Template: "id:{data.id};transaction-amount:{valor};status:{status};"
            var templatePayload = $"id:{dataId};transaction-amount:{amount};status:{status};";
            var templateResult = ComputeHmacWithKey(templatePayload, timestamp);
            if (templateResult == receivedSignature)
                return templateResult;

            // Fallback: body inteiro como payload
            var bodyResult = ComputeHmacWithKey(requestBody, timestamp);
            if (bodyResult == receivedSignature)
                return bodyResult;
        }
        catch (JsonException)
        {
            // Body não é JSON válido — usa raw body
        }

        // Fallback final: body como string
        return ComputeHmacWithKey(requestBody, timestamp);
    }

    private string ComputeHmacWithKey(string payload, long timestamp)
    {
        // Segue a documentação do MercadoPago:
        // HMAC-SHA256(secret, "ts={timestamp}|{payload}")
        var data = $"ts={timestamp}|{payload}";

        var keyBytes = Encoding.UTF8.GetBytes(_webhookSecret);
        var dataBytes = Encoding.UTF8.GetBytes(data);

#if NET8_0_OR_GREATER
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, dataBytes)).ToLowerInvariant();
#else
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLowerInvariant();
#endif
    }
}
