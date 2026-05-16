using src.Models;
using src.DTOs;

namespace src.Infrastructure.IRepository;

public interface IReservaRepository
{
    Task<Reservation> CriarAsync(Reservation reserva);
    Task<IEnumerable<ReservationDetailDto>> ListarPorUsuarioAsync(string cpf);
    Task<bool> CancelarAsync(int reservaId, string usuarioCpf);
    Task<int> ContarReservasUsuarioPorEventoAsync(string usuarioCpf, int eventoId);

    /// <summary>
    /// Conta todas as reservas (compras) de um usuário em qualquer evento.
    /// Usado para validar cupons de primeiro acesso.
    /// </summary>
    Task<int> ContarReservasUsuarioAsync(string usuarioCpf);
    Task<int> ContarReservasPorEventoAsync(int eventoId);
    Task<ReservationDetailDto?> ObterDetalhadaPorIdAsync(int reservaId, string usuarioCpf);
    Task<ReservationDetailDto?> ObterPorCodigoIngressoAsync(string codigoIngresso);
    Task<decimal> ObterReceitaTotalAsync();

    /// <summary>
    /// Conta o total de reservas (ingressos vendidos) em todo o sistema.
    /// </summary>
    Task<int> ContarReservasAsync();

    /// <summary>
    /// Realiza o check-in de um ingresso: marca como usado e registra a data/hora.
    /// Retorna true se o check-in foi realizado, false se o ingresso não existe ou já foi usado.
    /// </summary>
    Task<bool> RealizarCheckinAsync(string codigoIngresso);

    /// <summary>
    /// Retorna a lista de emails dos usuários com reservas ativas em um evento.
    /// Usado para notificações de alteração/cancelamento de eventos.
    /// </summary>
    Task<IEnumerable<string>> ObterEmailsUsuariosPorEventoAsync(int eventoId);

    // ── Dashboard Analytics ────────────────────────────────────────

    /// <summary>
    /// Vendas agrupadas por mês em um intervalo de datas.
    /// </summary>
    Task<IEnumerable<VendaPorPeriodoDTO>> ObterVendasPorPeriodoAsync(DateTime inicio, DateTime fim);

    /// <summary>
    /// Top N eventos mais vendidos por quantidade de ingressos.
    /// </summary>
    Task<IEnumerable<EventoMaisVendidoDTO>> ObterEventosMaisVendidosAsync(int top = 10);

    /// <summary>
    /// Estatísticas de cancelamento (total, canceladas, taxa, receita perdida).
    /// </summary>
    Task<CancelamentoStatsDTO> ObterCancelamentoStatsAsync();

    /// <summary>
    /// Relatório financeiro completo: receita, taxas, seguros, descontos.
    /// </summary>
    Task<RelatorioFinanceiroDTO> ObterRelatorioFinanceiroAsync(DateTime? inicio = null, DateTime? fim = null);

    /// <summary>
    /// Demanda agregada por local (cidade/região) para mapa de calor.
    /// </summary>
    Task<IEnumerable<DemandaLocalDTO>> ObterDemandaPorLocalAsync();

    /// <summary>
    /// Exporta linhas do relatório financeiro para CSV.
    /// </summary>
    Task<IEnumerable<RelatorioFinanceiroLinhaDTO>> ObterLinhasRelatorioFinanceiroAsync(DateTime? inicio = null, DateTime? fim = null);

    /// <summary>
    /// Transfere a propriedade do ingresso para outro usuário (por CPF).
    /// Retorna true se a transferência foi realizada, false se o ingresso não existe ou não pertence ao remetente.
    /// </summary>
    Task<bool> TransferirAsync(int reservaId, string cpfRemetente, string cpfDestinatario);

    /// <summary>
    /// Retorna lista detalhada de participantes de um evento para exportação CSV pelo admin.
    /// </summary>
    Task<IEnumerable<ParticipanteDto>> ListarParticipantesPorEventoAsync(int eventoId);

    /// <summary>
    /// Busca uma reserva pelo código de transação do gateway de pagamento (MercadoPago).
    /// Usado pelo webhook de confirmação de pagamento.
    /// </summary>
    Task<Reservation?> ObterPorCodigoTransacaoAsync(string codigoTransacao);

    /// <summary>
    /// Atualiza o status de pagamento de uma reserva (ex: 'approved', 'rejected', 'refunded').
    /// Usado pelo webhook de confirmação de pagamento.
    /// </summary>
    Task AtualizarStatusPagamentoAsync(int reservaId, string statusPagamento);

    /// <summary>
    /// Conta quantas reservas ATIVAS referenciam um determinado cupom.
    /// Usado para impedir deleção de cupons com reservas ativas.
    /// </summary>
    Task<int> ContarReservasPorCupomAsync(string codigoCupom);
}
