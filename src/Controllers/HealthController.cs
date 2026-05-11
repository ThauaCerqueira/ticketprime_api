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
    /// Health check com verificação do banco de dados.
    /// </summary>
    [HttpGet("/health")]
    public async Task<IResult> Health([FromServices] DbConnectionFactory dbFactory)
    {
        try
        {
            using var conn = dbFactory.CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
            conn.Close();

            return Results.Json(new
            {
                status = "OK",
                mensagem = "API e banco de dados operacionais. 🎫",
                database = "conectado",
                timestamp = DateTime.UtcNow,
                ambiente = "TicketPrime API"
            });
        }
        catch (Exception ex)
        {
            return Results.Json(new
            {
                status = "Degradado",
                mensagem = "API rodando, mas banco de dados indisponível.",
                database = "desconectado",
                erro = ex.Message,
                timestamp = DateTime.UtcNow,
                ambiente = "TicketPrime API"
            }, statusCode: 503);
        }
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
