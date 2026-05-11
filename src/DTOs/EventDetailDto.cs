using System.Text.Json.Serialization;

namespace src.DTOs;

/// <summary>
/// DTO público de um tipo de ingresso (setor) para exibição no frontend.
/// </summary>
public class TicketTypeDetailDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string? Descricao { get; set; }

    [JsonPropertyName("preco")]
    public decimal Preco { get; set; }

    [JsonPropertyName("capacidadeTotal")]
    public int CapacidadeTotal { get; set; }

    [JsonPropertyName("capacidadeRestante")]
    public int CapacidadeRestante { get; set; }

    [JsonPropertyName("ordem")]
    public int Ordem { get; set; }

    [JsonPropertyName("precoMeiaEntrada")]
    public decimal PrecoMeiaEntrada => Preco / 2;
}

/// <summary>
/// DTO público de um lote progressivo para exibição no frontend.
/// </summary>
public class LoteDetailDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("preco")]
    public decimal Preco { get; set; }

    [JsonPropertyName("quantidadeMaxima")]
    public int QuantidadeMaxima { get; set; }

    [JsonPropertyName("quantidadeVendida")]
    public int QuantidadeVendida { get; set; }

    [JsonPropertyName("disponivel")]
    public bool Disponivel { get; set; }
}

/// <summary>
/// DTO com todos os detalhes de um evento para exibição na página de detalhes.
/// </summary>
public class EventDetailDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string? Descricao { get; set; }

    /// <summary>
    /// URL pública da imagem de capa do evento (retornada por IStorageService).
    /// Usada para Open Graph (og:image) em redes sociais.
    /// </summary>
    [JsonPropertyName("imagemUrl")]
    public string? ImagemUrl { get; set; }

    [JsonPropertyName("dataEvento")]
    public DateTime DataEvento { get; set; }

    /// <summary>
    /// Data/hora de término do evento. Quando nulo, o frontend deve usar DataEvento.AddHours(4)
    /// como fallback para compatibilidade com eventos existentes.
    /// </summary>
    [JsonPropertyName("dataTermino")]
    public DateTime? DataTermino { get; set; }

    [JsonPropertyName("local")]
    public string Local { get; set; } = string.Empty;

    [JsonPropertyName("generoMusical")]
    public string GeneroMusical { get; set; } = string.Empty;

    [JsonPropertyName("precoPadrao")]
    public decimal PrecoPadrao { get; set; }

    [JsonPropertyName("taxaServico")]
    public decimal TaxaServico { get; set; }

    [JsonPropertyName("eventoGratuito")]
    public bool EventoGratuito { get; set; }

    [JsonPropertyName("temMeiaEntrada")]
    public bool TemMeiaEntrada { get; set; }

    /// <summary>
    /// Preço da meia-entrada calculado como 50% do PrecoPadrao.
    /// O frontend usa este valor para exibir o preço da meia-entrada.
    /// </summary>
    [JsonPropertyName("precoMeiaEntrada")]
    public decimal PrecoMeiaEntrada => PrecoPadrao / 2;

    [JsonPropertyName("capacidadeTotal")]
    public int CapacidadeTotal { get; set; }

    [JsonPropertyName("limiteIngressosPorUsuario")]
    public int LimiteIngressosPorUsuario { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Thumbnail principal em Base64 (sem prefixo data:image).</summary>
    [JsonPropertyName("fotoThumbnailBase64")]
    public string? FotoThumbnailBase64 { get; set; }

    /// <summary>Lista de thumbnails de todas as fotos do evento para galeria.</summary>
    [JsonPropertyName("fotos")]
    public List<string> Fotos { get; set; } = [];

    /// <summary>
    /// Vagas restantes (capacidadeTotal - reservas ativas).
    /// Calculado no servidor.
    /// </summary>
    [JsonPropertyName("vagasRestantes")]
    public int VagasRestantes { get; set; }

    /// <summary>
    /// Indica se o evento aceita cancelamento com reembolso via seguro.
    /// </summary>
    [JsonPropertyName("politicaReembolso")]
    public string PoliticaReembolso { get; set; } = string.Empty;

    // ══════════════════════════════════════════════════════════════════
    // NOVO: Tipos de Ingresso (Setores) e Lotes Progressivos
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista de tipos de ingresso (setores) disponíveis para este evento.
    /// Cada tipo tem seu próprio preço, capacidade e vagas restantes.
    /// Quando vazia, o evento usa o modelo legado (PrecoPadrao + CapacidadeTotal).
    /// </summary>
    [JsonPropertyName("tiposIngresso")]
    public List<TicketTypeDetailDto> TiposIngresso { get; set; } = [];

    /// <summary>
    /// Lista de lotes progressivos ativos para este evento.
    /// Podem ser globais ou específicos de um tipo de ingresso.
    /// </summary>
    [JsonPropertyName("lotes")]
    public List<LoteDetailDto> Lotes { get; set; } = [];
}
