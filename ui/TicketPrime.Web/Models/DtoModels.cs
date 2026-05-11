using System.Text.Json.Serialization;

namespace TicketPrime.Web.Models;

/// <summary>
/// DTO de evento para consumo via API REST.
/// Espelha src.Models.TicketEvent para deserialização JSON.
/// </summary>
public class TicketEvent
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("capacidadeTotal")]
    public int CapacidadeTotal { get; set; }

    [JsonPropertyName("dataEvento")]
    public DateTime DataEvento { get; set; }

    [JsonPropertyName("precoPadrao")]
    public decimal PrecoPadrao { get; set; }

    [JsonPropertyName("limiteIngressosPorUsuario")]
    public int LimiteIngressosPorUsuario { get; set; } = 6;

    [JsonPropertyName("local")]
    public string Local { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string? Descricao { get; set; }

    [JsonPropertyName("generoMusical")]
    public string GeneroMusical { get; set; } = string.Empty;

    [JsonPropertyName("eventoGratuito")]
    public bool EventoGratuito { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Rascunho";

    [JsonPropertyName("taxaServico")]
    public decimal TaxaServico { get; set; }

    [JsonPropertyName("temMeiaEntrada")]
    public bool TemMeiaEntrada { get; set; }

    /// <summary>
    /// Preço da meia-entrada (50% do PrecoPadrao, conforme Lei 12.933/2013).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public decimal PrecoMeiaEntrada => PrecoPadrao / 2;

    /// <summary>
    /// Thumbnail da primeira foto do evento (Base64, JPEG) para exibição na vitrine.
    /// </summary>
    [JsonPropertyName("fotoThumbnailBase64")]
    public string? FotoThumbnailBase64 { get; set; }

    [JsonPropertyName("imagemUrl")]
    public string? ImagemUrl { get; set; }

    [JsonPropertyName("categoria")]
    public string Categoria { get; set; } = string.Empty;
}

/// <summary>
/// DTO de reserva detalhada para consumo via API REST.
/// Espelha src.DTOs.ReservationDetailDto.
/// </summary>
public class ReservationDetailDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("usuarioCpf")]
    public string UsuarioCpf { get; set; } = string.Empty;

    [JsonPropertyName("eventoId")]
    public int EventoId { get; set; }

    [JsonPropertyName("dataCompra")]
    public DateTime DataCompra { get; set; }

    [JsonPropertyName("cupomUtilizado")]
    public string? CupomUtilizado { get; set; }

    [JsonPropertyName("valorFinalPago")]
    public decimal ValorFinalPago { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("local")]
    public string Local { get; set; } = string.Empty;

    [JsonPropertyName("dataEvento")]
    public DateTime DataEvento { get; set; }

    [JsonPropertyName("dataTermino")]
    public DateTime? DataTermino { get; set; }

    [JsonPropertyName("precoPadrao")]
    public decimal PrecoPadrao { get; set; }

    [JsonPropertyName("taxaServicoPago")]
    public decimal TaxaServicoPago { get; set; }

    [JsonPropertyName("temSeguro")]
    public bool TemSeguro { get; set; }

    [JsonPropertyName("valorSeguroPago")]
    public decimal ValorSeguroPago { get; set; }

    [JsonPropertyName("codigoIngresso")]
    public string CodigoIngresso { get; set; } = string.Empty;

    [JsonPropertyName("ehMeiaEntrada")]
    public bool EhMeiaEntrada { get; set; }

    [JsonPropertyName("ticketTypeId")]
    public int TicketTypeId { get; set; }

    [JsonPropertyName("ticketTypeNome")]
    public string TicketTypeNome { get; set; } = string.Empty;

    [JsonPropertyName("loteId")]
    public int? LoteId { get; set; }

    [JsonPropertyName("loteNome")]
    public string? LoteNome { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Ativa";

    [JsonPropertyName("valorDevolvivel")]
    public decimal ValorDevolvivel => TemSeguro
        ? ValorFinalPago - ValorSeguroPago
        : ValorFinalPago - TaxaServicoPago;
}

/// <summary>
/// DTO de cupom para criação via API REST.
/// Espelha src.DTOs.CreateCouponDto.
/// </summary>
public class CreateCouponDto
{
    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("tipoDesconto")]
    public int TipoDesconto { get; set; } // 0 = Percentual, 1 = ValorFixo

    [JsonPropertyName("porcentagemDesconto")]
    public decimal PorcentagemDesconto { get; set; }

    [JsonPropertyName("valorDescontoFixo")]
    public decimal? ValorDescontoFixo { get; set; }

    [JsonPropertyName("valorMinimoRegra")]
    public decimal ValorMinimoRegra { get; set; }

    [JsonPropertyName("dataExpiracao")]
    public DateTime? DataExpiracao { get; set; }

    [JsonPropertyName("limiteUsos")]
    public int LimiteUsos { get; set; } = 0;

    [JsonPropertyName("categoriaEvento")]
    public string? CategoriaEvento { get; set; }

    [JsonPropertyName("primeiroAcesso")]
    public bool PrimeiroAcesso { get; set; }
}

/// <summary>
/// DTO de cupom para leitura via API REST.
/// Espelha src.Models.Coupon.
/// </summary>
public class Coupon
{
    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("tipoDesconto")]
    public int TipoDesconto { get; set; }

    [JsonPropertyName("porcentagemDesconto")]
    public decimal PorcentagemDesconto { get; set; }

    [JsonPropertyName("valorDescontoFixo")]
    public decimal? ValorDescontoFixo { get; set; }

    [JsonPropertyName("valorMinimoRegra")]
    public decimal ValorMinimoRegra { get; set; }

    [JsonPropertyName("dataExpiracao")]
    public DateTime? DataExpiracao { get; set; }

    [JsonPropertyName("limiteUsos")]
    public int LimiteUsos { get; set; } = 0;

    [JsonPropertyName("totalUsado")]
    public int TotalUsado { get; set; } = 0;

    [JsonPropertyName("categoriaEvento")]
    public string? CategoriaEvento { get; set; }

    [JsonPropertyName("primeiroAcesso")]
    public bool PrimeiroAcesso { get; set; }
}

/// <summary>
/// DTO para compra de ingresso via API REST.
/// Espelha src.DTOs.PurchaseTicketDto.
/// </summary>
public class PurchaseTicketDto
{
    [JsonPropertyName("eventoId")]
    public int EventoId { get; set; }

    [JsonPropertyName("cupomUtilizado")]
    public string? CupomUtilizado { get; set; }

    [JsonPropertyName("contratarSeguro")]
    public bool ContratarSeguro { get; set; }

    [JsonPropertyName("ehMeiaEntrada")]
    public bool EhMeiaEntrada { get; set; }

    /// <summary>ID do tipo de ingresso (setor) selecionado.</summary>
    [JsonPropertyName("ticketTypeId")]
    public int TicketTypeId { get; set; }

    /// <summary>"pix" | "cartao_credito" | "cartao_debito"</summary>
    [JsonPropertyName("metodoPagamento")]
    public string MetodoPagamento { get; set; } = "pix";

    /// <summary>
    /// Token de cartão gerado pelo SDK Mercado Pago (MercadoPago.js v2) no navegador.
    /// Obrigatório para pagamento com cartão. Nunca os dados completos transitam pelo servidor.
    /// </summary>
    [JsonPropertyName("cardToken")]
    public string? CardToken { get; set; }

    /// <summary>Últimos 4 dígitos do cartão — apenas para exibição no recibo. Nunca o número completo.</summary>
    [JsonPropertyName("ultimos4Cartao")]
    public string? Ultimos4Cartao { get; set; }

    [JsonPropertyName("nomeTitular")]
    public string? NomeTitular { get; set; }

    [JsonPropertyName("validadeCartao")]
    public string? ValidadeCartao { get; set; }

    // ── Documento comprobatório de meia-entrada (Lei 12.933/2013) ──────────

    /// <summary>Documento comprobatório codificado em Base64. Obrigatório quando EhMeiaEntrada = true.</summary>
    [JsonPropertyName("documentoBase64")]
    public string? DocumentoBase64 { get; set; }

    /// <summary>Nome original do arquivo (ex: "carteirinha_estudantil.jpg").</summary>
    [JsonPropertyName("documentoNome")]
    public string? DocumentoNome { get; set; }

    /// <summary>MIME type do arquivo (ex: "image/jpeg", "application/pdf").</summary>
    [JsonPropertyName("documentoContentType")]
    public string? DocumentoContentType { get; set; }
}

/// <summary>
/// Resultado paginado da API REST.
/// Espelha src.DTOs.PaginatedResult<T>.
/// </summary>
public class PaginatedResult<T>
{
    [JsonPropertyName("itens")]
    public List<T> Itens { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("pagina")]
    public int Pagina { get; set; }

    [JsonPropertyName("tamanhoPagina")]
    public int TamanhoPagina { get; set; }

    [JsonPropertyName("totalPaginas")]
    public int TotalPaginas => (int)Math.Ceiling((double)Total / TamanhoPagina);
}

// ── Dashboard Models ──────────────────────────────────────────────

public class DashboardCompletoDTO
{
    [JsonPropertyName("totalEventos")]
    public int TotalEventos { get; set; }

    [JsonPropertyName("eventosPublicados")]
    public int EventosPublicados { get; set; }

    [JsonPropertyName("eventosRascunho")]
    public int EventosRascunho { get; set; }

    [JsonPropertyName("totalCupons")]
    public int TotalCupons { get; set; }

    [JsonPropertyName("totalUsuarios")]
    public int TotalUsuarios { get; set; }

    [JsonPropertyName("receitaTotal")]
    public decimal ReceitaTotal { get; set; }

    [JsonPropertyName("vendasPorPeriodo")]
    public List<VendaPorPeriodoDTO> VendasPorPeriodo { get; set; } = [];

    [JsonPropertyName("eventosMaisVendidos")]
    public List<EventoMaisVendidoDTO> EventosMaisVendidos { get; set; } = [];

    [JsonPropertyName("cancelamentoStats")]
    public CancelamentoStatsDTO CancelamentoStats { get; set; } = new();

    [JsonPropertyName("relatorioFinanceiro")]
    public RelatorioFinanceiroDTO RelatorioFinanceiro { get; set; } = new();

    [JsonPropertyName("demandaPorLocal")]
    public List<DemandaLocalDTO> DemandaPorLocal { get; set; } = [];
}

public class VendaPorPeriodoDTO
{
    [JsonPropertyName("ano")]
    public int Ano { get; set; }

    [JsonPropertyName("mes")]
    public int Mes { get; set; }

    [JsonPropertyName("rotulo")]
    public string Rotulo { get; set; } = string.Empty;

    [JsonPropertyName("quantidade")]
    public int Quantidade { get; set; }

    [JsonPropertyName("receita")]
    public decimal Receita { get; set; }
}

public class EventoMaisVendidoDTO
{
    [JsonPropertyName("eventoId")]
    public int EventoId { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("dataEvento")]
    public DateTime DataEvento { get; set; }

    [JsonPropertyName("ingressosVendidos")]
    public int IngressosVendidos { get; set; }

    [JsonPropertyName("receitaGerada")]
    public decimal ReceitaGerada { get; set; }

    [JsonPropertyName("capacidadeTotal")]
    public int CapacidadeTotal { get; set; }

    [JsonPropertyName("taxaOcupacao")]
    public double TaxaOcupacao { get; set; }
}

public class CancelamentoStatsDTO
{
    [JsonPropertyName("totalReservas")]
    public int TotalReservas { get; set; }

    [JsonPropertyName("totalCanceladas")]
    public int TotalCanceladas { get; set; }

    [JsonPropertyName("totalAtivas")]
    public int TotalAtivas { get; set; }

    [JsonPropertyName("totalUsadas")]
    public int TotalUsadas { get; set; }

    [JsonPropertyName("taxaCancelamento")]
    public double TaxaCancelamento { get; set; }

    [JsonPropertyName("receitaPerdidaCancelamentos")]
    public decimal ReceitaPerdidaCancelamentos { get; set; }
}

public class RelatorioFinanceiroDTO
{
    [JsonPropertyName("receitaBruta")]
    public decimal ReceitaBruta { get; set; }

    [JsonPropertyName("taxasServico")]
    public decimal TaxasServico { get; set; }

    [JsonPropertyName("segurosContratados")]
    public decimal SegurosContratados { get; set; }

    [JsonPropertyName("descontosConcedidos")]
    public decimal DescontosConcedidos { get; set; }

    [JsonPropertyName("receitaLiquida")]
    public decimal ReceitaLiquida { get; set; }

    [JsonPropertyName("totalIngressosVendidos")]
    public int TotalIngressosVendidos { get; set; }

    [JsonPropertyName("ticketMedio")]
    public decimal TicketMedio { get; set; }

    [JsonPropertyName("cuponsUtilizados")]
    public int CuponsUtilizados { get; set; }
}

public class DemandaLocalDTO
{
    [JsonPropertyName("local")]
    public string Local { get; set; } = string.Empty;

    [JsonPropertyName("totalEventos")]
    public int TotalEventos { get; set; }

    [JsonPropertyName("ingressosVendidos")]
    public int IngressosVendidos { get; set; }

    [JsonPropertyName("receitaGerada")]
    public decimal ReceitaGerada { get; set; }

    [JsonPropertyName("intensidade")]
    public string Intensidade { get; set; } = "Nenhuma";
}

/// <summary>
/// DTO de perfil do usuário via API REST.
/// Espelha src.DTOs.ProfileResponse.
/// </summary>
public class ProfileResponse
{
    [JsonPropertyName("cpf")]
    public string Cpf { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("perfil")]
    public string Perfil { get; set; } = string.Empty;

    [JsonPropertyName("telefone")]
    public string? Telefone { get; set; }
}

/// <summary>DTO de detalhes de um tipo de ingresso (setor), retornado na API de detalhes do evento.</summary>
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

/// <summary>DTO de detalhes de um lote progressivo, retornado na API de detalhes do evento.</summary>
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
/// DTO de detalhes completos de um evento, consumido pela página DetalheEvento.razor.
/// </summary>
public class EventDetailDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string? Descricao { get; set; }

    [JsonPropertyName("dataEvento")]
    public DateTime DataEvento { get; set; }

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

    [JsonPropertyName("capacidadeTotal")]
    public int CapacidadeTotal { get; set; }

    [JsonPropertyName("limiteIngressosPorUsuario")]
    public int LimiteIngressosPorUsuario { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("fotoThumbnailBase64")]
    public string? FotoThumbnailBase64 { get; set; }

    [JsonPropertyName("fotos")]
    public List<string> Fotos { get; set; } = [];

    [JsonPropertyName("temMeiaEntrada")]
    public bool TemMeiaEntrada { get; set; }

    [JsonPropertyName("precoMeiaEntrada")]
    public decimal PrecoMeiaEntrada { get; set; }

    [JsonPropertyName("vagasRestantes")]
    public int VagasRestantes { get; set; }

    [JsonPropertyName("imagemUrl")]
    public string? ImagemUrl { get; set; }

    [JsonPropertyName("politicaReembolso")]
    public string PoliticaReembolso { get; set; } = string.Empty;

    /// <summary>Lista de tipos de ingresso (setores) do evento.</summary>
    [JsonPropertyName("tiposIngresso")]
    public List<TicketTypeDetailDto>? TiposIngresso { get; set; }

    /// <summary>Lista de lotes progressivos de preço.</summary>
    [JsonPropertyName("lotes")]
    public List<LoteDetailDto>? Lotes { get; set; }
}

/// <summary>
/// Estatísticas públicas exibidas na Home da landing page.
/// </summary>
public class HomeStatsDTO
{
    [JsonPropertyName("totalEventosPublicados")]
    public int TotalEventosPublicados { get; set; }

    [JsonPropertyName("totalIngressosVendidos")]
    public int TotalIngressosVendidos { get; set; }

    [JsonPropertyName("totalUsuarios")]
    public int TotalUsuarios { get; set; }
}

// ── Avaliação (Reviews) ───────────────────────────────────────────

/// <summary>DTO de avaliação de evento. Espelha src.Models.Avaliacao.</summary>
public class AvaliacaoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("usuarioCpf")]
    public string UsuarioCpf { get; set; } = string.Empty;

    [JsonPropertyName("nota")]
    public byte Nota { get; set; }

    [JsonPropertyName("comentario")]
    public string? Comentario { get; set; }

    [JsonPropertyName("dataAvaliacao")]
    public DateTime DataAvaliacao { get; set; }

    [JsonPropertyName("nomeUsuario")]
    public string? NomeUsuario { get; set; }

    [JsonPropertyName("anonima")]
    public bool Anonima { get; set; }
}

/// <summary>DTO de resultado de avaliações, com média e lista.</summary>
public class AvaliacaoResultDto
{
    [JsonPropertyName("media")]
    public double? Media { get; set; }

    [JsonPropertyName("avaliacoes")]
    public List<AvaliacaoDto> Avaliacoes { get; set; } = [];
}

// ── Organizador (Perfil Público) ──────────────────────────────────

/// <summary>DTO do perfil público de um organizador.</summary>
public class OrganizadorPerfilDto
{
    /// <summary>Slug opaco do organizador (usado na URL no lugar do CPF).</summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("eventosPublicados")]
    public List<EventoResumoDto> EventosPublicados { get; set; } = [];
}

/// <summary>DTO de resposta ao entrar na fila de espera (espelha src.DTOs.WaitingQueueResponseDto).</summary>
public class WaitingQueueResponseDto
{
    [JsonPropertyName("mensagem")]
    public string Mensagem { get; set; } = string.Empty;

    [JsonPropertyName("posicao")]
    public int Posicao { get; set; }

    [JsonPropertyName("totalNaFila")]
    public int TotalNaFila { get; set; }
}

/// <summary>DTO de uma entrada na fila de espera (espelha src.DTOs.WaitingQueueDto).</summary>
public class WaitingQueueDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("usuarioCpf")]
    public string UsuarioCpf { get; set; } = string.Empty;

    [JsonPropertyName("usuarioNome")]
    public string? UsuarioNome { get; set; }

    [JsonPropertyName("eventoId")]
    public int EventoId { get; set; }

    [JsonPropertyName("eventoNome")]
    public string? EventoNome { get; set; }

    [JsonPropertyName("dataEntrada")]
    public DateTime DataEntrada { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Aguardando";

    [JsonPropertyName("posicao")]
    public int Posicao { get; set; }
}

/// <summary>DTO de resumo de evento para exibição no perfil do organizador.</summary>
public class EventoResumoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("dataEvento")]
    public DateTime DataEvento { get; set; }

    [JsonPropertyName("local")]
    public string Local { get; set; } = string.Empty;

    [JsonPropertyName("precoPadrao")]
    public decimal PrecoPadrao { get; set; }

    [JsonPropertyName("generoMusical")]
    public string GeneroMusical { get; set; } = string.Empty;
}

// ── Meia-entrada (Lei 12.933/2013) ────────────────────────────────────

/// <summary>DTO de documento comprobatório de meia-entrada.</summary>
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

/// <summary>DTO para verificação (aprovação/rejeição) de um documento.</summary>
public class MeiaEntradaVerificacaoDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("motivoRejeicao")]
    public string? MotivoRejeicao { get; set; }
}

/// <summary>DTO de resposta com documentos pendentes.</summary>
public class MeiaEntradaPendenteDto
{
    [JsonPropertyName("quantidadePendentes")]
    public int QuantidadePendentes { get; set; }

    [JsonPropertyName("documentos")]
    public List<MeiaEntradaDocumentoDto> Documentos { get; set; } = [];
}
