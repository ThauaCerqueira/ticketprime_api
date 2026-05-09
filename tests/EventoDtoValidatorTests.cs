using FluentValidation;
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

    // ── Fábrica de modelos válidos ──────────────────────────────────────────────
    /// <summary>Retorna um DTO completamente válido para ser mutado nos testes.</summary>
    private static EventoCreateDto ModeloValido() => new()
    {
        Nome           = "Rock na Praça 2026",
        DataHora       = DateTime.Now.AddHours(25),
        Local          = "Parque Ibirapuera, São Paulo – SP",
        Descricao      = "Festival de rock ao ar livre com bandas locais.",
        GeneroMusical  = "Rock",
        Preco          = 80m,
        EventoGratuito = false,
        CapacidadeMaxima = 500
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Nome
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Nome_Vazio_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Nome = string.Empty;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Nome)
            && e.ErrorMessage == "O nome do evento é obrigatório.");
    }

    [Theory]
    [InlineData("AB")]   // 2 chars – abaixo do mínimo
    [InlineData("A")]    // 1 char
    public void Nome_MenorQue3Chars_DeveRetornarErro(string nome)
    {
        var modelo = ModeloValido();
        modelo.Nome = nome;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Nome)
            && e.ErrorMessage == "O nome deve ter pelo menos 3 caracteres.");
    }

    [Fact]
    public void Nome_Com201Chars_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Nome = new string('X', 201);

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Nome)
            && e.ErrorMessage == "O nome não pode exceder 200 caracteres.");
    }

    [Theory]
    [InlineData("ABC")]           // exatamente 3 chars – limite inferior
    [InlineData("Rock Festival")] // valor normal
    public void Nome_Valido_NaoDeveRetornarErro(string nome)
    {
        var modelo = ModeloValido();
        modelo.Nome = nome;

        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.Nome));

        Assert.Empty(erros);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: DataHora
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DataHora_Nula_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.DataHora = null;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.DataHora)
            && e.ErrorMessage == "A data e hora são obrigatórias.");
    }

    [Fact]
    public void DataHora_MenosDe24HorasNaFrente_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.DataHora = DateTime.Now.AddHours(23);   // menos de 24 h de antecedência

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.DataHora)
            && e.ErrorMessage.Contains("24 horas de antecedência"));
    }

    [Fact]
    public void DataHora_NoPassado_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.DataHora = DateTime.Now.AddDays(-1);

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.DataHora));
    }

    [Fact]
    public void DataHora_Com25Horas_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.DataHora = DateTime.Now.AddHours(25);

        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.DataHora));

        Assert.Empty(erros);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Local
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Local_Vazio_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Local = string.Empty;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Local)
            && e.ErrorMessage == "O local do evento é obrigatório.");
    }

    [Fact]
    public void Local_Com501Chars_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Local = new string('R', 501);

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Local)
            && e.ErrorMessage == "O local não pode exceder 500 caracteres.");
    }

    [Fact]
    public void Local_ComLinkGoogleMaps_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Local = "https://maps.google.com/?q=Ibirapuera";

        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.Local));

        Assert.Empty(erros);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Descrição – limite de caracteres
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Descricao_Com2001Chars_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Descricao = new string('D', 2001);

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Descricao)
            && e.ErrorMessage == "A descrição não pode exceder 2000 caracteres.");
    }

    [Fact]
    public void Descricao_Nula_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Descricao = null;

        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.Descricao));

        Assert.Empty(erros);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Descrição – filtro de spam
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Evento especial! Clique aqui para saber mais.")]
    [InlineData("ganhe dinheiro fácil participando!")]
    [InlineData("Renda extra garantida neste show.")]
    [InlineData("Investimento garantido em diversão!")]
    [InlineData("Oportunidade única de ver sua banda favorita!")]
    public void Descricao_ComTermoSpam_DeveRetornarErro(string descricaoComSpam)
    {
        var modelo = ModeloValido();
        modelo.Descricao = descricaoComSpam;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Descricao)
            && e.ErrorMessage == "A descrição contém termos não permitidos. Revise o conteúdo.");
    }

    [Fact]
    public void Descricao_SemSpam_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Descricao = "Show de rock ao ar livre com três bandas locais. Traz sua cadeira e bebida.";

        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.Descricao));

        Assert.Empty(erros);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: GeneroMusical
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GeneroMusical_Vazio_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.GeneroMusical = string.Empty;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.GeneroMusical)
            && e.ErrorMessage == "Selecione o gênero musical do evento.");
    }

    [Theory]
    [InlineData("Rock")]
    [InlineData("MPB")]
    [InlineData("Forró")]
    [InlineData("Eletrônico")]
    public void GeneroMusical_Preenchido_NaoDeveRetornarErro(string genero)
    {
        var modelo = ModeloValido();
        modelo.GeneroMusical = genero;

        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.GeneroMusical));

        Assert.Empty(erros);
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

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Preco)
            && e.ErrorMessage == "Informe o preço do ingresso ou marque 'Evento gratuito'.");
    }

    [Fact]
    public void Preco_Negativo_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.Preco = -1m;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Preco)
            && e.ErrorMessage == "O preço não pode ser negativo.");
    }

    [Theory]
    [InlineData(50001)]
    [InlineData(100000)]
    public void Preco_AcimaDoMaximo_DeveRetornarErro(decimal preco)
    {
        var modelo = ModeloValido();
        modelo.Preco = preco;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Preco)
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

        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.Preco));

        Assert.Empty(erros);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CAMPO: Preço quando EventoGratuito = true
    // ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(0.01)]
    public void Preco_DifferenteDezeroQuandoGratuito_DeveRetornarErro(decimal preco)
    {
        var modelo = ModeloValido();
        modelo.EventoGratuito = true;
        modelo.Preco = preco;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.Preco)
            && e.ErrorMessage == "Evento gratuito não pode ter preço diferente de zero.");
    }

    [Fact]
    public void Preco_ZeroQuandoGratuito_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.EventoGratuito = true;
        modelo.Preco = 0;

        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.Preco));

        Assert.Empty(erros);
    }

    [Fact]
    public void Preco_NuloQuandoGratuito_NaoDeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.EventoGratuito = true;
        modelo.Preco = null;

        // Regra de preço obrigatório NÃO se aplica quando gratuito
        // Regra de preço != zero também NÃO dispara (Preco é null)
        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.Preco));

        Assert.Empty(erros);
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

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.CapacidadeMaxima)
            && e.ErrorMessage.Contains("mínima é de 1"));
    }

    [Fact]
    public void CapacidadeMaxima_AcimaDoLimite_DeveRetornarErro()
    {
        var modelo = ModeloValido();
        modelo.CapacidadeMaxima = 100_001;

        var resultado = _sut.Validate(modelo);

        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(EventoCreateDto.CapacidadeMaxima)
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

        var erros = _sut.Validate(modelo).Errors
            .Where(e => e.PropertyName == nameof(EventoCreateDto.CapacidadeMaxima));

        Assert.Empty(erros);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // VALIDAÇÃO COMPLETA
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ModeloCompleto_Valido_NaoDeveRetornarNenhumErro()
    {
        var modelo = ModeloValido();

        var resultado = _sut.Validate(modelo);

        Assert.True(resultado.IsValid,
            "Erros inesperados: " + string.Join(", ", resultado.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void ModeloCompleto_Invalido_DeveRetornarMultiplosErros()
    {
        // DTO totalmente inválido: todos os campos obrigatórios ausentes/errados
        var modelo = new EventoCreateDto
        {
            Nome             = "AB",          // muito curto
            DataHora         = DateTime.Now.AddHours(1),  // menos de 24 h
            Local            = string.Empty,  // obrigatório
            Descricao        = "Clique aqui", // spam
            GeneroMusical    = string.Empty,  // obrigatório
            Preco            = -5m,           // negativo
            EventoGratuito   = false,
            CapacidadeMaxima = 0              // abaixo do mínimo
        };

        var resultado = _sut.Validate(modelo);

        Assert.False(resultado.IsValid);
        Assert.True(resultado.Errors.Count >= 5,
            $"Esperava pelo menos 5 erros, obteve {resultado.Errors.Count}.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ValidarCampo – helper específico por campo
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidarCampo_Nome_DeveRetornarApenasErrosDoCampoNome()
    {
        var modelo = new EventoCreateDto { Nome = "X" };   // inválido

        var erros = _sut.ValidarCampo(modelo, nameof(EventoCreateDto.Nome));

        Assert.Single(erros);   // apenas "mínimo 3 chars" deve aparecer
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
