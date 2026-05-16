using FluentValidation;
using TicketPrime.Web.Shared.Models;

namespace TicketPrime.Web.Client.Validators;

/// <summary>
/// Validador FluentValidation para EventoCreateDto.
/// Todas as mensagens estão em português.
/// </summary>
public class EventoCreateDtoValidator : AbstractValidator<EventoCreateDto>
{
    // ── Regras de negócio ──────────────────────────────────────────────────────
    private const int    AntecedenciaMinimaHoras = 24;
    private const decimal PrecoMaximo            = 50_000m;
    private const int    CapacidadeMinima        = 1;
    private const int    CapacidadeMaxima        = 100_000;

    // Palavras proibidas (spam / conteúdo impróprio – lista extensível)
    private static readonly HashSet<string> _palavrasProibidas =
        ["grátis", "promoção imperdível", "clique aqui", "ganhe dinheiro",
         "renda extra", "investimento garantido", "faça agora", "não perca",
         "oportunidade única", "xxx", "18+", "adulto"];

    public EventoCreateDtoValidator()
    {
        // ── Nome ────────────────────────────────────────────────────────────────
        RuleFor(x => x.Nome)
            .NotEmpty()
                .WithMessage("O nome do evento é obrigatório.")
            .MinimumLength(3)
                .WithMessage("O nome deve ter pelo menos 3 caracteres.")
            .MaximumLength(200)
                .WithMessage("O nome não pode exceder 200 caracteres.");

        // ── Data/hora – antecedência mínima de 24 h ─────────────────────────────
        RuleFor(x => x.DataHora)
            .NotNull()
                .WithMessage("A data e hora são obrigatórias.")
            .Must(d => d.HasValue && d.Value >= DateTime.Now.AddHours(AntecedenciaMinimaHoras))
                .WithMessage($"O evento deve ser agendado com pelo menos {AntecedenciaMinimaHoras} horas de antecedência.")
            .When(x => x.DataHora.HasValue, ApplyConditionTo.CurrentValidator);

        // ── Local ───────────────────────────────────────────────────────────────
        RuleFor(x => x.Local)
            .NotEmpty()
                .WithMessage("O local do evento é obrigatório.")
            .MaximumLength(500)
                .WithMessage("O local não pode exceder 500 caracteres.");

        // ── Descrição – limite de caracteres + filtro de spam ───────────────────
        RuleFor(x => x.Descricao)
            .MaximumLength(2000)
                .WithMessage("A descrição não pode exceder 2000 caracteres.")
            .Must(NaoConterSpam)
                .WithMessage("A descrição contém termos não permitidos. Revise o conteúdo.")
            .When(x => !string.IsNullOrEmpty(x.Descricao));

        // ── Gênero musical ──────────────────────────────────────────────────────
        RuleFor(x => x.GeneroMusical)
            .NotEmpty()
                .WithMessage("Selecione o gênero musical do evento.");

        // ── Preço – quando pago ──────────────────────────────────────────────────
        RuleFor(x => x.Preco)
            .NotNull()
                .WithMessage("Informe o preço do ingresso ou marque 'Evento gratuito'.")
            .GreaterThanOrEqualTo(0)
                .WithMessage("O preço não pode ser negativo.")
            .LessThanOrEqualTo(PrecoMaximo)
                .WithMessage($"O preço não pode exceder R$ {PrecoMaximo:N0}.")
            .When(x => !x.EventoGratuito);

        // ── Preço = zero quando gratuito ────────────────────────────────────────
        RuleFor(x => x.Preco)
            .Must(p => p is null or 0)
                .WithMessage("Evento gratuito não pode ter preço diferente de zero.")
            .When(x => x.EventoGratuito && x.Preco.HasValue);
        // ── Taxa de serviço – máximo 5% do preço do ingresso ────────────────────
        RuleFor(x => x.TaxaServico)
            .GreaterThanOrEqualTo(0)
                .WithMessage("A taxa de serviço não pode ser negativa.")
            .Must((dto, taxa) =>
                !taxa.HasValue || dto.Preco is null or 0 || taxa.Value <= dto.Preco.Value * 0.05m)
                .WithMessage("A taxa de serviço não pode exceder 5% do preço do ingresso.")
            .When(x => !x.EventoGratuito);

        RuleFor(x => x.TaxaServico)
            .Must(t => t is null or 0)
                .WithMessage("Evento gratuito não pode cobrar taxa de serviço.")
            .When(x => x.EventoGratuito && x.TaxaServico.HasValue && x.TaxaServico > 0);
        // ── Capacidade máxima ───────────────────────────────────────────────────
        RuleFor(x => x.CapacidadeMaxima)
            .GreaterThanOrEqualTo(CapacidadeMinima)
                .WithMessage($"A capacidade mínima é de {CapacidadeMinima} participante.")
            .LessThanOrEqualTo(CapacidadeMaxima)
                .WithMessage($"A capacidade não pode exceder {CapacidadeMaxima:N0} participantes.");
    }

    // ── Métodos auxiliares ─────────────────────────────────────────────────────

    private static bool NaoConterSpam(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return true;
        var lower = texto.ToLowerInvariant();
        return !_palavrasProibidas.Any(lower.Contains);
    }

    /// <summary>
    /// Valida apenas um campo específico e retorna as mensagens de erro.
    /// Usado pelos delegates de validação do MudBlazor.
    /// </summary>
    public IEnumerable<string> ValidarCampo(EventoCreateDto model, string nomeCampo)
    {
        var ctx = ValidationContext<EventoCreateDto>.CreateWithOptions(
            model,
            opts => opts.IncludeProperties(nomeCampo));

        return Validate(ctx).Errors.Select(e => e.ErrorMessage);
    }
}
