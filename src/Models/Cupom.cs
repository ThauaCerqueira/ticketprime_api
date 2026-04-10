namespace src.Models;

public class Cupom
{
    public string Codigo { get; set; } = string.Empty; 
    public decimal PorcentagemDesconto { get; set; }
    public decimal valorMinimoregra { get; set; } 
}