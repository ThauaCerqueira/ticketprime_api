using Microsoft.Extensions.Logging;
using src.DTOs;
using src.Models;
using src.Infrastructure.IRepository;

namespace src.Service;

public class EventoService
{
    private const decimal TaxaServicoMaxPct = 0.05m; // 5% do preço do ingresso
    private const int MaxEventosPublicados = 5; // limite de eventos publicados simultaneamente

    private readonly IEventoRepository _Repository;
    private readonly IReservaRepository _reservaRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly EmailTemplateService _emailTemplate;
    private readonly ILogger<EventoService> _logger;

    public EventoService(
        IEventoRepository repository,
        IReservaRepository reservaRepository,
        IUsuarioRepository usuarioRepository,
        EmailTemplateService emailTemplate,
        ILogger<EventoService> logger)
    {
        _Repository = repository;
        _reservaRepository = reservaRepository;
        _usuarioRepository = usuarioRepository;
        _emailTemplate = emailTemplate;
        _logger = logger;
    }

    public async Task<TicketEvent?> CriarNovoEvento(CreateEventDto dto, string? organizadorCpf = null)
    {
        // Se tipos de ingresso foram informados, calcula capacidade total como soma
        if (dto.TiposIngresso is { Count: > 0 })
        {
            dto.CapacidadeTotal = dto.TiposIngresso.Sum(t => t.CapacidadeTotal);

            // Valida taxa de serviço contra o menor preço entre os tipos
            var menorPreco = dto.TiposIngresso.Min(t => t.Preco);
            if (menorPreco > 0 && dto.TaxaServico > menorPreco * TaxaServicoMaxPct)
                throw new ArgumentException(
                    $"A taxa de serviço não pode exceder 5% do menor preço de ingresso (máximo: R$ {menorPreco * TaxaServicoMaxPct:F2}).");
        }
        else
        {
            // Valida taxa de serviço contra PrecoPadrao (comportamento legado)
            if (dto.PrecoPadrao > 0 && dto.TaxaServico > dto.PrecoPadrao * TaxaServicoMaxPct)
                throw new ArgumentException(
                    $"A taxa de serviço não pode exceder 5% do preço do ingresso (máximo: R$ {dto.PrecoPadrao * TaxaServicoMaxPct:F2}).");
        }

        if (dto.TaxaServico < 0)
            throw new ArgumentException("A taxa de serviço não pode ser negativa.");

        if (dto.DataTermino.HasValue && dto.DataTermino.Value <= dto.DataEvento)
            throw new ArgumentException("A data de término deve ser posterior à data de início do evento.");

        var novoEvento = new TicketEvent(
            dto.Nome,
            dto.CapacidadeTotal,
            dto.DataEvento,
            dto.PrecoPadrao,
            dto.LimiteIngressosPorUsuario
        )
        {
            TaxaServico = dto.TaxaServico,
            Local = dto.Local,
            Descricao = dto.Descricao,
            GeneroMusical = dto.GeneroMusical,
            EventoGratuito = dto.EventoGratuito,
            Status = dto.Status,
            TemMeiaEntrada = dto.TemMeiaEntrada,
            OrganizadorCpf = organizadorCpf,
            DataTermino = dto.DataTermino
        };

        // Limite de eventos publicados simultaneamente (também verificado no frontend)
        if (novoEvento.Status == "Publicado")
        {
            var eventosAtivos = await _Repository.ObterDisponiveisAsync();
            if (eventosAtivos.Count() >= MaxEventosPublicados)
                throw new InvalidOperationException(
                    $"Limite de {MaxEventosPublicados} eventos publicados atingido. Encerre ou arquive um evento antes de publicar um novo.");
        }

        // ── Criação transacional: evento + tipos + lotes + fotos em uma única
        // transação SQL — se qualquer etapa falhar, tudo sofre rollback,
        // evitando registros órfãos no banco.
        var tipos = dto.TiposIngresso is { Count: > 0 }
            ? dto.TiposIngresso.Select(t => new TicketType(
                t.Nome, t.Preco, t.CapacidadeTotal, t.Ordem) { Descricao = t.Descricao }).ToList()
            : null;

        List<Lote>? lotes = null;
        if (dto.Lotes is { Count: > 0 } && tipos is { Count: > 0 })
        {
            lotes = dto.Lotes.Select(l => new Lote(
                l.Nome, l.Preco, l.QuantidadeMaxima, l.TicketTypeId)
            {
                DataInicio = l.DataInicio,
                DataFim = l.DataFim
            }).ToList();
        }

        await _Repository.CriarEventoComTransacaoAsync(novoEvento, tipos, lotes, dto.Fotos);

        return novoEvento;
    }

    public async Task<PaginatedResult<TicketEvent>> ListarEventos(int pagina = 1, int tamanhoPagina = 20)
    {
        return await _Repository.ObterTodosAsync(pagina, tamanhoPagina);
    }

    public async Task<IEnumerable<TicketEvent>> ListarEventosDisponiveis()
    {
        return await _Repository.ObterDisponiveisAsync();
    }

    public async Task<PaginatedResult<TicketEvent>> BuscarEventos(string? nome, string? genero, DateTime? dataMin, DateTime? dataMax, string? cidade, int pagina = 1, int tamanhoPagina = 20)
    {
        return await _Repository.BuscarDisponiveisAsync(nome, genero, dataMin, dataMax, cidade, pagina, tamanhoPagina);
    }

    /// <summary>
    /// Busca sugestões de eventos (autocomplete) usando full-text search.
    /// Retorna até <paramref name="limite"/> eventos ordenados por relevância.
    /// </summary>
    public async Task<IEnumerable<TicketEvent>> BuscarSugestoes(string termo, int limite = 5)
    {
        if (string.IsNullOrWhiteSpace(termo))
            return Enumerable.Empty<TicketEvent>();

        return await _Repository.BuscarSugestoesAsync(termo, limite);
    }

    /// <summary>
    /// Publica um evento (altera status de "Rascunho" para "Publicado").
    /// </summary>
    public async Task PublicarEventoAsync(int id)
    {
        var evento = await _Repository.ObterPorIdAsync(id)
            ?? throw new InvalidOperationException("Evento não encontrado.");

        if (evento.Status != "Rascunho")
            throw new InvalidOperationException(
                $"Não é possível publicar um evento com status '{evento.Status}'. O evento deve estar em 'Rascunho'.");

        if (evento.DataEvento <= DateTime.Now)
            throw new InvalidOperationException("Não é possível publicar um evento que já passou.");

        await _Repository.AtualizarStatusAsync(id, "Publicado");
    }

    /// <summary>
    /// Exclui um evento. Só permitido se não houver reservas ativas vinculadas
    /// e o evento ainda não foi realizado.
    /// </summary>
    public async Task DeletarEventoAsync(int id)
    {
        var evento = await _Repository.ObterPorIdAsync(id)
            ?? throw new InvalidOperationException("Evento não encontrado.");

        if (evento.Status == "Publicado" && evento.DataEvento <= DateTime.Now)
            throw new InvalidOperationException(
                "Não é possível excluir um evento que já foi realizado.");

        if (evento.Status != "Rascunho" && evento.Status != "Publicado")
            throw new InvalidOperationException(
                $"Não é possível excluir um evento com status '{evento.Status}'.");

        var deletou = await _Repository.DeletarAsync(id);
        if (!deletou)
            throw new InvalidOperationException(
                "Não é possível excluir o evento pois existem reservas ativas vinculadas a ele.");
    }

    /// <summary>
    /// Cancela um evento (altera status para "Cancelado") e notifica todos os compradores.
    /// </summary>
    public async Task CancelarEventoComNotificacaoAsync(int eventoId)
    {
        var evento = await _Repository.ObterPorIdAsync(eventoId)
            ?? throw new InvalidOperationException("Evento não encontrado.");

        if (evento.Status == "Cancelado")
            throw new InvalidOperationException("Evento já está cancelado.");

        // Altera status para Cancelado
        await _Repository.AtualizarStatusAsync(eventoId, "Cancelado");

        // Notifica todos os compradores em paralelo (o envio real é background)
        try
        {
            var emails = await _reservaRepository.ObterEmailsUsuariosPorEventoAsync(eventoId);
            var emailsList = emails.ToList();

            _logger.LogInformation("Notificando {Count} compradores sobre cancelamento do evento {EventoId}", emailsList.Count, eventoId);

            // Como EmailTemplateService agora enfileira em BackgroundEmailService
            // (Channel em memória), o custo de cada chamada é ~1ms.
            // Podemos disparar todas em paralelo sem medo.
            var tasks = emailsList.Select(async email =>
            {
                try
                {
                    var usuario = await _usuarioRepository.ObterPorEmail(email);
                    var nomeCliente = usuario?.Nome ?? "Cliente";

                    await _emailTemplate.SendEventCancelledNotificationAsync(
                        to: email,
                        nomeCliente: nomeCliente,
                        eventoNome: evento.Nome,
                        dataEventoOriginal: evento.DataEvento);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao notificar {Email} sobre cancelamento do evento {EventoId}", email, eventoId);
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Notificações de cancelamento enfileiradas para {Count} compradores do evento {EventoId}", emailsList.Count, eventoId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter lista de emails para notificação de cancelamento do evento {EventoId}", eventoId);
        }
    }

    /// <summary>
    /// Notifica todos os compradores de um evento sobre uma alteração.
    /// </summary>
    public async Task NotificarAlteracaoEventoAsync(int eventoId, string tipoAlteracao, string detalhes)
    {
        var evento = await _Repository.ObterPorIdAsync(eventoId)
            ?? throw new InvalidOperationException("Evento não encontrado.");

        var emails = await _reservaRepository.ObterEmailsUsuariosPorEventoAsync(eventoId);
        var emailsList = emails.ToList();

        _logger.LogInformation("Notificando {Count} compradores sobre alteração no evento {EventoId}: {Tipo}",
            emailsList.Count, eventoId, tipoAlteracao);

        foreach (var email in emailsList)
        {
            try
            {
                var usuario = await _usuarioRepository.ObterPorEmail(email);
                var nomeCliente = usuario?.Nome ?? "Cliente";

                await _emailTemplate.SendEventChangedNotificationAsync(
                    to: email,
                    nomeCliente: nomeCliente,
                    eventoNome: evento.Nome,
                    tipoAlteracao: tipoAlteracao,
                    detalhes: detalhes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao notificar {Email} sobre alteração no evento {EventoId}", email, eventoId);
                }
            }
        }
    
        /// <summary>
        /// Obtém um evento pelo ID (retorna null se não encontrado).
        /// </summary>
        public async Task<TicketEvent?> ObterPorIdAsync(int id)
            => await _Repository.ObterPorIdAsync(id);
    
        /// <summary>
        /// Retorna a lista de thumbnails Base64 das fotos de um evento.
        /// </summary>
        public async Task<List<string>> ObterFotosAsync(int eventoId)
            => await _Repository.ObterFotosPorEventoAsync(eventoId);
    
        /// <summary>
        /// Conta quantas reservas ativas existem para um evento.
        /// </summary>
        public async Task<int> ContarReservasAsync(int eventoId)
            => await _reservaRepository.ContarReservasPorEventoAsync(eventoId);
    }
