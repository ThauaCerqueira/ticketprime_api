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
    private readonly IStorageService _storage;

    public ProfileController(UserService userService, IUsuarioRepository usuarioRepo, IStorageService storage)
    {
        _userService = userService;
        _usuarioRepo = usuarioRepo;
        _storage = storage;
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
            usuario.Telefone,
            usuario.Bio,
            usuario.FotoUrl,
            usuario.BannerUrl
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

        // Validação: requer ao menos 10 dígitos numéricos (DDD + número), máximo 11 (com 9 dígito)
        if (!string.IsNullOrEmpty(dto.Telefone))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Telefone, @"^[\d\s\(\)\+\-]{7,20}$"))
                return Results.BadRequest(new { mensagem = "Telefone inválido. Use o formato (11) 91234-5678." });
            var digits = System.Text.RegularExpressions.Regex.Replace(dto.Telefone, @"[^\d]", "");
            if (digits.Length < 10 || digits.Length > 11)
                return Results.BadRequest(new { mensagem = "Telefone inválido. Informe DDD + número (10 ou 11 dígitos)." });
        }

        await _usuarioRepo.AtualizarTelefone(cpf, dto.Telefone);
        return Results.Ok(new { mensagem = "Telefone atualizado com sucesso." });
    }

    /// <summary>
    /// Upload da foto de perfil ou banner do organizador.
    /// POST /api/perfil/organizador/upload?tipo=foto
    /// POST /api/perfil/organizador/upload?tipo=banner
    /// </summary>
    [HttpPost("organizador/upload")]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IResult> UploadOrganizadorFoto(
        IFormFile file,
        [FromQuery] string tipo = "foto")
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest(new { mensagem = "Nenhum arquivo enviado." });

        if (tipo != "foto" && tipo != "banner")
            return Results.BadRequest(new { mensagem = "Tipo inválido. Use 'foto' ou 'banner'." });

        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new { mensagem = "Formato não aceito. Use JPEG, PNG ou WebP." });

        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        try
        {
            using var stream = file.OpenReadStream();
            var subpasta = tipo == "foto" ? "organizadores/fotos" : "organizadores/banners";
            var nomeArquivo = $"{cpf}_{DateTime.UtcNow:yyyyMMddHHmmss}.{file.ContentType.Split('/')[1]}";

            var url = await _storage.SalvarAsync(stream, nomeArquivo, file.ContentType, subpasta);

            if (tipo == "foto")
                await _usuarioRepo.AtualizarFotoUrl(cpf, url);
            else
                await _usuarioRepo.AtualizarBannerUrl(cpf, url);

            return Results.Ok(new { mensagem = $"{tipo} atualizado com sucesso.", url });
        }
        catch (Exception ex)
        {
            return Results.Json(new { mensagem = $"Erro ao fazer upload: {ex.Message}" }, statusCode: 500);
        }
    }

    /// <summary>
    /// Atualiza a biografia do organizador.
    /// </summary>
    [HttpPatch("organizador/bio")]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> AtualizarBio([FromBody] AtualizarBioDto dto)
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Bio))
            return Results.BadRequest(new { mensagem = "A biografia não pode estar vazia." });

        await _usuarioRepo.AtualizarBio(cpf, dto.Bio.Trim());
        return Results.Ok(new { mensagem = "Biografia atualizada com sucesso." });
    }
}

public record AtualizarTelefoneDto(string? Telefone);
public record AtualizarBioDto(string Bio);
