namespace src.Models;

public class Evento
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public int CapacidadeTotal { get; private set; }
    public DateTime DataEvento { get; private set; }
    public decimal PrecoPadrao { get; private set; }

    public Evento(string nome, int capacidadeTotal, DateTime dataEvento, decimal precoPadrao)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new Exception("O nome do evento é obrigatório.");
        if (dataEvento <= DateTime.Now) throw new Exception("A data do evento deve ser no futuro.");
        if (precoPadrao < 0) throw new Exception("O preço padrão deve ser um valor positivo.");
        if (capacidadeTotal <= 0) throw new Exception("A capacidade total deve ser um valor positivo.");

        Nome = nome;
        CapacidadeTotal = capacidadeTotal;
        DataEvento = dataEvento;
        PrecoPadrao = precoPadrao;
    }

    protected Evento() { }
}
