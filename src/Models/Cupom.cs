namespace src.Models;

public class Cupom
{
    public required string Codigo { get; set; } 
    public required decimal PorcentagemDesconto { get; set; }
    public required decimal valorMinimoregra { get; set; } 
}