namespace src.Models;

/// <summary>
/// Representa um documento comprobatório de elegibilidade para meia-entrada
/// (Lei 12.933/2013). Pode ser carteirinha estudantil, identidade de idoso,
/// laudo médico, etc. O documento é anexado pelo comprador no momento da
/// compra e verificado posteriormente pelo ADMIN.
/// </summary>
public class MeiaEntradaDocumento
{
    public int Id { get; set; }

    /// <summary>
    /// ID da reserva associada (pode ser null se o documento foi enviado
    /// antes da compra ser concluída).
    /// </summary>
    public int? ReservaId { get; set; }

    /// <summary>CPF do usuário que fez o upload.</summary>
    public string UsuarioCpf { get; set; } = string.Empty;

    /// <summary>ID do evento para o qual o documento é válido.</summary>
    public int EventoId { get; set; }

    // ── Dados do arquivo ────────────────────────────────────────────

    /// <summary>Caminho relativo do arquivo no disco.</summary>
    public string CaminhoArquivo { get; set; } = string.Empty;

    /// <summary>Nome original do arquivo enviado pelo usuário.</summary>
    public string NomeOriginal { get; set; } = string.Empty;

    /// <summary>MIME type do arquivo (ex: image/jpeg, application/pdf).</summary>
    public string TipoMime { get; set; } = string.Empty;

    /// <summary>Tamanho do arquivo em bytes.</summary>
    public long TamanhoBytes { get; set; }

    // ── Status da verificação ────────────────────────────────────────

    /// <summary>
    /// Status da verificação: Pendente, Aprovado ou Rejeitado.
    /// </summary>
    public string Status { get; set; } = "Pendente";

    /// <summary>Data/hora do upload.</summary>
    public DateTime DataUpload { get; set; }

    /// <summary>Data/hora da verificação pelo admin.</summary>
    public DateTime? DataVerificacao { get; set; }

    /// <summary>CPF do admin que realizou a verificação.</summary>
    public string? VerificadoPorCpf { get; set; }

    /// <summary>Motivo da rejeição (preenchido quando Status = Rejeitado).</summary>
    public string? MotivoRejeicao { get; set; }
}
