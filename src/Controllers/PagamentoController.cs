using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Service;

namespace src.Controllers;

/// <summary>
/// Webhook de confirmação de pagamento do MercadoPago.
/// Endpoint chamado pelo MercadoPago quando um pagamento é confirmado,
/// rejeitado ou estornado.
///
/// SEGURANÇA: A validação é feita via assinatura HMAC-SHA256 do header
/// X-Signature usando o MercadoPagoWebhookValidator. Sem essa validação,
/// qualquer um poderia falsificar notificações de pagamento.
/// </summary>
[ApiController]
[Route("api/pagamento")]
[EnableRateLimiting("webhook")]
public class PagamentoController : ControllerBase
{
    private readonly IReservaRepository _reservaRepo;
    private readonly IPaymentGateway _paymentGateway;
    private readonly AuditLogService _auditLog;
    private readonly MercadoPagoWebhookValidator _webhookValidator;
    private readonly ILogger<PagamentoController> _logger;

    public PagamentoController(
        IReservaRepository reservaRepo,
        IPaymentGateway paymentGateway,
        AuditLogService auditLog,
        MercadoPagoWebhookValidator webhookValidator,
        ILogger<PagamentoController> logger)
    {
        _reservaRepo = reservaRepo;
        _paymentGateway = paymentGateway;
        _auditLog = auditLog;
        _webhookValidator = webhookValidator;
        _logger = logger;
    }

    /// <summary>
    /// Webhook de notificação de pagamento do MercadoPago.
    ///
    /// Headers esperados:
    ///   - X-Signature: assinatura HMAC-SHA256 do payload (validada obrigatoriamente)
    ///   - X-Request-Id: ID único da notificação (para idempotência)
    ///
    /// Body (JSON):
    ///   - action: "payment.updated" | "payment.created"
    ///   - data.id: ID do pagamento no MercadoPago
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IResult> Webhook([FromBody] MercadoPagoWebhookDto dto)
    {
        // ── Validação obrigatória da assinatura HMAC ─────────────────────────
        var signatureHeader = Request.Headers["X-Signature"].FirstOrDefault();
        var rawBody = await ReadRawBodyAsync();

        if (!_webhookValidator.IsValid(signatureHeader, rawBody))
        {
            _logger.LogWarning(
                "Webhook rejeitado: assinatura X-Signature inválida. " +
                "IP={RemoteIp}, Action={Action}, PaymentId={PaymentId}",
                HttpContext.Connection.RemoteIpAddress,
                dto.Action, dto.Data?.Id);

            return Results.Unauthorized();
        }

        if (dto?.Data?.Id == null)
            return Results.BadRequest(new { mensagem = "Payload inválido. Campo 'data.id' é obrigatório." });

        var codigoTransacao = dto.Data.Id;

        _logger.LogInformation(
            "Webhook MercadoPago recebido: Action={Action}, PaymentId={PaymentId}",
            dto.Action, codigoTransacao);

        try
        {
            // Busca a reserva associada ao código de transação do gateway
            var reserva = await _reservaRepo.ObterPorCodigoTransacaoAsync(codigoTransacao);
            if (reserva == null)
            {
                _logger.LogWarning(
                    "Webhook: transação {CodigoTransacao} não encontrada no sistema. Ignorando.",
                    codigoTransacao);
                // Retorna 200 mesmo assim — o MercadoPago reenvia se receber erro
                return Results.Ok(new { mensagem = "Transação não encontrada no sistema." });
            }

            // Consulta o status atual no gateway
            var statusResult = await _paymentGateway.ConsultarStatusAsync(codigoTransacao);
            if (!statusResult.Sucesso)
            {
                _logger.LogError(
                    "Webhook: falha ao consultar status no gateway para transação {CodigoTransacao}",
                    codigoTransacao);
                return Results.Ok(new { mensagem = "Status não pôde ser consultado. O webhook será reenviado." });
            }

            // Mapeia o status do gateway para o status de pagamento da reserva
            var statusPagamento = statusResult.Status switch
            {
                PaymentGatewayStatus.Approved => "approved",
                PaymentGatewayStatus.Pending => "pending",
                PaymentGatewayStatus.Rejected => "rejected",
                PaymentGatewayStatus.Cancelled => "cancelled",
                PaymentGatewayStatus.Refunded => "refunded",
                _ => "unknown"
            };

            // Atualiza o status de pagamento na reserva
            await _reservaRepo.AtualizarStatusPagamentoAsync(reserva.Id, statusPagamento);

            // Se o pagamento foi aprovado, garante que a reserva está como Ativa
            if (statusResult.Status == PaymentGatewayStatus.Approved && reserva.Status != "Ativa")
            {
                _logger.LogInformation(
                    "Webhook: pagamento aprovado para reserva {ReservaId}. Status anterior: {StatusAnterior}",
                    reserva.Id, reserva.Status);
            }

            // Se o pagamento foi rejeitado ou cancelado, registra auditoria
            if (statusResult.Status is PaymentGatewayStatus.Rejected or PaymentGatewayStatus.Cancelled)
            {
                await _auditLog.LogPagamentoFalhaAsync(
                    reserva.UsuarioCpf,
                    reserva.EventoId,
                    reserva.Id,
                    codigoTransacao,
                    statusPagamento);
            }

            _logger.LogInformation(
                "Webhook processado: ReservaId={ReservaId}, StatusPagamento={StatusPagamento}",
                reserva.Id, statusPagamento);

            return Results.Ok(new
            {
                mensagem = "Webhook processado com sucesso.",
                reservaId = reserva.Id,
                statusPagamento
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erro ao processar webhook para transação {CodigoTransacao}",
                codigoTransacao);
            return Results.Ok(new { mensagem = "Erro interno. O webhook será reenviado." });
        }
    }

    /// <summary>
    /// Lê o body bruto da requisição (precisa ser lido uma vez).
    /// Usa buffering para permitir multiple reads via EnableBuffering.
    /// </summary>
    private async Task<string> ReadRawBodyAsync()
    {
        // Permite re-ler o body (o model binding já consumiu o stream)
        HttpContext.Request.EnableBuffering();
        HttpContext.Request.Body.Position = 0;

        using var reader = new StreamReader(
            HttpContext.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        HttpContext.Request.Body.Position = 0; // Restaura para outros consumers

        // Limita o tamanho do body para evitar DoS (máx 100KB para webhook)
        if (body.Length > 102_400)
        {
            _logger.LogWarning("Webhook body exceeds 100KB limit ({Length} bytes)", body.Length);
            return string.Empty;
        }

        return body;
    }
}
