using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace TicketPrime.Web.Services;

/// <summary>
/// Gerencia a sessão do usuário com persistência cifrada no localStorage via
/// ProtectedLocalStorage (DPAPI/DataProtection — previne leitura em plain-text
/// no DevTools do browser).
///
/// - No login, armazena o refresh token + dados básicos do usuário.
/// - Em caso de refresh (F5), tenta reautenticar automaticamente.
/// - Se o refresh token expirou, o usuário é redirecionado ao login normalmente.
/// </summary>
public class SessionService
{
    private readonly ProtectedLocalStorage _protectedStorage;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string KeyRefreshToken = "tp-refresh-token";
    private const string KeyUserInfo     = "tp-user-info";

    public string? Cpf { get; private set; }
    public string? Nome { get; private set; }
    public string? Perfil { get; private set; }
    public string? Token { get; private set; }
    public bool EstaLogado => !string.IsNullOrEmpty(Token);
    public bool EhAdmin => Perfil == "ADMIN";

    /// <summary>
    /// Indica que o usuário está autenticado com senha temporária e DEVE trocar a senha
    /// antes de acessar qualquer outra funcionalidade.
    /// </summary>
    public bool DeveTrocarSenha { get; private set; }

    public void MarcarSenhaTemporaria() => DeveTrocarSenha = true;
    public void LimparSenhaTemporaria() => DeveTrocarSenha = false;

    public event Action? OnChange;

    public SessionService(ProtectedLocalStorage protectedStorage, IHttpClientFactory httpClientFactory)
    {
        _protectedStorage = protectedStorage;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Carrega a sessão. Se houver refresh token persistido, tenta renovar o JWT
    /// automaticamente — o usuário não precisa logar novamente após F5.
    /// </summary>
    public async Task CarregarAsync()
    {
        try
        {
            var rtResult   = await _protectedStorage.GetAsync<string>(KeyRefreshToken);
            var infoResult = await _protectedStorage.GetAsync<string>(KeyUserInfo);

            if (!rtResult.Success   || string.IsNullOrEmpty(rtResult.Value) ||
                !infoResult.Success || string.IsNullOrEmpty(infoResult.Value))
            {
                await LimparStorageAsync();
                return;
            }

            var userInfo = JsonSerializer.Deserialize<UserInfoData>(infoResult.Value);
            if (userInfo == null)
            {
                await LimparStorageAsync();
                return;
            }

            var client   = _httpClientFactory.CreateClient("TicketPrimeApi");
            var response = await client.PostAsJsonAsync("api/auth/refresh", new { refreshToken = rtResult.Value });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RefreshResponse>();
                if (result != null && !string.IsNullOrEmpty(result.Token))
                {
                    Token  = result.Token;
                    Cpf    = userInfo.Cpf;
                    Nome   = userInfo.Nome;
                    Perfil = userInfo.Perfil;

                    // Mantém a flag de senha temporária mesmo após F5
                    if (result.SenhaTemporaria)
                        DeveTrocarSenha = true;

                    if (!string.IsNullOrEmpty(result.RefreshToken))
                        await _protectedStorage.SetAsync(KeyRefreshToken, result.RefreshToken);

                    return;
                }
            }

            await LimparStorageAsync();
        }
        catch
        {
            // ProtectedLocalStorage pode lançar durante pré-renderização — cair no login é seguro
            await LimparStorageAsync();
        }
        finally
        {
            NotificarMudanca();
        }
    }

    /// <summary>
    /// Define a sessão com os dados do login bem-sucedido e persiste
    /// refresh token + dados do usuário de forma cifrada.
    /// </summary>
    public async Task LogarAsync(string cpf, string nome, string perfil, string token, string? refreshToken = null)
    {
        Cpf    = cpf;
        Nome   = nome;
        Perfil = perfil;
        Token  = token;

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var userInfo     = new UserInfoData { Cpf = cpf, Nome = nome, Perfil = perfil };
            var userInfoJson = JsonSerializer.Serialize(userInfo);

            try
            {
                await _protectedStorage.SetAsync(KeyRefreshToken, refreshToken);
                await _protectedStorage.SetAsync(KeyUserInfo,     userInfoJson);
            }
            catch
            {
                // ProtectedLocalStorage pode falhar na primeira renderização — sessão segue funcional
            }
        }

        NotificarMudanca();
    }

    /// <summary>
    /// Limpa a sessão em memória, revoga o refresh token no servidor
    /// e remove os dados persistidos.
    /// </summary>
    public async Task DeslogarAsync()
    {
        // Revoga o refresh token no servidor antes de limpar o estado local
        try
        {
            string? refreshToken = null;
            var rtResult = await _protectedStorage.GetAsync<string>(KeyRefreshToken);
            if (rtResult.Success && !string.IsNullOrEmpty(rtResult.Value))
                refreshToken = rtResult.Value;

            var client = _httpClientFactory.CreateClient("TicketPrimeApi");
            if (!string.IsNullOrEmpty(Token))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

            await client.PostAsJsonAsync("api/auth/logout", new { refreshToken });
        }
        catch
        {
            // Falha na revogação server-side não impede o logout local
        }

        Cpf    = null;
        Nome   = null;
        Perfil = null;
        Token  = null;

        await LimparStorageAsync();
        NotificarMudanca();
    }

    // ---- Helpers privados ----

    private async Task LimparStorageAsync()
    {
        try
        {
            await _protectedStorage.DeleteAsync(KeyRefreshToken);
            await _protectedStorage.DeleteAsync(KeyUserInfo);
        }
        catch
        {
            // Falha silenciosa — pode não estar disponível durante pré-renderização
        }
    }

    private void NotificarMudanca() => OnChange?.Invoke();

    // ---- Modelos internos ----

    private class UserInfoData
    {
        public string Cpf    { get; set; } = "";
        public string Nome   { get; set; } = "";
        public string Perfil { get; set; } = "";
    }

    private class RefreshResponse
    {
        public string Token        { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public int ExpiresInMinutes { get; set; }
        public bool SenhaTemporaria { get; set; } = false;
    }
}
