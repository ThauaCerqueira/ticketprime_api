using Dapper;
using src.Infrastructure.IRepository;
using src.Models;

namespace src.Infrastructure.Repository;

public sealed class TransacaoCompraExecutor : ITransacaoCompraExecutor
{
    private readonly DbConnectionFactory _connectionFactory;

    public TransacaoCompraExecutor(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Reservation> ExecutarTransacaoAsync(
        Reservation reserva,
        TicketEvent evento,
        string? cupomUtilizado,
        bool aplicarDesconto,
        int ticketTypeId,
        int? loteId = null)
    {
        // R2 + R3 + Incremento cupom + Criação de reserva — transação SQL atômica com UPDLOCK
        // para evitar race condition no limite por CPF e no estoque de vagas.
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // R2 - Verificação do limite por CPF dentro da transação com UPDLOCK
            var reservasCpf = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM Reservas WITH (UPDLOCK) WHERE UsuarioCpf = @Cpf AND EventoId = @EventoId",
                new { Cpf = reserva.UsuarioCpf, EventoId = reserva.EventoId }, transaction);

            if (reservasCpf >= evento.LimiteIngressosPorUsuario)
                throw new InvalidOperationException(
                    $"Você já atingiu o limite de {evento.LimiteIngressosPorUsuario} reservas para este evento.");

            // R3 - Decremento de vagas do tipo de ingresso (setor) específico
            var rows = await connection.ExecuteAsync(
                @"UPDATE TiposIngresso SET CapacidadeRestante = CapacidadeRestante - 1
                  WHERE Id = @TicketTypeId AND CapacidadeRestante > 0",
                new { TicketTypeId = ticketTypeId }, transaction);

            if (rows == 0)
                throw new InvalidOperationException("Não há mais vagas disponíveis para este setor.");

            // R3a - Decremento de vagas do evento (contador geral de disponibilidade)
            await connection.ExecuteAsync(
                @"UPDATE Eventos SET CapacidadeRestante = CapacidadeRestante - 1
                  WHERE Id = @EventoId AND CapacidadeRestante > 0",
                new { EventoId = reserva.EventoId }, transaction);

            // R3b - Se um lote foi selecionado, incrementa a quantidade vendida do lote
            if (loteId.HasValue)
            {
                await connection.ExecuteAsync(
                    @"UPDATE Lotes SET QuantidadeVendida = QuantidadeVendida + 1
                      WHERE Id = @LoteId AND QuantidadeVendida < QuantidadeMaxima",
                    new { LoteId = loteId.Value }, transaction);
            }

            if (aplicarDesconto)
            {
                await connection.ExecuteAsync(
                    "UPDATE Cupons SET TotalUsado = TotalUsado + 1 WHERE Codigo = @Codigo",
                    new { Codigo = cupomUtilizado }, transaction);
            }

            const string insertSql = @"
                INSERT INTO Reservas (UsuarioCpf, EventoId, DataCompra, CupomUtilizado, ValorFinalPago,
                                      TaxaServicoPago, TemSeguro, ValorSeguroPago, CodigoIngresso,
                                      EhMeiaEntrada, TicketTypeId, LoteId, CodigoTransacaoGateway,
                                      IdempotencyKey, StatusPagamento)
                VALUES (@UsuarioCpf, @EventoId, GETDATE(), @CupomUtilizado, @ValorFinalPago,
                        @TaxaServicoPago, @TemSeguro, @ValorSeguroPago, @CodigoIngresso,
                        @EhMeiaEntrada, @TicketTypeId, @LoteId, @CodigoTransacaoGateway,
                        @IdempotencyKey, @StatusPagamento);
                SELECT CAST(SCOPE_IDENTITY() AS INT)";

            var id = await connection.QuerySingleAsync<int>(insertSql, reserva, transaction);
            reserva.Id = id;

            transaction.Commit();
            return reserva;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
