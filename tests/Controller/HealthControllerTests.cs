using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Moq;
using src.Controllers;
using Xunit;

namespace TicketPrime.Tests.Controller;

public class HealthControllerTests
{
    [Fact]
    public void Home_EmDesenvolvimento_DeveRedirecionarParaSwagger()
    {
        var controller = new HealthController();
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = httpContext
        };

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        envMock.Setup(e => e.ContentRootFileProvider).Returns(new NullFileProvider());
        envMock.Setup(e => e.WebRootFileProvider).Returns(new NullFileProvider());

        controller.Home(envMock.Object, httpContext);

        Assert.Equal("/swagger", httpContext.Response.Headers["Location"].ToString());
    }

    [Fact]
    public void Home_EmProducao_DeveRetornarJson()
    {
        var controller = new HealthController();
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = httpContext
        };

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        envMock.Setup(e => e.ContentRootFileProvider).Returns(new NullFileProvider());
        envMock.Setup(e => e.WebRootFileProvider).Returns(new NullFileProvider());

        var result = controller.Home(envMock.Object, httpContext);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<IResult>(result);
    }
}
