using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging;
using Moq;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Service;
using TicketPrime.Web.Validators;
using Xunit;

// Aliases para resolver ambiguidade entre src.* e TicketPrime.Web.*
using Evento = src.Models.TicketEvent;
using Reserva = src.Models.Reservation;
using Usuario = src.Models.User;
using ReservaDetalhadaDTO = src.DTOs.ReservationDetailDto;
// Mantém TicketPrime.Web.Models para EventoCreateDto, FotoCriptografada, etc.
using TicketPrime.Web.Models;

namespace TicketPrime.Tests.TaxaServico;

// ─────────────────────────────────────────────────────────────────────────────
// Validação no frontend (FluentValidation)
// ─────────────────────────────────────────────────────────────────────────────
public class TaxaServicoValidatorTests
{
    private readonly EventoCreateDtoValidator _v = new();

    [Theory]
    [InlineData(100.00, 5.00)]   // exatamente 5% → válido
    [InlineData(100.00, 4.99)]   // abaixo de 5% → válido
    [InlineData(100.00, 0.00)]   // zero → válido
    [InlineData(200.00, 10.00)]  // 5% de 200 → válido
    public void TaxaServico_DentroDoLimite_DevePassar(decimal preco, decimal taxa)
    {
        var dto = new EventoCreateDto
        {
            Nome = "Evento Teste",
            DataHora = DateTime.Now.AddDays(2),
            Local = "Local válido",
            GeneroMusical = "Rock",
            Preco = preco,
            TaxaServico = taxa,
            CapacidadeMaxima = 100,
            EventoGratuito = false
        };

        var result = _v.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.TaxaServico);
    }

    [Theory]
    [InlineData(100.00, 5.01)]   // 0.01 acima de 5% → inválido
    [InlineData(100.00, 10.00)]  // 10% → inválido
    [InlineData(200.00, 10.01)]  // 5.005% → inválido
    public void TaxaServico_AcimaDoLimite_DeveFalhar(decimal preco, decimal taxa)
    {
        var dto = new EventoCreateDto
        {
            Nome = "Evento Teste",
            DataHora = DateTime.Now.AddDays(2),
            Local = "Local válido",
            GeneroMusical = "Rock",
            Preco = preco,
            TaxaServico = taxa,
            CapacidadeMaxima = 100,
            EventoGratuito = false
        };

        var result = _v.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TaxaServico)
              .WithErrorMessage("A taxa de serviço não pode exceder 5% do preço do ingresso.");
    }

    [Fact]
    public void TaxaServico_Negativa_DeveFalhar()
    {
        var dto = new EventoCreateDto
        {
            Nome = "Evento Teste",
            DataHora = DateTime.Now.AddDays(2),
            Local = "Local válido",
            GeneroMusical = "Rock",
            Preco = 100m,
            TaxaServico = -1m,
            CapacidadeMaxima = 100,
            EventoGratuito = false
        };

        var result = _v.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TaxaServico)
              .WithErrorMessage("A taxa de serviço não pode ser negativa.");
    }

    [Fact]
    public void TaxaServico_EventoGratuito_ComTaxa_DeveFalhar()
    {
        var dto = new EventoCreateDto
        {
            Nome = "Evento Teste",
            DataHora = DateTime.Now.AddDays(2),
            Local = "Local válido",
            GeneroMusical = "Rock",
            Preco = 0m,
            TaxaServico = 5m,
            CapacidadeMaxima = 100,
            EventoGratuito = true
        };

        var result = _v.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TaxaServico)
              .WithErrorMessage("Evento gratuito não pode cobrar taxa de serviço.");
    }

    [Fact]
    public void TaxaServico_EventoGratuito_SemTaxa_DevePassar()
    {
        var dto = new EventoCreateDto
        {
            Nome = "Evento Teste",
            DataHora = DateTime.Now.AddDays(2),
            Local = "Local válido",
            GeneroMusical = "Rock",
            Preco = null,
            TaxaServico = null,
            CapacidadeMaxima = 100,
            EventoGratuito = true
        };

        var result = _v.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.TaxaServico);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// EventoService – validação de taxa no backend
// ─────────────────────────────────────────────────────────────────────────────
public class EventoServiceTaxaTests
{
    private static EventService CriarService(Mock<IEventoRepository> repoMock)
    {
        var reservaMock = new Mock<IReservaRepository>();
        var usuarioMock = new Mock<IUsuarioRepository>();
        var emailTemplateMock = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!);
        var loggerMock = new Mock<ILogger<EventService>>();
        return new EventService(repoMock.Object, reservaMock.Object, usuarioMock.Object,
                                 emailTemplateMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task CriarEvento_TaxaAcimaDe5Pct_DeveLancarExcecao()
    {
        var mock = new Mock<IEventoRepository>();
        var svc = CriarService(mock);

        var dto = new src.DTOs.CreateEventDto
        {
            Nome = "Evento",
            CapacidadeTotal = 100,
            DataEvento = DateTime.Now.AddDays(5),
            PrecoPadrao = 100m,
            TaxaServico = 6m  // 6% > 5%
        };

        await Assert.ThrowsAsync<ArgumentException>(() => svc.CriarNovoEvento(dto));
    }

    [Fact]
    public async Task CriarEvento_TaxaNegativa_DeveLancarExcecao()
    {
        var mock = new Mock<IEventoRepository>();
        var svc = CriarService(mock);

        var dto = new src.DTOs.CreateEventDto
        {
            Nome = "Evento",
            CapacidadeTotal = 100,
            DataEvento = DateTime.Now.AddDays(5),
            PrecoPadrao = 100m,
            TaxaServico = -1m
        };

        await Assert.ThrowsAsync<ArgumentException>(() => svc.CriarNovoEvento(dto));
    }

    [Fact]
    public async Task CriarEvento_TaxaExatamente5Pct_DevePassar()
    {
        var mock = new Mock<IEventoRepository>();
        mock.Setup(r => r.AdicionarAsync(It.IsAny<Evento>())).ReturnsAsync(1);
        var svc = CriarService(mock);

        var dto = new src.DTOs.CreateEventDto
        {
            Nome = "Evento",
            CapacidadeTotal = 100,
            DataEvento = DateTime.Now.AddDays(5),
            PrecoPadrao = 100m,
            TaxaServico = 5m  // exatamente 5%
        };

        var resultado = await svc.CriarNovoEvento(dto);
        Assert.NotNull(resultado);
        Assert.Equal(5m, resultado.TaxaServico);
    }

    [Fact]
    public async Task CriarEvento_TaxaZero_DevePassar()
    {
        var mock = new Mock<IEventoRepository>();
        mock.Setup(r => r.AdicionarAsync(It.IsAny<Evento>())).ReturnsAsync(1);
        var svc = CriarService(mock);

        var dto = new src.DTOs.CreateEventDto
        {
            Nome = "Evento",
            CapacidadeTotal = 100,
            DataEvento = DateTime.Now.AddDays(5),
            PrecoPadrao = 100m,
            TaxaServico = 0m
        };

        var resultado = await svc.CriarNovoEvento(dto);
        Assert.NotNull(resultado);
        Assert.Equal(0m, resultado.TaxaServico);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// EventoService – DeletarEventoAsync
// ─────────────────────────────────────────────────────────────────────────────
public class EventoServiceDeletarTests
{
    private static EventService CriarService(Mock<IEventoRepository> repoMock)
    {
        var reservaMock = new Mock<IReservaRepository>();
        var usuarioMock = new Mock<IUsuarioRepository>();
        var emailTemplateMock = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!);
        var loggerMock = new Mock<ILogger<EventService>>();
        return new EventService(repoMock.Object, reservaMock.Object, usuarioMock.Object,
                                 emailTemplateMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task DeletarEvento_ComReservas_DeveLancarExcecao()
    {
        var mock = new Mock<IEventoRepository>();
        mock.Setup(r => r.ObterPorIdAsync(1))
            .ReturnsAsync(new Evento { Id = 1, Nome = "Evento", CapacidadeTotal = 100,
                DataEvento = DateTime.Now.AddDays(5), PrecoPadrao = 50m });
        mock.Setup(r => r.DeletarAsync(1)).ReturnsAsync(false); // false = tem reservas

        var svc = CriarService(mock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeletarEventoAsync(1));
    }

    [Fact]
    public async Task DeletarEvento_SemReservas_DeveSuceder()
    {
        var mock = new Mock<IEventoRepository>();
        mock.Setup(r => r.ObterPorIdAsync(2))
            .ReturnsAsync(new Evento { Id = 2, Nome = "Evento", CapacidadeTotal = 100,
                DataEvento = DateTime.Now.AddDays(5), PrecoPadrao = 50m });
        mock.Setup(r => r.DeletarAsync(2)).ReturnsAsync(true);

        var svc = CriarService(mock);

        // Não deve lançar exceção
        await svc.DeletarEventoAsync(2);
        mock.Verify(r => r.DeletarAsync(2), Times.Once);
    }

    [Fact]
    public async Task DeletarEvento_NaoEncontrado_DeveLancarExcecao()
    {
        var mock = new Mock<IEventoRepository>();
        mock.Setup(r => r.ObterPorIdAsync(99)).ReturnsAsync((Evento?)null);

        var svc = CriarService(mock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeletarEventoAsync(99));
        Assert.Contains("não encontrado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ReservaService – lógica de compra com taxa e seguro
// ─────────────────────────────────────────────────────────────────────────────
public class ReservaServiceSeguroTests
{
    private static (ReservationService svc,
                    Mock<ITransacaoCompraExecutor> transacaoMock)
        CriarServico(Evento evento)
    {
        var reservaMock = new Mock<IReservaRepository>();
        var eventoMock  = new Mock<IEventoRepository>();
        var usuarioMock = new Mock<IUsuarioRepository>();
        var cupomMock   = new Mock<ICupomRepository>();
        var transacaoMock = new Mock<ITransacaoCompraExecutor>();

        eventoMock.Setup(r => r.ObterPorIdAsync(evento.Id)).ReturnsAsync(evento);
        eventoMock.Setup(r => r.ObterTipoIngressoPorIdAsync(It.IsAny<int>()))
                  .ReturnsAsync(new src.Models.TicketType
                  {
                      Id = 1, EventoId = evento.Id, Nome = "Pista",
                      Preco = evento.PrecoPadrao, CapacidadeTotal = 100, CapacidadeRestante = 100
                  });
        usuarioMock.Setup(r => r.ObterPorCpf("12345678901"))
                   .ReturnsAsync(new Usuario { Cpf = "12345678901", Nome = "Teste",
                                               Email = "a@b.com", Senha = "x", EmailVerificado = true });
        reservaMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync("12345678901", evento.Id))
                   .ReturnsAsync(0);
        reservaMock.Setup(r => r.ContarReservasPorEventoAsync(evento.Id)).ReturnsAsync(0);

        // O mock do executor retorna a própria reserva que recebeu (com Id=1)
        transacaoMock
            .Setup(t => t.ExecutarTransacaoAsync(
                It.IsAny<Reserva>(), It.IsAny<Evento>(), It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync((Reserva r, Evento e, string? c, bool a, int t, int? l) =>
            {
                r.Id = 1;
                return r;
            });

        var emailTemplateMock = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!);
        var loggerMock = new Mock<ILogger<ReservationService>>();
        var filaEsperaServiceMock = new Mock<IWaitingQueueService>();

        var auditLogRepoMock = new Mock<IAuditLogRepository>();
        var auditLoggerMock = new Mock<ILogger<AuditLogService>>();
        var auditLogSvc = new AuditLogService(auditLogRepoMock.Object, auditLoggerMock.Object);
        var paymentGatewayMock = new Mock<IPaymentGateway>();
        paymentGatewayMock.Setup(p => p.ProcessarAsync(It.IsAny<PaymentRequest>()))
                          .ReturnsAsync(PaymentResult.Ok("TX-TESTE-001"));
        var svc = new ReservationService(reservaMock.Object, eventoMock.Object,
                                     usuarioMock.Object, cupomMock.Object,
                                     transacaoMock.Object,
                                     new DbConnectionFactory("Server=.;Database=TicketPrime_UnitTest;Trusted_Connection=true;"),
                                     emailTemplateMock.Object,
                                     loggerMock.Object,
                                     filaEsperaServiceMock.Object,
                                     auditLogSvc,
                                     paymentGatewayMock.Object,
                                     new Mock<IMeiaEntradaRepository>().Object,
                                     new Mock<IMeiaEntradaStorageService>().Object);
        return (svc, transacaoMock);
    }

    private static Evento EventoPadrao => new()
    {
        Id = 1, Nome = "Show", CapacidadeTotal = 100,
        DataEvento = DateTime.Now.AddDays(10),
        PrecoPadrao = 100m, TaxaServico = 5m
    };

    [Fact]
    public async Task ComprarSemSeguro_ValorFinalIncluiTaxaMasNaoSeguro()
    {
        var (svc, transacaoMock) = CriarServico(EventoPadrao);

        var reserva = await svc.ComprarIngressoAsync("12345678901", 1, 1,
                                                      contratarSeguro: false);

        // ValorFinal = ingresso (100) + taxa (5) + seguro (0) = 105
        Assert.Equal(105m, reserva.ValorFinalPago);
        Assert.Equal(5m,   reserva.TaxaServicoPago);
        Assert.False(reserva.TemSeguro);
        Assert.Equal(0m,   reserva.ValorSeguroPago);

        // Verificar que o executor foi chamado com os valores corretos
        transacaoMock.Verify(t => t.ExecutarTransacaoAsync(
            It.Is<Reserva>(r => r.ValorFinalPago == 105m
                             && r.TaxaServicoPago == 5m
                             && r.TemSeguro == false
                             && r.ValorSeguroPago == 0m),
            It.IsAny<Evento>(),
            null,
            false,
            It.IsAny<int>(),
            It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task ComprarComSeguro_ValorFinalIncluiTaxaESeguro()
    {
        var (svc, transacaoMock) = CriarServico(EventoPadrao);

        var reserva = await svc.ComprarIngressoAsync("12345678901", 1, 1,
                                                      contratarSeguro: true);

        // Seguro = 15% de 100 = 15
        // ValorFinal = 100 + 5 + 15 = 120
        Assert.Equal(120m, reserva.ValorFinalPago);
        Assert.Equal(5m,   reserva.TaxaServicoPago);
        Assert.True(reserva.TemSeguro);
        Assert.Equal(15m,  reserva.ValorSeguroPago);

        // Verificar que o executor foi chamado com os valores corretos
        transacaoMock.Verify(t => t.ExecutarTransacaoAsync(
            It.Is<Reserva>(r => r.ValorFinalPago == 120m
                             && r.TaxaServicoPago == 5m
                             && r.TemSeguro == true
                             && r.ValorSeguroPago == 15m),
            It.IsAny<Evento>(),
            null,
            false,
            It.IsAny<int>(),
            It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public void ValorDevolvivel_SemSeguro_NaoIncluiTaxa()
    {
        // Sem seguro: devolve só o ingresso (ValorFinalPago - TaxaServicoPago)
        var dto = new ReservaDetalhadaDTO
        {
            ValorFinalPago  = 105m,
            TaxaServicoPago = 5m,
            TemSeguro       = false,
            ValorSeguroPago = 0m
        };

        Assert.Equal(100m, dto.ValorDevolvivel);
    }

    [Fact]
    public void ValorDevolvivel_ComSeguro_IncluiTaxa()
    {
        // Com seguro: devolve ingresso + taxa (perde só o seguro)
        var dto = new ReservaDetalhadaDTO
        {
            ValorFinalPago  = 120m,
            TaxaServicoPago = 5m,
            TemSeguro       = true,
            ValorSeguroPago = 15m
        };

        // Devolvível = 120 - 15 = 105 (ingresso + taxa)
        Assert.Equal(105m, dto.ValorDevolvivel);
    }

    [Fact]
    public void ValorDevolvivel_SemSeguro_SemTaxa_DevolveValorIntegral()
    {
        // Sem taxa e sem seguro: devolve 100% do ingresso
        var dto = new ReservaDetalhadaDTO
        {
            ValorFinalPago  = 100m,
            TaxaServicoPago = 0m,
            TemSeguro       = false,
            ValorSeguroPago = 0m
        };

        Assert.Equal(100m, dto.ValorDevolvivel);
    }
}
