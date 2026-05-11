using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.DTOs;
using src.Infrastructure.IRepository;
using System.Text;

namespace src.Controllers;

[ApiController]
[Route("api/admin")]
[EnableRateLimiting("geral")]
public class AdminController : ControllerBase
{
    private readonly IEventoRepository _eventoRepo;
    private readonly IReservaRepository _reservaRepo;
    private readonly ICupomRepository _cupomRepo;
    private readonly IUsuarioRepository _usuarioRepo;

    public AdminController(
        IEventoRepository eventoRepo,
        IReservaRepository reservaRepo,
        ICupomRepository cupomRepo,
        IUsuarioRepository usuarioRepo)
    {
        _eventoRepo = eventoRepo;
        _reservaRepo = reservaRepo;
        _cupomRepo = cupomRepo;
        _usuarioRepo = usuarioRepo;
    }

    /// <summary>
    /// Dashboard admin resumido (cards) — mantido para compatibilidade.
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> Dashboard()
    {
        var eventos = (await _eventoRepo.ObterTodosAsync()).Itens.ToList();
        var cupons  = (await _cupomRepo.ListarAsync()).ToList();
        var usuarios = (await _usuarioRepo.ListarUsuarios()).ToList();
        var receitaTotal = await _reservaRepo.ObterReceitaTotalAsync();

        return Results.Ok(new
        {
            totalEventos      = eventos.Count,
            eventosPublicados = eventos.Count(e => e.Status == "Publicado"),
            eventosRascunho   = eventos.Count(e => e.Status == "Rascunho"),
            totalCupons       = cupons.Count,
            totalUsuarios     = usuarios.Count,
            receitaTotal      = receitaTotal,
        });
    }

    /// <summary>
    /// Dashboard completo com gráficos, tabelas e métricas.
    /// </summary>
    [HttpGet("dashboard/completo")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> DashboardCompleto()
    {
        var eventos = (await _eventoRepo.ObterTodosAsync()).Itens.ToList();
        var cupons  = (await _cupomRepo.ListarAsync()).ToList();
        var usuarios = (await _usuarioRepo.ListarUsuarios()).ToList();
        var receitaTotal = await _reservaRepo.ObterReceitaTotalAsync();

        var fim = DateTime.UtcNow;
        var inicio = fim.AddMonths(-12);

        var vendasPorPeriodo = (await _reservaRepo.ObterVendasPorPeriodoAsync(inicio, fim)).ToList();
        var eventosMaisVendidos = (await _reservaRepo.ObterEventosMaisVendidosAsync(10)).ToList();
        var cancelamentoStats = await _reservaRepo.ObterCancelamentoStatsAsync();
        var relatorioFinanceiro = await _reservaRepo.ObterRelatorioFinanceiroAsync(inicio, fim);
        var demandaPorLocal = (await _reservaRepo.ObterDemandaPorLocalAsync()).ToList();

        var completo = new DashboardCompletoDTO
        {
            TotalEventos      = eventos.Count,
            EventosPublicados = eventos.Count(e => e.Status == "Publicado"),
            EventosRascunho   = eventos.Count(e => e.Status == "Rascunho"),
            TotalCupons       = cupons.Count,
            TotalUsuarios     = usuarios.Count,
            ReceitaTotal      = receitaTotal,

            VendasPorPeriodo    = vendasPorPeriodo,
            EventosMaisVendidos = eventosMaisVendidos,
            CancelamentoStats   = cancelamentoStats,
            RelatorioFinanceiro = relatorioFinanceiro,
            DemandaPorLocal     = demandaPorLocal,
        };

        return Results.Ok(completo);
    }

    /// <summary>
    /// Relatório financeiro detalhado (formato JSON).
    /// </summary>
    [HttpGet("relatorio-financeiro")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> RelatorioFinanceiro(
        [FromQuery] DateTime? inicio = null,
        [FromQuery] DateTime? fim = null)
    {
        inicio ??= DateTime.UtcNow.AddMonths(-12);
        fim ??= DateTime.UtcNow;

        var relatorio = await _reservaRepo.ObterRelatorioFinanceiroAsync(inicio, fim);
        var linhas = (await _reservaRepo.ObterLinhasRelatorioFinanceiroAsync(inicio, fim)).ToList();

        return Results.Ok(new
        {
            resumo = relatorio,
            linhas = linhas,
            totalLinhas = linhas.Count
        });
    }

    /// <summary>
    /// Exporta relatório financeiro em CSV.
    /// </summary>
    [HttpGet("relatorio-financeiro/csv")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> RelatorioFinanceiroCsv(
        [FromQuery] DateTime? inicio = null,
        [FromQuery] DateTime? fim = null)
    {
        inicio ??= DateTime.UtcNow.AddMonths(-12);
        fim ??= DateTime.UtcNow;

        var linhas = (await _reservaRepo.ObterLinhasRelatorioFinanceiroAsync(inicio, fim)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("ReservaId,Evento,DataCompra,CPF,Usuario,ValorIngresso,Desconto,TaxaServico,Seguro,ValorPago,Cupom,Status");

        foreach (var l in linhas)
        {
            sb.AppendLine(
                $"{l.ReservaId}," +
                $"\"{l.EventoNome}\"," +
                $"{l.DataCompra:yyyy-MM-dd HH:mm}," +
                $"\"{l.UsuarioCpf}\"," +
                $"\"{l.UsuarioNome}\"," +
                $"{l.ValorIngresso:F2}," +
                $"{l.Desconto:F2}," +
                $"{l.TaxaServico:F2}," +
                $"{l.Seguro:F2}," +
                $"{l.ValorPago:F2}," +
                $"\"{l.Cupom}\"," +
                $"{l.Status}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Results.File(bytes, "text/csv", $"relatorio-financeiro-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>
    /// Exporta relatório financeiro em formato TSV (abrível como planilha).
    /// Alternativa simples ao PDF, compatível com Excel/Google Sheets.
    /// </summary>
    [HttpGet("relatorio-financeiro/pdf")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> RelatorioFinanceiroPdf(
        [FromQuery] DateTime? inicio = null,
        [FromQuery] DateTime? fim = null)
    {
        inicio ??= DateTime.UtcNow.AddMonths(-12);
        fim ??= DateTime.UtcNow;

        var relatorio = await _reservaRepo.ObterRelatorioFinanceiroAsync(inicio, fim);
        var linhas = (await _reservaRepo.ObterLinhasRelatorioFinanceiroAsync(inicio, fim)).ToList();

        // Gera HTML que pode ser impresso como PDF pelo navegador
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; padding: 2rem; color: #1a1a2e; }");
        html.AppendLine("h1 { font-size: 1.5rem; margin-bottom: 0.25rem; }");
        html.AppendLine(".subtitle { color: #6B7280; font-size: 0.875rem; margin-bottom: 1.5rem; }");
        html.AppendLine(".summary { display: flex; gap: 2rem; margin-bottom: 2rem; flex-wrap: wrap; }");
        html.AppendLine(".summary-item { background: #F8FAFC; border-radius: 8px; padding: 1rem; min-width: 140px; }");
        html.AppendLine(".summary-item .label { font-size: 0.75rem; color: #6B7280; text-transform: uppercase; }");
        html.AppendLine(".summary-item .value { font-size: 1.25rem; font-weight: 700; color: #5B5BD6; }");
        html.AppendLine("table { width: 100%; border-collapse: collapse; font-size: 0.8rem; }");
        html.AppendLine("th { background: #F1F5F9; color: #1a1a2e; font-weight: 600; padding: 0.5rem; text-align: left; }");
        html.AppendLine("td { padding: 0.4rem 0.5rem; border-bottom: 1px solid #E5E7EB; }");
        html.AppendLine(".footer { margin-top: 2rem; font-size: 0.75rem; color: #9CA3AF; }");
        html.AppendLine("</style></head><body>");
        html.AppendLine($"<h1>Relatório Financeiro — TicketPrime</h1>");
        html.AppendLine($"<p class='subtitle'>Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}</p>");

        html.AppendLine("<div class='summary'>");
        html.AppendLine($"<div class='summary-item'><div class='label'>Receita Bruta</div><div class='value'>{relatorio.ReceitaBruta:C}</div></div>");
        html.AppendLine($"<div class='summary-item'><div class='label'>Taxas de Serviço</div><div class='value'>{relatorio.TaxasServico:C}</div></div>");
        html.AppendLine($"<div class='summary-item'><div class='label'>Seguros</div><div class='value'>{relatorio.SegurosContratados:C}</div></div>");
        html.AppendLine($"<div class='summary-item'><div class='label'>Descontos</div><div class='value'>{relatorio.DescontosConcedidos:C}</div></div>");
        html.AppendLine($"<div class='summary-item'><div class='label'>Receita Líquida</div><div class='value'>{relatorio.ReceitaLiquida:C}</div></div>");
        html.AppendLine($"<div class='summary-item'><div class='label'>Ticket Médio</div><div class='value'>{relatorio.TicketMedio:C}</div></div>");
        html.AppendLine($"<div class='summary-item'><div class='label'>Ingressos Vendidos</div><div class='value'>{relatorio.TotalIngressosVendidos}</div></div>");
        html.AppendLine("</div>");

        html.AppendLine("<table>");
        html.AppendLine("<tr><th>ID</th><th>Evento</th><th>Data</th><th>Usuário</th><th>Valor</th><th>Desc.</th><th>Taxa</th><th>Seg.</th><th>Pago</th><th>Cupom</th><th>Status</th></tr>");
        foreach (var l in linhas)
        {
            html.AppendLine($"<tr>" +
                $"<td>{l.ReservaId}</td>" +
                $"<td>{System.Net.WebUtility.HtmlEncode(l.EventoNome)}</td>" +
                $"<td>{l.DataCompra:dd/MM/yyyy}</td>" +
                $"<td>{System.Net.WebUtility.HtmlEncode(l.UsuarioNome)}</td>" +
                $"<td>{l.ValorIngresso:F2}</td>" +
                $"<td>{l.Desconto:F2}</td>" +
                $"<td>{l.TaxaServico:F2}</td>" +
                $"<td>{l.Seguro:F2}</td>" +
                $"<td>{l.ValorPago:F2}</td>" +
                $"<td>{l.Cupom}</td>" +
                $"<td>{l.Status}</td>" +
                $"</tr>");
        }
        html.AppendLine("</table>");
        html.AppendLine($"<div class='footer'>Gerado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC — TicketPrime</div>");
        html.AppendLine("</body></html>");

        var bytes = Encoding.UTF8.GetBytes(html.ToString());
        return Results.File(bytes, "text/html", $"relatorio-financeiro-{DateTime.UtcNow:yyyyMMdd}.html");
    }

    /// <summary>
    /// Exporta lista de participantes de um evento em CSV (somente ADMIN).
    /// Útil para controle de entrada, lista de presença e conciliação.
    /// </summary>
    [HttpGet("eventos/{id:int}/participantes.csv")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IResult> ExportarParticipantes(int id)
    {
        var evento = await _eventoRepo.ObterPorIdAsync(id);
        if (evento == null)
            return Results.NotFound(new { mensagem = "Evento não encontrado." });

        var reservas = await _reservaRepo.ListarParticipantesPorEventoAsync(id);

        var csv = new StringBuilder();
        csv.AppendLine("CodigoIngresso,NomeParticipante,CPF,Email,Setor,ValorPago,Status,DataCompra,DataCheckin,MeiaEntrada,TemSeguro");

        foreach (var r in reservas)
        {
            // Sanitiza campos para evitar CSV injection (OWASP)
            var nome  = SanitizarCsvCampo(r.NomeParticipante);
            var email = SanitizarCsvCampo(r.Email);
            var setor = SanitizarCsvCampo(r.Setor);

            csv.AppendLine(
                $"{r.CodigoIngresso},{nome},{r.Cpf},{email},{setor}," +
                $"{r.ValorPago:F2},{r.Status}," +
                $"{r.DataCompra:dd/MM/yyyy HH:mm}," +
                $"{(r.DataCheckin.HasValue ? r.DataCheckin.Value.ToString("dd/MM/yyyy HH:mm") : "")}," +
                $"{(r.MeiaEntrada ? "Sim" : "Não")}," +
                $"{(r.TemSeguro ? "Sim" : "Não")}");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var nomeArquivo = $"participantes-evento-{id}-{DateTime.UtcNow:yyyyMMdd}.csv";
        return Results.File(bytes, "text/csv; charset=utf-8", nomeArquivo);
    }

    private static string SanitizarCsvCampo(string? campo)
    {
        if (string.IsNullOrEmpty(campo)) return string.Empty;
        // Remove caracteres que disparam fórmulas em planilhas (CSV injection)
        var sanitizado = campo.TrimStart('+', '-', '=', '@', '\t', '\r');
        // Escapa aspas duplas e envolve em aspas se contiver vírgula, aspas ou quebra de linha
        if (sanitizado.Contains(',') || sanitizado.Contains('"') || sanitizado.Contains('\n'))
        {
            sanitizado = sanitizado.Replace("\"", "\"\"");
            return $"\"{sanitizado}\"";
        }
        return sanitizado;
    }
}
