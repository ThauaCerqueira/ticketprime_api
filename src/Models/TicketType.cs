namespace src.Models;

/// <summary>
/// Representa um tipo/setor de ingresso dentro de um evento (ex: Pista, VIP, Camarote, Área Premium).
/// Cada tipo tem seu próprio nome, preço e capacidade.
/// </summary>
public class TicketType
{
    public int Id { get; set; }

    /// <summary>ID do evento ao qual este tipo de ingresso pertence.</summary>
    public int EventoId { get; set; }

    /// <summary>Nome do tipo/setor (ex: "Pista", "VIP", "Camarote", "Arquibancada").</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Descrição opcional do setor (ex: "Setor atrás do palco").</summary>
    public string? Descricao { get; set; }

    /// <summary>Preço base deste tipo de ingresso (pode ser sobrescrito por lotes).</summary>
    public decimal Preco { get; set; }

    /// <summary>Capacidade total deste setor.</summary>
    public int CapacidadeTotal { get; set; }

    /// <summary>Vagas restantes (capacidade total - reservas ativas).</summary>
    public int CapacidadeRestante { get; set; }

    /// <summary>Ordem de exibição (menor = primeiro).</summary>
    public int Ordem { get; set; }

    public TicketType() { }

    public TicketType(string nome, decimal preco, int capacidadeTotal, int ordem = 0)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("O nome do tipo de ingresso é obrigatório.");
        if (preco < 0)
            throw new ArgumentException("O preço deve ser um valor positivo.");
        if (capacidadeTotal <= 0)
            throw new ArgumentException("A capacidade total deve ser um valor positivo.");

        Nome = nome;
        Preco = preco;
        CapacidadeTotal = capacidadeTotal;
        CapacidadeRestante = capacidadeTotal;
        Ordem = ordem;
    }
}
