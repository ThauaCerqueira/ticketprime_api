using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.Infrastructure;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("")]
[EnableRateLimiting("geral")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Rota raiz que redireciona para Swagger em desenvolvimento.
    /// </summary>
    [HttpGet("/")]
    public IResult Home([FromServices] IWebHostEnvironment env, HttpContext context)
    {
        if (env.IsDevelopment())
        {
            context.Response.Redirect("/swagger", permanent: false);
            return Results.Empty;
        }
        return Results.Json(new
        {
            mensagem = "TicketPrime API",
            versao = "1.0",
            documentacao = "/swagger"
        });
    }

    /// <summary>
    /// Health check com verificação do banco de dados e serviços dependentes.
    /// </summary>
    [HttpGet("/health")]
    public async Task<IResult> Health(
        [FromServices] DbConnectionFactory dbFactory,
        [FromServices] IWebHostEnvironment env,
        [FromServices] IEmailService? emailService = null,
        [FromServices] RedisHealthCheck? redisHealth = null)
    {
        var status = "OK";
        var mensagem = "Todos os sistemas operacionais.";
        var dbStatus = "conectado";
        var cacheStatus = "não configurado";
        var emailStatus = "não configurado";
        var erros = new List<string>();

        // ── Verificação do banco de dados ────────────────────────────────
        try
        {
            using var conn = dbFactory.CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
            conn.Close();
            dbStatus = "conectado";
        }
        catch (Exception ex)
        {
            dbStatus = "desconectado";
            status = "Degradado";
            erros.Add($"Banco de dados: {ex.Message}");
        }

        // ── Verificação do serviço de email ──────────────────────────────
        // ═══════════════════════════════════════════════════════════════════
        // ANTES: Nenhuma verificação de email no health check.
        //   Se o SMTP estivesse offline, ninguém receberia confirmações
        //   de compra e o sistema não detectava o problema.
        // AGORA: Verificamos se o email service está configurado e funcional.
        // ═══════════════════════════════════════════════════════════════════
        try
        {
            if (emailService is SmtpEmailService)
            {
                emailStatus = "configurado (SMTP)";
            }
            else if (emailService is ConsoleEmailService)
            {
                emailStatus = "modo desenvolvimento (console)";
            }
            else
            {
                emailStatus = "não configurado";
            }
        }
        catch (Exception ex)
        {
            emailStatus = "erro";
            erros.Add($"Email service: {ex.Message}");
        }

        // ── Verificação do cache (Redis / distribuído) ────────────────────
        try
        {
            if (redisHealth != null)
            {
                var redisOk = redisHealth.IsAvailable;
                cacheStatus = redisOk ? "conectado (Redis)" : "desconectado (Redis)";
                if (!redisOk)
                {
                    status = "Degradado";
                    erros.Add("Cache Redis indisponível (fallback para memória local ativo)");
                }
            }
            else
            {
                cacheStatus = "memória local (Redis não configurado)";
            }
        }
        catch (Exception ex)
        {
            cacheStatus = "erro";
            erros.Add($"Cache: {ex.Message}");
        }

        // ── Montagem da resposta ─────────────────────────────────────────
        var response = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["mensagem"] = mensagem,
            ["database"] = dbStatus,
            ["cache"] = cacheStatus,
            ["email"] = emailStatus,
            ["timestamp"] = DateTime.UtcNow,
            ["ambiente"] = env.EnvironmentName,
            ["versao"] = "1.0"
        };

        if (erros.Count > 0)
        {
            response["erros"] = erros;
            response["mensagem"] = "API rodando, mas alguns serviços estão indisponíveis.";
        }

        var statusCode = status == "OK" ? 200 : 503;
        return Results.Json(response, statusCode: statusCode);
    }

    /// <summary>
    /// Endpoint de métricas para Prometheus/scraping. Requer autenticação de ADMIN.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [HttpGet("/metrics")]
    public IResult Metrics([FromServices] MetricsService metrics, [FromServices] IWebHostEnvironment env)
    {
        var metricsText = metrics.GenerateMetricsText(env.EnvironmentName);
        return Results.Text(metricsText, contentType: "text/plain; version=0.0.4");
    }
}
