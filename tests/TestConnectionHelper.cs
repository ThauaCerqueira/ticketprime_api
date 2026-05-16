using src.Infrastructure;

namespace TicketPrime.Tests;

/// <summary>
/// ═══════════════════════════════════════════════════════════════════
/// Helper centralizado para connection strings de testes.
///
/// PROBLEMA ANTERIOR:
///   Cada arquivo de teste tinha sua própria connection string de
///   fallback hardcoded. Se a variável de ambiente TEST_DB_CONNECTION
///   não estivesse configurada, os testes usavam um banco local
///   sem aviso — podendo acidentalmente apontar para produção.
///
/// SOLUÇÃO:
///   - Connection string única centralizada aqui.
///   - Se TEST_DB_CONNECTION não estiver configurada, retorna NULL
///     em vez de uma string hardcoded, forçando o teste a falhar
///     com uma mensagem clara.
///   - Em CI/CD, use: $env:TEST_DB_CONNECTION="Server=...;Database=...;..."
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
public static class TestConnectionHelper
{
    /// <summary>
    /// Nome da variável de ambiente que deve conter a connection string
    /// para o banco de testes. Nunca usamos um fallback hardcoded.
    /// </summary>
    public const string EnvVarName = "TEST_DB_CONNECTION";

    /// <summary>
    /// Obtém a connection string de testes da variável de ambiente.
    /// Se não configurada, lança um erro claro — evitando uso acidental
    /// de banco de produção.
    /// </summary>
    /// <param name="databaseName">Nome opcional do banco de testes (padrão: TicketPrime_Tests)</param>
    public static string GetConnectionString(string? databaseName = null)
    {
        var envConn = Environment.GetEnvironmentVariable(EnvVarName);

        if (!string.IsNullOrWhiteSpace(envConn))
            return envConn;

        // ══════════════════════════════════════════════════════════════
        // NÃO usamos fallback hardcoded! Se não tem variável de ambiente,
        // o teste falha com instruções claras de como configurar.
        //
        // Motivo de segurança: uma connection string hardcoded com
        // TrustServerCertificate=true pode vazar em logs de CI público,
        // ou um desenvolvedor pode esquecer de mudá-la e rodar testes
        // contra produção acidentalmente.
        // ══════════════════════════════════════════════════════════════
        throw new InvalidOperationException(
            $"""
            ============================================================
            ⚠️  VARIÁVEL DE AMBIENTE NÃO CONFIGURADA: {EnvVarName}

            Para rodar os testes localmente, configure a variável:
              PowerShell:  $env:{EnvVarName}="Server=localhost;Database={databaseName ?? "TicketPrime_Tests"};..."
              CMD:         set {EnvVarName}=Server=localhost;Database=...
              CI/CD:       Adicione ao pipeline:
                             - name: {EnvVarName}
                               value: $(TEST_DB_CONNECTION)

            Exemplo (SQL Server com autenticação Windows):
              $env:{EnvVarName}="Server=localhost\SQLEXPRESS;Database={databaseName ?? "TicketPrime_Tests"};Trusted_Connection=true;TrustServerCertificate=True;"

            Exemplo (SQL Server com SA):
              $env:{EnvVarName}="Server=localhost,1433;Database={databaseName ?? "TicketPrime_Tests"};User Id=sa;Password=your_password;TrustServerCertificate=True;"
            ============================================================
            """);
    }

    /// <summary>
    /// Cria um DbConnectionFactory usando a connection string configurada.
    /// </summary>
    public static DbConnectionFactory CreateDbConnectionFactory(string? databaseName = null)
    {
        return new DbConnectionFactory(GetConnectionString(databaseName));
    }
}
