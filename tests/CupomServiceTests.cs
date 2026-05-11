using Moq;
using src.Models;
using src.DTOs;
using src.Service;
using src.Infrastructure.IRepository;
using Xunit;

namespace TicketPrime.Tests.Service
{
    public class CupomServiceTests
    {
        private readonly Mock<ICupomRepository> _repositoryMock;
        private readonly CouponService _cupomService;

        public CupomServiceTests()
        {
            _repositoryMock = new Mock<ICupomRepository>();
            _cupomService = new CouponService(_repositoryMock.Object);
        }

        [Fact]
        public async Task CriarAsync_DeveLancarExcecao_QuandoCodigoForVazio()
        {
            
            var dto = new CreateCouponDto { Codigo = "", TipoDesconto = DiscountType.Percentual, PorcentagemDesconto = 10 };

            
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _cupomService.CriarAsync(dto));
            Assert.Equal("Código do cupom é obrigatório.", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(101)]
        public async Task CriarAsync_DeveLancarExcecao_QuandoDescontoInvalido(int descontoInvalido)
        {
            
            var dto = new CreateCouponDto { Codigo = "PROMO10", TipoDesconto = DiscountType.Percentual, PorcentagemDesconto = descontoInvalido };

            
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _cupomService.CriarAsync(dto));
            Assert.Equal("Desconto percentual deve ser entre 1 e 100.", ex.Message);
        }

        [Fact]
        public async Task CriarAsync_DeveLancarExcecao_QuandoCupomJaExistir()
        {
            
            var dto = new CreateCouponDto { Codigo = "BEMVINDO", TipoDesconto = DiscountType.Percentual, PorcentagemDesconto = 20 };
            var cupomExistente = new Coupon { Codigo = "BEMVINDO" };

            
            _repositoryMock.Setup(r => r.ObterPorCodigoAsync(dto.Codigo))
                           .ReturnsAsync(cupomExistente);

            
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _cupomService.CriarAsync(dto));
            Assert.Equal("Cupom já existe.", ex.Message);
        }

        [Fact]
        public async Task CriarAsync_DeveRetornarTrue_QuandoDadosForemValidos()
        {
            
            var dto = new CreateCouponDto { Codigo = "VALE50", TipoDesconto = DiscountType.Percentual, PorcentagemDesconto = 50, ValorMinimoRegra = 100 };

            
            _repositoryMock.Setup(r => r.ObterPorCodigoAsync(dto.Codigo))
                           .ReturnsAsync((Coupon?)null);

            
            _repositoryMock.Setup(r => r.CriarAsync(It.IsAny<Coupon>()))
                           .ReturnsAsync(1);

           
            var resultado = await _cupomService.CriarAsync(dto);

            
            Assert.True(resultado);
            
            _repositoryMock.Verify(r => r.CriarAsync(It.IsAny<Coupon>()), Times.Once);
        }
    }
}
