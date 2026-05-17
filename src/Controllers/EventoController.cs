using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Models;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/eventos")]
[EnableRateLimiting("geral")]
public class EventoController : ControllerBase
{
    private readonly EventoService _eventService;
    private readonly IEventoRepository _eventoRepository;
    private readonly IStorageService _storage;
    private readonly ILogger<EventoController> _logger;
    private readonly IDistributedCache _cache;

    private const string CacheKeyEventos = "EventosDisponiveis";
    private static readonly TimeSpan CacheEventosDuration = TimeSpan.FromMinutes(2);

    public EventoController(EventoService eventService, IEventoRepository eventoRepository, IStorageService storage, ILogger<EventoController> logger, IDistributedCache cache)
    {
        _eventService = eventService;
        _eventoRepository = eventoRepository;
        _storage = storage;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Cria um novo evento (somente ADMIN).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Criar([FromBody] CreateEventDto dto)
    {
        try
        {
            var organizadorCpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var resultado = await _eventService.CriarNovoEvento(dto, organizadorCpf);
            if (resultado == null)
                return Results.BadRequest(new { mensagem = "Erro ao criar evento." });

            return Results.Created($"/api/eventos/{resultado.Id}", resultado);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao criar evento");
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }

    /// <summary>
    /// Lista eventos com paginação (cache distribuído de 2 minutos).
    /// ═══════════════════════════════════════════════════════════════════
    /// ANTES: IMemoryCache — cache local por réplica.
    ///   Em deploy multi-instância, cada réplica tinha seu próprio cache,
    ///   causando dados inconsistentes entre requisições.
    ///
    /// AGORA: IDistributedCache — cache compartilhado via Redis (se configurado)
    ///   ou DistributedMemoryCache (fallback para single-instance).
    ///   Dados consistentes independentemente de quantas réplicas.
    /// ═══════════════════════════════════════════════════════════════════
    /// </summary>
    [HttpGet]
    public async Task<IResult> Listar(int pagina = 1, int tamanhoPagina = 20)
    {
        if (tamanhoPagina < 1) tamanhoPagina = 1;
        if (tamanhoPagina > 100) tamanhoPagina = 100;
        if (pagina < 1) pagina = 1;

        var cacheKey = $"{CacheKeyEventos}_p{pagina}_s{tamanhoPagina}";

        // Tenta obter do cache distribuído
        var cachedBytes = await _cache.GetAsync(cacheKey);
        if (cachedBytes != null)
        {
            var cached = JsonSerializer.Deserialize<PaginatedResult<TicketEvent>>(cachedBytes);
            if (cached != null)
                return Results.Ok(cached);
        }

        // Cache miss — consulta o banco
        var eventos = await _eventService.ListarEventos(pagina, tamanhoPagina);

        // Armazena no cache distribuído
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheEventosDuration
        };
        var serialized = JsonSerializer.SerializeToUtf8Bytes(eventos);
        await _cache.SetAsync(cacheKey, serialized, options);

        return Results.Ok(eventos);
    }

    /// <summary>
    /// Obtém detalhes completos de um evento pelo ID, incluindo fotos, vagas restantes,
    /// tipos de ingresso (setores) e lotes progressivos.
    /// Endpoint público (sem autenticação).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IResult> Detalhe(int id)
    {
        var evento = await _eventService.ObterPorIdAsync(id);
        if (evento == null)
            return Results.NotFound(new { mensagem = "Evento não encontrado." });

        var fotos = await _eventService.ObterFotosAsync(id);
        var vagasOcupadas = await _eventService.ContarReservasAsync(id);
        var vagasRestantes = Math.Max(0, evento.CapacidadeRestante);

        // Carrega tipos de ingresso (setores) e lotes progressivos
        var tiposIngresso = await _eventoRepository.ObterTiposIngressoPorEventoAsync(id);
        var lotes = await _eventoRepository.ObterLotesPorEventoAsync(id);

        var dto = new EventDetailDto
        {
            Id                      = evento.Id,
            Nome                    = evento.Nome,
            Descricao               = evento.Descricao,
            ImagemUrl               = evento.ImagemUrl,
            DataEvento              = evento.DataEvento,
            DataTermino             = evento.DataTermino,
            Local                   = evento.Local,
            GeneroMusical           = evento.GeneroMusical,
            PrecoPadrao             = evento.PrecoPadrao,
            TaxaServico             = evento.TaxaServico,
            EventoGratuito          = evento.EventoGratuito,
            CapacidadeTotal         = evento.CapacidadeTotal,
            LimiteIngressosPorUsuario = evento.LimiteIngressosPorUsuario,
            Status                  = evento.Status,
            TemMeiaEntrada          = evento.TemMeiaEntrada,
            FotoThumbnailBase64     = evento.FotoThumbnailBase64,
            Fotos                   = fotos,
            VagasRestantes          = vagasRestantes,
            PoliticaReembolso       = evento.TaxaServico > 0
                ? "Cancelamento gratuito com reembolso integral via seguro de devolução (15% do valor do ingresso)."
                : "Reembolso sujeito à análise. Consulte os termos no momento da compra.",
            TiposIngresso = tiposIngresso.Select(t => new TicketTypeDetailDto
            {
                Id = t.Id,
                Nome = t.Nome,
                Descricao = t.Descricao,
                Preco = t.Preco,
                CapacidadeTotal = t.CapacidadeTotal,
                CapacidadeRestante = t.CapacidadeRestante,
                Ordem = t.Ordem
            }).ToList(),
            Lotes = lotes.Select(l => new LoteDetailDto
            {
                Id = l.Id,
                Nome = l.Nome,
                Preco = l.Preco,
                QuantidadeMaxima = l.QuantidadeMaxima,
                QuantidadeVendida = l.QuantidadeVendida,
                Disponivel = l.EstaDisponivel()
            }).ToList()
        };

        return Results.Ok(dto);
    }

    /// <summary>
    /// Busca/filtro de eventos disponíveis: ?nome=rock&amp;genero=Rock&amp;dataMin=2026-01-01&amp;dataMax=2026-12-31
    /// Utiliza SQL Server Full-Text Search com ranking por relevância quando ?nome= é informado.
    /// </summary>
    [HttpGet("disponiveis")]
    public async Task<IResult> BuscarDisponiveis(
        string? nome, string? genero,
        DateTime? dataMin, DateTime? dataMax,
        string? cidade,
        int pagina = 1, int tamanhoPagina = 20)
    {
        if (tamanhoPagina < 1) tamanhoPagina = 1;
        if (tamanhoPagina > 100) tamanhoPagina = 100;
        if (pagina < 1) pagina = 1;

        var eventos = await _eventService.BuscarEventos(nome, genero, dataMin, dataMax, cidade, pagina, tamanhoPagina);
        return Results.Ok(eventos);
    }

    /// <summary>
    /// Sugestões de busca (autocomplete) usando full-text search: ?q=rock&amp;limite=5
    /// Retorna eventos ordenados por relevância. Ideal para combo/search box no frontend.
    /// </summary>
    [HttpGet("sugestoes")]
    public async Task<IResult> Sugestoes(string? q, int limite = 5)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.Ok(Array.Empty<object>());

        if (limite < 1) limite = 1;
        if (limite > 20) limite = 20;

        var sugestoes = await _eventService.BuscarSugestoes(q, limite);
        return Results.Ok(sugestoes);
    }

    /// <summary>
    /// Lista todos os eventos (somente ADMIN).
    /// </summary>
    [HttpGet("meus")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> ListarTodos()
    {
        var eventos = await _eventService.ListarEventos();
        return Results.Ok(eventos);
    }

    /// <summary>
    /// Publica um evento (somente ADMIN).
    /// </summary>
    [HttpPut("{id:int}/publicar")]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Publicar(int id)
    {
        try
        {
            await _eventService.PublicarEventoAsync(id);
            return Results.Ok(new { mensagem = "Evento publicado com sucesso!" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao publicar evento {EventoId}", id);
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }

    /// <summary>
    /// Exclui um evento (somente ADMIN).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Excluir(int id)
    {
        try
        {
            await _eventService.DeletarEventoAsync(id);
            return Results.Ok(new { mensagem = "Evento excluído com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao excluir evento {EventoId}", id);
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }

    /// <summary>
    /// Cancela um evento com notificação a todos os compradores (somente ADMIN).
    /// </summary>
    [HttpPost("{id:int}/cancelar-com-notificacao")]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> CancelarComNotificacao(int id)
    {
        try
        {
            await _eventService.CancelarEventoComNotificacaoAsync(id);
            return Results.Ok(new { mensagem = "Evento cancelado e compradores notificados por email." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao cancelar evento {EventoId}", id);
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }

    /// <summary>
    /// Notifica compradores sobre alteração no evento (somente ADMIN).
    /// </summary>
    [HttpPost("{id:int}/notificar-alteracao")]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> NotificarAlteracao(int id, string tipo, string detalhes)
    {
        try
        {
            await _eventService.NotificarAlteracaoEventoAsync(id, tipo, detalhes);
            return Results.Ok(new { mensagem = "Compradores notificados sobre a alteração." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao notificar alteração do evento {EventoId}", id);
            return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
        }
    }

    /// <summary>
    /// Upload de imagem de capa do evento (somente ADMIN).
    /// Aceita JPEG, PNG, WebP ou GIF — máximo 5 MB.
    /// Retorna a URL pública da imagem salva.
    /// </summary>
    [HttpPost("{id:int}/imagem")]
    [Authorize(Roles = "ADMIN")]
    [EnableRateLimiting("escrita")]
    [RequestSizeLimit(6 * 1024 * 1024)] // 6 MB de headroom para o multipart
    public async Task<IResult> UploadImagem(int id, IFormFile arquivo)
    {
        if (arquivo == null || arquivo.Length == 0)
            return Results.BadRequest(new { mensagem = "Nenhum arquivo enviado." });

        var evento = await _eventService.ObterPorIdAsync(id);
        if (evento == null)
            return Results.NotFound(new { mensagem = "Evento não encontrado." });

        try
        {
            await using var stream = arquivo.OpenReadStream();
            var url = await _storage.SalvarAsync(stream, arquivo.FileName, arquivo.ContentType, "eventos");

            // Persiste a URL da imagem no banco
            await _eventoRepository.AtualizarImagemUrlAsync(id, url);

            return Results.Ok(new { mensagem = "Imagem enviada com sucesso.", imagemUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao fazer upload de imagem do evento {EventoId}", id);
            return Results.Json(new { mensagem = "Erro interno ao salvar a imagem." }, statusCode: 500);
        }
    }
}
