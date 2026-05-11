using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace src.Infrastructure;

/// <summary>
/// Fábrica de políticas de rate limiting que particionam por usuário autenticado (CPF)
/// em vez de apenas IP. Resolve o problema de botnets com múltiplos IPs:
/// cada conta tem seu próprio limite, independentemente de quantos IPs o atacante utilizar.
///
/// Use em conjunto com <c>options.AddPolicy(string, Func{HttpContext, RateLimitPartition{string}})</c>.
/// </summary>
public static class PerUserRateLimiterPolicy
{
    /// <summary>
    /// Cria uma política de rate limiting do tipo "fixed window" que particiona
    /// por usuário autenticado (CPF via ClaimTypes.NameIdentifier).
    ///
    /// - Usuários autenticados: particiona por CPF (ex: "user_12345678900")
    /// - Usuários não autenticados: particiona por IP (ex: "ip_192.168.1.1")
    /// - Admins: recebem um limite mais alto automaticamente
    /// </summary>
    /// <param name="name">Nome da política (ex: "escrita", "compra-ingresso")</param>
    /// <param name="anonymousLimit">Limite de requisições por janela para não autenticados</param>
    /// <param name="authenticatedLimit">Limite de requisições por janela para usuários comuns</param>
    /// <param name="adminLimit">Limite de requisições por janela para admins</param>
    /// <param name="window">Janela de tempo (padrão: 1 minuto)</param>
    public static void AddPerUserPolicy(
        this RateLimiterOptions options,
        string name,
        int anonymousLimit = 5,
        int authenticatedLimit = 20,
        int adminLimit = 100,
        TimeSpan? window = null)
    {
        window ??= TimeSpan.FromMinutes(1);

        options.AddPolicy(name, context =>
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAuthenticated = !string.IsNullOrEmpty(userId);
            var isAdmin = context.User.IsInRole("ADMIN");

            // Define o limite conforme o perfil
            var permitLimit = isAdmin
                ? adminLimit
                : isAuthenticated
                    ? authenticatedLimit
                    : anonymousLimit;

            // A chave de partição garante isolamento por usuário ou IP
            var partitionKey = isAuthenticated
                ? $"user_{userId}"
                : $"ip_{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = window.Value,
                    PermitLimit = permitLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });
    }

    /// <summary>
    /// Versão simplificada com limites padrão para endpoints sensíveis (compra de ingressos).
    /// </summary>
    public static void AddPerUserCompraPolicy(this RateLimiterOptions options, string name)
    {
        // Compra de ingresso: 3/min autenticado, 1/min anônimo (impossível comprar sem login),
        // 30/min admin
        options.AddPerUserPolicy(name,
            anonymousLimit: 1,
            authenticatedLimit: 3,
            adminLimit: 30,
            window: TimeSpan.FromMinutes(1));
    }
}
