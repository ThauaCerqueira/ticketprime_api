using src.DTOs;
using src.Infrastructure.IRepository;
using src.Models;

namespace src.Service;

/// <summary>
/// Serviço responsável pela gestão da fila de espera de eventos lotados.
/// Quando um evento está esgotado, os usuários podem entrar na fila de espera.
/// Quando alguém cancela, o próximo da fila é automaticamente notificado por email.
/// </summary>
public class WaitingQueueService : IWaitingQueueService
{
    private readonly IFilaEsperaRepository _filaEsperaRepository;
    private readonly IEventoRepository _eventoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly EmailTemplateService _emailTemplate;
    private readonly ILogger<WaitingQueueService> _logger;

    public WaitingQueueService(
        IFilaEsperaRepository filaEsperaRepository,
        IEventoRepository eventoRepository,
        IUsuarioRepository usuarioRepository,
        EmailTemplateService emailTemplate,
        ILogger<WaitingQueueService> logger)
    {
        _filaEsperaRepository = filaEsperaRepository;
        _eventoRepository = eventoRepository;
        _usuarioRepository = usuarioRepository;
        _emailTemplate = emailTemplate;
        _logger = logger;
    }

    /// <summary>
    /// Adiciona o usuário na fila de espera de um evento lotado.
    /// </summary>
    public async Task<WaitingQueueResponseDto> EntrarNaFilaAsync(string usuarioCpf, int eventoId)
    {
        var usuario = await _usuarioRepository.ObterPorCpf(usuarioCpf);
        if (usuario == null)
            throw new InvalidOperationException("Usuário não encontrado.");

        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);
        if (evento == null)
            throw new InvalidOperationException("Evento não encontrado.");

        if (evento.DataEvento <= DateTime.Now)
            throw new InvalidOperationException("Este evento já aconteceu.");

        // Verifica se o evento está realmente lotado usando CapacidadeRestante
        // (mantido transacionalmente no banco — decrementado na compra, incrementado no cancelamento)
        if (evento.CapacidadeRestante > 0)
            throw new InvalidOperationException("Ainda há vagas disponíveis para este evento. Compre seu ingresso!");

        // Verifica se o usuário já está na fila
        var jaEstaNaFila = await _filaEsperaRepository.EstaNaFilaAsync(usuarioCpf, eventoId);
        if (jaEstaNaFila)
        {
            var posicao = await _filaEsperaRepository.ObterPosicaoAsync(usuarioCpf, eventoId);
            var total = await _filaEsperaRepository.ContarPorEventoAsync(eventoId);
            return new WaitingQueueResponseDto
            {
                Mensagem = "Você já está na fila de espera para este evento.",
                Posicao = posicao,
                TotalNaFila = total
            };
        }

        var entrada = new WaitingQueue
        {
            UsuarioCpf = usuarioCpf,
            EventoId = eventoId
        };

        var id = await _filaEsperaRepository.AdicionarAsync(entrada);
        var posicaoFinal = await _filaEsperaRepository.ObterPosicaoAsync(usuarioCpf, eventoId);
        var totalFila = await _filaEsperaRepository.ContarPorEventoAsync(eventoId);

        _logger.LogInformation(
            "Usuário {Cpf} entrou na fila de espera do evento {EventoId}. Posição: {Posicao}/{Total}",
            usuarioCpf, eventoId, posicaoFinal, totalFila);

        return new WaitingQueueResponseDto
        {
            Mensagem = $"Você entrou na fila de espera! Sua posição atual é {posicaoFinal}º de {totalFila}.",
            Posicao = posicaoFinal,
            TotalNaFila = totalFila
        };
    }

    /// <summary>
    /// Remove o usuário da fila de espera (desistência voluntária).
    /// </summary>
    public async Task SairDaFilaAsync(string usuarioCpf, int eventoId)
    {
        var removeu = await _filaEsperaRepository.RemoverPorCpfEEventoAsync(usuarioCpf, eventoId);
        if (!removeu)
            throw new InvalidOperationException("Você não está na fila de espera para este evento.");

        _logger.LogInformation(
            "Usuário {Cpf} saiu da fila de espera do evento {EventoId}",
            usuarioCpf, eventoId);
    }

    /// <summary>
    /// Lista as posições da fila de espera de um evento (acesso administrativo).
    /// </summary>
    public async Task<IEnumerable<WaitingQueueDto>> ListarFilaPorEventoAsync(int eventoId)
    {
        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);
        if (evento == null)
            throw new InvalidOperationException("Evento não encontrado.");

        return await _filaEsperaRepository.ListarPorEventoAsync(eventoId);
    }

    /// <summary>
    /// Lista as filas de espera em que o usuário está inscrito.
    /// </summary>
    public async Task<IEnumerable<WaitingQueueDto>> ListarMinhasFilasAsync(string usuarioCpf)
    {
        return await _filaEsperaRepository.ListarPorUsuarioAsync(usuarioCpf);
    }

    /// <summary>
    /// Notifica o próximo da fila de espera sobre uma vaga disponível.
    /// Chamado automaticamente após um cancelamento de reserva.
    /// </summary>
    public async Task NotificarProximoDaFilaAsync(int eventoId)
    {
        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);
        if (evento == null) return;

        var proximo = await _filaEsperaRepository.ObterProximoDaFilaAsync(eventoId);
        if (proximo == null) return;

        // Marca como notificado
        await _filaEsperaRepository.MarcarComoNotificadoAsync(proximo.Id);

        // Obtém dados do usuário
        var usuario = await _usuarioRepository.ObterPorCpf(proximo.UsuarioCpf);
        if (usuario == null) return;

        // Envia email de notificação
        try
        {
            await _emailTemplate.SendWaitingListNotificationAsync(
                to: usuario.Email,
                nomeCliente: usuario.Nome,
                eventoNome: evento.Nome,
                eventoId: evento.Id,
                dataEvento: evento.DataEvento);

            _logger.LogInformation(
                "Notificação de vaga enviada para {Email} | Evento: {EventoNome} | FilaEsperaId: {FilaId}",
                usuario.Email, evento.Nome, proximo.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Falha ao enviar notificação de vaga para {Email} | Evento: {EventoNome}",
                usuario.Email, evento.Nome);
        }
    }
}
