using System.Text.Json.Serialization;

namespace src.DTOs;

/// <summary>
/// DTO completo do dashboard administrativo.
/// </summary>
public class DashboardCompletoDTO
{
    // ── Cards resumo ────────────────────────────────────────────────
    public int TotalEventos { get; set; }
    public int EventosPublicados { get; set; }
    public int EventosRascunho { get; set; }
    public int TotalCupons { get; set; }
    public int TotalUsuarios { get; set; }
    public decimal ReceitaTotal { get; set; }

    // ── Gráfico vendas por período (últimos 12 meses) ───────────────
    public List<VendaPorPeriodoDTO> VendasPorPeriodo { get; set; } = [];

    // ── Eventos mais vendidos ───────────────────────────────────────
    public List<EventoMaisVendidoDTO> EventosMaisVendidos { get; set; } = [];

    // ── Taxa de cancelamento ────────────────────────────────────────
    public CancelamentoStatsDTO CancelamentoStats { get; set; } = new();

    // ── Relatório financeiro (últimos 12 meses) ─────────────────────
    public RelatorioFinanceiroDTO RelatorioFinanceiro { get; set; } = new();

    // ── Mapa de calor de demanda por local ──────────────────────────
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
    public double TaxaOcupacao => CapacidadeTotal > 0
        ? Math.Round((double)IngressosVendidos / CapacidadeTotal * 100, 1)
        : 0;
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
    public double TaxaCancelamento => TotalReservas > 0
        ? Math.Round((double)TotalCanceladas / TotalReservas * 100, 2)
        : 0;

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
    public decimal ReceitaLiquida => ReceitaBruta - TaxasServico - SegurosContratados + DescontosConcedidos;

    [JsonPropertyName("totalIngressosVendidos")]
    public int TotalIngressosVendidos { get; set; }

    [JsonPropertyName("ticketMedio")]
    public decimal TicketMedio => TotalIngressosVendidos > 0
        ? Math.Round(ReceitaBruta / TotalIngressosVendidos, 2)
        : 0;

    [JsonPropertyName("cuponsUtilizados")]
    public int CuponsUtilizados { get; set; }
}

/// <summary>
/// Linha do relatório financeiro para exportação CSV.
/// </summary>
public class RelatorioFinanceiroLinhaDTO
{
    [JsonPropertyName("reservaId")]
    public int ReservaId { get; set; }

    [JsonPropertyName("eventoNome")]
    public string EventoNome { get; set; } = string.Empty;

    [JsonPropertyName("dataCompra")]
    public DateTime DataCompra { get; set; }

    [JsonPropertyName("usuarioCpf")]
    public string UsuarioCpf { get; set; } = string.Empty;

    [JsonPropertyName("usuarioNome")]
    public string UsuarioNome { get; set; } = string.Empty;

    [JsonPropertyName("valorIngresso")]
    public decimal ValorIngresso { get; set; }

    [JsonPropertyName("desconto")]
    public decimal Desconto { get; set; }

    [JsonPropertyName("taxaServico")]
    public decimal TaxaServico { get; set; }

    [JsonPropertyName("seguro")]
    public decimal Seguro { get; set; }

    [JsonPropertyName("valorPago")]
    public decimal ValorPago { get; set; }

    [JsonPropertyName("cupom")]
    public string? Cupom { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO com as estatísticas públicas exibidas na Home da landing page.
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
    public string Intensidade => IngressosVendidos switch
    {
        > 100 => "Alta",
        > 50  => "Média",
        > 0   => "Baixa",
        _     => "Nenhuma"
    };
}
