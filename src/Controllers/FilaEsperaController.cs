using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.Infrastructure.IRepository;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/fila-espera")]
[EnableRateLimiting("geral")]
public class WaitingQueueController : ControllerBase
{
    private readonly IWaitingQueueService _waitingQueueService;

    public WaitingQueueController(IWaitingQueueService waitingQueueService)
    {
        _waitingQueueService = waitingQueueService;
    }

    /// <summary>
    /// Entra na fila de espera de um evento lotado.
    /// </summary>
    [HttpPost("entrar")]
    [Authorize]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> EntrarNaFila([FromQuery] int eventoId)
    {
        try
        {
            var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(cpf))
                return Results.Unauthorized();

            var resultado = await _waitingQueueService.EntrarNaFilaAsync(cpf, eventoId);
            return Results.Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>
    /// Sai da fila de espera de um evento (desistência voluntária).
    /// </summary>
    [HttpPost("sair")]
    [Authorize]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> SairDaFila([FromQuery] int eventoId)
    {
        try
        {
            var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(cpf))
                return Results.Unauthorized();

            await _waitingQueueService.SairDaFilaAsync(cpf, eventoId);
            return Results.Ok(new { mensagem = "Você saiu da fila de espera." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>
    /// Lista as filas de espera do usuário autenticado.
    /// </summary>
    [HttpGet("minhas-filas")]
    [Authorize]
    public async Task<IResult> MinhasFilas()
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        var filas = await _waitingQueueService.ListarMinhasFilasAsync(cpf);
        return Results.Ok(filas);
    }

    /// <summary>
    /// Lista a fila de espera de um evento (somente ADMIN).
    /// </summary>
    [HttpGet("evento/{eventoId:int}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> ListarFilaPorEvento(int eventoId)
    {
        try
        {
            var fila = await _waitingQueueService.ListarFilaPorEventoAsync(eventoId);
            return Results.Ok(fila);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { mensagem = ex.Message });
        }
    }
}
