using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace src.Service;

/// <summary>
/// Gateway de pagamento simulado — aprova todos os pagamentos automaticamente.
/// ═══════════════════════════════════════════════════════════════════
/// ⚠️ ATENÇÃO: Este gateway NÃO processa pagamentos reais!
///
/// Uso:
///   - Desenvolvimento e testes apenas.
///   - Em produção, substitua por MercadoPagoPaymentGateway configurando
///     a variável de ambiente 'MercadoPago__AccessToken'.
///
///   Se você está vendo esta mensagem em produção, PARE IMEDIATAMENTE.
///   Clientes podem estar comprando ingressos sem pagar de verdade!
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    private static readonly Random _rng = new();
    private readonly ILogger<SimulatedPaymentGateway> _logger;

    /// <summary>
    /// ⚠️ AVISO DE SEGURANÇA: loga um alerta sempre que é instanciado.
    /// </summary>
    public SimulatedPaymentGateway(ILogger<SimulatedPaymentGateway>? logger = null)
    {
        _logger = logger ?? NullLogger<SimulatedPaymentGateway>.Instance;
        _logger.LogWarning(
            "⚠️ [SEGURANÇA] SIMULATED PAYMENT GATEWAY ATIVO! " +
            "Nenhum pagamento real está sendo processado. " +
            "Configure MercadoPago__AccessToken para produção.");
    }

    public Task<PaymentResult> ProcessarAsync(PaymentRequest request)
    {
        _logger.LogWarning(
            "⚠️ [SIMULATED] Processando pagamento SIMULADO de {Valor:C} para '{Descricao}'. " +
            "Método: {Metodo}. NENHUM PAGAMENTO REAL FOI PROCESSADO!",
            request.Valor, request.Descricao, request.MetodoPagamento);

        // Gera código de transação único (simulado)
        var txId = $"SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{_rng.Next(100000, 999999)}";

        if (request.MetodoPagamento == "pix")
        {
            // Em produção, aqui seria gerado o payload Pix (EMV/QR Code)
            const string chavePix = "ticketprime@pagamentos.pix"; // chave fixa de exemplo
            return Task.FromResult(PaymentResult.Ok(txId, chavePix));
        }

        // Cartão: valida apenas que últimos 4 dígitos foram informados (sem cobrar nada de verdade)
        if (request.MetodoPagamento is "cartao_credito" or "cartao_debito")
        {
            if (string.IsNullOrWhiteSpace(request.Ultimos4Cartao)
                || request.Ultimos4Cartao.Length != 4
                || !request.Ultimos4Cartao.All(char.IsDigit))
            {
                return Task.FromResult(PaymentResult.Falha("Dados do cartão inválidos."));
            }
        }

        return Task.FromResult(PaymentResult.Ok(txId));
    }

    public Task<RefundResult> EstornarAsync(string codigoTransacao, decimal valor, string motivo)
    {
        _logger.LogWarning(
            "⚠️ [SIMULATED] Estorno SIMULADO da transação {Codigo}. NENHUM ESTORNO REAL FOI PROCESSADO!",
            codigoTransacao);
        var refundId = $"REF-SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{_rng.Next(100000, 999999)}";
        return Task.FromResult(RefundResult.Ok(refundId));
    }

    public Task<PaymentStatusResult> ConsultarStatusAsync(string codigoTransacao)
    {
        // Gateway simulado: transações são sempre aprovadas.
        // Prefixos que indicam estorno: se a transação foi estornada (prefixo REF-SIM-),
        // retorna Refunded; caso contrário Approved.
        var status = codigoTransacao.StartsWith("REF-SIM-", StringComparison.OrdinalIgnoreCase)
            ? PaymentGatewayStatus.Refunded
            : PaymentGatewayStatus.Approved;

        return Task.FromResult(PaymentStatusResult.Ok(status, status.ToString()));
    }
}
