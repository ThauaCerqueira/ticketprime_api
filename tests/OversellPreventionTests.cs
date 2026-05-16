using Microsoft.Extensions.Logging;
using Moq;
using src.DTOs;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Models;
using src.Service;
using Xunit;

namespace TicketPrime.Tests;

/// <summary>
/// Testes de prevenção de oversell (sobrevenda de ingressos).
///
/// Contextualiza a correção implementada no High Fix #1:
///   ANTES: TransacaoCompraExecutor decrementava a capacidade do evento sem verificar
///          quantas linhas foram afetadas → possível venda além da capacidade.
///   DEPOIS: ExecuteAsync + IF rowsEvento == 0 → lança InvalidOperationException
///
/// Cenários cobertos:
///   ── Validação pre-transação (ReservationService) ──
///   - Compra quando setor sem vagas → rejeitada antes da transação
///   - Compra com evento já ocorrido → rejeitada
///   - Compra com usuário não encontrado → rejeitada
///   - Compra com tipo de ingresso de outro evento → rejeitada
///   ── Executor (ITransacaoCompraExecutor mock) ──
///   - Executor lançando "setor sem vagas" → serviço propaga exceção
///   - Executor lançando "evento sem vagas" → serviço propaga exceção
///   - Executor lançando "limite por CPF" → serviço propaga exceção
///   ── Fluxo feliz ──
///   - Todos os dados válidos → executor é chamado exatamente uma vez
///   - Executor recebe o evento com capacidade atual
///   ── Variações de capacidade ──
///   - Setor com 0 vagas → rejeitado pré-transação
///   - Setor com vagas mas email não verificado → rejeitado
/// </summary>
public class OversellPreventionTests
{
    // ─────────────────────────────────────────────────────────────────
    // Builders de modelo
    // ─────────────────────────────────────────────────────────────────

    private static TicketEvent CriarEvento(
        int id = 1,
        int capacidadeRestante = 10,
        int limiteIngressos = 4,
        int capacidadeTotal = 100)
    {
        // TicketEvent requires: nome, capacidadeTotal, dataEvento, precoPadrao, limiteIngressosPorUsuario
        var evento = new TicketEvent("Evento Teste", capacidadeTotal, DateTime.UtcNow.AddDays(10), 100m, limiteIngressos);
        evento.Id = id;
        evento.CapacidadeRestante = capacidadeRestante;
        return evento;
    }

    private static TicketType CriarTicketType(int id = 1, int eventoId = 1, int capacidadeRestante = 10)
    {
        return new TicketType
        {
            Id = id,
            EventoId = eventoId,
            Nome = "Pista",
            CapacidadeRestante = capacidadeRestante,
            CapacidadeTotal = capacidadeRestante + 10,
            Preco = 100m
        };
    }

    private static User CriarUsuario(string cpf = "12345678901", bool emailVerificado = true)
    {
        return new User
        {
            Cpf = cpf,
            Nome = "Comprador Teste",
            Email = "teste@ticketprime.com",
            EmailVerificado = emailVerificado,
            Senha = "Senha@123Hashed"
        };
    }

    private static Reservation CriarReserva(string cpf = "12345678901", int eventoId = 1)
    {
        return new Reservation
        {
            Id = 42,
            UsuarioCpf = cpf,
            EventoId = eventoId,
            CodigoIngresso = Guid.NewGuid().ToString("N"),
            ValorFinalPago = 110m,
            StatusPagamento = "PENDENTE"
        };
    }

    // ─────────────────────────────────────────────────────────────────
    // Factory de ReservationService com mocks configurados
    // ─────────────────────────────────────────────────────────────────

    private static ReservationService CriarServico(
        Mock<ITransacaoCompraExecutor>? executorMock = null,
        TicketEvent? evento = null,
        TicketType? ticketType = null,
        User? usuario = null,
        Mock<IReservaRepository>? reservaRepoMock = null)
    {
        evento ??= CriarEvento(capacidadeRestante: 10);
        ticketType ??= CriarTicketType(eventoId: evento.Id, capacidadeRestante: 10);
        usuario ??= CriarUsuario();

        var eventoRepoMock = new Mock<IEventoRepository>();
        eventoRepoMock.Setup(r => r.ObterPorIdAsync(evento.Id)).ReturnsAsync(evento);
        eventoRepoMock.Setup(r => r.ObterTipoIngressoPorIdAsync(ticketType.Id)).ReturnsAsync(ticketType);

        var usuarioRepoMock = new Mock<IUsuarioRepository>();
        usuarioRepoMock.Setup(r => r.ObterPorCpf(usuario.Cpf)).ReturnsAsync(usuario);

        var cupomRepoMock = new Mock<ICupomRepository>();
        executorMock ??= new Mock<ITransacaoCompraExecutor>();
        reservaRepoMock ??= new Mock<IReservaRepository>();
        // Nenhuma reserva prévia por padrão (limite não atingido)
        reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(
            It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(0);

        var dbFactory = new DbConnectionFactory("Server=fake;Database=UnitTests;Trusted_Connection=True;TrustServerCertificate=True;");
        var emailMock = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!, null!);
        var loggerMock = new Mock<ILogger<ReservationService>>();
        var waitingQueueMock = new Mock<IWaitingQueueService>();

        var auditLogRepoMock = new Mock<IAuditLogRepository>();
        var auditLoggerMock = new Mock<ILogger<AuditLogService>>();
        var auditLogMock = new Mock<AuditLogService>(auditLogRepoMock.Object, auditLoggerMock.Object) { CallBase = false };

        var paymentGatewayMock = new Mock<IPaymentGateway>();
        // Gateway sempre retorna sucesso em testes de unidade
        paymentGatewayMock.Setup(g => g.ProcessarAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(PaymentResult.Ok("TX-UNIT-TEST-123"));
        var meiaEntradaRepoMock = new Mock<IMeiaEntradaRepository>();
        var meiaEntradaStorageMock = new Mock<IMeiaEntradaStorageService>();
        var pixCryptoMock = new Mock<PixCryptoService>(null!).Object;

        return new ReservationService(
            reservaRepoMock.Object,
            eventoRepoMock.Object,
            usuarioRepoMock.Object,
            cupomRepoMock.Object,
            executorMock.Object,
            dbFactory,
            emailMock.Object,
            loggerMock.Object,
            waitingQueueMock.Object,
            auditLogMock.Object,
            paymentGatewayMock.Object,
            meiaEntradaRepoMock.Object,
            meiaEntradaStorageMock.Object,
            pixCryptoMock
        );
    }

    // ─────────────────────────────────────────────────────────────────
    // Validação pre-transação — rejeição antes do executor
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ComprarIngresso_SetorSemVagas_DeveRejeitarAntesDeExecutor()
    {
        var evento = CriarEvento();
        var ticketType = CriarTicketType(eventoId: evento.Id, capacidadeRestante: 0); // Sem vagas
        var executorMock = new Mock<ITransacaoCompraExecutor>();

        var svc = CriarServico(executorMock: executorMock, evento: evento, ticketType: ticketType);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ComprarIngressoAsync("12345678901", evento.Id, ticketType.Id));

        Assert.Contains("vagas", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Executor jamais deve ser chamado
        executorMock.Verify(e => e.ExecutarTransacaoAsync(
            It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
            It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ComprarIngresso_EventoJaOcorrido_DeveRejeitarImediatamente()
    {
        var evento = CriarEvento();
        evento.DataEvento = DateTime.UtcNow.AddDays(-1); // Passado

        var executorMock = new Mock<ITransacaoCompraExecutor>();
        var svc = CriarServico(executorMock: executorMock, evento: evento);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ComprarIngressoAsync("12345678901", evento.Id, 1));

        Assert.Contains("já aconteceu", ex.Message, StringComparison.OrdinalIgnoreCase);
        executorMock.Verify(e => e.ExecutarTransacaoAsync(
            It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
            It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ComprarIngresso_UsuarioNaoEncontrado_DeveRejeitarImediatamente()
    {
        var evento = CriarEvento();
        var eventoRepoMock = new Mock<IEventoRepository>();
        var ticketType = CriarTicketType(eventoId: evento.Id);
        eventoRepoMock.Setup(r => r.ObterPorIdAsync(evento.Id)).ReturnsAsync(evento);
        eventoRepoMock.Setup(r => r.ObterTipoIngressoPorIdAsync(ticketType.Id)).ReturnsAsync(ticketType);

        var usuarioRepoMock = new Mock<IUsuarioRepository>();
        usuarioRepoMock.Setup(r => r.ObterPorCpf(It.IsAny<string>())).ReturnsAsync((User?)null);

        var executorMock = new Mock<ITransacaoCompraExecutor>();

        var dbFactory = new DbConnectionFactory("Server=fake;Database=UnitTests;Trusted_Connection=True;TrustServerCertificate=True;");
        var emailMock = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!, null!);
        var loggerMock = new Mock<ILogger<ReservationService>>();
        var auditLogRepoMock = new Mock<IAuditLogRepository>();
        var auditLoggerMock = new Mock<ILogger<AuditLogService>>();
        var auditLogMock = new Mock<AuditLogService>(auditLogRepoMock.Object, auditLoggerMock.Object) { CallBase = false };

        var svc = new ReservationService(
            new Mock<IReservaRepository>().Object,
            eventoRepoMock.Object,
            usuarioRepoMock.Object,
            new Mock<ICupomRepository>().Object,
            executorMock.Object,
            dbFactory,
            emailMock.Object,
            loggerMock.Object,
            new Mock<IWaitingQueueService>().Object,
            auditLogMock.Object,
            new Mock<IPaymentGateway>().Object,
            new Mock<IMeiaEntradaRepository>().Object,
            new Mock<IMeiaEntradaStorageService>().Object,
            new Mock<PixCryptoService>(null!).Object
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ComprarIngressoAsync("99999999999", evento.Id, ticketType.Id));

        Assert.Contains("Usuário não encontrado", ex.Message);
    }

    [Fact]
    public async Task ComprarIngresso_TicketTypeDoutroEvento_DeveRejeitarComMensagemCorreta()
    {
        var evento = CriarEvento(id: 1);
        var ticketTypeDeOutroEvento = CriarTicketType(id: 99, eventoId: 999); // EventoId diferente!

        var eventoRepoMock = new Mock<IEventoRepository>();
        eventoRepoMock.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(evento);
        eventoRepoMock.Setup(r => r.ObterTipoIngressoPorIdAsync(99)).ReturnsAsync(ticketTypeDeOutroEvento);

        var usuarioRepoMock = new Mock<IUsuarioRepository>();
        usuarioRepoMock.Setup(r => r.ObterPorCpf(It.IsAny<string>())).ReturnsAsync(CriarUsuario());

        var executorMock = new Mock<ITransacaoCompraExecutor>();

        var dbFactory = new DbConnectionFactory("Server=fake;Database=UnitTests;Trusted_Connection=True;TrustServerCertificate=True;");
        var emailMock = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!, null!);
        var loggerMock = new Mock<ILogger<ReservationService>>();
        var auditLogRepoMock = new Mock<IAuditLogRepository>();
        var auditLoggerMock = new Mock<ILogger<AuditLogService>>();
        var auditLogMock = new Mock<AuditLogService>(auditLogRepoMock.Object, auditLoggerMock.Object) { CallBase = false };

        var svc = new ReservationService(
            new Mock<IReservaRepository>().Object,
            eventoRepoMock.Object,
            usuarioRepoMock.Object,
            new Mock<ICupomRepository>().Object,
            executorMock.Object,
            dbFactory,
            emailMock.Object,
            loggerMock.Object,
            new Mock<IWaitingQueueService>().Object,
            auditLogMock.Object,
            new Mock<IPaymentGateway>().Object,
            new Mock<IMeiaEntradaRepository>().Object,
            new Mock<IMeiaEntradaStorageService>().Object,
            new Mock<PixCryptoService>(null!).Object
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ComprarIngressoAsync("12345678901", 1, 99));

        Assert.Contains("não pertence ao evento", ex.Message);
        executorMock.Verify(e => e.ExecutarTransacaoAsync(
            It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
            It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ComprarIngresso_EmailNaoVerificado_DeveRejeitarComMensagemCorreta()
    {
        var evento = CriarEvento();
        var ticketType = CriarTicketType(eventoId: evento.Id);
        var usuarioSemEmail = CriarUsuario(emailVerificado: false); // Email não verificado

        var executorMock = new Mock<ITransacaoCompraExecutor>();
        var svc = CriarServico(
            executorMock: executorMock,
            evento: evento,
            ticketType: ticketType,
            usuario: usuarioSemEmail);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ComprarIngressoAsync(usuarioSemEmail.Cpf, evento.Id, ticketType.Id));

        Assert.Contains("verificar seu email", ex.Message, StringComparison.OrdinalIgnoreCase);
        executorMock.Verify(e => e.ExecutarTransacaoAsync(
            It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
            It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────
    // Executor propagando erros de oversell
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ComprarIngresso_ExecutorLancaSetorSemVagas_ServicoDevePropagarExcecao()
    {
        // Simula race condition: entre a verificação pré-transação e o UPDATE no executor
        // outro usuário comprou a última vaga do setor
        var executorMock = new Mock<ITransacaoCompraExecutor>();
        executorMock
            .Setup(e => e.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("Não há mais vagas disponíveis para este setor."));

        var svc = CriarServico(executorMock: executorMock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ComprarIngressoAsync("12345678901", 1, 1));

        Assert.Contains("vagas disponíveis", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComprarIngresso_ExecutorLancaEventoSemVagas_ServicoDevePropagarExcecao()
    {
        // Simula o cenário do High Fix #1: evento.CapacidadeRestante atingiu 0 na transação
        var executorMock = new Mock<ITransacaoCompraExecutor>();
        executorMock
            .Setup(e => e.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("O evento não possui mais vagas disponíveis."));

        var svc = CriarServico(executorMock: executorMock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ComprarIngressoAsync("12345678901", 1, 1));

        Assert.Equal("O evento não possui mais vagas disponíveis.", ex.Message);
    }

    [Fact]
    public async Task ComprarIngresso_ExecutorLancaLimitePorCpf_ServicoDevePropagarExcecao()
    {
        // Simula race condition: entre a validação pré-transação e o UPDLOCK no executor
        var executorMock = new Mock<ITransacaoCompraExecutor>();
        executorMock
            .Setup(e => e.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("Você já atingiu o limite de 4 reservas para este evento."));

        var svc = CriarServico(executorMock: executorMock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ComprarIngressoAsync("12345678901", 1, 1));

        Assert.Contains("limite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────
    // Verificações de mensagem exata (prevenção de regressão)
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Não há mais vagas disponíveis para este setor.")]
    [InlineData("O evento não possui mais vagas disponíveis.")]
    public async Task ComprarIngresso_MensagensDeOversellSaoExatas(string mensagemEsperada)
    {
        var executorMock = new Mock<ITransacaoCompraExecutor>();
        executorMock
            .Setup(e => e.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException(mensagemEsperada));

        var svc = CriarServico(executorMock: executorMock);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ComprarIngressoAsync("12345678901", 1, 1));

        Assert.Equal(mensagemEsperada, ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────
    // Executor chamado exatamente uma vez por compra bem-sucedida
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ComprarIngresso_FluxoCompleto_ExecutorEhChamadoExatamenteUmaVez()
    {
        var evento = CriarEvento(id: 1, capacidadeRestante: 5);
        var ticketType = CriarTicketType(id: 1, eventoId: 1, capacidadeRestante: 5);
        var usuario = CriarUsuario("12345678901");
        var reserva = CriarReserva("12345678901", 1);

        var executorMock = new Mock<ITransacaoCompraExecutor>();
        executorMock
            .Setup(e => e.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(reserva);

        var svc = CriarServico(executorMock: executorMock, evento: evento, ticketType: ticketType, usuario: usuario);

        await svc.ComprarIngressoAsync("12345678901", 1, 1);

        executorMock.Verify(
            e => e.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()),
            Times.Once,
            "O executor de transação deve ser chamado exatamente uma vez por compra.");
    }

    // ─────────────────────────────────────────────────────────────────
    // Executor recebe o evento com capacidade correta
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ComprarIngresso_EventoPassadoAoExecutor_TemCapacidadeAtualizada()
    {
        var evento = CriarEvento(id: 1, capacidadeRestante: 3);
        var ticketType = CriarTicketType(id: 1, eventoId: 1, capacidadeRestante: 3);
        var usuario = CriarUsuario("12345678901");
        var reserva = CriarReserva("12345678901", 1);

        TicketEvent? eventoPassadoAoExecutor = null;
        var executorMock = new Mock<ITransacaoCompraExecutor>();
        executorMock
            .Setup(e => e.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<Reservation, TicketEvent, string?, bool, int, int?>(
                (r, ev, cup, desc, ttId, lId) => eventoPassadoAoExecutor = ev)
            .ReturnsAsync(reserva);

        var svc = CriarServico(executorMock: executorMock, evento: evento, ticketType: ticketType, usuario: usuario);

        await svc.ComprarIngressoAsync("12345678901", 1, 1);

        Assert.NotNull(eventoPassadoAoExecutor);
        Assert.Equal(1, eventoPassadoAoExecutor!.Id);
        Assert.Equal(3, eventoPassadoAoExecutor.CapacidadeRestante);
    }

    // ─────────────────────────────────────────────────────────────────
    // Variações de capacidade
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 1, false)]   // 1 setor / 1 evento → deve chegar ao executor
    [InlineData(5, 10, false)]
    [InlineData(100, 100, false)]
    [InlineData(0, 10, true)]   // Setor sem vagas → rejeitado antes do executor
    public async Task ComprarIngresso_VariasCapacidades_ComportamentoEsperado(
        int setorCapacidade,
        int eventoCapacidade,
        bool esperaRejeicaoPreTransacao)
    {
        var evento = CriarEvento(id: 1, capacidadeRestante: eventoCapacidade);
        var ticketType = CriarTicketType(id: 1, eventoId: 1, capacidadeRestante: setorCapacidade);
        var usuario = CriarUsuario("12345678901");
        var reserva = CriarReserva("12345678901", 1);

        var chamadoExecutor = false;
        var executorMock = new Mock<ITransacaoCompraExecutor>();
        executorMock
            .Setup(e => e.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()))
            .Callback<Reservation, TicketEvent, string?, bool, int, int?>(
                (_, _, _, _, _, _) => chamadoExecutor = true)
            .ReturnsAsync(reserva);

        var svc = CriarServico(executorMock: executorMock, evento: evento, ticketType: ticketType, usuario: usuario);

        if (esperaRejeicaoPreTransacao)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ComprarIngressoAsync("12345678901", 1, 1));

            Assert.Contains("vagas", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(chamadoExecutor, "Executor não deve ser chamado quando setor sem vagas");
        }
        else
        {
            await svc.ComprarIngressoAsync("12345678901", 1, 1);
            Assert.True(chamadoExecutor, "Executor deveria ter sido chamado");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Múltiplos CPFs: isolamento de partições
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ComprarIngresso_DoisUsuariosDiferentes_SaoIndependentes()
    {
        // Usuário 1 compra com sucesso; Usuário 2 encontra evento lotado
        var evento = CriarEvento(id: 1, capacidadeRestante: 5);
        var reserva = CriarReserva("11111111111", 1);

        var executorMock = new Mock<ITransacaoCompraExecutor>();
        // Usuário 1 → sucesso
        executorMock
            .Setup(e => e.ExecutarTransacaoAsync(
                It.Is<Reservation>(r => r.UsuarioCpf == "11111111111"),
                It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(reserva);

        // Usuário 2 → evento lotado (race condition)
        executorMock
            .Setup(e => e.ExecutarTransacaoAsync(
                It.Is<Reservation>(r => r.UsuarioCpf == "22222222222"),
                It.IsAny<TicketEvent>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("O evento não possui mais vagas disponíveis."));

        // Usuário 1
        var usuario1 = CriarUsuario("11111111111");
        var svc1 = CriarServico(executorMock: executorMock, usuario: usuario1);
        var resultado = await svc1.ComprarIngressoAsync("11111111111", 1, 1);
        Assert.Equal(42, resultado.Id);

        // Usuário 2
        var usuario2 = CriarUsuario("22222222222");
        var svc2 = CriarServico(executorMock: executorMock, usuario: usuario2);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc2.ComprarIngressoAsync("22222222222", 1, 1));
        Assert.Equal("O evento não possui mais vagas disponíveis.", ex.Message);
    }
}
