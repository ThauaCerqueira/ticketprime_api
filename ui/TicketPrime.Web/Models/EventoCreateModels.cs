namespace TicketPrime.Web.Models;

// ─────────────────────────────────────────────────────────────────────────────
// EventoStatus – estados possíveis do ciclo de vida do evento
// ─────────────────────────────────────────────────────────────────────────────
public static class EventStatus
{
    /// <summary>Criado mas ainda não publicado; pode ser editado livremente.</summary>
    public const string Rascunho   = "Rascunho";

    /// <summary>Visível ao público; ingressos podem ser reservados.</summary>
    public const string Publicado  = "Publicado";

    /// <summary>Encerrado manualmente antes da data ou após ela.</summary>
    public const string Encerrado  = "Encerrado";

    /// <summary>Cancelado pelo organizador; reservas devem ser estornadas.</summary>
    public const string Cancelado  = "Cancelado";
}

// ─────────────────────────────────────────────────────────────────────────────
// EventoCreateDto – dados do formulário de criação de evento
// ─────────────────────────────────────────────────────────────────────────────
public class EventoCreateDto
{
    public string   Nome              { get; set; } = string.Empty;
    public DateTime? DataHora         { get; set; }

    /// <summary>
    /// Data/hora de término do evento (opcional). Quando nulo, o sistema usa DataHora + 4h como fallback.
    /// </summary>
    public DateTime? DataHoraTermino  { get; set; }
    public string   Local             { get; set; } = string.Empty;
    public string?  Descricao         { get; set; }
    public string   GeneroMusical     { get; set; } = string.Empty;
    public decimal? Preco             { get; set; }
    public bool     EventoGratuito    { get; set; }

    /// <summary>Indica se o evento oferece meia-entrada (Lei 12.933/2013).</summary>
    public bool TemMeiaEntrada { get; set; }

    /// <summary>Taxa de serviço por ingresso. Máximo 5% do preço. Zero para eventos gratuitos.</summary>
    public decimal? TaxaServico       { get; set; }

    /// <summary>Capacidade máxima de participantes. Controla o limite de reservas.</summary>
    public int CapacidadeMaxima { get; set; }

    /// <summary>Estado inicial do evento. Começa como Rascunho e só é publicado após revisão.</summary>
    public string Status { get; set; } = EventStatus.Rascunho;

    // ═══════════════════════════════════════════════════════════════
    // NOVO: Tipos de Ingresso (Setores) e Lotes Progressivos
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Lista de tipos de ingresso (setores) do evento.</summary>
    public List<TicketTypeFormItem> TiposIngresso { get; set; } = [];

    /// <summary>Lista de lotes progressivos de preço.</summary>
    public List<LoteFormItem> Lotes { get; set; } = [];
}

/// <summary>Modelo de formulário para um tipo de ingresso (setor).</summary>
public class TicketTypeFormItem
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public int CapacidadeTotal { get; set; }
    public int Ordem { get; set; }
}

/// <summary>Modelo de formulário para um lote progressivo.</summary>
public class LoteFormItem
{
    public string Nome { get; set; } = string.Empty;
    /// <summary>Índice do tipo de ingresso (0-based) a que este lote pertence, ou null se global.</summary>
    public int? TicketTypeIndex { get; set; }
    public decimal Preco { get; set; }
    public int QuantidadeMaxima { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// FotoCriptografada – pacote de uma foto após criptografia ponta a ponta
// ─────────────────────────────────────────────────────────────────────────────
public class EncryptedPhoto
{
    /// <summary>Conteúdo cifrado (AES-GCM ciphertext + auth-tag) em Base64.</summary>
    public string CiphertextBase64 { get; set; } = string.Empty;

    /// <summary>IV de 12 bytes usado no AES-GCM, em Base64.</summary>
    public string IvBase64 { get; set; } = string.Empty;

    /// <summary>Chave AES-GCM empacotada via AES-KW (segredo ECDH), em Base64.</summary>
    public string ChaveAesCifradaBase64 { get; set; } = string.Empty;

    /// <summary>Chave pública ECDH P-256 do organizador (JSON JWK serializado).</summary>
    public string ChavePublicaOrgJwk { get; set; } = string.Empty;

    /// <summary>SHA-256 do nome original do arquivo, em Base64.</summary>
    public string HashNomeOriginal { get; set; } = string.Empty;

    /// <summary>Tipo MIME original (image/jpeg, image/png, image/webp).</summary>
    public string TipoMime { get; set; } = string.Empty;

    /// <summary>Tamanho em bytes do arquivo original (antes da criptografia).</summary>
    public long TamanhoBytes { get; set; }

    /// <summary>Indica que a criptografia foi concluída com sucesso.</summary>
    public bool Criptografada { get; set; }

    /// <summary>
    /// Thumbnail redimensionado (JPEG, max 400px largura) em Base64 (sem prefixo).
    /// Armazenado sem criptografia para exibição na vitrine pública.
    /// </summary>
    public string ThumbnailBase64 { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// PacoteImagem – payload completo enviado ao endpoint /api/eventos (POST)
// ─────────────────────────────────────────────────────────────────────────────
public class ImagePackage
{
    /// <summary>Metadados do evento (campos do formulário).</summary>
    public EventoCreateDto Evento { get; set; } = new();

    /// <summary>Lista de fotos da banda, cada uma criptografada individualmente.</summary>
    public List<EncryptedPhoto> Fotos { get; set; } = [];

    /// <summary>Chave pública ECDH P-256 do organizador (JWK). O servidor usa esta chave
    /// em conjunto com sua chave privada para derivar o mesmo segredo ECDH e descriptografar
    /// as chaves AES das imagens.</summary>
    public string ChavePublicaOrgJwk { get; set; } = string.Empty;

    /// <summary>Timestamp UTC do envio.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Versão do protocolo de criptografia.</summary>
    public string VersaoProtocolo { get; set; } = "1.0-ecdh-p256-aesgcm256";
}
