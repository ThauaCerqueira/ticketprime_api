using System.Net.Http.Json;
using TicketPrime.Web.Shared.Models;

namespace TicketPrime.Web.Client.Services;

/// <summary>
/// Serviço frontend para operações com cupons via API REST.
/// Substitui a referência direta a src.Service.CupomService.
/// </summary>
public class CupomService
{
    private readonly HttpClient _http;

    public CupomService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Coupon>> ListarAsync()
    {
        var result = await _http.GetFromJsonAsync<List<Coupon>>("api/cupons");
        return result ?? [];
    }

    public async Task<bool> DeletarAsync(string codigo)
    {
        var response = await _http.DeleteAsync($"api/cupons/{Uri.EscapeDataString(codigo)}");
        return response.IsSuccessStatusCode;
    }
}
