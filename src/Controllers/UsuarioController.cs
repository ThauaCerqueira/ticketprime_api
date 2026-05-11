using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.Infrastructure;
using src.Models;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/usuarios")]
[EnableRateLimiting("geral")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;
    private readonly DbConnectionFactory _connectionFactory;

    public UserController(UserService userService, DbConnectionFactory connectionFactory)
    {
        _userService = userService;
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Auto-cadastro de usuário (perfil sempre CLIENTE).
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Cadastrar([FromBody] User usuario)
    {
        try
        {
            // Perfil é sempre CLIENTE em auto-cadastro — nunca aceitar valor enviado pelo cliente
            usuario.Perfil = "CLIENTE";

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers.UserAgent.FirstOrDefault();

            var resultado = await _userService.CadastrarUsuario(usuario, ipAddress, userAgent);
            return Results.Created($"/api/usuarios/{resultado.Cpf}", new { mensagem = "Usuário criado com sucesso.", dados = resultado });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception)
        {
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }

    // ── LGPD: Direitos do Titular (Lei 13.709/2018) ──────────────────────────

    /// <summary>
    /// LGPD Art. 18 I — Acesso: retorna todos os dados pessoais do titular.
    /// </summary>
    [HttpGet("meus-dados")]
    [Authorize]
    public async Task<IResult> MeusDados()
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        try
        {
            var usuario = await _userService.BuscarPorCpf(cpf);
            if (usuario == null)
                return Results.NotFound(new { mensagem = "Usuário não encontrado." });

            using var conn = _connectionFactory.CreateConnection();

            var reservas = await conn.QueryAsync(
                @"SELECT r.Id, r.EventoId, e.Nome AS Evento, r.DataCompra,
                         r.ValorFinalPago, r.Status, r.CodigoIngresso
                  FROM Reservas r
                  INNER JOIN Eventos e ON e.Id = r.EventoId
                  WHERE r.UsuarioCpf = @Cpf
                  ORDER BY r.DataCompra DESC",
                new { Cpf = cpf });

            return Results.Ok(new
            {
                titular = new
                {
                    cpf          = usuario.Cpf,
                    nome         = usuario.Nome,
                    email        = usuario.Email,
                    telefone     = usuario.Telefone,
                    perfil       = usuario.Perfil,
                    emailVerificado = usuario.EmailVerificado
                },
                compras  = reservas,
                geradoEm = DateTime.UtcNow
            });
        }
        catch (Exception)
        {
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }

    /// <summary>
    /// LGPD Art. 18 VI — Exclusão: anonimiza os dados pessoais do titular.
    /// O CPF e o histórico financeiro são mantidos para obrigações legais (Art. 16 II).
    /// </summary>
    [HttpDelete("minha-conta")]
    [Authorize]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> ExcluirConta()
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        try
        {
            // Verifica se há reservas ativas — não pode excluir com ingressos válidos
            using var conn = _connectionFactory.CreateConnection();
            var reservasAtivas = await conn.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM Reservas WHERE UsuarioCpf = @Cpf AND Status = 'Ativa'",
                new { Cpf = cpf });

            if (reservasAtivas > 0)
                return Results.BadRequest(new
                {
                    mensagem = "Não é possível excluir a conta com ingressos ativos. Cancele todos os ingressos antes de prosseguir."
                });

            // Anonimização (não deleção física) — mantém histórico financeiro para obrigações legais
            await conn.ExecuteAsync(
                @"UPDATE Usuarios
                  SET Nome     = 'Usuário Removido',
                      Email    = CONCAT('removido_', Cpf, '@anonimizado.local'),
                      Senha    = '',
                      Telefone = NULL,
                      TokenVerificacaoEmail = NULL,
                      TokenExpiracaoEmail   = NULL,
                      ResetToken            = NULL,
                      ResetTokenExpiracao   = NULL
                  WHERE Cpf = @Cpf",
                new { Cpf = cpf });

            // Invalida todos os refresh tokens do usuário
            await conn.ExecuteAsync(
                "UPDATE RefreshTokens SET RevokedAt = GETUTCDATE() WHERE UsuarioCpf = @Cpf AND RevokedAt IS NULL",
                new { Cpf = cpf });

            return Results.Ok(new { mensagem = "Seus dados pessoais foram anonimizados conforme a LGPD. O histórico financeiro é mantido por obrigação legal." });
        }
        catch (Exception)
        {
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }
}
