using src.Infrastructure.IRepository;
using src.Models;

namespace src.Service;

public class AvaliacaoService
{
    private readonly IAvaliacaoRepository _avaliacaoRepo;
    private readonly IReservaRepository _reservaRepo;

    public AvaliacaoService(IAvaliacaoRepository avaliacaoRepo, IReservaRepository reservaRepo)
    {
        _avaliacaoRepo = avaliacaoRepo;
        _reservaRepo = reservaRepo;
    }

    /// <summary>
    /// Creates a review for an event. The user must have a 'Usada' ticket for that event
    /// and must not have reviewed it before.
    /// </summary>
    public async Task AvaliarAsync(string usuarioCpf, int eventoId, byte nota, string? comentario, bool anonima = false)
    {
        if (nota < 1 || nota > 5)
            throw new ArgumentException("A nota deve ser entre 1 e 5.");

        // Validate user attended the event
        var reservas = await _reservaRepo.ListarPorUsuarioAsync(usuarioCpf);
        var assistiu = reservas.Any(r =>
            r.EventoId == eventoId &&
            string.Equals(r.Status, "Usada", StringComparison.OrdinalIgnoreCase));

        if (!assistiu)
            throw new InvalidOperationException("Você só pode avaliar eventos cujo ingresso foi utilizado.");

        if (await _avaliacaoRepo.UsuarioJaAvaliouAsync(usuarioCpf, eventoId))
            throw new InvalidOperationException("Você já avaliou este evento.");

        if (!string.IsNullOrWhiteSpace(comentario) && comentario.Length > 1000)
            comentario = comentario[..1000];

        await _avaliacaoRepo.CriarAsync(new Avaliacao
        {
            UsuarioCpf = usuarioCpf,
            EventoId = eventoId,
            Nota = nota,
            Comentario = comentario?.Trim(),
            Anonima = anonima
        });
    }
}
