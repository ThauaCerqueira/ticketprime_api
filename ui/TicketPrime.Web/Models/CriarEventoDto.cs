namespace TicketPrime.Web.Models;

/// <summary>
/// DTO para criação de evento, enviado via POST /api/eventos.
/// Duplicado no frontend para evitar dependência direta do projeto src/.
/// </summary>
public class CreateEventDto
{
    public string Nome { get; set; } = string.Empty;
    public int CapacidadeTotal { get; set; }
    public DateTime DataEvento { get; set; }

    /// <summary>Data/hora de término do evento (opcional). Quando nulo, o servidor usa DataEvento.AddHours(4).</summary>
    public DateTime? DataTermino { get; set; }
    public decimal PrecoPadrao { get; set; }
    public int LimiteIngressosPorUsuario { get; set; } = 6;
    public decimal TaxaServico { get; set; }
    public string Local { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string GeneroMusical { get; set; } = string.Empty;
    public bool EventoGratuito { get; set; }
    public bool TemMeiaEntrada { get; set; }
    public string Status { get; set; } = "Rascunho";
    public List<EncryptedPhotoDto>? Fotos { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // NOVO: Tipos de Ingresso (Setores) e Lotes Progressivos
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Lista de tipos de ingresso (setores) do evento.</summary>
    public List<TicketTypeDto>? TiposIngresso { get; set; }

    /// <summary>Lista de lotes progressivos de preço.</summary>
    public List<LoteDto>? Lotes { get; set; }
}

/// <summary>DTO para criação de um tipo de ingresso (setor).</summary>
public class TicketTypeDto
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public int CapacidadeTotal { get; set; }
    public int Ordem { get; set; }
}

/// <summary>DTO para criação de um lote progressivo.</summary>
public class LoteDto
{
    public string Nome { get; set; } = string.Empty;
    public int? TicketTypeId { get; set; }
    public decimal Preco { get; set; }
    public int QuantidadeMaxima { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
}

/// <summary>
/// DTO para foto criptografada (E2E), enviado junto com o evento.
/// </summary>
public class EncryptedPhotoDto
{
    public string CiphertextBase64 { get; set; } = string.Empty;
    public string IvBase64 { get; set; } = string.Empty;
    public string ChaveAesCifradaBase64 { get; set; } = string.Empty;
    public string ChavePublicaOrgJwk { get; set; } = string.Empty;
    public string HashNomeOriginal { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanhoBytes { get; set; }
    public bool Criptografada { get; set; } = true;
    /// <summary>
    /// Thumbnail redimensionado (JPEG, max 400px largura) em Base64 (sem prefixo).
    /// Armazenado sem criptografia para exibição na vitrine pública.
    /// </summary>
    public string? ThumbnailBase64 { get; set; }
}
