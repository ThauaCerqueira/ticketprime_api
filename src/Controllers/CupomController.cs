using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.DTOs;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/cupons")]
[EnableRateLimiting("geral")]
public class CouponController : ControllerBase
{
    private readonly CouponService _couponService;
    private readonly ILogger<CouponController> _logger;

    public CouponController(CouponService couponService, ILogger<CouponController> logger)
    {
        _couponService = couponService;
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
            var sucesso = await _couponService.CriarAsync(dto);
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
    /// Lista todos os cupons (autenticado).
    /// </summary>
    [HttpGet]
    public async Task<IResult> Listar()
    {
        var cupons = await _couponService.ListarAsync();
        return Results.Ok(cupons);
    }

    /// <summary>
    /// Obtém um cupom pelo código (autenticado).
    /// </summary>
    [HttpGet("{codigo}")]
    public async Task<IResult> ObterPorCodigo(string codigo)
    {
        var cupom = await _couponService.ObterPorCodigoAsync(codigo);
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
        var removido = await _couponService.DeletarAsync(codigo);
        if (removido)
            return Results.NoContent();
        return Results.NotFound(new { mensagem = "Cupom não encontrado." });
    }
}
