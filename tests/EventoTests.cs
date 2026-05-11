using Xunit;
using src.Models;

namespace TicketPrime.Tests.Models
{
    public class EventoTests
    {
        [Fact]
        public void CriarEvento_DeveCriarEventoComSucesso()
        {
            var nome = "Tomorrowland";
            var capacidade = 100000;
            var data =  DateTime.Now.AddDays(15);
            var preco = 500.00m;

            var evento = new TicketEvent(nome, capacidade, data, preco);

            Assert.NotNull(evento);
            Assert.Equal(nome, evento.Nome);

        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        [InlineData(-100)]
        public void CriarEvento_ComCapacidadeInvalida_DeveLancarExcecao(int capacidadeInvalida)
        {
            var nome = "Rock in Rio";
            var data = DateTime.Now.AddDays(30);
            var preco = 300.00m;

            var exception = Assert.Throws<ArgumentException>(() => new TicketEvent(nome, capacidadeInvalida, data, preco));
            Assert.Equal("A capacidade total deve ser um valor positivo.", exception.Message);
        }
    }
}