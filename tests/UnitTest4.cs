using Moq;
using src.Models;
using src.DTOs; // Ajuste conforme seu namespace
using src.Service;
using src.Infrastructure.IRepository;
using Xunit;

namespace TicketPrime.Tests.Service
{
    public class CupomServiceTests
    {
        private readonly Mock<ICupomRepository> _repositoryMock;
        private readonly CupomService _cupomService;

        public CupomServiceTests()
        {
            _repositoryMock = new Mock<ICupomRepository>();
            _cupomService = new CupomService(_repositoryMock.Object);
        }

        [Fact]
        public async Task CriarAsync_DeveLancarExcecao_QuandoCodigoForVazio()
        {
            
            var dto = new CriarCupomDTO { Codigo = "", PorcentagemDesconto = 10 };

            
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _cupomService.CriarAsync(dto));
            Assert.Equal("Código do cupom é obrigatório.", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(101)]
        public async Task CriarAsync_DeveLancarExcecao_QuandoDescontoInvalido(int descontoInvalido)
        {
            
            var dto = new CriarCupomDTO { Codigo = "PROMO10", PorcentagemDesconto = descontoInvalido };

            
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _cupomService.CriarAsync(dto));
            Assert.Equal("Desconto deve ser entre 0 e 100.", ex.Message);
        }

        [Fact]
        public async Task CriarAsync_DeveLancarExcecao_QuandoCupomJaExistir()
        {
            
            var dto = new CriarCupomDTO { Codigo = "BEMVINDO", PorcentagemDesconto = 20 };
            var cupomExistente = new Cupom { Codigo = "BEMVINDO" };

            
            _repositoryMock.Setup(r => r.ObterPorCodigoAsync(dto.Codigo))
                           .ReturnsAsync(cupomExistente);

            
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _cupomService.CriarAsync(dto));
            Assert.Equal("Cupom já existe.", ex.Message);
        }

        [Fact]
        public async Task CriarAsync_DeveRetornarTrue_QuandoDadosForemValidos()
        {
            
            var dto = new CriarCupomDTO { Codigo = "VALE50", PorcentagemDesconto = 50, ValorMinimoRegra = 100 };

            
            _repositoryMock.Setup(r => r.ObterPorCodigoAsync(dto.Codigo))
                           .ReturnsAsync((Cupom?)null);

            
            _repositoryMock.Setup(r => r.CriarAsync(It.IsAny<Cupom>()))
                           .ReturnsAsync(1);

           
            var resultado = await _cupomService.CriarAsync(dto);

            
            Assert.True(resultado);
            
            _repositoryMock.Verify(r => r.CriarAsync(It.IsAny<Cupom>()), Times.Once);
        }
    }
}