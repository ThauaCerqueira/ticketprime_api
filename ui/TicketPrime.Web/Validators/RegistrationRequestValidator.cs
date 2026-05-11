using FluentValidation;
using TicketPrime.Web.Models;

namespace TicketPrime.Web.Validators;

/// <summary>
/// Validador FluentValidation para RegistrationRequest (cadastro de usuário).
/// Centraliza as regras de validação do formulário de criação de conta.
/// </summary>
public class RegistrationRequestValidator : AbstractValidator<RegistrationRequest>
{
    public RegistrationRequestValidator()
    {
        // ── CPF ─────────────────────────────────────────────────────────────────
        RuleFor(x => x.Cpf)
            .NotEmpty()
                .WithMessage("O CPF é obrigatório.")
            .Length(11)
                .WithMessage("CPF deve ter exatamente 11 dígitos.")
            .Must(ConterApenasDigitos)
                .WithMessage("CPF deve conter apenas números.");

        // ── Nome ────────────────────────────────────────────────────────────────
        RuleFor(x => x.Nome)
            .NotEmpty()
                .WithMessage("O nome é obrigatório.")
            .MinimumLength(3)
                .WithMessage("O nome deve ter pelo menos 3 caracteres.")
            .MaximumLength(100)
                .WithMessage("O nome não pode exceder 100 caracteres.");

        // ── E-mail ──────────────────────────────────────────────────────────────
        RuleFor(x => x.Email)
            .NotEmpty()
                .WithMessage("O e-mail é obrigatório.")
            .EmailAddress()
                .WithMessage("Informe um e-mail válido.")
            .MaximumLength(200)
                .WithMessage("O e-mail não pode exceder 200 caracteres.");

        // ── Senha ───────────────────────────────────────────────────────────────
        RuleFor(x => x.Senha)
            .NotEmpty()
                .WithMessage("A senha é obrigatória.")
            .MinimumLength(8)
                .WithMessage("A senha deve ter no mínimo 8 caracteres.")
            .MaximumLength(100)
                .WithMessage("A senha não pode exceder 100 caracteres.");
    }

    private static bool ConterApenasDigitos(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;
        return valor.All(char.IsDigit);
    }
}
