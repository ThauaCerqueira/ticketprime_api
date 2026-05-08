namespace src.Models;

public class Evento
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int CapacidadeTotal { get; set; }
    public DateTime DataEvento { get; set; }
    public decimal PrecoPadrao { get; set; }
    public int LimiteIngressosPorUsuario { get; set; } = 6;

    public Evento(string nome, int capacidadeTotal, DateTime dataEvento, decimal precoPadrao, int limiteIngressosPorUsuario = 6)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("O nome do evento é obrigatório.");
        if (dataEvento <= DateTime.Now) throw new Exception("A data do evento deve ser no futuro.");
        if (precoPadrao < 0) throw new Exception("O preço padrão deve ser um valor positivo.");
        if (capacidadeTotal <= 0) throw new Exception("A capacidade total deve ser um valor positivo.");
        if (limiteIngressosPorUsuario <= 0) throw new Exception("O limite de ingressos por usuário deve ser um valor positivo.");

        Nome = nome;
        CapacidadeTotal = capacidadeTotal;
        DataEvento = dataEvento;
        PrecoPadrao = precoPadrao;
        LimiteIngressosPorUsuario = limiteIngressosPorUsuario;
    }

    public Evento() { }
}