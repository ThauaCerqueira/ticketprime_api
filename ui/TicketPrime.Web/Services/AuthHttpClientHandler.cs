using System.Net.Http.Headers;

namespace TicketPrime.Web.Services;

/// <summary>
/// Adiciona automaticamente o token JWT ao header Authorization de todas as requisições HTTP.
/// </summary>
public class AuthHttpClientHandler : DelegatingHandler
{
    private readonly SessionService _sessionService;

    public AuthHttpClientHandler(SessionService sessionService)
    {
        _sessionService = sessionService;
        InnerHandler ??= new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Adicionar token ao header se usuário está logado
        if (!string.IsNullOrEmpty(_sessionService.Token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _sessionService.Token);
        }

        // Propagar requisição
        return await base.SendAsync(request, cancellationToken);
    }
}
