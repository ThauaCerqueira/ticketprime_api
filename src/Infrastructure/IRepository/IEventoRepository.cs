using src.DTOs;
using src.Models;
 
namespace src.Infrastructure.IRepository;
 
public interface IEventoRepository
{
    Task<int> AdicionarAsync(TicketEvent evento);
    Task<PaginatedResult<TicketEvent>> ObterTodosAsync(int pagina = 1, int tamanhoPagina = 20);
    Task<IEnumerable<TicketEvent>> ObterDisponiveisAsync();
    Task<PaginatedResult<TicketEvent>> BuscarDisponiveisAsync(string? nome, string? genero, DateTime? dataMin, DateTime? dataMax, int pagina = 1, int tamanhoPagina = 20);
    Task<TicketEvent?> ObterPorIdAsync(int id);
    Task<bool> DiminuirCapacidadeAsync(int eventoId);
    Task AumentarCapacidadeAsync(int eventoId);
    Task<bool> DeletarAsync(int id);
    Task AtualizarStatusAsync(int id, string status);

    /// <summary>
    /// Insere registros de fotos criptografadas na tabela EventoFotos
    /// associadas a um evento recém-criado.
    /// </summary>
    Task AdicionarFotosAsync(int eventoId, List<EncryptedPhotoDto> fotos);

    /// <summary>
    /// Busca sugestões de eventos (autocomplete) usando full-text search.
    /// Retorna eventos completos ordenados por relevância do nome.
    /// </summary>
    Task<IEnumerable<TicketEvent>> BuscarSugestoesAsync(string termo, int limite = 5);

    /// <summary>
    /// Retorna a lista de thumbnails Base64 de todas as fotos associadas a um evento.
    /// </summary>
    Task<List<string>> ObterFotosPorEventoAsync(int eventoId);

    /// <summary>
    /// Retorna o thumbnail Base64 da primeira foto de um evento (ou null se não houver).
    /// Usado pelo endpoint público de thumbnail para Open Graph (og:image).
    /// </summary>
    Task<string?> ObterThumbnailPorEventoAsync(int eventoId);

    // ═══════════════════════════════════════════════════════════════
    // NOVO: Tipos de Ingresso e Lotes Progressivos
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Insere múltiplos tipos de ingresso (setores) para um evento.
    /// </summary>
    Task AdicionarTiposIngressoAsync(int eventoId, List<TicketType> tipos);

    /// <summary>
    /// Retorna todos os tipos de ingresso de um evento, ordenados por Ordem.
    /// </summary>
    Task<List<TicketType>> ObterTiposIngressoPorEventoAsync(int eventoId);

    /// <summary>
    /// Obtém um tipo de ingresso pelo ID.
    /// </summary>
    Task<TicketType?> ObterTipoIngressoPorIdAsync(int id);

    /// <summary>
    /// Decrementa a capacidade restante de um tipo de ingresso.
    /// Retorna false se não houver vagas.
    /// </summary>
    Task<bool> DiminuirCapacidadeTipoIngressoAsync(int ticketTypeId);

    /// <summary>
    /// Incrementa a capacidade restante de um tipo de ingresso (após cancelamento).
    /// </summary>
    Task AumentarCapacidadeTipoIngressoAsync(int ticketTypeId);

    /// <summary>
    /// Insere múltiplos lotes progressivos para um evento.
    /// </summary>
    Task AdicionarLotesAsync(int eventoId, List<Lote> lotes);

    /// <summary>
    /// Retorna todos os lotes de um evento.
    /// </summary>
    Task<List<Lote>> ObterLotesPorEventoAsync(int eventoId);

    /// <summary>
    /// Obtém um lote pelo ID.
    /// </summary>
    Task<Lote?> ObterLotePorIdAsync(int id);

    /// <summary>
    /// Incrementa a quantidade vendida de um lote.
    /// </summary>
    Task IncrementarQuantidadeVendidaLoteAsync(int loteId);

    /// <summary>
    /// Decrementa a quantidade vendida de um lote (após cancelamento).
    /// </summary>
    Task DecrementarQuantidadeVendidaLoteAsync(int loteId);

    /// <summary>
    /// Atualiza a URL da imagem de capa do evento.
    /// </summary>
    Task AtualizarImagemUrlAsync(int eventoId, string imagemUrl);
}
