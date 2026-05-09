using TicketPrime.Web.Models;

namespace TicketPrime.Tests.Models;

/// <summary>
/// Testes unitários para os modelos do EventoCreate:
///   EventoStatus   – constantes de ciclo de vida do evento
///   EventoCreateDto – valores padrão e integridade do DTO
///   FotoCriptografada – estrutura e defaults do pacote cifrado
///   PacoteImagem    – defaults e integridade do payload de envio
/// </summary>
public class EventoCreateModelsTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // EventoStatus – constantes
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EventoStatus_Rascunho_DeveTerValorCorreto()
    {
        Assert.Equal("Rascunho", EventoStatus.Rascunho);
    }

    [Fact]
    public void EventoStatus_Publicado_DeveTerValorCorreto()
    {
        Assert.Equal("Publicado", EventoStatus.Publicado);
    }

    [Fact]
    public void EventoStatus_Encerrado_DeveTerValorCorreto()
    {
        Assert.Equal("Encerrado", EventoStatus.Encerrado);
    }

    [Fact]
    public void EventoStatus_Cancelado_DeveTerValorCorreto()
    {
        Assert.Equal("Cancelado", EventoStatus.Cancelado);
    }

    [Fact]
    public void EventoStatus_TodosOsValores_DevemSerDistintos()
    {
        var valores = new[] {
            EventoStatus.Rascunho,
            EventoStatus.Publicado,
            EventoStatus.Encerrado,
            EventoStatus.Cancelado
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

        Assert.Equal(EventoStatus.Rascunho, dto.Status);
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

        dto.Status = EventoStatus.Publicado;
        Assert.Equal(EventoStatus.Publicado, dto.Status);

        dto.Status = EventoStatus.Cancelado;
        Assert.Equal(EventoStatus.Cancelado, dto.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FotoCriptografada – estrutura e defaults
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FotoCriptografada_NovaCriada_CriptografadaDeveSerFalse()
    {
        var foto = new FotoCriptografada();

        Assert.False(foto.Criptografada);
    }

    [Fact]
    public void FotoCriptografada_NovaCriada_TamanhoBytesDeveSerZero()
    {
        var foto = new FotoCriptografada();

        Assert.Equal(0L, foto.TamanhoBytes);
    }

    [Fact]
    public void FotoCriptografada_NovaCriada_StringsDevemSerVazias()
    {
        var foto = new FotoCriptografada();

        Assert.Equal(string.Empty, foto.CiphertextBase64);
        Assert.Equal(string.Empty, foto.IvBase64);
        Assert.Equal(string.Empty, foto.ChaveAesCifradaBase64);
        Assert.Equal(string.Empty, foto.ChavePublicaOrgJwk);
        Assert.Equal(string.Empty, foto.HashNomeOriginal);
        Assert.Equal(string.Empty, foto.TipoMime);
    }

    [Fact]
    public void FotoCriptografada_Preenchida_DevePersistirDados()
    {
        var foto = new FotoCriptografada
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
    // PacoteImagem – defaults e integridade do payload
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PacoteImagem_NovoCriado_DeveConterEventoIniciado()
    {
        var pacote = new PacoteImagem();

        Assert.NotNull(pacote.Evento);
    }

    [Fact]
    public void PacoteImagem_NovoCriado_ListaDeFotosDeveEstarVaziaENaoNula()
    {
        var pacote = new PacoteImagem();

        Assert.NotNull(pacote.Fotos);
        Assert.Empty(pacote.Fotos);
    }

    [Fact]
    public void PacoteImagem_NovoCriado_VersaoProtocoloDeveEstarPreenchida()
    {
        var pacote = new PacoteImagem();

        Assert.False(string.IsNullOrEmpty(pacote.VersaoProtocolo));
        Assert.Contains("aesgcm256", pacote.VersaoProtocolo);
        Assert.Contains("ecdh-p256", pacote.VersaoProtocolo);
    }

    [Fact]
    public void PacoteImagem_NovoCriado_TimestampDeveSerProximoDoUtcNow()
    {
        var antes  = DateTime.UtcNow;
        var pacote = new PacoteImagem();
        var depois = DateTime.UtcNow;

        Assert.InRange(pacote.Timestamp, antes, depois);
    }

    [Fact]
    public void PacoteImagem_AdicionarFotos_DeveRefletirNaLista()
    {
        var pacote = new PacoteImagem();
        pacote.Fotos.Add(new FotoCriptografada { TipoMime = "image/png", Criptografada = true });
        pacote.Fotos.Add(new FotoCriptografada { TipoMime = "image/jpeg", Criptografada = true });

        Assert.Equal(2, pacote.Fotos.Count);
        Assert.All(pacote.Fotos, f => Assert.True(f.Criptografada));
    }

    [Fact]
    public void PacoteImagem_ComEventoPreenchido_DeveRetornarDadosCorretamente()
    {
        var dto = new EventoCreateDto
        {
            Nome           = "Show Teste",
            GeneroMusical  = "MPB",
            CapacidadeMaxima = 200,
            Status         = EventoStatus.Rascunho
        };

        var pacote = new PacoteImagem { Evento = dto };

        Assert.Equal("Show Teste",          pacote.Evento.Nome);
        Assert.Equal("MPB",                 pacote.Evento.GeneroMusical);
        Assert.Equal(200,                   pacote.Evento.CapacidadeMaxima);
        Assert.Equal(EventoStatus.Rascunho, pacote.Evento.Status);
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

        Assert.NotEqual(EventoStatus.Publicado, dto.Status);
    }

    [Fact]
    public void EventoCreateDto_StatusInicial_NuncaDeveSerCancelado()
    {
        var dto = new EventoCreateDto();

        Assert.NotEqual(EventoStatus.Cancelado, dto.Status);
    }
}
