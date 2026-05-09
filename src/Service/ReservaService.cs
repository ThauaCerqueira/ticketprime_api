using src.Models;
using src.Infrastructure.IRepository;
using src.DTOs;
 
namespace src.Service;
 
public class ReservaService
{
    private readonly IReservaRepository _reservaRepository;
    private readonly IEventoRepository _eventoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ICupomRepository _cupomRepository;
 
    public ReservaService(
        IReservaRepository reservaRepository,
        IEventoRepository eventoRepository,
        IUsuarioRepository usuarioRepository,
        ICupomRepository cupomRepository)
    {
        _reservaRepository = reservaRepository;
        _eventoRepository = eventoRepository;
        _usuarioRepository = usuarioRepository;
        _cupomRepository = cupomRepository;
    }
 
    public async Task<Reserva> ComprarIngressoAsync(string usuarioCpf, int eventoId, string? cupomUtilizado = null, bool contratarSeguro = false)
    {
        // R1 — Validação de Integridade
        var usuario = await _usuarioRepository.ObterPorCpf(usuarioCpf);
        if (usuario == null)
            throw new InvalidOperationException("Usuário não encontrado.");

        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);
        if (evento == null)
            throw new InvalidOperationException("Evento não encontrado.");

        if (evento.DataEvento <= DateTime.Now)
            throw new InvalidOperationException("Este evento já aconteceu.");

        // R2 — Limite por CPF (máximo 2 reservas por CPF por mesmo evento)
        var reservasCpf = await _reservaRepository.ContarReservasUsuarioPorEventoAsync(usuarioCpf, eventoId);
        if (reservasCpf >= 2)
            throw new InvalidOperationException("Você já atingiu o limite de 2 reservas para este evento.");

        // R3 — Controle de Estoque
        var totalReservas = await _reservaRepository.ContarReservasPorEventoAsync(eventoId);
        if (totalReservas >= evento.CapacidadeTotal)
            throw new InvalidOperationException("Não há mais vagas disponíveis para este evento.");

        // R4 — Motor de Cupons
        decimal valorIngresso = evento.PrecoPadrao;

        if (!string.IsNullOrEmpty(cupomUtilizado))
        {
            var cupom = await _cupomRepository.ObterPorCodigoAsync(cupomUtilizado);
            if (cupom == null)
                throw new InvalidOperationException("Cupom inválido ou inexistente.");

            // Desconto só é aplicado se o preço do evento for >= valor mínimo do cupom
            if (evento.PrecoPadrao >= cupom.ValorMinimoRegra)
            {
                var desconto = evento.PrecoPadrao * (cupom.PorcentagemDesconto / 100);
                valorIngresso = evento.PrecoPadrao - desconto;
            }
        }

        // R5 — Taxa de serviço (fixa por ingresso, não devolvida sem seguro)
        decimal taxaServico = evento.TaxaServico;

        // R6 — Seguro de devolução integral (opcional, 15% do preço original do ingresso)
        decimal valorSeguro = contratarSeguro ? evento.PrecoPadrao * 0.15m : 0m;

        var reserva = new Reserva
        {
            UsuarioCpf       = usuarioCpf,
            EventoId         = eventoId,
            CupomUtilizado   = cupomUtilizado,
            TaxaServicoPago  = taxaServico,
            TemSeguro        = contratarSeguro,
            ValorSeguroPago  = valorSeguro,
            ValorFinalPago   = valorIngresso + taxaServico + valorSeguro
        };

        return await _reservaRepository.CriarAsync(reserva);
    }
 
    public async Task<IEnumerable<ReservaDetalhadaDTO>> ListarReservasUsuarioAsync(string cpf)
    {
        return await _reservaRepository.ListarPorUsuarioAsync(cpf);
    }
 
    public async Task CancelarIngressoAsync(int reservaId, string usuarioCpf)
    {
        var reserva = await _reservaRepository.ObterDetalhadaPorIdAsync(reservaId, usuarioCpf)
            ?? throw new InvalidOperationException("Reserva não encontrada.");

        var cancelou = await _reservaRepository.CancelarAsync(reservaId, usuarioCpf);

        if (!cancelou)
            throw new InvalidOperationException("Não foi possível cancelar a reserva.");

        await _eventoRepository.AumentarCapacidadeAsync(reserva.EventoId);
    }
}
 