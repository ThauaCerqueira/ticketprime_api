using Moq;
using src.Models;
using src.DTOs;
using src.Service;
using src.Infrastructure.IRepository;
using Xunit;

namespace TicketPrime.Tests.Service
{
    public class ReservaServiceTests
    {
        private readonly Mock<IReservaRepository> _reservaRepoMock;
        private readonly Mock<IEventoRepository> _eventoRepoMock;
        private readonly Mock<IUsuarioRepository> _usuarioRepoMock;
        private readonly Mock<ICupomRepository> _cupomRepoMock;
        private readonly ReservaService _reservaService;

        private readonly Usuario _usuarioValido;
        private readonly Evento _eventoValido;
        private readonly Evento _eventoPassado;
        private readonly Evento _eventoLotado;
        private readonly Cupom _cupomValido;
        private readonly Cupom _cupomValorAlto;

        public ReservaServiceTests()
        {
            _reservaRepoMock = new Mock<IReservaRepository>();
            _eventoRepoMock = new Mock<IEventoRepository>();
            _usuarioRepoMock = new Mock<IUsuarioRepository>();
            _cupomRepoMock = new Mock<ICupomRepository>();

            _reservaService = new ReservaService(
                _reservaRepoMock.Object,
                _eventoRepoMock.Object,
                _usuarioRepoMock.Object,
                _cupomRepoMock.Object
            );

            _usuarioValido = new Usuario
            {
                Cpf = "12345678901",
                Nome = "João Silva",
                Email = "joao@email.com",
                Senha = "senha123",
                Perfil = "CLIENTE"
            };

            _eventoValido = new Evento(
                nome: "Show de Rock",
                capacidadeTotal: 100,
                dataEvento: DateTime.Now.AddDays(30),
                precoPadrao: 150.00m
            );

            _eventoPassado = new Evento
            {
                Id = 99,
                Nome = "Evento Antigo",
                CapacidadeTotal = 50,
                DataEvento = DateTime.Now.AddDays(-10),
                PrecoPadrao = 50.00m
            };

            _eventoLotado = new Evento(
                nome: "Evento Lotado",
                capacidadeTotal: 1,
                dataEvento: DateTime.Now.AddDays(15),
                precoPadrao: 200.00m
            );

            _cupomValido = new Cupom
            {
                Codigo = "PRIME10",
                PorcentagemDesconto = 10,
                ValorMinimoRegra = 50.00m
            };

            _cupomValorAlto = new Cupom
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
                            .ReturnsAsync((Usuario?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync("00000000000", _eventoValido.Id));

            Assert.Equal("Usuário não encontrado.", ex.Message);
        }

        [Fact]
        public async Task ComprarIngresso_R1_DeveLancarExcecao_QuandoEventoNaoEncontrado()
        {
            // Arrange
            _usuarioRepoMock.Setup(r => r.ObterPorCpf(_usuarioValido.Cpf))
                            .ReturnsAsync(_usuarioValido);

            _eventoRepoMock.Setup(r => r.ObterPorIdAsync(99999))
                           .ReturnsAsync((Evento?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, 99999));

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
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, _eventoPassado.Id));

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
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, _eventoValido.Id));

            Assert.Equal("Você já atingiu o limite de 2 reservas para este evento.", ex.Message);
        }

        // ============================
        // REGRA R3 — CONTROLE DE ESTOQUE
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
                            .ReturnsAsync(0); // Não atingiu limite de CPF

            _reservaRepoMock.Setup(r => r.ContarReservasPorEventoAsync(_eventoLotado.Id))
                            .ReturnsAsync(1); // CapacidadeTotal = 1, já tem 1 reserva => lotado

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, _eventoLotado.Id));

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

            _reservaRepoMock.Setup(r => r.ContarReservasPorEventoAsync(_eventoValido.Id))
                            .ReturnsAsync(0);

            _cupomRepoMock.Setup(r => r.ObterPorCodigoAsync("PRIME10"))
                          .ReturnsAsync(_cupomValido);

            var reservaEsperada = new Reserva
            {
                Id = 1,
                UsuarioCpf = _usuarioValido.Cpf,
                EventoId = _eventoValido.Id,
                CupomUtilizado = "PRIME10",
                ValorFinalPago = 135.00m // 150 - 10% = 135
            };

            _reservaRepoMock.Setup(r => r.CriarAsync(It.IsAny<Reserva>()))
                            .ReturnsAsync(reservaEsperada);

            // Act
            var resultado = await _reservaService.ComprarIngressoAsync(
                _usuarioValido.Cpf, _eventoValido.Id, "PRIME10");

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(135.00m, resultado.ValorFinalPago);
            Assert.Equal("PRIME10", resultado.CupomUtilizado);

            _reservaRepoMock.Verify(r => r.CriarAsync(It.Is<Reserva>(
                res => res.ValorFinalPago == 135.00m && res.CupomUtilizado == "PRIME10"
            )), Times.Once);
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

            _reservaRepoMock.Setup(r => r.ContarReservasPorEventoAsync(_eventoValido.Id))
                            .ReturnsAsync(0);

            // Cupom com ValorMinimoRegra = 500, mas evento custa 150
            _cupomRepoMock.Setup(r => r.ObterPorCodigoAsync("SUPER50"))
                          .ReturnsAsync(_cupomValorAlto);

            var reservaEsperada = new Reserva
            {
                Id = 2,
                UsuarioCpf = _usuarioValido.Cpf,
                EventoId = _eventoValido.Id,
                CupomUtilizado = "SUPER50",
                ValorFinalPago = 150.00m // Preço cheio pois 150 < 500 (ValorMinimoRegra)
            };

            _reservaRepoMock.Setup(r => r.CriarAsync(It.IsAny<Reserva>()))
                            .ReturnsAsync(reservaEsperada);

            // Act
            var resultado = await _reservaService.ComprarIngressoAsync(
                _usuarioValido.Cpf, _eventoValido.Id, "SUPER50");

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(150.00m, resultado.ValorFinalPago); // Preço cheio sem desconto
            Assert.Equal("SUPER50", resultado.CupomUtilizado);
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

            _reservaRepoMock.Setup(r => r.ContarReservasPorEventoAsync(_eventoValido.Id))
                            .ReturnsAsync(0);

            _cupomRepoMock.Setup(r => r.ObterPorCodigoAsync("INVALIDO"))
                          .ReturnsAsync((Cupom?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reservaService.ComprarIngressoAsync(_usuarioValido.Cpf, _eventoValido.Id, "INVALIDO"));

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

            _reservaRepoMock.Setup(r => r.ContarReservasPorEventoAsync(_eventoValido.Id))
                            .ReturnsAsync(0);

            var reservaEsperada = new Reserva
            {
                Id = 3,
                UsuarioCpf = _usuarioValido.Cpf,
                EventoId = _eventoValido.Id,
                ValorFinalPago = 150.00m
            };

            _reservaRepoMock.Setup(r => r.CriarAsync(It.IsAny<Reserva>()))
                            .ReturnsAsync(reservaEsperada);

            // Act
            var resultado = await _reservaService.ComprarIngressoAsync(
                _usuarioValido.Cpf, _eventoValido.Id);

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(150.00m, resultado.ValorFinalPago);
            Assert.Null(resultado.CupomUtilizado);

            _reservaRepoMock.Verify(r => r.CriarAsync(It.IsAny<Reserva>()), Times.Once);
        }

        // ============================
        // GET /api/reservas/{cpf} — LISTAR RESERVAS DO USUÁRIO
        // ============================

        [Fact]
        public async Task ListarReservas_DeveRetornarLista_QuandoUsuarioPossuiReservas()
        {
            // Arrange
            var reservasFake = new List<ReservaDetalhadaDTO>
            {
                new ReservaDetalhadaDTO
                {
                    Id = 1,
                    Nome = "Show de Rock",
                    DataEvento = DateTime.Now.AddDays(30),
                    PrecoPadrao = 150.00m,
                    ValorFinalPago = 135.00m,
                    CupomUtilizado = "PRIME10",
                    DataCompra = DateTime.Now
                },
                new ReservaDetalhadaDTO
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
                            .ReturnsAsync(new List<ReservaDetalhadaDTO>());

            // Act
            var resultado = await _reservaService.ListarReservasUsuarioAsync("00000000000");

            // Assert
            Assert.NotNull(resultado);
            Assert.Empty(resultado);
        }
    }
}
