using System.Text.Json.Serialization;

namespace src.DTOs;

/// <summary>
/// DTO de resposta com os dados de um documento de meia-entrada.
/// </summary>
public class MeiaEntradaDocumentoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("reservaId")]
    public int? ReservaId { get; set; }

    [JsonPropertyName("usuarioCpf")]
    public string UsuarioCpf { get; set; } = string.Empty;

    [JsonPropertyName("usuarioNome")]
    public string? UsuarioNome { get; set; }

    [JsonPropertyName("eventoId")]
    public int EventoId { get; set; }

    [JsonPropertyName("eventoNome")]
    public string? EventoNome { get; set; }

    [JsonPropertyName("nomeOriginal")]
    public string NomeOriginal { get; set; } = string.Empty;

    [JsonPropertyName("tipoMime")]
    public string TipoMime { get; set; } = string.Empty;

    [JsonPropertyName("tamanhoBytes")]
    public long TamanhoBytes { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("dataUpload")]
    public DateTime DataUpload { get; set; }

    [JsonPropertyName("dataVerificacao")]
    public DateTime? DataVerificacao { get; set; }

    [JsonPropertyName("verificadoPorCpf")]
    public string? VerificadoPorCpf { get; set; }

    [JsonPropertyName("motivoRejeicao")]
    public string? MotivoRejeicao { get; set; }

    [JsonPropertyName("conteudoBase64")]
    public string? ConteudoBase64 { get; set; }
}

/// <summary>
/// DTO para o ADMIN verificar (aprovar ou rejeitar) um documento.
/// </summary>
public class MeiaEntradaVerificacaoDto
{
    /// <summary>"Aprovado" ou "Rejeitado"</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Motivo da rejeição (obrigatório quando Status = Rejeitado).</summary>
    [JsonPropertyName("motivoRejeicao")]
    public string? MotivoRejeicao { get; set; }
}

/// <summary>
/// DTO de resposta para listagem de documentos pendentes.
/// </summary>
public class MeiaEntradaPendenteDto
{
    [JsonPropertyName("quantidadePendentes")]
    public int QuantidadePendentes { get; set; }

    [JsonPropertyName("documentos")]
    public List<MeiaEntradaDocumentoDto> Documentos { get; set; } = [];
}
