namespace src.DTOs;
 
public class ComprarIngressoDTO
{
    public int EventoId { get; set; }
    public string? CupomUtilizado { get; set; }
    /// <summary>
    /// Quando true, cobra 15% do preço do ingresso como seguro de devolução integral.
    /// Sem seguro, a devolução retorna apenas o valor do ingresso (sem a taxa de serviço).
    /// </summary>
    public bool ContratarSeguro { get; set; }
}
 