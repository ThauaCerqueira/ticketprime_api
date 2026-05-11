namespace src.DTOs;

public class PaginatedResult<T>
{
    public IEnumerable<T> Itens { get; set; } = [];
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
    public int TotalPaginas => (int)Math.Ceiling((double)Total / TamanhoPagina);
}
