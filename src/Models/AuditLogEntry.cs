using System.Security.Cryptography;
using System.Text;

namespace src.Models;

/// <summary>
/// Entrada de auditoria financeira imutável.
/// 
/// Cada registro é encadeado ao anterior via hash SHA256 (blockchain-like),
/// garantindo que qualquer alteração em registros passados quebre a cadeia.
/// 
/// Campos monitorados:
///   - Quem: UsuarioCpf
///   - De onde: IpAddress + UserAgent
///   - Quando: Timestamp (UTC)
///   - O quê: ActionType + Detalhes (JSON)
///   - Valor financeiro: ValorTransacionado
/// </summary>
public class AuditLogEntry
{
    public int Id { get; set; }

    /// <summary>Timestamp UTC da ação.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tipo da ação auditada.
    /// Ex: "CompraIngresso", "CancelamentoIngresso", "Login", "CadastroUsuario",
    ///     "TrocaSenha", "RefreshToken", "RedefinicaoSenha"
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>CPF do usuário que executou a ação (pode ser nulo para ações anônimas).</summary>
    public string? UsuarioCpf { get; set; }

    /// <summary>ID do evento envolvido (quando aplicável).</summary>
    public int? EventoId { get; set; }

    /// <summary>ID da reserva/ingresso envolvido (quando aplicável).</summary>
    public int? ReservaId { get; set; }

    /// <summary>Valor financeiro transacionado (R$).</summary>
    public decimal? ValorTransacionado { get; set; }

    /// <summary>Endereço IP de origem da requisição.</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>User-Agent do cliente (navegador/app).</summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Detalhes estruturados em JSON contendo informações complementares.
    /// Ex: cupom utilizado, descontos aplicados, taxa de serviço, seguro, etc.
    /// </summary>
    public string? Detalhes { get; set; }

    /// <summary>
    /// Hash SHA256 da entrada anterior na cadeia.
    /// Para a primeira entrada, deve ser "0" (raiz da cadeia).
    /// </summary>
    public string PreviousHash { get; set; } = "0";

    /// <summary>
    /// Hash SHA256 desta entrada.
    /// Calculado como: SHA256(Timestamp|ActionType|UsuarioCpf|EventoId|ReservaId|ValorTransacionado|IpAddress|UserAgent|Detalhes|PreviousHash)
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Calcula o hash SHA256 desta entrada com base em seus campos e no hash anterior.
    /// </summary>
    public static string ComputeHash(
        DateTime timestamp,
        string actionType,
        string? usuarioCpf,
        int? eventoId,
        int? reservaId,
        decimal? valorTransacionado,
        string ipAddress,
        string? userAgent,
        string? detalhes,
        string previousHash)
    {
        var sb = new StringBuilder();
        sb.Append(timestamp.ToString("O"));
        sb.Append('|').Append(actionType);
        sb.Append('|').Append(usuarioCpf ?? "");
        sb.Append('|').Append(eventoId?.ToString() ?? "");
        sb.Append('|').Append(reservaId?.ToString() ?? "");
        sb.Append('|').Append(valorTransacionado?.ToString("F2") ?? "");
        sb.Append('|').Append(ipAddress);
        sb.Append('|').Append(userAgent ?? "");
        sb.Append('|').Append(detalhes ?? "");
        sb.Append('|').Append(previousHash);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Calcula o hash desta instância.
    /// </summary>
    public string ComputeHash() =>
        ComputeHash(Timestamp, ActionType, UsuarioCpf, EventoId, ReservaId,
                     ValorTransacionado, IpAddress, UserAgent, Detalhes, PreviousHash);
}
