using src.DTOs;
using src.Models;

namespace src.Infrastructure.IRepository;

/// <summary>
/// Repositório para a fila de espera de eventos lotados.
/// </summary>
public interface IFilaEsperaRepository
{
    /// <summary>
    /// Adiciona um usuário à fila de espera de um evento.
    /// </summary>
    Task<int> AdicionarAsync(WaitingQueue entrada);

    /// <summary>
    /// Remove um usuário da fila de espera de um evento (desistência voluntária).
    /// </summary>
    Task<bool> RemoverAsync(int id, string usuarioCpf);

    /// <summary>
    /// Remove um usuário da fila de espera pelo CPF e EventoId.
    /// </summary>
    Task<bool> RemoverPorCpfEEventoAsync(string usuarioCpf, int eventoId);

    /// <summary>
    /// Verifica se o usuário já está na fila de espera de um determinado evento.
    /// </summary>
    Task<bool> EstaNaFilaAsync(string usuarioCpf, int eventoId);

    /// <summary>
    /// Conta quantas pessoas estão na fila de espera de um evento (status Aguardando).
    /// </summary>
    Task<int> ContarPorEventoAsync(int eventoId);

    /// <summary>
    /// Obtém a posição do usuário na fila de espera de um evento.
    /// </summary>
    Task<int> ObterPosicaoAsync(string usuarioCpf, int eventoId);

    /// <summary>
    /// Lista todas as entradas da fila de espera de um evento (admin).
    /// </summary>
    Task<IEnumerable<WaitingQueueDto>> ListarPorEventoAsync(int eventoId);

    /// <summary>
    /// Lista as entradas na fila de espera de um usuário específico.
    /// </summary>
    Task<IEnumerable<WaitingQueueDto>> ListarPorUsuarioAsync(string usuarioCpf);

    /// <summary>
    /// Obtém o próximo da fila de espera (o mais antigo com status Aguardando).
    /// Retorna null se a fila estiver vazia.
    /// </summary>
    Task<WaitingQueueDto?> ObterProximoDaFilaAsync(int eventoId);

    /// <summary>
    /// Marca uma entrada como notificada.
    /// </summary>
    Task<bool> MarcarComoNotificadoAsync(int id);

    /// <summary>
    /// Marca uma entrada como confirmada (compra realizada após notificação).
    /// </summary>
    Task<bool> MarcarComoConfirmadoAsync(int id);
}
