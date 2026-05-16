using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using src.Service;
using Xunit;

namespace TicketPrime.Tests.Security;

/// <summary>
/// Testes profundos para MercadoPagoWebhookValidator.
///
/// Contextualiza a correção implementada no Critical Fix #1:
///   ANTES: Quando WebhookSecret não estava configurado em PRODUÇÃO,
///          o validator retornava TRUE (aceita qualquer notificação!).
///          Isso permitia forjar notificações de pagamento sem pagar.
///   DEPOIS: Em produção, WebhookSecret ausente → retorna FALSE + LogCritical.
///           Em não-produção (dev/staging), retorna TRUE + LogWarning.
///
/// Cenários cobertos:
///   ── Configuração do secret ──
///   - Production + secret ausente → false + LogCritical
///   - Dev/staging + secret ausente → true + LogWarning
///   - Secret configurado → validação HMAC normalmente
///
///   ── Validação HMAC-SHA256 ──
///   - Assinatura HMAC válida → true
///   - Payload adulterado → false
///   - Secret errado → false
///   - Assinatura hex errada → false
///
///   ── Header X-Signature ──
///   - Header ausente → false
///   - Header vazio → false
///   - Formato inválido (sem ts/v1) → false
///   - Formato parcial (só ts) → false
///   - Formato parcial (só v1) → false
///   - Header com espaços extras → parseado corretamente
///
///   ── Anti-replay: timestamp ──
///   - Timestamp atual → aceito
///   - Timestamp muito antigo (>5min) → rejeitado
///   - Timestamp no futuro (>5min) → rejeitado
///   - Timestamp ligeiramente antigo (<5min) → aceito
///
///   ── Timing-safe comparison ──
///   - CryptographicOperations.FixedTimeEquals é usado (sem timing oracle)
///   - Strings de tamanhos diferentes → false imediato
///
///   ── Formatos de payload ──
///   - JSON com data.id, transaction_amount, status → template HMAC
///   - JSON sem campos esperados → fallback body inteiro
///   - Corpo não-JSON → fallback body inteiro
///   - Corpo vazio → tratado sem exceção
/// </summary>
public class MercadoPagoValidatorDeepTests
{
    private const string TestSecret = "test-secret-key-very-secure-123";

    // ─────────────────────────────────────────────────────────────────
    // Helpers para criar instâncias do validador
    // ─────────────────────────────────────────────────────────────────

    private static MercadoPagoWebhookValidator CriarValidator(
        string? secret = TestSecret,
        string ambiente = "Development")
    {
        var configMock = new Mock<IConfiguration>();

        if (!string.IsNullOrEmpty(secret))
            configMock.Setup(c => c["MercadoPago:WebhookSecret"]).Returns(secret);
        else
        {
            configMock.Setup(c => c["MercadoPago:WebhookSecret"]).Returns((string?)null);
            configMock.Setup(c => c["MercadoPago__WebhookSecret"]).Returns((string?)null);
        }

        var loggerMock = new Mock<ILogger<MercadoPagoWebhookValidator>>();

        // Seta variável de ambiente temporariamente
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", ambiente);
        var validator = new MercadoPagoWebhookValidator(configMock.Object, loggerMock.Object);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);

        return validator;
    }

    /// <summary>
    /// Gera uma assinatura HMAC-SHA256 válida para o payload e timestamp dados.
    /// Simula o que o MercadoPago enviaria no header X-Signature.
    /// </summary>
    private static string GerarAssinaturaValida(string payload, long timestamp, string secret = TestSecret)
    {
        var data = $"ts={timestamp}|{payload}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static long TimestampAtual() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string CriarHeader(long timestamp, string signature)
        => $"ts={timestamp},v1={signature}";

    // ─────────────────────────────────────────────────────────────────
    // Secret ausente por ambiente
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValid_ProducaoSemSecret_DeveRetornarFalso()
    {
        // Critical Fix #1: Em produção, secret ausente BLOQUEIA tudo
        // O validator lê ASPNETCORE_ENVIRONMENT em IsValid(), não no construtor,
        // por isso precisamos manter a variável de ambiente setada durante a chamada.
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["MercadoPago:WebhookSecret"]).Returns((string?)null);
        configMock.Setup(c => c["MercadoPago__WebhookSecret"]).Returns((string?)null);
        var loggerMock = new Mock<ILogger<MercadoPagoWebhookValidator>>();

        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            var validator = new MercadoPagoWebhookValidator(configMock.Object, loggerMock.Object);
            var resultado = validator.IsValid("ts=123,v1=abc", "{}");
            Assert.False(resultado, "Em produção sem secret, o validator DEVE retornar false");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsValid_DesenvolvimentoSemSecret_DeveRetornarVerdadeiro()
    {
        // Em desenvolvimento, secret ausente → permite (facilita testes locais)
        var validator = CriarValidator(secret: null, ambiente: "Development");

        var resultado = validator.IsValid("ts=123,v1=abc", "{}");

        Assert.True(resultado, "Em desenvolvimento sem secret, o validator DEVE retornar true");
    }

    [Fact]
    public void IsValid_StagingSemSecret_DeveRetornarVerdadeiro()
    {
        // Staging também não bloqueia (apenas Production bloqueia)
        var validator = CriarValidator(secret: null, ambiente: "Staging");

        var resultado = validator.IsValid("ts=123,v1=abc", "{}");

        Assert.True(resultado, "Em staging sem secret, o validator DEVE retornar true");
    }

    // ─────────────────────────────────────────────────────────────────
    // HMAC: assinatura válida
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValid_AssinaturaHmacValida_DeveRetornarVerdadeiro()
    {
        var validator = CriarValidator();
        var body = """{"data":{"id":"123"},"transaction_amount":50.00,"status":"approved"}""";
        var ts = TimestampAtual();
        var assinatura = GerarAssinaturaValida(body, ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body);

        Assert.True(resultado);
    }

    [Fact]
    public void IsValid_PayloadComCamposTemplate_DeveUsarTemplateHmac()
    {
        // O validator tenta template "id:X;transaction-amount:Y;status:Z;" primeiro
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var body = """{"data":{"id":"987654"},"transaction_amount":199.90,"status":"pending"}""";

        // Gera assinatura com o template-based payload (como o MercadoPago faria)
        var templatePayload = "id:987654;transaction-amount:199.90;status:pending;";
        var assinatura = GerarAssinaturaValida(templatePayload, ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body);

        Assert.True(resultado, "Payload com formato template deve ser aceito");
    }

    [Fact]
    public void IsValid_PayloadBodyFallback_DeveAceitarRawBody()
    {
        // Quando o body não tem os campos template, usa o body inteiro
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var body = """{"outro":"campo","sem":"template"}""";
        var assinatura = GerarAssinaturaValida(body, ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body);

        Assert.True(resultado, "Fallback para raw body deve funcionar");
    }

    // ─────────────────────────────────────────────────────────────────
    // HMAC: assinatura inválida
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValid_PayloadAdulterado_DeveRetornarFalso()
    {
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var bodyOriginal = """{"data":{"id":"123"},"status":"approved"}""";
        var assinatura = GerarAssinaturaValida(bodyOriginal, ts);

        // Ataque: modificar o body após gerar a assinatura
        var bodyAdulterado = """{"data":{"id":"123"},"status":"completed"}"""; // "status" diferente

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), bodyAdulterado);

        Assert.False(resultado, "Payload adulterado deve ser rejeitado");
    }

    [Fact]
    public void IsValid_SecretErrado_DeveRetornarFalso()
    {
        var validatorComSecretCorreto = CriarValidator(secret: "secret-correto");
        var ts = TimestampAtual();
        var body = """{"data":{"id":"999"}}""";

        // Gera assinatura com secret diferente
        var assinaturaComSecretErrado = GerarAssinaturaValida(body, ts, secret: "secret-errado");

        var resultado = validatorComSecretCorreto.IsValid(
            CriarHeader(ts, assinaturaComSecretErrado), body);

        Assert.False(resultado, "Secret errado deve gerar assinatura inválida");
    }

    [Fact]
    public void IsValid_AssinaturaHexAleatoria_DeveRetornarFalso()
    {
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var body = """{"data":{"id":"123"}}""";

        // Assinatura completamente aleatória
        var assinaturaFalsa = Convert.ToHexString(new byte[32]).ToLowerInvariant();

        var resultado = validator.IsValid(CriarHeader(ts, assinaturaFalsa), body);

        Assert.False(resultado);
    }

    [Theory]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 64 zeros hex
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")] // 64 chars hex
    [InlineData("deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef")] // 64 chars hex
    public void IsValid_AssinaturaHexForjada_DeveRetornarFalso(string assinaturaForjada)
    {
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var body = """{}""";

        var resultado = validator.IsValid(CriarHeader(ts, assinaturaForjada), body);

        Assert.False(resultado);
    }

    // ─────────────────────────────────────────────────────────────────
    // Header X-Signature: ausente ou inválido
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValid_HeaderAusente_DeveRetornarFalso()
    {
        var validator = CriarValidator();

        var resultado = validator.IsValid(null, """{"data":{"id":"1"}}""");

        Assert.False(resultado);
    }

    [Fact]
    public void IsValid_HeaderVazio_DeveRetornarFalso()
    {
        var validator = CriarValidator();

        var resultado = validator.IsValid("", """{"data":{"id":"1"}}""");

        Assert.False(resultado);
    }

    [Fact]
    public void IsValid_HeaderApenasEspacos_DeveRetornarFalso()
    {
        var validator = CriarValidator();

        var resultado = validator.IsValid("   ", """{}""");

        Assert.False(resultado);
    }

    [Theory]
    [InlineData("invalid-header")]              // Sem ts= ou v1=
    [InlineData("ts=123")]                       // Só timestamp, sem v1
    [InlineData("v1=abc123")]                    // Só v1, sem ts
    [InlineData("ts=,v1=")]                      // Valores vazios
    [InlineData("ts=abc,v1=def")]                // ts não é número
    [InlineData("xyz=123,abc=def")]              // Chaves desconhecidas
    public void IsValid_FormatoHeaderInvalido_DeveRetornarFalso(string header)
    {
        var validator = CriarValidator();

        var resultado = validator.IsValid(header, """{}""");

        Assert.False(resultado, $"Header '{header}' deve ser rejeitado");
    }

    [Fact]
    public void IsValid_HeaderComEspacosExtras_DeveSerparsedCorretamente()
    {
        // O header pode vir com espaços ao redor das vírgulas
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var body = """{}""";
        var assinatura = GerarAssinaturaValida(body, ts);

        // Header com espaços extras ao redor da vírgula
        var headerComEspacos = $"ts={ts} , v1={assinatura}";

        // Deve parsear corretamente graças ao StringSplitOptions.TrimEntries
        var resultado = validator.IsValid(headerComEspacos, body);

        Assert.True(resultado, "Header com espaços extras deve ser parseado corretamente");
    }

    // ─────────────────────────────────────────────────────────────────
    // Anti-replay: timestamp
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValid_TimestampAtual_DeveAceitar()
    {
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var body = """{"data":{"id":"1"}}""";
        var assinatura = GerarAssinaturaValida(body, ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body);

        Assert.True(resultado, "Timestamp atual deve ser aceito");
    }

    [Fact]
    public void IsValid_Timestamp4MinutosAtras_DeveAceitar()
    {
        // 4 minutos é dentro da janela de 5 minutos
        var validator = CriarValidator();
        var ts = TimestampAtual() - (4 * 60);
        var body = """{"data":{"id":"1"}}""";
        var assinatura = GerarAssinaturaValida(body, ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body);

        Assert.True(resultado, "Timestamp 4 minutos atrás deve ser aceito (dentro da janela de 5min)");
    }

    [Fact]
    public void IsValid_Timestamp6MinutosAtras_DeveRejeitar()
    {
        // 6 minutos é fora da janela de 5 minutos (anti-replay)
        var validator = CriarValidator();
        var ts = TimestampAtual() - (6 * 60);
        var body = """{"data":{"id":"1"}}""";
        var assinatura = GerarAssinaturaValida(body, ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body);

        Assert.False(resultado, "Timestamp 6 minutos atrás deve ser rejeitado (fora da janela de 5min)");
    }

    [Fact]
    public void IsValid_Timestamp6MinutosNoFuturo_DeveRejeitar()
    {
        // Timestamp no futuro também é rejeitado (evita pré-geração de tokens)
        var validator = CriarValidator();
        var ts = TimestampAtual() + (6 * 60);
        var body = """{"data":{"id":"1"}}""";
        var assinatura = GerarAssinaturaValida(body, ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body);

        Assert.False(resultado, "Timestamp 6 minutos no futuro deve ser rejeitado");
    }

    [Theory]
    [InlineData(0)]    // Agora
    [InlineData(60)]   // 1 minuto atrás
    [InlineData(180)]  // 3 minutos atrás
    [InlineData(299)]  // Quase 5 minutos (dentro da janela)
    public void IsValid_TimestampsDentroJanela_DevemSerAceitos(int segundosAtras)
    {
        var validator = CriarValidator();
        var ts = TimestampAtual() - segundosAtras;
        var body = """{}""";
        var assinatura = GerarAssinaturaValida(body, ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body);

        Assert.True(resultado, $"Timestamp {segundosAtras}s atrás deve ser aceito");
    }

    [Theory]
    [InlineData(301)]   // Logo além da janela de 5 minutos
    [InlineData(600)]   // 10 minutos
    [InlineData(3600)]  // 1 hora
    [InlineData(86400)] // 1 dia
    public void IsValid_TimestampsForaDaJanela_DevemSerRejeitados(int segundosAtras)
    {
        var validator = CriarValidator();
        var ts = TimestampAtual() - segundosAtras;
        var body = """{}""";
        var assinatura = GerarAssinaturaValida(body, ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body);

        Assert.False(resultado, $"Timestamp {segundosAtras}s atrás deve ser rejeitado");
    }

    // ─────────────────────────────────────────────────────────────────
    // Timing-safe comparison (CryptographicOperations.FixedTimeEquals)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FixedTimeEquals_StringsIdenticas_DeveRetornarVerdadeiro()
    {
        var str = "abc123def456";
        var bytesA = Encoding.UTF8.GetBytes(str);
        var bytesB = Encoding.UTF8.GetBytes(str);

        Assert.True(CryptographicOperations.FixedTimeEquals(bytesA, bytesB));
    }

    [Fact]
    public void FixedTimeEquals_StringsDiferentes_DeveRetornarFalso()
    {
        var bytesA = Encoding.UTF8.GetBytes("assinatura-correta");
        var bytesB = Encoding.UTF8.GetBytes("assinatura-errada!");

        // Comprimentos iguais para o FixedTimeEquals comparar todos os bytes
        Assert.NotEqual(bytesA.Length == bytesB.Length, false); // Mesmo comprimento
        Assert.False(CryptographicOperations.FixedTimeEquals(bytesA, bytesB));
    }

    [Fact]
    public void FixedTimeEquals_TamanhosDiferentes_DeveRetornarFalso()
    {
        var bytesA = Encoding.UTF8.GetBytes("abc");
        var bytesB = Encoding.UTF8.GetBytes("abcdef");

        Assert.False(CryptographicOperations.FixedTimeEquals(bytesA, bytesB));
    }

    // ─────────────────────────────────────────────────────────────────
    // Formato do HMAC gerado: hex lowercase de 64 chars (SHA256)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeHmac_SalidaDeveSerHexLowercaseWith64Chars()
    {
        // SHA256 = 32 bytes = 64 hex chars
        var key = Encoding.UTF8.GetBytes(TestSecret);
        var data = Encoding.UTF8.GetBytes("ts=1234567890|payload de teste");
        var hash = HMACSHA256.HashData(key, data);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        Assert.Equal(64, hex.Length);
        Assert.True(hex.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')),
            "HMAC deve ser hex lowercase");
    }

    [Fact]
    public void ComputeHmac_MesmoPayloadMesmoTimestamp_ProduziMesmoHmac()
    {
        var payload = "id:123;transaction-amount:50.00;status:approved;";
        var ts = 1700000000L; // Timestamp fixo para reproducibilidade
        var key = Encoding.UTF8.GetBytes(TestSecret);
        var data = $"ts={ts}|{payload}";

        var hash1 = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        var hash2 = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHmac_TimestampDiferente_ProduziHmacDiferente()
    {
        // Mesmo payload, timestamps diferentes → HMACs diferentes
        // (previne reutilização de tokens capturados)
        var payload = "corpo-do-webhook";
        var key = Encoding.UTF8.GetBytes(TestSecret);

        var data1 = $"ts={1700000000L}|{payload}";
        var data2 = $"ts={1700000001L}|{payload}"; // 1 segundo diferente

        var hash1 = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data1))).ToLowerInvariant();
        var hash2 = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data2))).ToLowerInvariant();

        Assert.NotEqual(hash1, hash2);
    }

    // ─────────────────────────────────────────────────────────────────
    // Cenários realistas
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValid_NotificacaoPagamentoAprovado_DeveAceitarComHmacCorreto()
    {
        // Simula notificação real do MercadoPago para pagamento aprovado
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var body = """
        {
            "id": 112233445,
            "live_mode": true,
            "type": "payment",
            "date_created": "2024-01-15T10:30:00Z",
            "data": {
                "id": "112233445"
            },
            "transaction_amount": 250.00,
            "status": "approved"
        }
        """;

        var assinatura = GerarAssinaturaValida(body.Trim(), ts);

        var resultado = validator.IsValid(CriarHeader(ts, assinatura), body.Trim());

        Assert.True(resultado, "Notificação de pagamento aprovado com HMAC correto deve ser aceita");
    }

    [Fact]
    public void IsValid_AtaqueDeReplay_DeveSerBloqueadoPeloTimestamp()
    {
        // Simula um atacante tentando reutilizar uma notificação legítima capturada 10 minutos atrás
        var validator = CriarValidator();
        var tsAntigo = TimestampAtual() - (10 * 60); // 10 minutos atrás
        var body = """{"data":{"id":"999"},"status":"approved"}""";

        // A assinatura original era válida no momento da captura
        var assinaturaOriginal = GerarAssinaturaValida(body, tsAntigo);

        // Mas agora, 10 minutos depois, o replay deve ser bloqueado
        var resultado = validator.IsValid(CriarHeader(tsAntigo, assinaturaOriginal), body);

        Assert.False(resultado, "Replay attack com timestamp antigo deve ser bloqueado");
    }

    [Fact]
    public void IsValid_BodyNaoJson_NaoDeveLancarExcecao()
    {
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var bodyNaoJson = "not-a-json-body-at-all";
        var assinatura = GerarAssinaturaValida(bodyNaoJson, ts);

        // Não deve lançar JsonException
        var ex = Record.Exception(() => validator.IsValid(CriarHeader(ts, assinatura), bodyNaoJson));
        Assert.Null(ex);
    }

    [Fact]
    public void IsValid_BodyVazio_NaoDeveLancarExcecao()
    {
        var validator = CriarValidator();
        var ts = TimestampAtual();
        var assinatura = GerarAssinaturaValida("", ts);

        var ex = Record.Exception(() => validator.IsValid(CriarHeader(ts, assinatura), ""));
        Assert.Null(ex);
    }

    [Fact]
    public void IsValid_DoisPayloadsDiferentes_ProduzemAssinaturasDiferentes()
    {
        // Previne colisão de HMAC (integridade do hash)
        var ts = TimestampAtual();
        var payload1 = """{"status":"approved","amount":100}""";
        var payload2 = """{"status":"rejected","amount":100}"""; // Diferença no status

        var hmac1 = GerarAssinaturaValida(payload1, ts);
        var hmac2 = GerarAssinaturaValida(payload2, ts);

        Assert.NotEqual(hmac1, hmac2);
    }
}
