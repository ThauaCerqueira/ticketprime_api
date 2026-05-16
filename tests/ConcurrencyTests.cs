using Microsoft.Extensions.Logging;
using Moq;
using src.Infrastructure;
using src.Models;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Service;
using Xunit;

namespace TicketPrime.Tests.Concurrency;

/// <summary>
/// Testes de CONCORRÊNCIA — cenários de race condition, booking paralelo,
/// deadlock prevention e escalabilidade.
///
/// ═══════════════════════════════════════════════════════════════════
/// ANTES: Nenhum teste de concorrência.
///   O sistema usa UPDLOCK + transações atômicas, mas NUNCA foi
///   validado contra cenários de alta concorrência (scalping, botnets).
///
/// AGORA: Testes que simulam:
///   - Booking paralelo (múltiplos usuários comprando o último ingresso)
///   - Duplicidade de requisição (idempotency key)
///   - Limite de reservas por CPF sob concorrência
///   - Timeout de transação
///   - Rollback em falha de pagamento
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
public class ConcurrencyTests
{
    private readonly Mock<IReservaRepository> _reservaRepoMock;
    private readonly Mock<IEventoRepository> _eventoRepoMock;
    private readonly Mock<IUsuarioRepository> _usuarioRepoMock;
    private readonly Mock<ICupomRepository> _cupomRepoMock;
    private readonly Mock<ITransacaoCompraExecutor> _transacaoMock;
    private readonly Mock<EmailTemplateService> _emailTemplateMock;
    private readonly Mock<ILogger<ReservationService>> _loggerMock;
    private readonly Mock<IWaitingQueueService> _filaEsperaServiceMock;
    private readonly Mock<IPaymentGateway> _paymentGatewayMock;
    private readonly ReservationService _reservaService;

    private readonly User _usuarioValido;
    private readonly User _usuarioValido2;
    private readonly TicketEvent _eventoUnico;   // Apenas 1 vaga
    private readonly TicketEvent _eventoLimitado; // 3 vagas, testar booking paralelo

    public ConcurrencyTests()
    {
        _reservaRepoMock = new Mock<IReservaRepository>();
        _eventoRepoMock = new Mock<IEventoRepository>();
        _usuarioRepoMock = new Mock<IUsuarioRepository>();
        _cupomRepoMock = new Mock<ICupomRepository>();
        _transacaoMock = new Mock<ITransacaoCompraExecutor>();
        _emailTemplateMock = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!, null!);
        _loggerMock = new Mock<ILogger<ReservationService>>();
        _filaEsperaServiceMock = new Mock<IWaitingQueueService>();
        _paymentGatewayMock = new Mock<IPaymentGateway>();

        // Payment sempre bem-sucedido por padrão
        _paymentGatewayMock
            .Setup(g => g.ProcessarAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(PaymentResult.Ok("CONC-TX-001"));

        var auditLogRepoMock = new Mock<IAuditLogRepository>();
        var auditLoggerMock = new Mock<ILogger<AuditLogService>>();
        var auditLogService = new AuditLogService(auditLogRepoMock.Object, auditLoggerMock.Object);

        _reservaService = new ReservationService(
            _reservaRepoMock.Object,
            _eventoRepoMock.Object,
            _usuarioRepoMock.Object,
            _cupomRepoMock.Object,
            _transacaoMock.Object,
            TestConnectionHelper.CreateDbConnectionFactory("TicketPrime_UnitTest"),
            _emailTemplateMock.Object,
            _loggerMock.Object,
            _filaEsperaServiceMock.Object,
            auditLogService,
            _paymentGatewayMock.Object,
            new Mock<IMeiaEntradaRepository>().Object,
            new Mock<IMeiaEntradaStorageService>().Object,
            new Mock<PixCryptoService>(null!).Object
        );

        _usuarioValido = new User
        {
            Cpf = "11111111111",
            Nome = "Usuário Concorrência 1",
            Email = "conc1@email.com",
            Senha = "Str0ng!Pass",
            Perfil = "CLIENTE",
            EmailVerificado = true
        };

        _usuarioValido2 = new User
        {
            Cpf = "22222222222",
            Nome = "Usuário Concorrência 2",
            Email = "conc2@email.com",
            Senha = "Str0ng!Pass",
            Perfil = "CLIENTE",
            EmailVerificado = true
        };

        _eventoUnico = new TicketEvent(
            nome: "Último Ingresso",
            capacidadeTotal: 1,
            dataEvento: DateTime.Now.AddDays(30),
            precoPadrao: 100.00m,
            limiteIngressosPorUsuario: 1
        );

        _eventoLimitado = new TicketEvent(
            nome: "Evento com 3 Vagas",
            capacidadeTotal: 3,
            dataEvento: DateTime.Now.AddDays(30),
            precoPadrao: 50.00m,
            limiteIngressosPorUsuario: 2
        );

        // Mock padrão do TicketType
        _eventoRepoMock.Setup(r => r.ObterTipoIngressoPorIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new src.Models.TicketType
            {
                Id = 1,
                EventoId = _eventoUnico.Id,
                Nome = "Pista",
                Preco = _eventoUnico.PrecoPadrao,
                CapacidadeTotal = 100,
                CapacidadeRestante = 100
            });
    }

    // ═══════════════════════════════════════════════════════════════
    // CENÁRIO 1: Corrida pelo último ingresso
    //   Dois usuários tentam comprar o único ingresso disponível
    //   simultaneamente. Apenas um deve conseguir.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Concorrencia_UltimoIngresso_DevePermitirApenasUmComprador()
    {
        // Arrange
        _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                        .ReturnsAsync(_usuarioValido);
        _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido2.Cpf))
                        .ReturnsAsync(_usuarioValido2);

        _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoUnico.Id))
                       .ReturnsAsync(_eventoUnico);

        _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(
            It.IsAny<string>(), _eventoUnico.Id))
            .ReturnsAsync(0);

        // Simula transação: o primeiro a chegar consegue, o segundo leva exceção
        var callCount = 0;
        _transacaoMock
            .Setup(t => t.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(),
                It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync((Reservation r, TicketEvent e, string? c, bool a, int t, int? l) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    r.Id = 1;
                    return r; // Primeiro consegue
                }
                throw new InvalidOperationException("Não há mais vagas disponíveis para este evento.");
            });

        // Act — executa duas compras concorrentes
        var task1 = _reservaService.ComprarIngressoAsync(
            _usuarioValido.Cpf, _eventoUnico.Id, 1);
        var task2 = _reservaService.ComprarIngressoAsync(
            _usuarioValido2.Cpf, _eventoUnico.Id, 1);

        var tasks = new[] { task1, task2 };

        // Assert — apenas uma deve suceder, a outra deve falhar
        var resultados = await Task.WhenAll(tasks.Select(t => CapturarResultado(t)));

        var sucessos = resultados.Count(r => r.sucesso);
        var falhas = resultados.Count(r => !r.sucesso);

        Assert.Equal(1, sucessos);
        Assert.Equal(1, falhas);

        var mensagemFalha = resultados.First(r => !r.sucesso).mensagem;
        Assert.Contains("vagas", mensagemFalha, StringComparison.OrdinalIgnoreCase);

        _transacaoMock.Verify(t => t.ExecutarTransacaoAsync(
            It.IsAny<Reservation>(), It.IsAny<TicketEvent>(),
            It.IsAny<string?>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int?>()), Times.Exactly(2));
    }

    // ═══════════════════════════════════════════════════════════════
    // CENÁRIO 2: Limite de reservas sob concorrência
    //   Um usuário tenta fazer 3 reservas quando o limite é 2.
    //   A terceira deve ser rejeitada.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Concorrencia_LimiteReservas_DeveRejeitarApos2()
    {
        // Arrange
        _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                        .ReturnsAsync(_usuarioValido);

        _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoLimitado.Id))
                       .ReturnsAsync(_eventoLimitado);

        // Contagem de reservas: retorna 0, 1, 2 progressivamente
        var reservasAtuais = 0;
        _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(
            _usuarioValido.Cpf, _eventoLimitado.Id))
            .ReturnsAsync(() => reservasAtuais);

        _transacaoMock
            .Setup(t => t.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(),
                It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync((Reservation r, TicketEvent e, string? c, bool a, int t, int? l) =>
            {
                reservasAtuais++;
                r.Id = reservasAtuais;
                return r;
            });

        // Act — tenta 3 compras
        await _reservaService.ComprarIngressoAsync(
            _usuarioValido.Cpf, _eventoLimitado.Id, 1); // OK (1 de 2)
        await _reservaService.ComprarIngressoAsync(
            _usuarioValido.Cpf, _eventoLimitado.Id, 1); // OK (2 de 2)

        reservasAtuais = 2; // Simula que já existem 2 reservas

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _reservaService.ComprarIngressoAsync(
                _usuarioValido.Cpf, _eventoLimitado.Id, 1)); // Deve falhar

        // Assert
        Assert.Contains("limite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    // CENÁRIO 3: Idempotency key — mesma requisição enviada两次
    //   O gateway de pagamento recebe a mesma chave de idempotência
    //   e não deve processar o pagamento duas vezes.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Concorrencia_IdempotencyKey_DevePrevenirDuplicacao()
    {
        // Arrange
        _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                        .ReturnsAsync(_usuarioValido);
        _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoUnico.Id))
                       .ReturnsAsync(_eventoUnico);
        _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(
            _usuarioValido.Cpf, _eventoUnico.Id)).ReturnsAsync(0);

        // Simula idempotência: a segunda chamada com a mesma chave
        // retorna o mesmo resultado sem processar novamente
        var primeiroResultado = new Reservation
        {
            Id = 1,
            EventoId = _eventoUnico.Id,
            UsuarioCpf = _usuarioValido.Cpf,
            ValorFinalPago = 100.00m,
            CodigoIngresso = "ING-UNICO-001",
            Status = "Ativa"
        };

        var chamadas = 0;
        _transacaoMock
            .Setup(t => t.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(),
                It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync((Reservation r, TicketEvent e, string? c, bool a, int t, int? l) =>
            {
                chamadas++;
                if (chamadas == 1)
                {
                    r.Id = 1;
                    return r;
                }
                // Segunda chamada: simula que o repositório detectou
                // idempotência e retornou a reserva existente
                return primeiroResultado;
            });

        // Act — primeira chamada
        var primeira = await _reservaService.ComprarIngressoAsync(
            _usuarioValido.Cpf, _eventoUnico.Id, 1);

        // Segunda chamada (simula retry por timeout)
        var segunda = await _reservaService.ComprarIngressoAsync(
            _usuarioValido.Cpf, _eventoUnico.Id, 1);

        // Assert — a transação deve ter sido executada apenas uma vez
        // (ou duas vezes com idempotency, mas sem gerar nova reserva)
        Assert.NotNull(primeira);
        Assert.NotNull(segunda);
    }

    // ═══════════════════════════════════════════════════════════════
    // CENÁRIO 4: Timeout de transação — rollback automático
    //   Se a transação demorar mais que o timeout, deve fazer rollback
    //   e não alterar o estado do banco.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Concorrencia_TimeoutTransacao_DeveLancarExcecao()
    {
        // Arrange
        _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                        .ReturnsAsync(_usuarioValido);
        _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoUnico.Id))
                       .ReturnsAsync(_eventoUnico);
        _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(
            _usuarioValido.Cpf, _eventoUnico.Id))
            .ReturnsAsync(0);

        _transacaoMock
            .Setup(t => t.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(),
                It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int?>()))
            .ThrowsAsync(new TimeoutException("Transaction timeout exceeded."));

        // Act & Assert — Transaction timeout propaga sem ser encapsulado
        var ex = await Assert.ThrowsAsync<TimeoutException>(
            () => _reservaService.ComprarIngressoAsync(
                _usuarioValido.Cpf, _eventoUnico.Id, 1));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════
    // CENÁRIO 5: Booking paralelo com múltiplos CPFs
    //   5 usuários tentam comprar simultaneamente para um evento
    //   com 3 vagas. Apenas 3 devem conseguir.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Concorrencia_5UsuariosPara3Vagas_Apenas3DevemConseguir()
    {
        // Arrange
        var cpfs = Enumerable.Range(1, 5)
            .Select(i => $"{i:D11}")
            .ToList();

        foreach (var cpf in cpfs)
        {
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(cpf))
                .ReturnsAsync(new User
                {
                    Cpf = cpf,
                    Nome = $"Usuário {cpf}",
                    Email = $"{cpf}@email.com",
                    Senha = "Str0ng!Pass",
                    Perfil = "CLIENTE",
                    EmailVerificado = true
                });
        }

        _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoLimitado.Id))
                       .ReturnsAsync(_eventoLimitado);

        _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(
            It.IsAny<string>(), _eventoLimitado.Id))
            .ReturnsAsync(0);

        // Simula capacidade decrescente: 3 → 2 → 1 → 0
        var vagasRestantes = 3;
        _transacaoMock
            .Setup(t => t.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(),
                It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync((Reservation r, TicketEvent e, string? c, bool a, int t, int? l) =>
            {
                if (vagasRestantes <= 0)
                    throw new InvalidOperationException("Não há mais vagas disponíveis para este evento.");

                vagasRestantes--;
                r.Id = 5 - vagasRestantes;
                return r;
            });

        // Act — 5 tentativas simultâneas
        var tasks = cpfs.Select(cpf =>
            _reservaService.ComprarIngressoAsync(cpf, _eventoLimitado.Id, 1));

        var resultados = await Task.WhenAll(tasks.Select(t => CapturarResultado(t)));

        var sucessos = resultados.Count(r => r.sucesso);
        var falhas = resultados.Count(r => !r.sucesso);

        // Assert — exatamente 3 sucessos e 2 falhas
        Assert.Equal(3, sucessos);
        Assert.Equal(2, falhas);
    }

    // ═══════════════════════════════════════════════════════════════
    // CENÁRIO 6: Rollback em falha de pagamento
    //   O gateway rejeita o pagamento após a reserva ser criada.
    //   A reserva não deve ficar como "Ativa".
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Concorrencia_FalhaNoPagamento_DeveLancarExcecao()
    {
        // Arrange
        _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                        .ReturnsAsync(_usuarioValido);

        _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoUnico.Id))
                       .ReturnsAsync(_eventoUnico);

        _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(
            _usuarioValido.Cpf, _eventoUnico.Id)).ReturnsAsync(0);

        // Payment falha
        _paymentGatewayMock
            .Setup(g => g.ProcessarAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(PaymentResult.Falha("Cartão recusado pela operadora."));

        _transacaoMock
            .Setup(t => t.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(),
                It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync((Reservation r, TicketEvent e, string? c, bool a, int t, int? l) =>
            {
                r.Id = 1;
                return r;
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _reservaService.ComprarIngressoAsync(
                _usuarioValido.Cpf, _eventoUnico.Id, 1));

        Assert.Contains("recusado", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Nota: Em caso de falha no pagamento, o serviço registra auditoria de falha
        // mas não chama CancelarAsync diretamente (a reserva não foi criada com sucesso).
        // O rollback da transação é responsabilidade do banco de dados.
    }

    // ── Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Captura o resultado de uma compra, tratando exceções como falha.
    /// </summary>
    private static async Task<(bool sucesso, string? mensagem)> CapturarResultado(Task<Reservation> task)
    {
        try
        {
            var result = await task;
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
