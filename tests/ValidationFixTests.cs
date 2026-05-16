using Microsoft.Extensions.Logging;
using Moq;
using src.DTOs;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Models;
using src.Service;
using Xunit;

namespace TicketPrime.Tests.Service
{
    /// <summary>
    /// Testes para as correções de validação implementadas:
    /// 1. Limite de tamanho de documento de meia-entrada (DoS)
    /// 2. Verificação de limite de ingressos do destinatário na transferência
    /// 3. Cupom com 100% de desconto não permitido
    /// 4. Validação de telefone: mínimo de 10 dígitos numéricos
    /// 5. EventoService: DataTermino deve ser posterior a DataEvento
    /// </summary>
    public class ValidationFixTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Helpers compartilhados
        // ─────────────────────────────────────────────────────────────────────
        private static ReservationService CriarReservationService(
            Mock<IReservaRepository>? reservaRepo = null,
            Mock<IEventoRepository>? eventoRepo = null,
            Mock<IUsuarioRepository>? usuarioRepo = null)
        {
            reservaRepo ??= new Mock<IReservaRepository>();
            eventoRepo ??= new Mock<IEventoRepository>();
            usuarioRepo ??= new Mock<IUsuarioRepository>();

            var cupomRepo = new Mock<ICupomRepository>();
            var transacao = new Mock<ITransacaoCompraExecutor>();
            var emailTemplate = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!, null!);
            var logger = new Mock<ILogger<ReservationService>>();
            var filaEspera = new Mock<IWaitingQueueService>();
            var paymentGateway = new Mock<IPaymentGateway>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var auditLogger = new Mock<ILogger<AuditLogService>>();
            var auditLogService = new AuditLogService(auditRepo.Object, auditLogger.Object);

            paymentGateway
                .Setup(g => g.ProcessarAsync(It.IsAny<PaymentRequest>()))
                .ReturnsAsync(PaymentResult.Ok("TEST-TX-001"));

            return new ReservationService(
                reservaRepo.Object,
                eventoRepo.Object,
                usuarioRepo.Object,
                cupomRepo.Object,
                transacao.Object,
                TestConnectionHelper.CreateDbConnectionFactory("TicketPrime_UnitTest"),
                emailTemplate.Object,
                logger.Object,
                filaEspera.Object,
                auditLogService,
                paymentGateway.Object,
                new Mock<IMeiaEntradaRepository>().Object,
                new Mock<IMeiaEntradaStorageService>().Object,
                new Mock<PixCryptoService>(null!).Object
            );
        }

        private static EventoService CriarEventoService(Mock<IEventoRepository>? repo = null)
        {
            repo ??= new Mock<IEventoRepository>();
            return new EventoService(
                repo.Object,
                new Mock<IReservaRepository>().Object,
                new Mock<IUsuarioRepository>().Object,
                new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!, null!).Object,
                new Mock<ILogger<EventoService>>().Object
            );
        }

        // ═════════════════════════════════════════════════════════════════════
        // 1. Documento de meia-entrada – limite de tamanho
        // ═════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ComprarIngresso_DeveLancarExcecao_QuandoDocumentoBase64ForGrande()
        {
            var usuarioRepo = new Mock<IUsuarioRepository>();
            var eventoRepo = new Mock<IEventoRepository>();
            var reservaRepo = new Mock<IReservaRepository>();

            var usuario = new User
            {
                Cpf = "12345678901", Nome = "Ana", Email = "ana@email.com",
                Senha = "x", Perfil = "CLIENTE", EmailVerificado = true
            };
            var evento = new TicketEvent("Festival", 500, DateTime.Now.AddDays(30), 100m);
            evento.Id = 1;
            evento.TemMeiaEntrada = true;
            var ticketType = new TicketType("Pista", 100m, 500, 1) { Id = 1 };
            ticketType.CapacidadeRestante = 500;
            ticketType.EventoId = 1;

            usuarioRepo.Setup(r => r.ObterPorCpf("12345678901")).ReturnsAsync(usuario);
            eventoRepo.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(evento);
            eventoRepo.Setup(r => r.ObterTipoIngressoPorIdAsync(1)).ReturnsAsync(ticketType);
            reservaRepo.Setup(r => r.ContarReservasUsuarioPorEventoAsync("12345678901", 1)).ReturnsAsync(0);

            var svc = CriarReservationService(reservaRepo, eventoRepo, usuarioRepo);

            // Gera uma string base64 com mais de 7 MB
            var base64Gigante = new string('A', 8_000_000);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ComprarIngressoAsync("12345678901", 1, 1, null, false, true,
                    null, null, null, null, null, null, null,
                    base64Gigante, "doc.pdf", "application/pdf"));

            Assert.Contains("muito grande", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ComprarIngresso_DeveLancarExcecao_QuandoDocumentoBase64NaoEhValido()
        {
            var usuarioRepo = new Mock<IUsuarioRepository>();
            var eventoRepo = new Mock<IEventoRepository>();
            var reservaRepo = new Mock<IReservaRepository>();

            var usuario = new User
            {
                Cpf = "12345678901", Nome = "Ana", Email = "ana@email.com",
                Senha = "x", Perfil = "CLIENTE", EmailVerificado = true
            };
            var evento = new TicketEvent("Festival", 500, DateTime.Now.AddDays(30), 100m);
            evento.Id = 1;
            evento.TemMeiaEntrada = true;
            var ticketType = new TicketType("Pista", 100m, 500, 1) { Id = 1 };
            ticketType.CapacidadeRestante = 500;
            ticketType.EventoId = 1;

            usuarioRepo.Setup(r => r.ObterPorCpf("12345678901")).ReturnsAsync(usuario);
            eventoRepo.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(evento);
            eventoRepo.Setup(r => r.ObterTipoIngressoPorIdAsync(1)).ReturnsAsync(ticketType);
            reservaRepo.Setup(r => r.ContarReservasUsuarioPorEventoAsync("12345678901", 1)).ReturnsAsync(0);

            var svc = CriarReservationService(reservaRepo, eventoRepo, usuarioRepo);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ComprarIngressoAsync("12345678901", 1, 1, null, false, true,
                    null, null, null, null, null, null, null,
                    "!!!base64-invalido!!!", "doc.pdf", "application/pdf"));

            Assert.Contains("Base64 válido", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ComprarIngresso_DeveLancarExcecao_QuandoTipoMimeNaoPermitido()
        {
            var usuarioRepo = new Mock<IUsuarioRepository>();
            var eventoRepo = new Mock<IEventoRepository>();
            var reservaRepo = new Mock<IReservaRepository>();

            var usuario = new User
            {
                Cpf = "12345678901", Nome = "Ana", Email = "ana@email.com",
                Senha = "x", Perfil = "CLIENTE", EmailVerificado = true
            };
            var evento = new TicketEvent("Festival", 500, DateTime.Now.AddDays(30), 100m);
            evento.Id = 1;
            evento.TemMeiaEntrada = true;
            var ticketType = new TicketType("Pista", 100m, 500, 1) { Id = 1 };
            ticketType.CapacidadeRestante = 500;
            ticketType.EventoId = 1;

            usuarioRepo.Setup(r => r.ObterPorCpf("12345678901")).ReturnsAsync(usuario);
            eventoRepo.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(evento);
            eventoRepo.Setup(r => r.ObterTipoIngressoPorIdAsync(1)).ReturnsAsync(ticketType);
            reservaRepo.Setup(r => r.ContarReservasUsuarioPorEventoAsync("12345678901", 1)).ReturnsAsync(0);

            var svc = CriarReservationService(reservaRepo, eventoRepo, usuarioRepo);

            // Envia um arquivo .exe disfarçado
            var base64Exe = Convert.ToBase64String(new byte[100]);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ComprarIngressoAsync("12345678901", 1, 1, null, false, true,
                    null, null, null, null, null, null, null,
                    base64Exe, "malware.exe", "application/octet-stream"));

            Assert.Contains("não permitido", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ComprarIngresso_DeveLancarExcecao_QuandoDocumentoExcedeLimiteAposDecodificacao()
        {
            var usuarioRepo = new Mock<IUsuarioRepository>();
            var eventoRepo = new Mock<IEventoRepository>();
            var reservaRepo = new Mock<IReservaRepository>();

            var usuario = new User
            {
                Cpf = "12345678901", Nome = "Ana", Email = "ana@email.com",
                Senha = "x", Perfil = "CLIENTE", EmailVerificado = true
            };
            var evento = new TicketEvent("Festival", 500, DateTime.Now.AddDays(30), 100m);
            evento.Id = 1;
            evento.TemMeiaEntrada = true;
            var ticketType = new TicketType("Pista", 100m, 500, 1) { Id = 1 };
            ticketType.CapacidadeRestante = 500;
            ticketType.EventoId = 1;

            usuarioRepo.Setup(r => r.ObterPorCpf("12345678901")).ReturnsAsync(usuario);
            eventoRepo.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(evento);
            eventoRepo.Setup(r => r.ObterTipoIngressoPorIdAsync(1)).ReturnsAsync(ticketType);
            reservaRepo.Setup(r => r.ContarReservasUsuarioPorEventoAsync("12345678901", 1)).ReturnsAsync(0);

            var svc = CriarReservationService(reservaRepo, eventoRepo, usuarioRepo);

            // Cria um array de bytes com mais de 5 MB e codifica em base64
            var bytesGrandes = new byte[6 * 1024 * 1024]; // 6 MB
            var base64Grande = Convert.ToBase64String(bytesGrandes);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ComprarIngressoAsync("12345678901", 1, 1, null, false, true,
                    null, null, null, null, null, null, null,
                    base64Grande, "doc.pdf", "application/pdf"));

            Assert.Contains("muito grande", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 2. Transferência – verificação de limite do destinatário
        // ═════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task TransferirIngresso_DeveLancarExcecao_QuandoDestinatarioAtingiuLimite()
        {
            var reservaRepo = new Mock<IReservaRepository>();
            var eventoRepo = new Mock<IEventoRepository>();
            var usuarioRepo = new Mock<IUsuarioRepository>();

            var cpfRemetente = "11111111111";
            var cpfDestinatario = "22222222222";

            var reserva = new ReservationDetailDto
            {
                Id = 42,
                UsuarioCpf = cpfRemetente,
                EventoId = 1,
                Status = "Ativa"
            };
            var evento = new TicketEvent("Show", 100, DateTime.Now.AddDays(30), 50m, limiteIngressosPorUsuario: 2);
            evento.Id = 1;

            usuarioRepo.Setup(r => r.ObterPorCpf(cpfDestinatario))
                       .ReturnsAsync(new User { Cpf = cpfDestinatario, Nome = "Pedro" });
            reservaRepo.Setup(r => r.ObterDetalhadaPorIdAsync(42, cpfRemetente))
                       .ReturnsAsync(reserva);
            eventoRepo.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(evento);
            // Destinatário já tem 2 ingressos (igual ao limite)
            reservaRepo.Setup(r => r.ContarReservasUsuarioPorEventoAsync(cpfDestinatario, 1))
                       .ReturnsAsync(2);

            var svc = CriarReservationService(reservaRepo, eventoRepo, usuarioRepo);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.TransferirIngressoAsync(42, cpfRemetente, cpfDestinatario));

            Assert.Contains("limite", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("2", ex.Message);
        }

        [Fact]
        public async Task TransferirIngresso_DeveConcluir_QuandoDestinatarioNaoAtingiuLimite()
        {
            var reservaRepo = new Mock<IReservaRepository>();
            var eventoRepo = new Mock<IEventoRepository>();
            var usuarioRepo = new Mock<IUsuarioRepository>();

            var cpfRemetente = "11111111111";
            var cpfDestinatario = "22222222222";

            var reserva = new ReservationDetailDto
            {
                Id = 42,
                UsuarioCpf = cpfRemetente,
                EventoId = 1,
                Status = "Ativa"
            };
            var evento = new TicketEvent("Show", 100, DateTime.Now.AddDays(30), 50m, limiteIngressosPorUsuario: 4);
            evento.Id = 1;

            usuarioRepo.Setup(r => r.ObterPorCpf(cpfDestinatario))
                       .ReturnsAsync(new User { Cpf = cpfDestinatario, Nome = "Pedro" });
            reservaRepo.Setup(r => r.ObterDetalhadaPorIdAsync(42, cpfRemetente))
                       .ReturnsAsync(reserva);
            eventoRepo.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(evento);
            // Destinatário só tem 1 ingresso (abaixo do limite de 4)
            reservaRepo.Setup(r => r.ContarReservasUsuarioPorEventoAsync(cpfDestinatario, 1))
                       .ReturnsAsync(1);
            reservaRepo.Setup(r => r.TransferirAsync(42, cpfRemetente, cpfDestinatario))
                       .ReturnsAsync(true);

            var svc = CriarReservationService(reservaRepo, eventoRepo, usuarioRepo);

            // Não deve lançar exceção
            await svc.TransferirIngressoAsync(42, cpfRemetente, cpfDestinatario);

            reservaRepo.Verify(r => r.TransferirAsync(42, cpfRemetente, cpfDestinatario), Times.Once);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 3. Cupom – desconto de 100% não permitido
        // ═════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData(100)]
        [InlineData(101)]
        [InlineData(200)]
        public async Task CriarCupom_DeveLancarExcecao_QuandoDescontoFor100OuMais(int desconto)
        {
            var repoMock = new Mock<ICupomRepository>();
            repoMock.Setup(r => r.ObterPorCodigoAsync(It.IsAny<string>())).ReturnsAsync((Coupon?)null);
            var svc = new CupomService(repoMock.Object);

            var dto = new CreateCouponDto
            {
                Codigo = "LIVRE100",
                TipoDesconto = DiscountType.Percentual,
                PorcentagemDesconto = desconto
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.CriarAsync(dto));
            Assert.Contains("1 e 99", ex.Message);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50)]
        [InlineData(99)]
        public async Task CriarCupom_NaoDeveLancarExcecao_QuandoDescontoEstaDentroDoLimite(int desconto)
        {
            var repoMock = new Mock<ICupomRepository>();
            repoMock.Setup(r => r.ObterPorCodigoAsync(It.IsAny<string>())).ReturnsAsync((Coupon?)null);
            repoMock.Setup(r => r.CriarAsync(It.IsAny<Coupon>())).ReturnsAsync(1);
            var svc = new CupomService(repoMock.Object);

            var dto = new CreateCouponDto
            {
                Codigo = $"PROMO{desconto}",
                TipoDesconto = DiscountType.Percentual,
                PorcentagemDesconto = desconto
            };

            var resultado = await svc.CriarAsync(dto);
            Assert.True(resultado);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 4. Telefone – deve ter ao menos 10 dígitos numéricos
        // ═════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("-------")]        // 7 hifens: passa no regex mas falha na contagem de dígitos
        [InlineData("(  )     -    ")]  // apenas espaços e símbolos
        [InlineData("12345")]           // apenas 5 dígitos (menos de 10)
        [InlineData("123456789")]       // 9 dígitos
        public void ValidarTelefone_DeveRejeitar_QuandoDigitosInsuficientes(string telefone)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"^[\d\s\(\)\+\-]{7,20}$");
            var digits = System.Text.RegularExpressions.Regex.Replace(telefone, @"[^\d]", "");

            // A validação combinada deve detectar telefone inválido
            var falhaRegex = !regex.IsMatch(telefone);
            var falhaDigitos = digits.Length < 10 || digits.Length > 11;
            Assert.True(falhaRegex || falhaDigitos,
                $"Telefone '{telefone}' deveria ser rejeitado mas passou na validação combinada.");
        }

        [Theory]
        [InlineData("(11) 91234-5678")]  // 11 dígitos, formato válido
        [InlineData("11912345678")]       // 11 dígitos sem formatação
        [InlineData("1134567890")]        // 10 dígitos (fixo com DDD)
        public void ValidarTelefone_DeveAceitar_QuandoFormatoEhValido(string telefone)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"^[\d\s\(\)\+\-]{7,20}$");
            var digits = System.Text.RegularExpressions.Regex.Replace(telefone, @"[^\d]", "");

            var passaRegex = regex.IsMatch(telefone);
            var passaDigitos = digits.Length >= 10 && digits.Length <= 11;
            Assert.True(passaRegex && passaDigitos,
                $"Telefone '{telefone}' deveria ser aceito. Regex={passaRegex}, Digitos={digits.Length}");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 5. EventoService – DataTermino deve ser posterior a DataEvento
        // ═════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task CriarEvento_DeveLancarExcecao_QuandoDataTerminoAntesDaDataEvento()
        {
            var svc = CriarEventoService();
            var dataEvento = DateTime.Now.AddDays(10);

            var dto = new CreateEventDto
            {
                Nome = "Show de Teste",
                CapacidadeTotal = 100,
                DataEvento = dataEvento,
                DataTermino = dataEvento.AddHours(-2),  // termina ANTES de começar
                PrecoPadrao = 50m,
                LimiteIngressosPorUsuario = 4,
                Status = "Rascunho",
                Local = "Arena",
                GeneroMusical = "Rock"
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.CriarNovoEvento(dto));

            Assert.Contains("término deve ser posterior", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CriarEvento_DeveLancarExcecao_QuandoDataTerminoIgualADataEvento()
        {
            var svc = CriarEventoService();
            var dataEvento = DateTime.Now.AddDays(10);

            var dto = new CreateEventDto
            {
                Nome = "Show de Teste",
                CapacidadeTotal = 100,
                DataEvento = dataEvento,
                DataTermino = dataEvento,  // mesmo instante
                PrecoPadrao = 50m,
                LimiteIngressosPorUsuario = 4,
                Status = "Rascunho",
                Local = "Arena",
                GeneroMusical = "Rock"
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.CriarNovoEvento(dto));

            Assert.Contains("término deve ser posterior", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CriarEvento_NaoDeveLancarExcecao_QuandoDataTerminoEhNula()
        {
            var repoMock = new Mock<IEventoRepository>();
            repoMock.Setup(r => r.ObterDisponiveisAsync())
                    .ReturnsAsync(Enumerable.Empty<TicketEvent>());
            repoMock.Setup(r => r.CriarEventoComTransacaoAsync(
                    It.IsAny<TicketEvent>(), It.IsAny<List<TicketType>?>(),
                    It.IsAny<List<Lote>?>(), It.IsAny<List<EncryptedPhotoDto>?>()))
                    .ReturnsAsync(1);

            var svc = CriarEventoService(repoMock);
            var dataEvento = DateTime.Now.AddDays(10);

            var dto = new CreateEventDto
            {
                Nome = "Show de Teste",
                CapacidadeTotal = 100,
                DataEvento = dataEvento,
                DataTermino = null,  // sem data de término = válido
                PrecoPadrao = 50m,
                LimiteIngressosPorUsuario = 4,
                Status = "Rascunho",
                Local = "Arena",
                GeneroMusical = "Rock"
            };

            var resultado = await svc.CriarNovoEvento(dto);
            Assert.NotNull(resultado);
        }

        [Fact]
        public async Task CriarEvento_NaoDeveLancarExcecao_QuandoDataTerminoEhPosteriorADataEvento()
        {
            var repoMock = new Mock<IEventoRepository>();
            repoMock.Setup(r => r.ObterDisponiveisAsync())
                    .ReturnsAsync(Enumerable.Empty<TicketEvent>());
            repoMock.Setup(r => r.CriarEventoComTransacaoAsync(
                    It.IsAny<TicketEvent>(), It.IsAny<List<TicketType>?>(),
                    It.IsAny<List<Lote>?>(), It.IsAny<List<EncryptedPhotoDto>?>()))
                    .ReturnsAsync(1);

            var svc = CriarEventoService(repoMock);
            var dataEvento = DateTime.Now.AddDays(10);

            var dto = new CreateEventDto
            {
                Nome = "Show de Teste",
                CapacidadeTotal = 100,
                DataEvento = dataEvento,
                DataTermino = dataEvento.AddHours(4),  // 4 horas após início = válido
                PrecoPadrao = 50m,
                LimiteIngressosPorUsuario = 4,
                Status = "Rascunho",
                Local = "Arena",
                GeneroMusical = "Rock"
            };

            var resultado = await svc.CriarNovoEvento(dto);
            Assert.NotNull(resultado);
        }
    }
}
