using src.Service;

namespace src.Infrastructure;

/// <summary>
/// Middleware que registra métricas de todas as requisições HTTP
/// (endpoint, status code, duração) via MetricsService.
/// Extraído do Program.cs para manter a configuração organizada.
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;

    public MetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var metrics = context.RequestServices.GetRequiredService<MetricsService>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            metrics.RecordRequest(
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedTicks);
        }
    }
}
