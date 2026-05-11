using Microsoft.Extensions.Logging;
using Moq;
using src.Infrastructure;
using src.Models;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Service;
using Xunit;
namespace TicketPrime.Tests.Service
{
    public class ReservaServiceTests
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
        private readonly TicketEvent _eventoValido;
        private readonly TicketEvent _eventoPassado;
        private readonly TicketEvent _eventoLotado;
        private readonly Coupon _cupomValido;
        private readonly Coupon _cupomValorAlto;

        public ReservaServiceTests()
        {
            _reservaRepoMock = new Mock<IReservaRepository>();
            _eventoRepoMock = new Mock<IEventoRepository>();
            _usuarioRepoMock = new Mock<IUsuarioRepository>();
            _cupomRepoMock = new Mock<ICupomRepository>();
            _transacaoMock = new Mock<ITransacaoCompraExecutor>();
            _emailTemplateMock = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!);
            _loggerMock = new Mock<ILogger<ReservationService>>();
            _filaEsperaServiceMock = new Mock<IWaitingQueueService>();
            _paymentGatewayMock = new Mock<IPaymentGateway>();
            // Default: payment always succeeds in unit tests
            _paymentGatewayMock
                .Setup(g => g.ProcessarAsync(It.IsAny<PaymentRequest>()))
                .ReturnsAsync(PaymentResult.Ok("TEST-TX-001"));

            var auditLogRepoMock = new Mock<IAuditLogRepository>();
            var auditLoggerMock = new Mock<ILogger<AuditLogService>>();
            var auditLogService = new AuditLogService(auditLogRepoMock.Object, auditLoggerMock.Object);
            _reservaService = new ReservationService(
                _reservaRepoMock.Object,
                _eventoRepoMock.Object,
                _usuarioRepoMock.Object,
                _cupomRepoMock.Object,
                _transacaoMock.Object,
                new DbConnectionFactory(
                    Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
                    ?? "Server=.;Database=TicketPrime_UnitTest;Trusted_Connection=true;TrustServerCertificate=True;"
                ),
                _emailTemplateMock.Object,
                _loggerMock.Object,
                _filaEsperaServiceMock.Object,
                auditLogService,
                _paymentGatewayMock.Object,
                new Mock<IMeiaEntradaRepository>().Object,
                new Mock<IMeiaEntradaStorageService>().Object
            );

            _usuarioValido = new User
            {
                Cpf = "12345678901",
                Nome = "João Silva",
                Email = "joao@email.com",
                Senha = "senha123",
                Perfil = "CLIENTE",
                EmailVerificado = true
            };

            _eventoValido = new TicketEvent(
                nome: "Show de Rock",
                capacidadeTotal: 100,
                dataEvento: DateTime.Now.AddDays(30),
                precoPadrao: 150.00m,
                limiteIngressosPorUsuario: 2
            );

            _eventoPassado = new TicketEvent
            {
                Id = 99,
                Nome = "Evento Antigo",
                CapacidadeTotal = 50,
                DataEvento = DateTime.Now.AddDays(-10),
                PrecoPadrao = 50.00m
            };

            _eventoLotado = new TicketEvent(
                nome: "Evento Lotado",
                capacidadeTotal: 1,
                dataEvento: DateTime.Now.AddDays(15),
                precoPadrao: 200.00m
            );

            // Mock padrão do TicketType para testes que precisam passar pela validação R1a
            _eventoRepoMock.Setup(r => r.ObterTipoIngressoPorIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new src.Models.TicketType
                {
                    Id = 1,
                    EventoId = _eventoValido.Id,
                    Nome = "Pista",
                    Preco = _eventoValido.PrecoPadrao,
                    CapacidadeTotal = 100,
                    CapacidadeRestante = 100
                });

            _cupomValido = new Coupon
            {
                Codigo = "PRIME10",
                PorcentagemDesconto = 10,
                ValorMinimoRegra = 50.00m
            };

            _cupomValorAlto = new Coupon
            {
                Codigo = "SUPER50",
                PorcentagemDesconto = 50,
                ValorMinimoRegra = 500.00m // Valor mínimo maior que o preço do evento
            };
        }

        // ============================
        // REGRA R1 — VALIDAÇÃO DE INTEGRIDADE
        // ============================

        [Fact]
        public async Task ComprarIngresso_R1_DeveLancarExcecao_QuandoUsuarioNaoEncontrado()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf("00000000000"))
                            .ReturnsAsync((User?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync("00000000000", _eventoValido.Id, 1));

            Assert.Equal("Usuário não encontrado.", ex.Message);
        }

        [Fact]
        public async Task ComprarIngresso_R1_DeveLancarExcecao_QuandoEventoNaoEncontrado()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                            .ReturnsAsync(_usuarioValido);

            _eventoRepoMock.Setup(r => r.ObterPorIdAsync(99999))
                           .ReturnsAsync((TicketEvent?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, 99999, 1));

            Assert.Equal("Evento não encontrado.", ex.Message);
        }

        [Fact]
        public async Task ComprarIngresso_R1_DeveLancarExcecao_QuandoEventoJaAconteceu()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                            .ReturnsAsync(_usuarioValido);

            _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoPassado.Id))
                           .ReturnsAsync(_eventoPassado);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, _eventoPassado.Id, 1));

            Assert.Equal("Este evento já aconteceu.", ex.Message);
        }

        // ============================
        // REGRA R2 — LIMITE DE 2 RESERVAS POR CPF
        // ============================

        [Fact]
        public async Task ComprarIngresso_R2_DeveLancarExcecao_QuandoLimiteDe2ReservasAtingido()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                            .ReturnsAsync(_usuarioValido);

            _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoValido.Id))
                           .ReturnsAsync(_eventoValido);

            _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(_usuarioValido.Cpf, _eventoValido.Id))
                            .ReturnsAsync(2); // Já atingiu o limite

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, _eventoValido.Id, 1));

            Assert.Equal("Você já atingiu o limite de 2 reservas para este evento.", ex.Message);
        }

        // ============================
        // REGRA R3 — CONTROLE DE ESTOQUE (atômico)
        // ============================

        [Fact]
        public async Task ComprarIngresso_R3_DeveLancarExcecao_QuandoCapacidadeEsgotada()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                            .ReturnsAsync(_usuarioValido);

            _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoLotado.Id))
                           .ReturnsAsync(_eventoLotado);

            _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(_usuarioValido.Cpf, _eventoLotado.Id))
                            .ReturnsAsync(0);

            // Mock do executor transacional para simular R3 - capacidade esgotada
            _transacaoMock
                .Setup(t => t.ExecutarTransacaoAsync(
                    It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(), It.IsAny<bool>(),
                    It.IsAny<int>(), It.IsAny<int?>()))
                .ThrowsAsync(new InvalidOperationException("Não há mais vagas disponíveis para este evento."));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, _eventoLotado.Id, 1));

            Assert.Equal("Não há mais vagas disponíveis para este evento.", ex.Message);
        }

        // ============================
        // REGRA R4 — MOTOR DE CUPONS
        // ============================

        [Fact]
        public async Task ComprarIngresso_R4_DeveAplicarDesconto_QuandoCupomValidoEValorMinimoAtingido()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                            .ReturnsAsync(_usuarioValido);

            _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoValido.Id))
                           .ReturnsAsync(_eventoValido);

            _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(_usuarioValido.Cpf, _eventoValido.Id))
                            .ReturnsAsync(0);

            _cupomRepoMock.Setup(r => r.ObterPorCodigoAsync("PRIME10"))
                          .ReturnsAsync(_cupomValido);

            // Capturar a reserva que será passada para o executor para validar o cálculo
            _transacaoMock
                .Setup(t => t.ExecutarTransacaoAsync(
                    It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(), It.IsAny<bool>(),
                    It.IsAny<int>(), It.IsAny<int?>()))
                .ReturnsAsync((Reservation r, TicketEvent e, string? c, bool a, int t, int? l) =>
                {
                    r.Id = 1;
                    return r;
                });

            // Act
            var resultado = await _reservaService.ComprarIngressoAsync(
                _usuarioValido.Cpf, _eventoValido.Id, 1, "PRIME10");

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(135.00m, resultado.ValorFinalPago); // 150 - 10% = 135
            Assert.Equal("PRIME10", resultado.CupomUtilizado);

            // Verificar que o executor foi chamado com a reserva calculada corretamente
            _transacaoMock.Verify(t => t.ExecutarTransacaoAsync(
                It.Is<Reservation>(r => r.ValorFinalPago == 135.00m && r.CupomUtilizado == "PRIME10"),
                It.IsAny<TicketEvent>(),
                "PRIME10",
                true,
                It.IsAny<int>(),
                It.IsAny<int?>()), Times.Once);
        }

        [Fact]
        public async Task ComprarIngresso_R4_DeveManterPrecoCheio_QuandoValorMinimoNaoAtingido()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                            .ReturnsAsync(_usuarioValido);

            _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoValido.Id))
                           .ReturnsAsync(_eventoValido);

            _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(_usuarioValido.Cpf, _eventoValido.Id))
                            .ReturnsAsync(0);

            // Cupom com ValorMinimoRegra = 500, mas evento custa 150
            _cupomRepoMock.Setup(r => r.ObterPorCodigoAsync("SUPER50"))
                          .ReturnsAsync(_cupomValorAlto);

            _transacaoMock
                .Setup(t => t.ExecutarTransacaoAsync(
                    It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(), It.IsAny<bool>(),
                    It.IsAny<int>(), It.IsAny<int?>()))
                .ReturnsAsync((Reservation r, TicketEvent e, string? c, bool a, int t, int? l) =>
                {
                    r.Id = 2;
                    return r;
                });

            // Act
            var resultado = await _reservaService.ComprarIngressoAsync(
                _usuarioValido.Cpf, _eventoValido.Id, 1, "SUPER50");

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(150.00m, resultado.ValorFinalPago); // Preço cheio sem desconto
            Assert.Equal("SUPER50", resultado.CupomUtilizado);

            // Verificar que o executor foi chamado com aplicarDesconto=false
            _transacaoMock.Verify(t => t.ExecutarTransacaoAsync(
                It.Is<Reservation>(r => r.ValorFinalPago == 150.00m),
                It.IsAny<TicketEvent>(),
                "SUPER50",
                false,
                It.IsAny<int>(),
                It.IsAny<int?>()), Times.Once);
        }

        [Fact]
        public async Task ComprarIngresso_R4_DeveLancarExcecao_QuandoCupomInvalido()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                            .ReturnsAsync(_usuarioValido);

            _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoValido.Id))
                           .ReturnsAsync(_eventoValido);

            _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(_usuarioValido.Cpf, _eventoValido.Id))
                            .ReturnsAsync(0);

            _cupomRepoMock.Setup(r => r.ObterPorCodigoAsync("INVALIDO"))
                          .ReturnsAsync((Coupon?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, _eventoValido.Id, 1, "INVALIDO"));

            Assert.Equal("Cupom inválido ou inexistente.", ex.Message);
        }

        // ============================
        // FLUXO FELIZ — COMPRA SEM CUPOM
        // ============================

        [Fact]
        public async Task ComprarIngresso_DeveFinalizarCompraComSucesso_QuandoDadosValidosSemCupom()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                            .ReturnsAsync(_usuarioValido);

            _eventoRepoMock.Setup(r => r.ObterPorIdAsync(_eventoValido.Id))
                           .ReturnsAsync(_eventoValido);

            _reservaRepoMock.Setup(r => r.ContarReservasUsuarioPorEventoAsync(_usuarioValido.Cpf, _eventoValido.Id))
                            .ReturnsAsync(0);

            var reservaEsperada = new Reservation
            {
                Id = 3,
                UsuarioCpf = _usuarioValido.Cpf,
                EventoId = _eventoValido.Id,
                ValorFinalPago = 150.00m
            };

            _transacaoMock
                .Setup(t => t.ExecutarTransacaoAsync(
                    It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), It.IsAny<string?>(), It.IsAny<bool>(),
                    It.IsAny<int>(), It.IsAny<int?>()))
                .ReturnsAsync(reservaEsperada);

            // Act
            var resultado = await _reservaService.ComprarIngressoAsync(
                _usuarioValido.Cpf, _eventoValido.Id, 1);

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(150.00m, resultado.ValorFinalPago);
            Assert.Null(resultado.CupomUtilizado);

            _transacaoMock.Verify(t => t.ExecutarTransacaoAsync(
                It.IsAny<Reservation>(), It.IsAny<TicketEvent>(), null, false,
                It.IsAny<int>(), It.IsAny<int?>()), Times.Once);
        }

        // ============================
        // GET /api/reservas/{cpf} — LISTAR RESERVAS DO USUÁRIO
        // ============================

        [Fact]
        public async Task ListarReservas_DeveRetornarLista_QuandoUsuarioPossuiReservas()
        {
            // Arrange
            var reservasFake = new List<ReservationDetailDto>
            {
                new ReservationDetailDto
                {
                    Id = 1,
                    Nome = "Show de Rock",
                    DataEvento = DateTime.Now.AddDays(30),
                    PrecoPadrao = 150.00m,
                    ValorFinalPago = 135.00m,
                    CupomUtilizado = "PRIME10",
                    DataCompra = DateTime.Now
                },
                new ReservationDetailDto
                {
                    Id = 2,
                    Nome = "Teatro",
                    DataEvento = DateTime.Now.AddDays(15),
                    PrecoPadrao = 80.00m,
                    ValorFinalPago = 80.00m,
                    CupomUtilizado = null,
                    DataCompra = DateTime.Now
                }
            };

            _reservaRepoMock.Setup(r => r.ListarPorUsuarioAsync("12345678901"))
                            .ReturnsAsync(reservasFake);

            // Act
            var resultado = await _reservaService.ListarReservasUsuarioAsync("12345678901");

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(2, resultado.Count());

            var primeira = resultado.First();
            Assert.Equal(135.00m, primeira.ValorFinalPago);
            Assert.Equal("PRIME10", primeira.CupomUtilizado);
            Assert.Equal(150.00m, primeira.PrecoPadrao);

            var segunda = resultado.Last();
            Assert.Equal(80.00m, segunda.ValorFinalPago);
            Assert.Null(segunda.CupomUtilizado);
        }

        [Fact]
        public async Task ListarReservas_DeveRetornarListaVazia_QuandoCpfNaoPossuiReservas()
        {
            // Arrange
            _reservaRepoMock.Setup(r => r.ListarPorUsuarioAsync("00000000000"))
                            .ReturnsAsync(new List<ReservationDetailDto>());

            // Act
            var resultado = await _reservaService.ListarReservasUsuarioAsync("00000000000");

            // Assert
            Assert.NotNull(resultado);
            Assert.Empty(resultado);
        }
    }
}
