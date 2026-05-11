using Microsoft.Extensions.Logging;
using Moq;
using src.Models;
using src.Service;
using src.Infrastructure.IRepository;
using Xunit;

namespace TicketPrime.Tests.Service
{
    public class EventoServiceTests
    {
        private readonly Mock<IEventoRepository> _repositoryMock;
        private readonly Mock<IReservaRepository> _reservaRepoMock;
        private readonly Mock<IUsuarioRepository> _usuarioRepoMock;
        private readonly Mock<EmailTemplateService> _emailTemplateMock;
        private readonly Mock<ILogger<EventService>> _loggerMock;
        private readonly EventService _eventoService;

        public EventoServiceTests()
        {
            _repositoryMock = new Mock<IEventoRepository>();
            _reservaRepoMock = new Mock<IReservaRepository>();
            _usuarioRepoMock = new Mock<IUsuarioRepository>();
            _emailTemplateMock = new Mock<EmailTemplateService>(MockBehavior.Loose, null!, null!);
            _loggerMock = new Mock<ILogger<EventService>>();

            _eventoService = new EventService(
                _repositoryMock.Object,
                _reservaRepoMock.Object,
                _usuarioRepoMock.Object,
                _emailTemplateMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task ListarEventos_DeveRetornarListaDeEventos_QuandoExistiremDados()
        {
            var listaFake = new List<TicketEvent>
            {
                new TicketEvent("Show de Rock", 100, DateTime.Now.AddDays(10), 50.00m),
                new TicketEvent("Teatro", 50, DateTime.Now.AddDays(5), 30.00m)
            };

            var paginatedResult = new src.DTOs.PaginatedResult<TicketEvent>
            {
                Itens = listaFake,
                Total = 2,
                Pagina = 1,
                TamanhoPagina = 20
            };

            _repositoryMock
                .Setup(r => r.ObterTodosAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(paginatedResult);

            var resultado = await _eventoService.ListarEventos();

            Assert.NotNull(resultado);
            Assert.Equal(2, resultado.Total);
            Assert.Equal("Show de Rock", resultado.Itens.First().Nome);
            _repositoryMock.Verify(r => r.ObterTodosAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }
    }
}
