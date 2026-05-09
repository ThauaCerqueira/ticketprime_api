namespace TicketPrime.Web.Services;

public class SessionService
{
    public string? Cpf { get; private set; }
    public string? Nome { get; private set; }
    public string? Perfil { get; private set; }
    public string? Token { get; private set; }
    public bool EstaLogado => !string.IsNullOrEmpty(Token);
    public bool EhAdmin => Perfil == "ADMIN";

    public event Action? OnChange;

    public void Logar(string cpf, string nome, string perfil, string token)
    {
        Cpf = cpf;
        Nome = nome;
        Perfil = perfil;
        Token = token;
        NotificarMudanca();
    }

    public void Deslogar()
    {
        Cpf = null;
        Nome = null;
        Perfil = null;
        Token = null;
        NotificarMudanca();
    }

    private void NotificarMudanca() => OnChange?.Invoke();
}