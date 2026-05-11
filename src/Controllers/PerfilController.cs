using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.Infrastructure.IRepository;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/perfil")]
[EnableRateLimiting("geral")]
public class ProfileController : ControllerBase
{
    private readonly UserService _userService;
    private readonly IUsuarioRepository _usuarioRepo;

    public ProfileController(UserService userService, IUsuarioRepository usuarioRepo)
    {
        _userService = userService;
        _usuarioRepo = usuarioRepo;
    }

    /// <summary>
    /// Retorna os dados do perfil do usuário autenticado.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IResult> ObterPerfil()
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        var usuario = await _userService.BuscarPorCpf(cpf);

        if (usuario == null)
            return Results.NotFound(new { mensagem = "Usuário não encontrado." });

        return Results.Ok(new
        {
            usuario.Cpf,
            usuario.Nome,
            usuario.Email,
            usuario.Perfil,
            usuario.Telefone
        });
    }

    /// <summary>
    /// Atualiza o telefone de contato do usuário autenticado.
    /// </summary>
    [HttpPatch("telefone")]
    [Authorize]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> AtualizarTelefone([FromBody] AtualizarTelefoneDto dto)
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        // Validação simples: apenas dígitos, parênteses, hifens, espaços e '+'
        if (!string.IsNullOrEmpty(dto.Telefone) &&
            !System.Text.RegularExpressions.Regex.IsMatch(dto.Telefone, @"^[\d\s\(\)\+\-]{7,20}$"))
            return Results.BadRequest(new { mensagem = "Telefone inválido. Use o formato (11) 91234-5678." });

        await _usuarioRepo.AtualizarTelefone(cpf, dto.Telefone);
        return Results.Ok(new { mensagem = "Telefone atualizado com sucesso." });
    }
}

public record AtualizarTelefoneDto(string? Telefone);
