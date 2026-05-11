using Dapper;
using src.Models;
using src.Infrastructure.IRepository;
using src.DTOs;

namespace src.Infrastructure.Repository;

public class ReservaRepository : IReservaRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public ReservaRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Reservation> CriarAsync(Reservation reserva)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"INSERT INTO Reservas (UsuarioCpf, EventoId, DataCompra, CupomUtilizado, ValorFinalPago,
                                          TaxaServicoPago, TemSeguro, ValorSeguroPago, CodigoIngresso, Status,
                                          EhMeiaEntrada, TicketTypeId, LoteId)
                    VALUES (@UsuarioCpf, @EventoId, GETDATE(), @CupomUtilizado, @ValorFinalPago,
                            @TaxaServicoPago, @TemSeguro, @ValorSeguroPago, @CodigoIngresso, 'Ativa',
                            @EhMeiaEntrada, @TicketTypeId, @LoteId);
                    SELECT CAST(SCOPE_IDENTITY() AS INT)";
        var id = await connection.QuerySingleAsync<int>(sql, reserva);
        reserva.Id = id;
        return reserva;
    }

    public async Task<IEnumerable<ReservationDetailDto>> ListarPorUsuarioAsync(string cpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT r.Id, r.UsuarioCpf, r.EventoId, r.DataCompra,
                           r.CupomUtilizado, r.ValorFinalPago,
                           r.TaxaServicoPago, r.TemSeguro, r.ValorSeguroPago,
                           r.CodigoIngresso, r.Status, r.EhMeiaEntrada,
                           r.TicketTypeId, r.LoteId,
                           r.CodigoTransacaoGateway, r.IdEstornoGateway, r.DataEstorno,
                           e.Nome, e.Local, e.DataEvento, e.DataTermino, e.PrecoPadrao,
                           ti.Nome AS TicketTypeNome,
                           l.Nome AS LoteNome
                    FROM Reservas r
                    INNER JOIN Eventos e ON e.Id = r.EventoId
                    LEFT JOIN TiposIngresso ti ON ti.Id = r.TicketTypeId
                    LEFT JOIN Lotes l ON l.Id = r.LoteId
                    WHERE r.UsuarioCpf = @Cpf
                    ORDER BY r.DataCompra DESC";
        return await connection.QueryAsync<ReservationDetailDto>(sql, new { Cpf = cpf });
    }

    public async Task<bool> CancelarAsync(int reservaId, string usuarioCpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE Reservas
                    SET Status = 'Cancelada',
                        DataCancelamento = GETUTCDATE(),
                        MotivoCancelamento = 'Cancelado pelo usuário'
                    WHERE Id = @ReservaId AND UsuarioCpf = @UsuarioCpf AND Status = 'Ativa'";
        var rows = await connection.ExecuteAsync(sql, new { ReservaId = reservaId, UsuarioCpf = usuarioCpf });
        return rows > 0;
    }

    public async Task<int> ContarReservasUsuarioPorEventoAsync(string usuarioCpf, int eventoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT COUNT(*) FROM Reservas
                    WHERE UsuarioCpf = @UsuarioCpf AND EventoId = @EventoId AND Status = 'Ativa'";
        return await connection.QuerySingleAsync<int>(sql, new { UsuarioCpf = usuarioCpf, EventoId = eventoId });
    }

    public async Task<int> ContarReservasUsuarioAsync(string usuarioCpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT COUNT(*) FROM Reservas
                    WHERE UsuarioCpf = @UsuarioCpf AND Status IN ('Ativa', 'Usada')";
        return await connection.QuerySingleAsync<int>(sql, new { UsuarioCpf = usuarioCpf });
    }

    public async Task<int> ContarReservasPorEventoAsync(int eventoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT COUNT(*) FROM Reservas
                    WHERE EventoId = @EventoId AND Status = 'Ativa'";
        return await connection.QuerySingleAsync<int>(sql, new { EventoId = eventoId });
    }

    public async Task<ReservationDetailDto?> ObterDetalhadaPorIdAsync(int reservaId, string usuarioCpf)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT r.Id, r.UsuarioCpf, r.EventoId, r.DataCompra,
                           r.CupomUtilizado, r.ValorFinalPago,
                           r.TaxaServicoPago, r.TemSeguro, r.ValorSeguroPago,
                           r.CodigoIngresso, r.Status, r.EhMeiaEntrada,
                           r.TicketTypeId, r.LoteId,
                           r.CodigoTransacaoGateway, r.IdEstornoGateway, r.DataEstorno,
                           e.Nome, e.Local, e.DataEvento, e.DataTermino, e.PrecoPadrao,
                           ti.Nome AS TicketTypeNome,
                           l.Nome AS LoteNome
                    FROM Reservas r
                    INNER JOIN Eventos e ON e.Id = r.EventoId
                    LEFT JOIN TiposIngresso ti ON ti.Id = r.TicketTypeId
                    LEFT JOIN Lotes l ON l.Id = r.LoteId
                    WHERE r.Id = @ReservaId AND r.UsuarioCpf = @UsuarioCpf";
        return await connection.QueryFirstOrDefaultAsync<ReservationDetailDto>(
            sql, new { ReservaId = reservaId, UsuarioCpf = usuarioCpf });
    }

    public async Task<ReservationDetailDto?> ObterPorCodigoIngressoAsync(string codigoIngresso)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT r.Id, r.UsuarioCpf, r.EventoId, r.DataCompra,
                           r.CupomUtilizado, r.ValorFinalPago,
                           r.TaxaServicoPago, r.TemSeguro, r.ValorSeguroPago,
                           r.CodigoIngresso, r.Status, r.DataCheckin, r.EhMeiaEntrada,
                           r.TicketTypeId, r.LoteId,
                           e.Nome, e.DataEvento, e.DataTermino, e.PrecoPadrao,
                           ti.Nome AS TicketTypeNome,
                           l.Nome AS LoteNome
                    FROM Reservas r
                    INNER JOIN Eventos e ON e.Id = r.EventoId
                    LEFT JOIN TiposIngresso ti ON ti.Id = r.TicketTypeId
                    LEFT JOIN Lotes l ON l.Id = r.LoteId
                    WHERE r.CodigoIngresso = @CodigoIngresso";
        return await connection.QueryFirstOrDefaultAsync<ReservationDetailDto>(
            sql, new { CodigoIngresso = codigoIngresso });
    }

    public async Task<int> ContarReservasAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM Reservas");
    }

    public async Task<bool> RealizarCheckinAsync(string codigoIngresso)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"UPDATE Reservas
                    SET DataCheckin = GETUTCDATE(),
                        Status = 'Usada'
                    WHERE CodigoIngresso = @CodigoIngresso
                      AND Status = 'Ativa'
                      AND DataCheckin IS NULL";
        var rows = await connection.ExecuteAsync(sql, new { CodigoIngresso = codigoIngresso });
        return rows > 0;
    }

    public async Task<decimal> ObterReceitaTotalAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<decimal>(
            "SELECT ISNULL(SUM(ValorFinalPago), 0) FROM Reservas WHERE Status = 'Ativa'");
    }

    public async Task<IEnumerable<string>> ObterEmailsUsuariosPorEventoAsync(int eventoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"SELECT u.Email FROM Reservas r
                    INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
                    WHERE r.EventoId = @EventoId AND r.Status = 'Ativa'";
        return await connection.QueryAsync<string>(sql, new { EventoId = eventoId });
    }

    // ── Dashboard Analytics ────────────────────────────────────────

    public async Task<IEnumerable<VendaPorPeriodoDTO>> ObterVendasPorPeriodoAsync(DateTime inicio, DateTime fim)
    {
        using var conn = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT
                YEAR(r.DataCompra) AS Ano,
                MONTH(r.DataCompra) AS Mes,
                FORMAT(r.DataCompra, 'yyyy-MM') AS Rotulo,
                COUNT(*) AS Quantidade,
                ISNULL(SUM(r.ValorFinalPago), 0) AS Receita
            FROM Reservas r
            WHERE r.DataCompra >= @Inicio
              AND r.DataCompra <= @Fim
            GROUP BY YEAR(r.DataCompra), MONTH(r.DataCompra), FORMAT(r.DataCompra, 'yyyy-MM')
            ORDER BY Ano ASC, Mes ASC";
        return await conn.QueryAsync<VendaPorPeriodoDTO>(sql,
            new { Inicio = inicio, Fim = fim });
    }

    public async Task<IEnumerable<EventoMaisVendidoDTO>> ObterEventosMaisVendidosAsync(int top = 10)
    {
        using var conn = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT TOP(@Top)
                e.Id AS EventoId,
                e.Nome,
                e.DataEvento,
                COUNT(r.Id) AS IngressosVendidos,
                ISNULL(SUM(r.ValorFinalPago), 0) AS ReceitaGerada,
                e.CapacidadeTotal
            FROM Eventos e
            LEFT JOIN Reservas r ON r.EventoId = e.Id AND r.Status IN ('Ativa', 'Usada')
            GROUP BY e.Id, e.Nome, e.DataEvento, e.CapacidadeTotal
            ORDER BY IngressosVendidos DESC";
        return await conn.QueryAsync<EventoMaisVendidoDTO>(sql, new { Top = top });
    }

    public async Task<CancelamentoStatsDTO> ObterCancelamentoStatsAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT
                COUNT(*) AS TotalReservas,
                ISNULL(SUM(CASE WHEN Status = 'Cancelada' THEN 1 ELSE 0 END), 0) AS TotalCanceladas,
                ISNULL(SUM(CASE WHEN Status = 'Ativa' THEN 1 ELSE 0 END), 0) AS TotalAtivas,
                ISNULL(SUM(CASE WHEN Status = 'Usada' THEN 1 ELSE 0 END), 0) AS TotalUsadas,
                ISNULL(SUM(CASE WHEN Status = 'Cancelada' THEN ValorFinalPago ELSE 0 END), 0) AS ReceitaPerdidaCancelamentos
            FROM Reservas";
        return await conn.QuerySingleAsync<CancelamentoStatsDTO>(sql);
    }

    public async Task<RelatorioFinanceiroDTO> ObterRelatorioFinanceiroAsync(DateTime? inicio = null, DateTime? fim = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT
                ISNULL(SUM(r.ValorFinalPago), 0) AS ReceitaBruta,
                ISNULL(SUM(r.TaxaServicoPago), 0) AS TaxasServico,
                ISNULL(SUM(r.ValorSeguroPago), 0) AS SegurosContratados,
                ISNULL(SUM(CASE WHEN r.CupomUtilizado IS NOT NULL THEN (e.PrecoPadrao - r.ValorFinalPago - r.TaxaServicoPago - r.ValorSeguroPago) ELSE 0 END), 0) AS DescontosConcedidos,
                COUNT(*) AS TotalIngressosVendidos,
                ISNULL(SUM(CASE WHEN r.CupomUtilizado IS NOT NULL THEN 1 ELSE 0 END), 0) AS CuponsUtilizados
            FROM Reservas r
            INNER JOIN Eventos e ON e.Id = r.EventoId
            WHERE r.DataCompra >= @Inicio AND r.DataCompra <= @Fim";
        return await conn.QuerySingleAsync<RelatorioFinanceiroDTO>(sql,
            new
            {
                Inicio = inicio ?? new DateTime(2000, 1, 1),
                Fim = fim ?? DateTime.UtcNow.AddYears(1)
            });
    }

    public async Task<IEnumerable<DemandaLocalDTO>> ObterDemandaPorLocalAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT
                e.Local,
                COUNT(DISTINCT e.Id) AS TotalEventos,
                ISNULL(SUM(CASE WHEN r.Status IN ('Ativa', 'Usada') THEN 1 ELSE 0 END), 0) AS IngressosVendidos,
                ISNULL(SUM(CASE WHEN r.Status IN ('Ativa', 'Usada') THEN r.ValorFinalPago ELSE 0 END), 0) AS ReceitaGerada
            FROM Eventos e
            LEFT JOIN Reservas r ON r.EventoId = e.Id
            GROUP BY e.Local
            ORDER BY IngressosVendidos DESC";
        return await conn.QueryAsync<DemandaLocalDTO>(sql);
    }

    public async Task<IEnumerable<RelatorioFinanceiroLinhaDTO>> ObterLinhasRelatorioFinanceiroAsync(DateTime? inicio = null, DateTime? fim = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT
                r.Id AS ReservaId,
                e.Nome AS EventoNome,
                r.DataCompra,
                r.UsuarioCpf,
                u.Nome AS UsuarioNome,
                e.PrecoPadrao AS ValorIngresso,
                CASE WHEN r.CupomUtilizado IS NOT NULL
                    THEN (e.PrecoPadrao - r.ValorFinalPago - r.TaxaServicoPago - r.ValorSeguroPago)
                    ELSE 0 END AS Desconto,
                r.TaxaServicoPago AS TaxaServico,
                r.ValorSeguroPago AS Seguro,
                r.ValorFinalPago AS ValorPago,
                r.CupomUtilizado AS Cupom,
                r.Status
            FROM Reservas r
            INNER JOIN Eventos e ON e.Id = r.EventoId
            INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
            WHERE r.DataCompra >= @Inicio AND r.DataCompra <= @Fim
            ORDER BY r.DataCompra DESC";
        return await conn.QueryAsync<RelatorioFinanceiroLinhaDTO>(sql,
            new
            {
                Inicio = inicio ?? new DateTime(2000, 1, 1),
                Fim = fim ?? DateTime.UtcNow.AddYears(1)
            });
    }

    public async Task<bool> TransferirAsync(int reservaId, string cpfRemetente, string cpfDestinatario)
    {
        using var conn = _connectionFactory.CreateConnection();
        var rows = await conn.ExecuteAsync(
            @"UPDATE Reservas
              SET UsuarioCpf = @CpfDestinatario
              WHERE Id = @ReservaId
                AND UsuarioCpf = @CpfRemetente
                AND Status = 'Ativa'",
            new { ReservaId = reservaId, CpfRemetente = cpfRemetente, CpfDestinatario = cpfDestinatario });
        return rows > 0;
    }

    public async Task<IEnumerable<ParticipanteDto>> ListarParticipantesPorEventoAsync(int eventoId)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<ParticipanteDto>(
            @"SELECT
                r.CodigoIngresso,
                u.Nome    AS NomeParticipante,
                -- CPF mascarado: oculta 3 primeiros e últimos 2 dígitos
                CONCAT('***.',
                       SUBSTRING(r.UsuarioCpf, 4, 3), '.',
                       SUBSTRING(r.UsuarioCpf, 7, 3), '-**')   AS Cpf,
                u.Email,
                ISNULL(ti.Nome, 'Geral')                         AS Setor,
                r.ValorFinalPago                                  AS ValorPago,
                r.Status,
                r.DataCompra,
                r.DataCheckin,
                ISNULL(r.MeiaEntrada, 0)                         AS MeiaEntrada,
                ISNULL(r.TemSeguro, 0)                           AS TemSeguro
              FROM Reservas r
              INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
              LEFT  JOIN TiposIngresso ti ON ti.Id = r.TipoIngressoId
              WHERE r.EventoId = @EventoId
              ORDER BY r.DataCompra",
            new { EventoId = eventoId });
    }
}
