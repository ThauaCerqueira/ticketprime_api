using TicketPrime.Web.Shared.Models;

namespace TicketPrime.Tests.Models;

/// <summary>
/// Testes unitários para os modelos do EventoCreate:
///   EventStatus   – constantes de ciclo de vida do evento
///   EventoCreateDto – valores padrão e integridade do DTO
///   EncryptedPhoto – estrutura e defaults do pacote cifrado
///   ImagePackage    – defaults e integridade do payload de envio
/// </summary>
public class EventoCreateModelsTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // EventStatus – constantes
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EventStatus_Rascunho_DeveTerValorCorreto()
    {
        Assert.Equal("Rascunho", EventStatus.Rascunho);
    }

    [Fact]
    public void EventStatus_Publicado_DeveTerValorCorreto()
    {
        Assert.Equal("Publicado", EventStatus.Publicado);
    }

    [Fact]
    public void EventStatus_Encerrado_DeveTerValorCorreto()
    {
        Assert.Equal("Encerrado", EventStatus.Encerrado);
    }

    [Fact]
    public void EventStatus_Cancelado_DeveTerValorCorreto()
    {
        Assert.Equal("Cancelado", EventStatus.Cancelado);
    }

    [Fact]
    public void EventStatus_TodosOsValores_DevemSerDistintos()
    {
        var valores = new[] {
            EventStatus.Rascunho,
            EventStatus.Publicado,
            EventStatus.Encerrado,
            EventStatus.Cancelado
        };

        // Todos distintos (sem duplicatas)
        Assert.Equal(valores.Length, valores.Distinct().Count());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EventoCreateDto – valores padrão
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EventoCreateDto_NovoCriado_DeveIniciarComoRascunho()
    {
        var dto = new EventoCreateDto();

        Assert.Equal(EventStatus.Rascunho, dto.Status);
    }

    [Fact]
    public void EventoCreateDto_NovoCriado_EventoGratuitoDeveSerFalse()
    {
        var dto = new EventoCreateDto();

        Assert.False(dto.EventoGratuito);
    }

    [Fact]
    public void EventoCreateDto_NovoCriado_DataHoraDeveSerNula()
    {
        var dto = new EventoCreateDto();

        Assert.Null(dto.DataHora);
    }

    [Fact]
    public void EventoCreateDto_NovoCriado_PrecoDeveSerNulo()
    {
        var dto = new EventoCreateDto();

        Assert.Null(dto.Preco);
    }

    [Fact]
    public void EventoCreateDto_NovoCriado_CapacidadeMaximaDeveSerZero()
    {
        var dto = new EventoCreateDto();

        // Valor padrão de int é 0 (campo ainda não preenchido pelo usuário)
        Assert.Equal(0, dto.CapacidadeMaxima);
    }

    [Fact]
    public void EventoCreateDto_NovoCriado_StringsDevemSerVazias()
    {
        var dto = new EventoCreateDto();

        Assert.Equal(string.Empty, dto.Nome);
        Assert.Equal(string.Empty, dto.Local);
        Assert.Equal(string.Empty, dto.GeneroMusical);
        Assert.Null(dto.Descricao);   // campo opcional → null por padrão
    }

    [Fact]
    public void EventoCreateDto_AtribuindoStatus_DeveAceitarQualquerEstado()
    {
        var dto = new EventoCreateDto();

        dto.Status = EventStatus.Publicado;
        Assert.Equal(EventStatus.Publicado, dto.Status);

        dto.Status = EventStatus.Cancelado;
        Assert.Equal(EventStatus.Cancelado, dto.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EncryptedPhoto – estrutura e defaults
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EncryptedPhoto_NovaCriada_CriptografadaDeveSerFalse()
    {
        var foto = new EncryptedPhoto();

        Assert.False(foto.Criptografada);
    }

    [Fact]
    public void EncryptedPhoto_NovaCriada_TamanhoBytesDeveSerZero()
    {
        var foto = new EncryptedPhoto();

        Assert.Equal(0L, foto.TamanhoBytes);
    }

    [Fact]
    public void EncryptedPhoto_NovaCriada_StringsDevemSerVazias()
    {
        var foto = new EncryptedPhoto();

        Assert.Equal(string.Empty, foto.CiphertextBase64);
        Assert.Equal(string.Empty, foto.IvBase64);
        Assert.Equal(string.Empty, foto.ChaveAesCifradaBase64);
        Assert.Equal(string.Empty, foto.ChavePublicaOrgJwk);
        Assert.Equal(string.Empty, foto.HashNomeOriginal);
        Assert.Equal(string.Empty, foto.TipoMime);
    }

    [Fact]
    public void EncryptedPhoto_Preenchida_DevePersistirDados()
    {
        var foto = new EncryptedPhoto
        {
            CiphertextBase64      = "abc123==",
            IvBase64              = "iv==",
            ChaveAesCifradaBase64 = "key==",
            ChavePublicaOrgJwk    = "{\"crv\":\"P-256\"}",
            HashNomeOriginal      = "sha256hash==",
            TipoMime              = "image/jpeg",
            TamanhoBytes          = 1_024_000,
            Criptografada         = true
        };

        Assert.True(foto.Criptografada);
        Assert.Equal("image/jpeg", foto.TipoMime);
        Assert.Equal(1_024_000L, foto.TamanhoBytes);
        Assert.Equal("abc123==", foto.CiphertextBase64);
        Assert.Contains("P-256", foto.ChavePublicaOrgJwk);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ImagePackage – defaults e integridade do payload
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ImagePackage_NovoCriado_DeveConterEventoIniciado()
    {
        var pacote = new ImagePackage();

        Assert.NotNull(pacote.Evento);
    }

    [Fact]
    public void ImagePackage_NovoCriado_ListaDeFotosDeveEstarVaziaENaoNula()
    {
        var pacote = new ImagePackage();

        Assert.NotNull(pacote.Fotos);
        Assert.Empty(pacote.Fotos);
    }

    [Fact]
    public void ImagePackage_NovoCriado_VersaoProtocoloDeveEstarPreenchida()
    {
        var pacote = new ImagePackage();

        Assert.False(string.IsNullOrEmpty(pacote.VersaoProtocolo));
        Assert.Contains("aesgcm256", pacote.VersaoProtocolo);
        Assert.Contains("ecdh-p256", pacote.VersaoProtocolo);
    }

    [Fact]
    public void ImagePackage_NovoCriado_TimestampDeveSerProximoDoUtcNow()
    {
        var antes  = DateTime.UtcNow;
        var pacote = new ImagePackage();
        var depois = DateTime.UtcNow;

        Assert.InRange(pacote.Timestamp, antes, depois);
    }

    [Fact]
    public void ImagePackage_AdicionarFotos_DeveRefletirNaLista()
    {
        var pacote = new ImagePackage();
        pacote.Fotos.Add(new EncryptedPhoto { TipoMime = "image/png", Criptografada = true });
        pacote.Fotos.Add(new EncryptedPhoto { TipoMime = "image/jpeg", Criptografada = true });

        Assert.Equal(2, pacote.Fotos.Count);
        Assert.All(pacote.Fotos, f => Assert.True(f.Criptografada));
    }

    [Fact]
    public void ImagePackage_ComEventoPreenchido_DeveRetornarDadosCorretamente()
    {
        var dto = new EventoCreateDto
        {
            Nome           = "Show Teste",
            GeneroMusical  = "MPB",
            CapacidadeMaxima = 200,
            Status         = EventStatus.Rascunho
        };

        var pacote = new ImagePackage { Evento = dto };

        Assert.Equal("Show Teste",          pacote.Evento.Nome);
        Assert.Equal("MPB",                 pacote.Evento.GeneroMusical);
        Assert.Equal(200,                   pacote.Evento.CapacidadeMaxima);
        Assert.Equal(EventStatus.Rascunho, pacote.Evento.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Regra de negócio: status inicial é sempre Rascunho
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EventoCreateDto_StatusInicial_NuncaDeveSerPublicado()
    {
        // Qualquer evento recém-criado começa como Rascunho,
        // nunca como Publicado (requer revisão do sistema)
        var dto = new EventoCreateDto();

        Assert.NotEqual(EventStatus.Publicado, dto.Status);
    }

    [Fact]
    public void EventoCreateDto_StatusInicial_NuncaDeveSerCancelado()
    {
        var dto = new EventoCreateDto();

        Assert.NotEqual(EventStatus.Cancelado, dto.Status);
    }
}
