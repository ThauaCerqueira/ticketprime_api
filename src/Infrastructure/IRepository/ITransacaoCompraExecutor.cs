using src.Models;

namespace src.Infrastructure.IRepository;

/// <summary>
/// Executa a transação SQL atômica de compra de ingresso (R2, R3, cupom, INSERT Reservas).
/// Separada em interface para permitir mocking em testes de unidade.
/// </summary>
public interface ITransacaoCompraExecutor
{
    Task<Reservation> ExecutarTransacaoAsync(
        Reservation reserva,
        TicketEvent evento,
        string? cupomUtilizado,
        bool aplicarDesconto,
        int ticketTypeId,
        int? loteId = null);
}
