using System.ComponentModel.DataAnnotations;
using src.DTOs;

namespace src.DTOs;

/// <summary>
/// DTO para criação de um tipo de ingresso (setor) no formulário de criação de evento.
/// </summary>
public class TicketTypeDto
{
    /// <summary>Nome do setor (ex: "Pista", "VIP", "Camarote").</summary>
    [Required(ErrorMessage = "O nome do tipo de ingresso é obrigatório.")]
    [StringLength(100, ErrorMessage = "O nome é muito longo.")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Descrição opcional do setor.</summary>
    [StringLength(500, ErrorMessage = "A descrição é muito longa.")]
    public string? Descricao { get; set; }

    /// <summary>Preço deste tipo de ingresso.</summary>
    [Range(0, 100000, ErrorMessage = "O preço não pode ser negativo.")]
    public decimal Preco { get; set; }

    /// <summary>Capacidade total deste setor.</summary>
    [Range(1, 1000000, ErrorMessage = "A capacidade deve ser entre 1 e 1.000.000.")]
    public int CapacidadeTotal { get; set; }

    /// <summary>Ordem de exibição.</summary>
    public int Ordem { get; set; }
}

/// <summary>
/// DTO para criação de um lote progressivo de preços.
/// </summary>
public class LoteDto
{
    /// <summary>Nome do lote (ex: "1º Lote", "2º Lote", "Early Bird").</summary>
    [Required(ErrorMessage = "O nome do lote é obrigatório.")]
    [StringLength(100, ErrorMessage = "O nome é muito longo.")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>ID do tipo de ingresso ao qual este lote se aplica. Null = global.</summary>
    public int? TicketTypeId { get; set; }

    /// <summary>Preço do ingresso neste lote.</summary>
    [Range(0, 100000, ErrorMessage = "O preço não pode ser negativo.")]
    public decimal Preco { get; set; }

    /// <summary>Quantidade máxima de ingressos neste lote.</summary>
    [Range(1, 1000000, ErrorMessage = "A quantidade deve ser entre 1 e 1.000.000.")]
    public int QuantidadeMaxima { get; set; }

    /// <summary>Data de início do lote (opcional).</summary>
    public DateTime? DataInicio { get; set; }

    /// <summary>Data de fim do lote (opcional).</summary>
    public DateTime? DataFim { get; set; }
}

public record CreateEventDto
{
    [Required(ErrorMessage = "O nome do evento é obrigatório.")]
    [StringLength(200, ErrorMessage = "O nome é muito longo.")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Capacidade total do evento. Se tipos de ingresso forem informados,
    /// este campo é calculado como a soma das capacidades dos tipos.
    /// Se não informado, mantém-se o valor original para compatibilidade.
    /// </summary>
    [Range(1, 1000000, ErrorMessage = "A capacidade deve ser entre 1 e 1.000.000.")]
    public int CapacidadeTotal { get; set; }

    [Required(ErrorMessage = "A data é obrigatória.")]
    public DateTime DataEvento { get; set; }

    /// <summary>
    /// Data/hora de término do evento. Quando nulo, o sistema usa DataEvento.AddHours(4)
    /// como fallback para compatibilidade com eventos existentes.
    /// </summary>
    public DateTime? DataTermino { get; set; }

    /// <summary>
    /// Preço padrão do evento. Usado como fallback quando não há tipos de ingresso
    /// ou como referência para exibição. Se tipos de ingresso forem informados,
    /// o preço real é definido por tipo.
    /// </summary>
    [Range(0, 100000, ErrorMessage = "O preço não pode ser negativo.")]
    public decimal PrecoPadrao { get; set; }

    [Range(1, 100, ErrorMessage = "O limite de ingressos por usuário deve ser entre 1 e 100.")]
    public int LimiteIngressosPorUsuario { get; set; } = 6;

    /// <summary>Taxa de serviço por ingresso. Não pode exceder 5% do PrecoPadrao.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "A taxa de serviço não pode ser negativa.")]
    public decimal TaxaServico { get; set; }

    // ── Novos campos para compatibilidade com EventoCreate (formulário rico) ──

    /// <summary>Local do evento (endereço).</summary>
    [StringLength(500, ErrorMessage = "O local é muito longo.")]
    public string Local { get; set; } = string.Empty;

    /// <summary>Descrição do evento.</summary>
    [StringLength(2000, ErrorMessage = "A descrição é muito longa.")]
    public string? Descricao { get; set; }

    /// <summary>Gênero musical do evento.</summary>
    [StringLength(100, ErrorMessage = "O gênero musical é muito longo.")]
    public string GeneroMusical { get; set; } = string.Empty;

    /// <summary>Indica se o evento é gratuito.</summary>
    public bool EventoGratuito { get; set; }

    /// <summary>Status inicial do evento. Padrão: "Rascunho".</summary>
    public string Status { get; set; } = "Rascunho";

    /// <summary>Indica se o evento oferece meia-entrada (Lei 12.933/2013).</summary>
    public bool TemMeiaEntrada { get; set; }

    /// <summary>Cidade do evento. Ex: "São Paulo".</summary>
    [StringLength(100, ErrorMessage = "O nome da cidade é muito longo.")]
    public string Cidade { get; set; } = string.Empty;

    /// <summary>Estado (UF) do evento. Ex: "SP".</summary>
    [StringLength(2, ErrorMessage = "Use a sigla do estado (2 caracteres).")]
    public string Estado { get; set; } = string.Empty;

    /// <summary>
    /// Fotos criptografadas ponta a ponta (E2E) usando ECDH P-256 + AES-GCM-256 + AES-KW.
    /// Cada foto é criptografada individualmente no navegador antes do envio.
    /// </summary>
    public List<EncryptedPhotoDto>? Fotos { get; set; }

    // ══════════════════════════════════════════════════════════════════
    // NOVO: Tipos de Ingresso (Setores) e Lotes Progressivos
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista de tipos de ingresso (setores) do evento.
    /// Ex: Pista, VIP, Camarote, Área Premium.
    /// Quando preenchida, cada tipo tem seu próprio preço e capacidade.
    /// Quando vazia, o comportamento é o legado (PrecoPadrao + CapacidadeTotal).
    /// </summary>
    public List<TicketTypeDto>? TiposIngresso { get; set; }

    /// <summary>
    /// Lista de lotes progressivos de preço.
    /// Ex: 1º Lote (R$ 50), 2º Lote (R$ 70), 3º Lote (R$ 90).
    /// Podem ser globais ou específicos de um tipo de ingresso.
    /// </summary>
    public List<LoteDto>? Lotes { get; set; }
}
