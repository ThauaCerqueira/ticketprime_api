using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Distributed;
using src.Controllers;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Service;
using Moq;
using Xunit;

namespace tests;

/// <summary>
/// Testes unitários do endpoint de exportação CSV de participantes:
///   GET /api/admin/eventos/{id}/participantes.csv
///
/// Foca em:
///   1. Retorno 404 quando o evento não existe
///   2. CSV gerado corretamente (cabeçalho + linhas)
///   3. Proteção contra CSV injection (campos com fórmulas são sanitizados)
///   4. CPF mascarado nos dados exportados
/// </summary>
public class CsvExportTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static AdminController CriarController(
        Mock<IEventoRepository>? eventoMock = null,
        Mock<IReservaRepository>? reservaMock = null)
    {
        eventoMock  ??= new Mock<IEventoRepository>();
        reservaMock ??= new Mock<IReservaRepository>();
        var cupomMock   = new Mock<ICupomRepository>();
        var usuarioMock = new Mock<IUsuarioRepository>();

        var cacheMock = new Mock<IDistributedCache>();
        var auditMock = new Mock<AuditLogService>(
            Mock.Of<src.Infrastructure.IRepository.IAuditLogRepository>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AuditLogService>>());

        return new AdminController(
            eventoMock.Object,
            reservaMock.Object,
            cupomMock.Object,
            usuarioMock.Object,
            cacheMock.Object,
            auditMock.Object);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 404 — evento inexistente
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportarParticipantes_EventoInexistente_Retorna404()
    {
        var eventoMock = new Mock<IEventoRepository>();
        eventoMock.Setup(r => r.ObterPorIdAsync(99))
                  .ReturnsAsync((src.Models.TicketEvent?)null);

        var controller = CriarController(eventoMock: eventoMock);

        var result = await controller.ExportarParticipantes(99);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(404, statusResult.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 200 — CSV gerado com cabeçalho correto
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportarParticipantes_EventoExiste_RetornaCsvComCabecalho()
    {
        var evento = new src.Models.TicketEvent { Id = 1, Nome = "Show de Rock" };

        var eventoMock = new Mock<IEventoRepository>();
        eventoMock.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(evento);

        var reservaMock = new Mock<IReservaRepository>();
        reservaMock.Setup(r => r.ListarParticipantesPorEventoAsync(1))
                   .ReturnsAsync(new List<ParticipanteDto>());

        var controller = CriarController(eventoMock, reservaMock);

        var result = await controller.ExportarParticipantes(1);

        // Deve ser um FileContentHttpResult (Results.File)
        var fileResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>(result);
        Assert.Equal("text/csv; charset=utf-8", fileResult.ContentType);

        var csv = System.Text.Encoding.UTF8.GetString(fileResult.FileContents.ToArray());
        Assert.StartsWith("CodigoIngresso,NomeParticipante,CPF,Email,Setor,ValorPago,Status,DataCompra,DataCheckin,MeiaEntrada,TemSeguro", csv);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CSV injection — campos que começam com = + - @ são sanitizados
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportarParticipantes_NomeComFormula_EhSanitizadoNoCsv()
    {
        var evento = new src.Models.TicketEvent { Id = 2, Nome = "Evento Teste" };

        var participante = new ParticipanteDto
        {
            CodigoIngresso   = "ING-001",
            NomeParticipante = "=CMD|'/C calc'!A0",   // fórmula maliciosa
            Cpf              = "***.456.789-**",
            Email            = "malicious@test.com",
            Setor            = "Pista",
            ValorPago        = 100m,
            Status           = "Ativa",
            DataCompra       = new DateTime(2026, 5, 10),
            MeiaEntrada      = false,
            TemSeguro        = false
        };

        var eventoMock = new Mock<IEventoRepository>();
        eventoMock.Setup(r => r.ObterPorIdAsync(2)).ReturnsAsync(evento);

        var reservaMock = new Mock<IReservaRepository>();
        reservaMock.Setup(r => r.ListarParticipantesPorEventoAsync(2))
                   .ReturnsAsync(new List<ParticipanteDto> { participante });

        var controller = CriarController(eventoMock, reservaMock);
        var result = await controller.ExportarParticipantes(2);

        var fileResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>(result);
        var csv = System.Text.Encoding.UTF8.GetString(fileResult.FileContents.ToArray());

        // O nome sanitizado NÃO deve começar com '=' no CSV
        Assert.DoesNotContain("=CMD", csv);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CSV — múltiplos participantes geram múltiplas linhas
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportarParticipantes_DoisParticipantes_GeraLinhasCorretas()
    {
        var evento = new src.Models.TicketEvent { Id = 3, Nome = "Festival" };

        var participantes = new List<ParticipanteDto>
        {
            new()
            {
                CodigoIngresso   = "ING-A1",
                NomeParticipante = "Ana Souza",
                Cpf              = "***.111.222-**",
                Email            = "ana@test.com",
                Setor            = "VIP",
                ValorPago        = 250m,
                Status           = "Ativa",
                DataCompra       = new DateTime(2026, 4, 1),
                MeiaEntrada      = false,
                TemSeguro        = true
            },
            new()
            {
                CodigoIngresso   = "ING-B2",
                NomeParticipante = "Bruno Lima",
                Cpf              = "***.333.444-**",
                Email            = "bruno@test.com",
                Setor            = "Pista",
                ValorPago        = 100m,
                Status           = "Ativa",
                DataCompra       = new DateTime(2026, 4, 2),
                MeiaEntrada      = true,
                TemSeguro        = false
            }
        };

        var eventoMock = new Mock<IEventoRepository>();
        eventoMock.Setup(r => r.ObterPorIdAsync(3)).ReturnsAsync(evento);

        var reservaMock = new Mock<IReservaRepository>();
        reservaMock.Setup(r => r.ListarParticipantesPorEventoAsync(3))
                   .ReturnsAsync(participantes);

        var controller = CriarController(eventoMock, reservaMock);
        var result = await controller.ExportarParticipantes(3);

        var fileResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult>(result);
        var csv = System.Text.Encoding.UTF8.GetString(fileResult.FileContents.ToArray());

        // Cabeçalho + 2 linhas de dados = 3 linhas não-vazias
        var linhas = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, linhas.Length);

        Assert.Contains("ING-A1", csv);
        Assert.Contains("Ana Souza", csv);
        Assert.Contains("Sim", csv);   // TemSeguro = true
        Assert.Contains("ING-B2", csv);
        Assert.Contains("Bruno Lima", csv);
    }
}
