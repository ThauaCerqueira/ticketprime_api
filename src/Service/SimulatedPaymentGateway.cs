namespace src.Service;

/// <summary>
/// Gateway de pagamento simulado — aprova todos os pagamentos automaticamente.
/// Use em desenvolvimento e testes. Substitua por MercadoPagoPaymentGateway,
/// StripePaymentGateway etc. em produção via injeção de dependência.
/// </summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    private static readonly Random _rng = new();

    public Task<PaymentResult> ProcessarAsync(PaymentRequest request)
    {
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
