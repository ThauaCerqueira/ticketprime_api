using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.Infrastructure.IRepository;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/ingressos")]
[EnableRateLimiting("geral")]
public class IngressoController : ControllerBase
{
    private readonly IReservaRepository _reservaRepo;
    private readonly AuditLogService _auditLog;

    public IngressoController(IReservaRepository reservaRepo, AuditLogService auditLog)
    {
        _reservaRepo = reservaRepo;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Validação de ingresso via código (somente ADMIN — para check-in na entrada do evento).
    /// Se válido, realiza o check-in e marca o ingresso como utilizado (consumo único).
    /// </summary>
    [HttpGet("{codigo}/validar")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> Validar(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Results.BadRequest(new { mensagem = "Código inválido." });

        var reserva = await _reservaRepo.ObterPorCodigoIngressoAsync(codigo);

        if (reserva == null)
            return Results.NotFound(new { valido = false, mensagem = "Ingresso não encontrado." });

        // Verifica se o ingresso já foi cancelado
        if (reserva.Status == "Cancelada")
            return Results.Ok(new
            {
                valido = false,
                mensagem = "Este ingresso foi cancelado e não é mais válido.",
                reservaId = reserva.Id,
                status = reserva.Status
            });

        // Verifica se o ingresso já foi usado em um check-in anterior
        if (reserva.DataCheckin.HasValue)
            return Results.Ok(new
            {
                valido = false,
                mensagem = $"Este ingresso já foi utilizado em {reserva.DataCheckin.Value:dd/MM/yyyy HH:mm:ss} UTC. Check-in duplicado rejeitado.",
                reservaId = reserva.Id,
                dataCheckin = reserva.DataCheckin.Value,
                status = reserva.Status
            });

        // Realiza o check-in atômico (marca como usado)
        var checkinRealizado = await _reservaRepo.RealizarCheckinAsync(codigo);

        if (!checkinRealizado)
            return Results.Conflict(new
            {
                valido = false,
                mensagem = "Não foi possível realizar o check-in. O ingresso pode já ter sido utilizado por outra pessoa."
            });

        // Registra check-in na auditoria
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.FirstOrDefault();
        await _auditLog.LogCheckinIngressoAsync(
            reserva.UsuarioCpf, reserva.EventoId, reserva.Id,
            ipAddress ?? "unknown", userAgent);

        return Results.Ok(new
        {
            valido = true,
            mensagem = "✅ Check-in realizado com sucesso! Bem-vindo ao evento.",
            reservaId = reserva.Id,
            usuarioCpf = reserva.UsuarioCpf,
            eventoNome = reserva.Nome,
            dataEvento = reserva.DataEvento,
            valorPago = reserva.ValorFinalPago,
            codigoIngresso = reserva.CodigoIngresso,
            dataCheckin = DateTime.UtcNow,
            status = "Usada"
        });
    }

    /// <summary>
    /// Consulta o status de um ingresso sem realizar check-in (somente ADMIN).
    /// Útil para verificar se um ingresso já foi usado antes de escanear.
    /// </summary>
    [HttpGet("{codigo}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> Consultar(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Results.BadRequest(new { mensagem = "Código inválido." });

        var reserva = await _reservaRepo.ObterPorCodigoIngressoAsync(codigo);

        if (reserva == null)
            return Results.NotFound(new { valido = false, mensagem = "Ingresso não encontrado." });

        return Results.Ok(new
        {
            valido = reserva.Status == "Ativa" && !reserva.DataCheckin.HasValue,
            reservaId = reserva.Id,
            usuarioCpf = reserva.UsuarioCpf,
            eventoNome = reserva.Nome,
            dataEvento = reserva.DataEvento,
            valorPago = reserva.ValorFinalPago,
            codigoIngresso = reserva.CodigoIngresso,
            status = reserva.Status,
            dataCheckin = reserva.DataCheckin
        });
    }
}
