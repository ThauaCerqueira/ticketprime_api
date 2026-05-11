using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using src.Controllers;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Service;
using Xunit;

namespace TicketPrime.Tests.Checkin;

public class IngressoCheckinTests
{
    private readonly Mock<IReservaRepository> _reservaRepoMock;
    private readonly IngressoController _controller;

    private readonly ReservationDetailDto _reservaAtiva;
    private readonly ReservationDetailDto _reservaCancelada;
    private readonly ReservationDetailDto _reservaUsada;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IngressoCheckinTests()
    {
        _reservaRepoMock = new Mock<IReservaRepository>();
        var auditLogMock = new Mock<IAuditLogRepository>();
        var auditLoggerMock = new Mock<ILogger<AuditLogService>>();
        var auditLogService = new AuditLogService(auditLogMock.Object, auditLoggerMock.Object);
        _controller = new IngressoController(_reservaRepoMock.Object, auditLogService);
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _reservaAtiva = new ReservationDetailDto
        {
            Id = 1,
            UsuarioCpf = "12345678901",
            EventoId = 10,
            DataCompra = DateTime.UtcNow.AddDays(-5),
            Nome = "Show de Rock",
            DataEvento = DateTime.UtcNow.AddDays(10),
            PrecoPadrao = 150.00m,
            ValorFinalPago = 150.00m,
            TaxaServicoPago = 15.00m,
            CodigoIngresso = "ABC123DEF456GHI789",
            Status = "Ativa",
            DataCheckin = null
        };

        _reservaCancelada = new ReservationDetailDto
        {
            Id = 2,
            UsuarioCpf = "98765432100",
            EventoId = 10,
            DataCompra = DateTime.UtcNow.AddDays(-3),
            Nome = "Show de Rock",
            DataEvento = DateTime.UtcNow.AddDays(10),
            PrecoPadrao = 150.00m,
            ValorFinalPago = 150.00m,
            TaxaServicoPago = 15.00m,
            CodigoIngresso = "CANCELEDCODE12345",
            Status = "Cancelada",
            DataCheckin = null
        };

        _reservaUsada = new ReservationDetailDto
        {
            Id = 3,
            UsuarioCpf = "55555555555",
            EventoId = 10,
            DataCompra = DateTime.UtcNow.AddDays(-5),
            Nome = "Show de Rock",
            DataEvento = DateTime.UtcNow.AddDays(-1),
            PrecoPadrao = 150.00m,
            ValorFinalPago = 150.00m,
            TaxaServicoPago = 15.00m,
            CodigoIngresso = "USEDTICKET99999",
            Status = "Usada",
            DataCheckin = DateTime.UtcNow.AddDays(-1)
        };
    }

    /// <summary>
    /// Serializa o Value do IResult para JsonElement, contornando a inacessibilidade
    /// de anonymous types (internal) entre assemblies.
    /// </summary>
    private static JsonElement GetBody(IResult result)
    {
        var valueProp = result.GetType().GetProperty("Value");
        var value = valueProp?.GetValue(result);
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
    }

    private static int GetStatusCode(IResult result)
    {
        var statusProp = result.GetType().GetProperty("StatusCode");
        return (int)(statusProp?.GetValue(result) ?? 0);
    }

    // ───────────────────────────────────────────────
    // VALIDAR — Check-in na entrada do evento
    // ───────────────────────────────────────────────

    [Fact]
    public async Task Validar_CodigoVazio_DeveRetornarBadRequest()
    {
        var result = await _controller.Validar("");

        Assert.Equal(400, GetStatusCode(result));
        var body = GetBody(result);
        Assert.Equal("Código inválido.", body.GetProperty("mensagem").GetString());
    }

    [Fact]
    public async Task Validar_CodigoNulo_DeveRetornarBadRequest()
    {
        var result = await _controller.Validar(null!);

        Assert.Equal(400, GetStatusCode(result));
    }

    [Fact]
    public async Task Validar_IngressoNaoEncontrado_DeveRetornarNotFound()
    {
        _reservaRepoMock
            .Setup(r => r.ObterPorCodigoIngressoAsync("INEXISTENTE"))
            .ReturnsAsync((ReservationDetailDto?)null);

        var result = await _controller.Validar("INEXISTENTE");

        Assert.Equal(404, GetStatusCode(result));
        var body = GetBody(result);
        Assert.False(body.GetProperty("valido").GetBoolean());
        Assert.Equal("Ingresso não encontrado.", body.GetProperty("mensagem").GetString());
    }

    [Fact]
    public async Task Validar_IngressoCancelado_DeveRetornarInvalido()
    {
        _reservaRepoMock
            .Setup(r => r.ObterPorCodigoIngressoAsync(_reservaCancelada.CodigoIngresso))
            .ReturnsAsync(_reservaCancelada);

        var result = await _controller.Validar(_reservaCancelada.CodigoIngresso);

        Assert.Equal(200, GetStatusCode(result));
        var body = GetBody(result);
        Assert.False(body.GetProperty("valido").GetBoolean());
        Assert.Contains("cancelado", body.GetProperty("mensagem").GetString()!.ToLower());
        Assert.Equal("Cancelada", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Validar_IngressoJaUtilizado_DeveRejeitarCheckinDuplicado()
    {
        _reservaRepoMock
            .Setup(r => r.ObterPorCodigoIngressoAsync(_reservaUsada.CodigoIngresso))
            .ReturnsAsync(_reservaUsada);

        var result = await _controller.Validar(_reservaUsada.CodigoIngresso);

        Assert.Equal(200, GetStatusCode(result));
        var body = GetBody(result);
        Assert.False(body.GetProperty("valido").GetBoolean());
        Assert.Contains("já foi utilizado", body.GetProperty("mensagem").GetString()!.ToLower());
        Assert.NotNull(body.GetProperty("dataCheckin").GetString());
    }

    [Fact]
    public async Task Validar_IngressoValido_DeveRealizarCheckinComSucesso()
    {
        _reservaRepoMock
            .Setup(r => r.ObterPorCodigoIngressoAsync(_reservaAtiva.CodigoIngresso))
            .ReturnsAsync(_reservaAtiva);

        _reservaRepoMock
            .Setup(r => r.RealizarCheckinAsync(_reservaAtiva.CodigoIngresso))
            .ReturnsAsync(true);

        var result = await _controller.Validar(_reservaAtiva.CodigoIngresso);

        Assert.Equal(200, GetStatusCode(result));
        var body = GetBody(result);
        Assert.True(body.GetProperty("valido").GetBoolean());
        Assert.Contains("check-in realizado com sucesso", body.GetProperty("mensagem").GetString()!.ToLower());
        Assert.Equal("Usada", body.GetProperty("status").GetString());
        Assert.Equal(_reservaAtiva.CodigoIngresso, body.GetProperty("codigoIngresso").GetString());

        _reservaRepoMock.Verify(r => r.RealizarCheckinAsync(_reservaAtiva.CodigoIngresso), Times.Once);
    }

    [Fact]
    public async Task Validar_RaceCondition_DeveRetornarConflict()
    {
        _reservaRepoMock
            .Setup(r => r.ObterPorCodigoIngressoAsync(_reservaAtiva.CodigoIngresso))
            .ReturnsAsync(_reservaAtiva);

        // Simula race condition: outra requisição já consumiu o ingresso
        _reservaRepoMock
            .Setup(r => r.RealizarCheckinAsync(_reservaAtiva.CodigoIngresso))
            .ReturnsAsync(false);

        var result = await _controller.Validar(_reservaAtiva.CodigoIngresso);

        Assert.Equal(409, GetStatusCode(result));
        var body = GetBody(result);
        Assert.False(body.GetProperty("valido").GetBoolean());
        Assert.Contains("não foi possível", body.GetProperty("mensagem").GetString()!.ToLower());
    }

    // ───────────────────────────────────────────────
    // CONSULTAR — Apenas consulta, sem consumir
    // ───────────────────────────────────────────────

    [Fact]
    public async Task Consultar_IngressoValido_DeveRetornarStatusSemRealizarCheckin()
    {
        _reservaRepoMock
            .Setup(r => r.ObterPorCodigoIngressoAsync(_reservaAtiva.CodigoIngresso))
            .ReturnsAsync(_reservaAtiva);

        var result = await _controller.Consultar(_reservaAtiva.CodigoIngresso);

        Assert.Equal(200, GetStatusCode(result));
        var body = GetBody(result);
        Assert.True(body.GetProperty("valido").GetBoolean());
        Assert.Equal("Ativa", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("dataCheckin").ValueKind);

        // Garante que NÃO chamou RealizarCheckinAsync (consulta é read-only)
        _reservaRepoMock.Verify(r => r.RealizarCheckinAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Consultar_IngressoJaUsado_DeveRetornarInvalido()
    {
        _reservaRepoMock
            .Setup(r => r.ObterPorCodigoIngressoAsync(_reservaUsada.CodigoIngresso))
            .ReturnsAsync(_reservaUsada);

        var result = await _controller.Consultar(_reservaUsada.CodigoIngresso);

        Assert.Equal(200, GetStatusCode(result));
        var body = GetBody(result);
        Assert.False(body.GetProperty("valido").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("dataCheckin").ValueKind);
    }

    [Fact]
    public async Task Consultar_CodigoInexistente_DeveRetornarNotFound()
    {
        _reservaRepoMock
            .Setup(r => r.ObterPorCodigoIngressoAsync("XYZ"))
            .ReturnsAsync((ReservationDetailDto?)null);

        var result = await _controller.Consultar("XYZ");

        Assert.Equal(404, GetStatusCode(result));
    }

    [Fact]
    public async Task Consultar_CodigoVazio_DeveRetornarBadRequest()
    {
        var result = await _controller.Consultar("   ");

        Assert.Equal(400, GetStatusCode(result));
    }
}
