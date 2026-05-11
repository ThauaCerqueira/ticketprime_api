using FluentValidation;
using FluentValidation.TestHelper;
using TicketPrime.Web.Models;
using TicketPrime.Web.Validators;

namespace TicketPrime.Tests.Validators;

/// <summary>
/// Testes unitários para EventoCreateDtoValidator.
///
/// Cobertura por campo:
///   Nome          → obrigatório, mínimo 3, máximo 200 chars
///   DataHora      → obrigatória, antecedência mínima de 24 h
///   Local         → obrigatório, máximo 500 chars
///   Descrição     → máximo 2000 chars, filtro de spam
///   GeneroMusical → obrigatório
///   Preço (pago)  → obrigatório, não negativo, máximo R$ 50.000
///   Evento gratuito → preço deve ser zero ou nulo
///   CapacidadeMaxima → mínimo 1, máximo 100.000
///   Válido completo → nenhum erro esperado
/// </summary>
public class EventoDtoValidatorTests
{
    private readonly EventoCreateDtoValidator _sut = new();

    // ── Fábrica de modelo válido ──────────────────────────────────────────────
    private static EventoCreateDto ModeloValido() => new()
    {
        Nome            = "Rock na Praça 2026",
        DataHora        = DateTime.Now.AddHours(25),
        Local           = "Parque Ibirapuera, São Paulo – SP",
        Descricao       = "Festival de rock ao ar livre com bandas locais.",
        GeneroMusical   = "Rock",
        Preco           = 80m,
        EventoGratuito  = false,
        CapacidadeMaxima = 500
    };

    // ── Dados compartilhados para Theory ──────────────────────────────────────
    public static TheoryData<string, string, string> CamposObrigatorios => new()
    {
        { nameof(EventoCreateDto.Nome),         "",       "O nome do evento é obrigatório." },
        { nameof(EventoCreateDto.Local),        "",       "O local do evento é obrigatório." },
        { nameof(EventoCreateDto.GeneroMusical), "",       "Selecione o gênero musical do evento." },
    };

    public static TheoryData<string, int, string> CamposComMaximoCaracteres => new()
    {
        { nameof(EventoCreateDto.Nome),         201, "O nome não pode exceder 200 caracteres." },
        { nameof(EventoCreateDto.Local),        501, "O local não pode exceder 500 caracteres." },
        { nameof(EventoCreateDto.Descricao),   2001, "A descrição não pode exceder 2000 caracteres." },
    };

    public static TheoryData<string, IEnumerable<string>> TermosSpam => new()
    {
        { "Evento especial! Clique aqui para saber mais.",       new[] { "clique aqui" } },
        { "ganhe dinheiro fácil participando!",                  new[] { "ganhe dinheiro" } },
        { "Renda extra garantida neste show.",                   new[] { "renda extra" } },
        { "Investimento garantido em diversão!",                  new[] { "investimento garantido" } },
        { "Oportunidade única de ver sua banda favorita!",        new[] { "oportunidade única" } },
        { "Não perca essa chance imperdível!",                    new[] { "não perca" } },
        { "Promoção imperdível, faça agora!",                     new[] { "promoção imperdível", "faça agora" } },
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPOS OBRIGATÓRIOS (Nome, Local, GeneroMusical)
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(CamposObrigatorios))]
    public void CampoObrigatorio_Vazio_DeveRetornarErro(string campo, string valor, string mensagem)
    {
        var modelo = ModeloValido();
        var prop = typeof(EventoCreateDto).GetProperty(campo)!;
        prop.SetValue(modelo, valor);

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(campo)
              .WithErrorMessage(mensagem);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Nome
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("AB")]   // 2 chars – abaixo do mínimo
    [InlineData("A")]    // 1 char
    public void Nome_MenorQue3Chars_DeveRetornarErro(string nome)
    {
        var modelo = ModeloValido();
        modelo.Nome = nome;

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.Nome)
              .WithErrorMessage("O nome deve ter pelo menos 3 caracteres.");
    }

    [Theory]
    [InlineData("ABC")]           // exatamente 3 chars – limite inferior
    [InlineData("Rock Festival")] // valor normal
    public void Nome_Valido_NaoDeveRetornarErro(string nome)
    {
        var modelo = ModeloValido();
        modelo.Nome = nome;

        var result = _sut.TestValidate(modelo);
        result.ShouldNotHaveValidationErrorFor(x => x.Nome);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: DataHora
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DataHora_Nula_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.DataHora = null;

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.DataHora)
              .WithErrorMessage("A data e hora são obrigatórias.");
    }

    [Fact]
    public void DataHora_MenosDe24HorasNaFrente_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.DataHora = DateTime.Now.AddHours(23);

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.DataHora);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EventoCreateDto.DataHora)
            && e.ErrorMessage.Contains("24 horas de antecedência"));
    }

    [Fact]
    public void DataHora_NoPassado_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.DataHora = DateTime.Now.AddDays(-1);

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.DataHora);
    }

    [Fact]
    public void DataHora_Com25Horas_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.DataHora = DateTime.Now.AddHours(25);

        var result = _sut.TestValidate(modelo);
        result.ShouldNotHaveValidationErrorFor(x => x.DataHora);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Local
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Local_ComLinkGoogleMaps_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Local = "https://maps.google.com/?q=Ibirapuera";

        var result = _sut.TestValidate(modelo);
        result.ShouldNotHaveValidationErrorFor(x => x.Local);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Descrição
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Descricao_Nula_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Descricao = null;

        var result = _sut.TestValidate(modelo);
        result.ShouldNotHaveValidationErrorFor(x => x.Descricao);
    }

    [Theory]
    [MemberData(nameof(TermosSpam))]
    public void Descricao_ComTermoSpam_DeveRetornarErro(string descricao, IEnumerable<string> _)
    {
        var modelo = ModeloValido();
        modelo.Descricao = descricao;

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.Descricao)
              .WithErrorMessage("A descrição contém termos não permitidos. Revise o conteúdo.");
    }

    [Fact]
    public void Descricao_SemSpam_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Descricao = "Show de rock ao ar livre com três bandas locais. Traz sua cadeira e bebida.";

        var result = _sut.TestValidate(modelo);
        result.ShouldNotHaveValidationErrorFor(x => x.Descricao);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPOS COM MÁXIMO DE CARACTERES (Nome:200, Local:500, Descricao:2000)
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(CamposComMaximoCaracteres))]
    public void Campo_ExcedeMaximoCaracteres_DeveRetornarErro(string campo, int tamanho, string mensagem)
    {
        var modelo = ModeloValido();
        var prop = typeof(EventoCreateDto).GetProperty(campo)!;
        prop.SetValue(modelo, new string('X', tamanho));

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(campo)
              .WithErrorMessage(mensagem);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Preço (evento pago)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Preco_Nulo_QuandoEventoPago_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.EventoGratuito = false;
        modelo.Preco = null;

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.Preco)
              .WithErrorMessage("Informe o preço do ingresso ou marque 'Evento gratuito'.");
    }

    [Fact]
    public void Preco_Negativo_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Preco = -1m;

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.Preco)
              .WithErrorMessage("O preço não pode ser negativo.");
    }

    [Theory]
    [InlineData(50001)]
    [InlineData(100000)]
    public void Preco_AcimaDoMaximo_DeveRetornarErro(decimal preco)
    {
        var modelo = ModeloValido();
        modelo.Preco = preco;

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.Preco);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EventoCreateDto.Preco)
            && e.ErrorMessage.Contains("50.000"));
    }

    [Theory]
    [InlineData(0)]        // gratuito mas com preço zero = ok
    [InlineData(10)]
    [InlineData(50000)]    // exatamente no limite
    public void Preco_DentroDoLimite_NaoDeveRetornarErro(decimal preco)
    {
        var modelo = ModeloValido();
        modelo.EventoGratuito = false;
        modelo.Preco = preco;

        var result = _sut.TestValidate(modelo);
        result.ShouldNotHaveValidationErrorFor(x => x.Preco);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Preço quando EventoGratuito = true
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(0.01)]
    public void Preco_DiferenteDeZeroQuandoGratuito_DeveRetornarErro(decimal preco)
    {
        var modelo = ModeloValido();
        modelo.EventoGratuito = true;
        modelo.Preco = preco;

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.Preco)
              .WithErrorMessage("Evento gratuito não pode ter preço diferente de zero.");
    }

    [Fact]
    public void Preco_ZeroQuandoGratuito_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.EventoGratuito = true;
        modelo.Preco = 0;

        var result = _sut.TestValidate(modelo);
        result.ShouldNotHaveValidationErrorFor(x => x.Preco);
    }

    [Fact]
    public void Preco_NuloQuandoGratuito_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.EventoGratuito = true;
        modelo.Preco = null;

        var result = _sut.TestValidate(modelo);
        result.ShouldNotHaveValidationErrorFor(x => x.Preco);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: CapacidadeMaxima
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CapacidadeMaxima_MenorQue1_DeveRetornarErro(int capacidade)
    {
        var modelo = ModeloValido();
        modelo.CapacidadeMaxima = capacidade;

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.CapacidadeMaxima);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EventoCreateDto.CapacidadeMaxima)
            && e.ErrorMessage.Contains("mínima é de 1"));
    }

    [Fact]
    public void CapacidadeMaxima_AcimaDoLimite_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.CapacidadeMaxima = 100_001;

        var result = _sut.TestValidate(modelo);
        result.ShouldHaveValidationErrorFor(x => x.CapacidadeMaxima);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EventoCreateDto.CapacidadeMaxima)
            && e.ErrorMessage.Contains("100.000"));
    }

    [Theory]
    [InlineData(1)]          // mínimo exato
    [InlineData(500)]
    [InlineData(100_000)]    // máximo exato
    public void CapacidadeMaxima_DentroDoLimite_NaoDeveRetornarErro(int capacidade)
    {
        var modelo = ModeloValido();
        modelo.CapacidadeMaxima = capacidade;

        var result = _sut.TestValidate(modelo);
        result.ShouldNotHaveValidationErrorFor(x => x.CapacidadeMaxima);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // VALIDAÇÃO COMPLETA
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ModeloCompleto_Valido_NaoDeveRetornarNenhumErro()
    {
        var modelo = ModeloValido();

        var result = _sut.TestValidate(modelo);

        Assert.True(result.IsValid,
            "Erros inesperados: " + string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void ModeloCompleto_Invalido_DeveRetornarMultiplosErros()
    {
        var modelo = new EventoCreateDto
        {
            Nome             = "AB",                            // muito curto
            DataHora         = DateTime.Now.AddHours(1),        // menos de 24 h
            Local            = string.Empty,                    // obrigatório
            Descricao        = "Clique aqui",                   // spam
            GeneroMusical    = string.Empty,                    // obrigatório
            Preco            = -5m,                             // negativo
            EventoGratuito   = false,
            CapacidadeMaxima = 0                                // abaixo do mínimo
        };

        var result = _sut.TestValidate(modelo);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5,
            $"Esperava pelo menos 5 erros, obteve {result.Errors.Count}.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ValidarCampo – método auxiliar do validator (usado pelo MudBlazor)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidarCampo_Nome_DeveRetornarApenasErrosDoCampoNome()
    {
        var modelo = new EventoCreateDto { Nome = "X" };

        var erros = _sut.ValidarCampo(modelo, nameof(EventoCreateDto.Nome));

        Assert.Single(erros);
        Assert.Contains("3 caracteres", erros.First());
    }

    [Fact]
    public void ValidarCampo_CampoValido_DeveRetornarListaVazia()
    {
        var modelo = ModeloValido();

        var erros = _sut.ValidarCampo(modelo, nameof(EventoCreateDto.Nome));

        Assert.Empty(erros);
    }
}
