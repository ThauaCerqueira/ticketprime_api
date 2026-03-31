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
        private readonly EventoService _eventoService;

        public EventoServiceTests()
        {
            _repositoryMock = new Mock<IEventoRepository>();
            _eventoService = new EventoService(_repositoryMock.Object);
        }

        [Fact]
        public async Task ListarEventos_DeveRetornarListaDeEventos_QuandoExistiremDados()
        {
            var listaFake = new List<Evento>
            {
                new Evento("Show de Rock", 100, DateTime.Now.AddDays(10), 50.00m),
                new Evento("Teatro", 50, DateTime.Now.AddDays(5), 30.00m)
            };

            _repositoryMock.Setup(repo => repo.ObterTodosAsync())
                           .ReturnsAsync(listaFake);

            var resultado = await _eventoService.ListarEventos();

            Assert.NotNull(resultado);
            Assert.Equal(2, resultado.Count());
            Assert.Contains(resultado, e => e.Nome == "Show de Rock"); 
        }

        [Fact]
        public async Task ListarEventos_DeveRetornarVazio_QuandoNaoHouverEventos()
        {
            _repositoryMock.Setup(repo => repo.ObterTodosAsync())
                           .ReturnsAsync(new List<Evento>());

            var resultado = await _eventoService.ListarEventos();

            Assert.Empty(resultado);
        }
    }
}