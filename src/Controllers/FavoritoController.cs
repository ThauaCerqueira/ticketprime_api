using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.Infrastructure.IRepository;
using System.Security.Claims;

namespace src.Controllers;

[ApiController]
[Route("api/favoritos")]
[EnableRateLimiting("geral")]
[Authorize]
public class FavoritoController : ControllerBase
{
    private readonly IFavoritoRepository _favoritoRepo;
    private readonly ILogger<FavoritoController> _logger;

    public FavoritoController(IFavoritoRepository favoritoRepo, ILogger<FavoritoController> logger)
    {
        _favoritoRepo = favoritoRepo;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IResult> Listar()
    {
        var cpf = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf)) return Results.Unauthorized();
        var favoritos = await _favoritoRepo.ListarPorUsuarioAsync(cpf);
        return Results.Ok(favoritos);
    }

    [HttpPost("{eventoId:int}")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Adicionar(int eventoId)
    {
        var cpf = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf)) return Results.Unauthorized();
        try
        {
            var existe = await _favoritoRepo.IsFavoritoAsync(cpf, eventoId);
            if (!existe)
                await _favoritoRepo.AdicionarAsync(cpf, eventoId);
            return Results.Ok(new { mensagem = "Evento adicionado aos favoritos." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao favoritar evento {EventoId}", eventoId);
            return Results.Json(new { mensagem = "Erro ao favoritar evento." }, statusCode: 500);
        }
    }

    [HttpDelete("{eventoId:int}")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Remover(int eventoId)
    {
        var cpf = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf)) return Results.Unauthorized();
        try
        {
            await _favoritoRepo.RemoverAsync(cpf, eventoId);
            return Results.Ok(new { mensagem = "Evento removido dos favoritos." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover favorito {EventoId}", eventoId);
            return Results.Json(new { mensagem = "Erro ao remover favorito." }, statusCode: 500);
        }
    }

    [HttpGet("{eventoId:int}/check")]
    public async Task<IResult> Check(int eventoId)
    {
        var cpf = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf)) return Results.Unauthorized();
        var isFav = await _favoritoRepo.IsFavoritoAsync(cpf, eventoId);
        return Results.Ok(new { isFavorito = isFav });
    }
}
