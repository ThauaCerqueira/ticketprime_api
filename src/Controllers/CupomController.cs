using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.DTOs;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/cupons")]
[EnableRateLimiting("geral")]
public class CupomController : ControllerBase
{
    private readonly CupomService _CupomService;
    private readonly ILogger<CupomController> _logger;

    public CupomController(CupomService CupomService, ILogger<CupomController> logger)
    {
        _CupomService = CupomService;
        _logger = logger;
    }

    /// <summary>
    /// Cria um novo cupom de desconto (somente ADMIN).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Criar([FromBody] CreateCouponDto dto)
    {
        try
        {
            var sucesso = await _CupomService.CriarAsync(dto);
            if (sucesso)
                return Results.Created($"/api/cupons/{dto.Codigo}", dto);

            return Results.BadRequest(new { mensagem = "Não foi possível criar o cupom." });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao criar cupôm {Codigo}", dto.Codigo);
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }

    /// <summary>
    /// Lista todos os cupons (somente ADMIN).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> Listar()
    {
        var cupons = await _CupomService.ListarAsync();
        return Results.Ok(cupons);
    }

    /// <summary>
    /// Obtém um cupom pelo código (somente ADMIN).
    /// </summary>
    [HttpGet("{codigo}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> ObterPorCodigo(string codigo)
    {
        var cupom = await _CupomService.ObterPorCodigoAsync(codigo);
        if (cupom == null)
            return Results.NotFound(new { mensagem = "Cupom não encontrado." });
        return Results.Ok(cupom);
    }

    /// <summary>
    /// Remove um cupom (somente ADMIN).
    /// </summary>
    [HttpDelete("{codigo}")]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Deletar(string codigo)
    {
        try
        {
            var removido = await _CupomService.DeletarAsync(codigo);
            if (removido)
                return Results.NoContent();
            return Results.NotFound(new { mensagem = "Cupom não encontrado." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar cupom {Codigo}", codigo);
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }
}
