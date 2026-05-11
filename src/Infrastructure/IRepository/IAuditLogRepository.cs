using src.Models;

namespace src.Infrastructure.IRepository;

/// <summary>
/// Repositório para persistência da trilha de auditoria financeira imutável.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Insere uma nova entrada de auditoria e retorna seu Id.
    /// O hash da entrada é computado ANTES da inserção para garantir integridade.
    /// </summary>
    Task<int> InserirAsync(AuditLogEntry entry);

    /// <summary>
    /// Obtém a última entrada de auditoria (para encadeamento de hash).
    /// </summary>
    Task<AuditLogEntry?> ObterUltimoAsync();

    /// <summary>
    /// Lista entradas de auditoria em um período de datas (ordem crescente).
    /// </summary>
    Task<IEnumerable<AuditLogEntry>> ListarPorPeriodoAsync(DateTime inicio, DateTime fim);

    /// <summary>
    /// Lista entradas de auditoria de um usuário específico.
    /// </summary>
    Task<IEnumerable<AuditLogEntry>> ListarPorUsuarioAsync(string cpf);

    /// <summary>
    /// Obtém uma entrada de auditoria por Id.
    /// </summary>
    Task<AuditLogEntry?> ObterPorIdAsync(int id);

    /// <summary>
    /// Lista entradas por tipo de ação.
    /// </summary>
    Task<IEnumerable<AuditLogEntry>> ListarPorTipoAcaoAsync(string actionType, int limite = 100);

    /// <summary>
    /// Verifica a integridade de toda a cadeia de auditoria.
    /// Recalcula o hash de cada entrada e compara com o armazenado,
    /// além de verificar o encadeamento (PreviousHash).
    /// Retorna true se a cadeia estiver íntegra.
    /// </summary>
    Task<bool> VerificarIntegridadeAsync();

    /// <summary>
    /// Obtém a quantidade total de registros de auditoria.
    /// </summary>
    Task<long> ContarTotalAsync();
}
