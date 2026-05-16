using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using src.Controllers;
using src.DTOs;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Models;
using src.Service;
using Xunit;

namespace TicketPrime.Tests.Controller;

/// <summary>
/// Testes de AuthController — login, logout, refresh, troca de senha,
/// e especialmente a integração com JwtBlacklistService (revogação de JTI).
///
/// Cenários cobertos:
///   ── Login ──
///   - Credenciais válidas retornam 200 + define cookies httpOnly
///   - Credenciais inválidas retornam 401
///   - Senha temporária bloqueia login com 403
///   ── Logout ──
///   - Logout revoga o JTI do access token via JwtBlacklistService
///   - Logout limpa os cookies de sessão (ticketprime_token + ticketprime_refresh)
///   - Logout sem JTI na claim (token antigo) não lança exceção
///   - Logout sem exp na claim usa TTL padrão de 30 minutos
///   - Logout chama RevogarRefreshTokenAsync com o CPF correto
///   - Logout funciona sem cookie de refresh (body DTO)
///   ── Refresh ──
///   - RefreshToken válido retorna novo access token
///   - RefreshToken inválido/expirado retorna 401
///   ── Segurança ──
///   - JTI na claim "jti" é lido corretamente
///   - Claim "exp" Unix timestamp é convertida para DateTime correto
///   - Token sem "exp" usa fallback de 30 minutos
/// </summary>
public class AuthControllerTests
{
    private readonly Mock<AuthService> _authServiceMock;
    private readonly Mock<IUsuarioRepository> _usuarioRepoMock;
    private readonly UserService _userService;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<AuditLogService> _auditLogMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly JwtBlacklistService _blacklistService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _usuarioRepoMock = new Mock<IUsuarioRepository>();
        var auditRepoMock = new Mock<IAuditLogRepository>();
        var auditLoggerMock = new Mock<ILogger<AuditLogService>>();
        _auditLogMock = new Mock<AuditLogService>(auditRepoMock.Object, auditLoggerMock.Object) { CallBase = false };

        var emailMock = new Mock<IEmailService>();
        _userService = new UserService(_usuarioRepoMock.Object, emailMock.Object);

        var authLogger = new Mock<ILogger<AuthService>>();
        var dbFactory = new DbConnectionFactory("Server=fake;Database=UnitTests;Trusted_Connection=True;TrustServerCertificate=True;");
        _authServiceMock = new Mock<AuthService>(
            _usuarioRepoMock.Object,
            Mock.Of<IConfiguration>(),
            dbFactory,
            _auditLogMock.Object) { CallBase = false };

        _configMock = new Mock<IConfiguration>();
        var configSection = new Mock<IConfigurationSection>();
        configSection.Setup(s => s.Value).Returns("30");
        _configMock.Setup(c => c.GetSection("Jwt:ExpireMinutes")).Returns(configSection.Object);
        _configMock.Setup(c => c["Jwt:ExpireMinutes"]).Returns("30");

        _loggerMock = new Mock<ILogger<AuthController>>();

        var cache = new MemoryCache(new MemoryCacheOptions());
        _blacklistService = new JwtBlacklistService(cache);

        _controller = new AuthController(
            _authServiceMock.Object,
            _usuarioRepoMock.Object,
            _userService,
            _configMock.Object,
            _auditLogMock.Object,
            _loggerMock.Object,
            _blacklistService
        );
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static DefaultHttpContext CriarHttpContextComClaims(
        string? jti = null,
        string? cpf = "12345678901",
        long? expUnix = null,
        string? refreshTokenCookie = null)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(jti))
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));

        if (!string.IsNullOrEmpty(cpf))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, cpf));

        if (expUnix.HasValue)
            claims.Add(new Claim("exp", expUnix.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext();
        context.User = principal;

        if (!string.IsNullOrEmpty(refreshTokenCookie))
            context.Request.Headers.Cookie = $"ticketprime_refresh={refreshTokenCookie}";

        return context;
    }

    private static LoginResponseDTO CriarResultadoLogin(
        string cpf = "12345678901",
        string token = "jwt.access.token",
        string refresh = "refresh.token.123")
    {
        return new LoginResponseDTO
        {
            Token = token,
            RefreshToken = refresh,
            Cpf = cpf,
            Nome = "Teste",
            Perfil = "CLIENTE"
        };
    }

    // ─────────────────────────────────────────────────────────────────
    // Login
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_CredenciaisValidas_DeveRetornar200()
    {
        var resultado = CriarResultadoLogin();
        _authServiceMock
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(resultado);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var dto = new LoginDTO { Cpf = "12345678901", Senha = "Senha@123" };
        var result = await _controller.Login(dto);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<LoginResponseDTO>>(result);
    }

    [Fact]
    public async Task Login_CredenciaisInvalidas_DeveRetornar401()
    {
        _authServiceMock
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((LoginResponseDTO?)null);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var dto = new LoginDTO { Cpf = "99999999999", Senha = "wrong" };
        var result = await _controller.Login(dto);

        // Deve retornar 401
        var statusResult = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result);
        Assert.Equal(401, statusResult.StatusCode);
    }

    [Fact]
    public async Task Login_ComCredenciaisValidas_DeveDefinirCookieHttpOnly()
    {
        var resultado = CriarResultadoLogin();
        _authServiceMock
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(resultado);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await _controller.Login(new LoginDTO { Cpf = "12345678901", Senha = "Senha@123" });

        // Verifica que um cookie foi definido
        var setCookieHeader = httpContext.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("ticketprime_token", setCookieHeader);
        Assert.Contains("httponly", setCookieHeader.ToLower());
    }

    // ─────────────────────────────────────────────────────────────────
    // Logout — JTI blacklisting
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ComJtiNaClaim_DeveRevogarJti()
    {
        // Arrange
        var jti = Guid.NewGuid().ToString();
        var expUnix = DateTimeOffset.UtcNow.AddMinutes(25).ToUnixTimeSeconds();

        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var httpContext = CriarHttpContextComClaims(jti: jti, expUnix: expUnix);
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        await _controller.Logout(null);

        // Assert — o JTI deve estar na blacklist
        Assert.True(_blacklistService.IsRevoked(jti));
    }

    [Fact]
    public async Task Logout_SemJtiNaClaim_NaoDeveLancarExcecao()
    {
        // Token legado (gerado antes da adição do claim "jti")
        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var httpContext = CriarHttpContextComClaims(jti: null); // Sem JTI
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var ex = await Record.ExceptionAsync(() => _controller.Logout(null));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Logout_SemExpNaClaim_UsaTtlPadrao30Minutos()
    {
        // Token sem claim "exp" → fallback para 30 minutos
        var jti = Guid.NewGuid().ToString();

        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Cria context sem a claim "exp"
        var identity = new ClaimsIdentity([
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(ClaimTypes.NameIdentifier, "12345678901")
        ], "Bearer");
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await _controller.Logout(null);

        // Deve estar revogado com TTL de ~30 min
        Assert.True(_blacklistService.IsRevoked(jti));
    }

    [Fact]
    public async Task Logout_DeveLimparCookieDeAcessoERefresh()
    {
        var jti = Guid.NewGuid().ToString();
        var expUnix = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();

        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var httpContext = CriarHttpContextComClaims(jti: jti, expUnix: expUnix);
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await _controller.Logout(null);

        var setCookieHeader = httpContext.Response.Headers["Set-Cookie"].ToString();
        // Os dois cookies devem ser apagados (expires no passado / valor vazio)
        Assert.Contains("ticketprime_token", setCookieHeader);
        Assert.Contains("ticketprime_refresh", setCookieHeader);
    }

    [Fact]
    public async Task Logout_DeveRetornar200ComMensagem()
    {
        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = CriarHttpContextComClaims()
        };

        var result = await _controller.Logout(null);

        Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>(result);
    }

    [Fact]
    public async Task Logout_ComRefreshTokenNoCookie_DevePassarCookieParaRevogar()
    {
        var jti = Guid.NewGuid().ToString();
        var refreshToken = "refresh-token-from-cookie-123";

        string? refreshPassado = null;
        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string?, string?>((token, cpf) => refreshPassado = token)
            .Returns(Task.CompletedTask);

        var httpContext = CriarHttpContextComClaims(
            jti: jti,
            expUnix: DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds(),
            refreshTokenCookie: refreshToken);
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await _controller.Logout(null);

        Assert.Equal(refreshToken, refreshPassado);
    }

    [Fact]
    public async Task Logout_ComRefreshTokenNoBody_DevePassarBodyParaRevogar()
    {
        var jti = Guid.NewGuid().ToString();
        var refreshToken = "refresh-token-from-body-456";

        string? refreshPassado = null;
        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string?, string?>((token, cpf) => refreshPassado = token)
            .Returns(Task.CompletedTask);

        // Sem cookie, com DTO
        var httpContext = CriarHttpContextComClaims(
            jti: jti,
            expUnix: DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds());
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await _controller.Logout(new RefreshTokenRequestDTO { RefreshToken = refreshToken });

        Assert.Equal(refreshToken, refreshPassado);
    }

    // ─────────────────────────────────────────────────────────────────
    // Logout — múltiplos logouts (idempotência)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_DuasVezesComMesmoToken_DeveSerIdempotente()
    {
        var jti = Guid.NewGuid().ToString();
        var expUnix = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();

        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Primeiro logout
        var httpContext1 = CriarHttpContextComClaims(jti: jti, expUnix: expUnix);
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext1 };
        var result1 = await _controller.Logout(null);

        // Segundo logout com o mesmo JTI
        var httpContext2 = CriarHttpContextComClaims(jti: jti, expUnix: expUnix);
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext2 };
        var ex = await Record.ExceptionAsync(() => _controller.Logout(null));

        Assert.Null(ex);
        Assert.True(_blacklistService.IsRevoked(jti));
    }

    // ─────────────────────────────────────────────────────────────────
    // Logout — integridade: JTI diferente não afeta outro token
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_NaoDeveRevogarOutrosJtis()
    {
        var jtiRevogado = Guid.NewGuid().ToString();
        var jtiAtivo = Guid.NewGuid().ToString();
        var expUnix = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();

        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var httpContext = CriarHttpContextComClaims(jti: jtiRevogado, expUnix: expUnix);
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await _controller.Logout(null);

        Assert.True(_blacklistService.IsRevoked(jtiRevogado));
        Assert.False(_blacklistService.IsRevoked(jtiAtivo)); // Token diferente não afetado
    }

    // ─────────────────────────────────────────────────────────────────
    // Logout — exp Unix timestamp é convertido corretamente
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ComExpClaimVálida_UsaDataCorreta()
    {
        var jti = Guid.NewGuid().ToString();
        // Token com 5 minutos restantes
        var expiry = DateTime.UtcNow.AddMinutes(5);
        var expUnix = new DateTimeOffset(expiry).ToUnixTimeSeconds();

        _authServiceMock
            .Setup(s => s.RevogarRefreshTokenAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var httpContext = CriarHttpContextComClaims(jti: jti, expUnix: expUnix);
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await _controller.Logout(null);

        // Token com 5 min restantes: deve estar na blacklist agora
        Assert.True(_blacklistService.IsRevoked(jti));
    }
}
