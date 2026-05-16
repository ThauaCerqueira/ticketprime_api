using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace src.Infrastructure;

/// <summary>
/// Fábrica de conexões SQL Server. Todas as queries Dapper herdam
/// o <see cref="DefaultCommandTimeout"/> configurado aqui.
/// </summary>
public class DbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Tempo limite padrão (em segundos) para comandos SQL via Dapper.
    /// 30s — evita que queries lentas seguem conexões no pool.
    /// </summary>
    public const int DefaultCommandTimeout = 30;

    /// <summary>
    /// Tempo limite para queries analíticas (relatórios, dashboards, CSV export).
    /// 300s (5 min) — relatórios financeiros podem processar muitos dados.
    /// </summary>
    public const int AnalyticsCommandTimeout = 300;

    public DbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Cria uma conexão cujos comandos Dapper herdam automaticamente
    /// o <see cref="DefaultCommandTimeout"/>.
    /// </summary>
    public IDbConnection CreateConnection()
    {
        var conn = new SqlConnection(_connectionString);
        conn.Open(); // Abre a conexão para compatibilidade com Dapper async via IDbConnection
        return new TimeoutConnectionDecorator(conn);
    }

    /// <summary>
    /// Decorator de IDbConnection que intercepta CreateCommand para
    /// definir CommandTimeout = DefaultCommandTimeout em todos os comandos.
    /// Dapper usa CreateCommand() internamente, então todas as queries
    /// herdam o timeout sem necessidade de parâmetro adicional.
    /// </summary>
    private sealed class TimeoutConnectionDecorator : IDbConnection
    {
        private readonly SqlConnection _inner;

        public TimeoutConnectionDecorator(SqlConnection inner) => _inner = inner;

        public string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value!;
        }

        public int ConnectionTimeout => _inner.ConnectionTimeout;
        public string Database => _inner.Database;
        public ConnectionState State => _inner.State;

        public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
        public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public void Close() => _inner.Close();
        public void Open() => _inner.Open();

        public IDbCommand CreateCommand()
        {
            var cmd = _inner.CreateCommand();
            cmd.CommandTimeout = DefaultCommandTimeout;
            return cmd;
        }

        public void Dispose() => _inner.Dispose();
    }
}
