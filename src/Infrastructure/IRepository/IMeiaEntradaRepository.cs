using src.DTOs;
using src.Models;

namespace src.Infrastructure.IRepository;

/// <summary>
/// Repositório para documentos comprobatórios de meia-entrada (Lei 12.933/2013).
/// </summary>
public interface IMeiaEntradaRepository
{
    /// <summary>
    /// Insere um novo documento no banco.
    /// </summary>
    Task<int> InserirAsync(MeiaEntradaDocumento documento);

    /// <summary>
    /// Atualiza o ReservaId de um documento após a compra ser concluída.
    /// </summary>
    Task VincularReservaAsync(int documentoId, int reservaId);

    /// <summary>
    /// Obtém um documento pelo ID.
    /// </summary>
    Task<MeiaEntradaDocumento?> ObterPorIdAsync(int id);

    /// <summary>
    /// Obtém todos os documentos pendentes, com dados do usuário e evento.
    /// </summary>
    Task<List<MeiaEntradaDocumentoDto>> ListarPendentesAsync();

    /// <summary>
    /// Obtém todos os documentos (para histórico do admin), ordenados por data de upload (mais recentes primeiro).
    /// </summary>
    Task<List<MeiaEntradaDocumentoDto>> ListarTodosAsync(string? filtroStatus = null);

    /// <summary>
    /// Aprova ou rejeita um documento.
    /// </summary>
    Task AtualizarStatusAsync(int id, string status, string verificadoPorCpf, string? motivoRejeicao = null);

    /// <summary>
    /// Conta quantos documentos estão pendentes.
    /// </summary>
    Task<int> ContarPendentesAsync();

    /// <summary>
    /// Obtém o documento vinculado a uma reserva (se existir).
    /// </summary>
    Task<MeiaEntradaDocumentoDto?> ObterPorReservaIdAsync(int reservaId);
}
