using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/crypto")]
[EnableRateLimiting("geral")]
public class CryptoController : ControllerBase
{
    private readonly CryptoKeyService _cryptoKeyService;

    public CryptoController(CryptoKeyService cryptoKeyService)
    {
        _cryptoKeyService = cryptoKeyService;
    }

    /// <summary>
    /// Chave pública ECDH P-256 do servidor para E2E encryption de fotos.
    /// </summary>
    [HttpGet("chave-publica")]
    public IResult ChavePublica()
    {
        return Results.Json(new
        {
            chavePublicaJwk = _cryptoKeyService.ObterChavePublicaJwk(),
            algoritmo = "ECDH",
            curva = "P-256",
            versao = "1.0"
        });
    }

    /// <summary>
    /// Chave pública por evento — cada evento tem seu próprio par de chaves ECDH.
    /// </summary>
    [HttpGet("chave-publica/{eventoId:int}")]
    public IResult ChavePublicaEvento(int eventoId)
    {
        return Results.Json(new
        {
            chavePublicaJwk = _cryptoKeyService.ObterChavePublicaEventoJwk(eventoId),
            algoritmo = "ECDH",
            curva = "P-256",
            eventoId = eventoId,
            versao = "2.0"
        });
    }
}
