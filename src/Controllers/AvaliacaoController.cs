using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.Infrastructure.IRepository;
using src.Service;
using System.Security.Claims;

namespace src.Controllers;

[ApiController]
[Route("api/eventos/{eventoId:int}/avaliacoes")]
[EnableRateLimiting("geral")]
public class AvaliacaoController : ControllerBase
{
    private readonly AvaliacaoService _avaliacaoService;
    private readonly IAvaliacaoRepository _avaliacaoRepo;

    public AvaliacaoController(AvaliacaoService avaliacaoService, IAvaliacaoRepository avaliacaoRepo)
    {
        _avaliacaoService = avaliacaoService;
        _avaliacaoRepo = avaliacaoRepo;
    }

    /// <summary>
    /// Lista avaliações de um evento e retorna a média de notas.
    /// </summary>
    [HttpGet]
    public async Task<IResult> Listar(int eventoId)
    {
        var avaliacoes = await _avaliacaoRepo.ListarPorEventoAsync(eventoId);
        var media = await _avaliacaoRepo.ObterMediaAsync(eventoId);
        return Results.Ok(new { media, avaliacoes });
    }

    /// <summary>
    /// Envia uma avaliação para o evento. Requer ingresso com status 'Usada'.
    /// </summary>
    [HttpPost]
    [Authorize]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Avaliar(int eventoId, [FromBody] AvaliarDto dto)
    {
        var cpf = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        try
        {
            await _avaliacaoService.AvaliarAsync(cpf, eventoId, dto.Nota, dto.Comentario, dto.Anonima);
            return Results.Created($"/api/eventos/{eventoId}/avaliacoes", new { mensagem = "Avaliação registrada com sucesso." });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch
        {
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }
}

/// <summary>
/// DTO para criação de avaliação.
/// </summary>
public record AvaliarDto(byte Nota, string? Comentario, bool Anonima = false);
