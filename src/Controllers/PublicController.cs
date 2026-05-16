using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using src.DTOs;
using src.Infrastructure.IRepository;

namespace src.Controllers;

/// <summary>
/// Endpoints públicos (sem autenticação) usados pela landing page.
/// </summary>
[ApiController]
[Route("api/public")]
[EnableRateLimiting("geral")]
public class PublicController : ControllerBase
{
    private readonly IEventoRepository _eventoRepo;
    private readonly IReservaRepository _reservaRepo;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PublicController> _logger;

    private const string CacheKeyStats = "PublicStats";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public PublicController(
        IEventoRepository eventoRepo,
        IReservaRepository reservaRepo,
        IUsuarioRepository usuarioRepo,
        IMemoryCache cache,
        ILogger<PublicController> logger)
    {
        _eventoRepo = eventoRepo;
        _reservaRepo = reservaRepo;
        _usuarioRepo = usuarioRepo;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Estatísticas públicas exibidas na Home (eventos publicados, ingressos vendidos, usuários cadastrados).
    /// Cache de 5 minutos — as estatísticas não mudam a cada requisição.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IResult> HomeStats()
    {
        var stats = await _cache.GetOrCreateAsync(CacheKeyStats, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            _logger.LogDebug("Cache miss para {CacheKey}. Consultando banco...", CacheKeyStats);

            var eventosPublicados = (await _eventoRepo.ObterDisponiveisAsync()).Count();
            var totalReservas     = await _reservaRepo.ContarReservasAsync();
            var totalUsuarios     = await _usuarioRepo.ContarUsuariosAsync();

            return new HomeStatsDTO
            {
                TotalEventosPublicados  = eventosPublicados,
                TotalIngressosVendidos  = totalReservas,
                TotalUsuarios           = totalUsuarios,
            };
        });

        return Results.Ok(stats);
    }

    /// <summary>
    /// Retorna um arquivo iCalendar (.ics) para o evento, permitindo ao usuário
    /// adicionar o evento ao Google Calendar, Apple Calendar ou Outlook.
    /// </summary>
    [HttpGet("eventos/{id:int}/calendar.ics")]
    public async Task<IResult> CalendarIcs(int id)
    {
        var evento = await _eventoRepo.ObterPorIdAsync(id);
        if (evento == null)
            return Results.NotFound();

        var dtStart = evento.DataEvento.ToString("yyyyMMdd'T'HHmmss");
        var dtEnd   = (evento.DataTermino ?? evento.DataEvento.AddHours(4)).ToString("yyyyMMdd'T'HHmmss");
        var uid     = $"evento-{evento.Id}@ticketprime.com";
        var summary = evento.Nome.Replace(",", "\\,").Replace(";", "\\;");
        var location = evento.Local.Replace(",", "\\,").Replace(";", "\\;");
        var descricao = (evento.Descricao ?? string.Empty)
                            .Replace(",", "\\,")
                            .Replace(";", "\\;")
                            .Replace("\n", "\\n")
                            .Replace("\r", "");

        var ics = $"""
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//TicketPrime//TicketPrime//PT-BR
            CALSCALE:GREGORIAN
            METHOD:PUBLISH
            BEGIN:VEVENT
            UID:{uid}
            DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}
            DTSTART:{dtStart}
            DTEND:{dtEnd}
            SUMMARY:{summary}
            LOCATION:{location}
            DESCRIPTION:{descricao}
            URL:https://{Request.Host}/evento/{evento.Id}
            STATUS:CONFIRMED
            END:VEVENT
            END:VCALENDAR
            """;

        var bytes = System.Text.Encoding.UTF8.GetBytes(ics);
        return Results.File(bytes, "text/calendar; charset=utf-8",
                            $"{evento.Nome.Replace(" ", "_")}.ics");
    }

    /// <summary>
    /// Retorna o perfil público de um organizador: nome e seus eventos publicados.
    /// A URL usa um slug opaco (GUID) em vez do CPF para evitar enumeração.
    /// </summary>
    [HttpGet("organizadores/{slug}")]
    public async Task<IResult> PerfilOrganizador(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return Results.BadRequest(new { mensagem = "Slug inválido." });

        var usuario = await _usuarioRepo.ObterPorSlug(slug);
        if (usuario == null)
            return Results.NotFound(new { mensagem = "Organizador não encontrado." });

        // Fetch events by this organizer that are published
        var todosEventos = await _eventoRepo.ObterDisponiveisAsync();
        var eventosDoOrganizador = todosEventos
            .Where(e => e.OrganizadorCpf == usuario.Cpf)
            .Select(e => new
            {
                e.Id, e.Nome, e.DataEvento, e.Local, e.PrecoPadrao,
                e.GeneroMusical, e.CapacidadeTotal, e.Status
            })
            .ToList();

        return Results.Ok(new
        {
            slug = usuario.Slug,
            nome = usuario.Nome,
            eventosPublicados = eventosDoOrganizador
        });
    }

    /// <summary>
    /// Retorna o thumbnail da primeira foto de um evento como imagem, para uso
    /// como og:image em redes sociais (WhatsApp, Facebook, Twitter).
    /// A resposta é uma imagem com cache de 1 hora (via header Cache-Control).
    /// </summary>
    [HttpGet("eventos/{id:int}/thumbnail")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IResult> EventoThumbnail(int id)
    {
        var evento = await _eventoRepo.ObterPorIdAsync(id);
        if (evento == null)
            return Results.NotFound();

        // 1. Tenta servir a imagem de capa a partir do disco (ImagemUrl do storage)
        if (!string.IsNullOrEmpty(evento.ImagemUrl))
        {
            var wwwRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var caminhoImagem = Path.GetFullPath(Path.Combine(wwwRoot, evento.ImagemUrl.TrimStart('/')));

            // Valida path traversal — garante que está dentro de wwwroot
            if (caminhoImagem.StartsWith(Path.GetFullPath(wwwRoot), StringComparison.OrdinalIgnoreCase)
                && System.IO.File.Exists(caminhoImagem))
            {
                var ext = Path.GetExtension(caminhoImagem).ToLowerInvariant();
                var mime = ext switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    _ => "image/jpeg"
                };
                var bytes = await System.IO.File.ReadAllBytesAsync(caminhoImagem);
                return Results.File(bytes, mime);
            }
        }

        // 2. Fallback: thumbnail Base64 salvo no banco (EventoFotos.ThumbnailBase64)
        var thumbnailBase64 = await _eventoRepo.ObterThumbnailPorEventoAsync(id);
        if (!string.IsNullOrEmpty(thumbnailBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(thumbnailBase64);
                return Results.File(bytes, "image/jpeg");
            }
            catch (FormatException)
            {
                // Base64 inválido — continua para o placeholder
            }
        }

        // 3. Placeholder SVG quando não há imagem disponível
        var nomeEvento = System.Net.WebUtility.HtmlEncode(evento.Nome);
        var localEvento = System.Net.WebUtility.HtmlEncode(evento.Local);
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="1200" height="630" viewBox="0 0 1200 630">
              <rect width="1200" height="630" fill="#5B5BD6"/>
              <text x="600" y="280" font-family="Arial,sans-serif" font-size="48" fill="white" text-anchor="middle" font-weight="bold">{nomeEvento}</text>
              <text x="600" y="350" font-family="Arial,sans-serif" font-size="24" fill="rgba(255,255,255,0.8)" text-anchor="middle">{localEvento}</text>
              <text x="600" y="420" font-family="Arial,sans-serif" font-size="20" fill="rgba(255,255,255,0.6)" text-anchor="middle">ticketprime.com</text>
            </svg>
            """;
        return Results.File(System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml");
    }
}
