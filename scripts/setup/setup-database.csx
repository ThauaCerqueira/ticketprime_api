#!/usr/bin/env dotnet-script

#r "nuget: Microsoft.Data.SqlClient, 5.1.0"

using Microsoft.Data.SqlClient;

var server = Environment.GetEnvironmentVariable("DB_SERVER") ?? "localhost,1433";
var database = Environment.GetEnvironmentVariable("DB_NAME") ?? "master";
var userId = Environment.GetEnvironmentVariable("DB_USER") ?? "sa";
var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "TicketPrime@2024!";

var connectionString = $"Server={server};Database={database};User Id={userId};Password={password};TrustServerCertificate=True;";
var scriptPath = @"db/script.sql";

try
{
    if (!File.Exists(scriptPath))
    {
        Console.WriteLine($"❌ Erro: Arquivo script.sql não encontrado em {scriptPath}");
        Environment.Exit(1);
    }

    Console.WriteLine("");
    Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   🔧  Configurando Banco de Dados (Script C#)              ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    Console.WriteLine("");

    var sqlScript = File.ReadAllText(scriptPath);
    var statements = sqlScript.Split(new[] { "\nGO\n", "\nGO\r\n", "\r\nGO\r\n" }, StringSplitOptions.RemoveEmptyEntries);

    using (var connection = new SqlConnection(connectionString))
    {
        connection.Open();
        Console.WriteLine("✓ Conectado ao SQL Server");
        Console.WriteLine("");

        int statementCount = 0;
        foreach (var statement in statements)
        {
            var trimmedStatement = statement.Trim();
            if (trimmedStatement.Length == 0)
                continue;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = trimmedStatement;
                command.CommandTimeout = 60;
                
                try
                {
                    command.ExecuteNonQuery();
                    statementCount++;
                    Console.WriteLine($"   ✓ Statement {statementCount} executado");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Erro no statement {statementCount + 1}: {ex.Message}");
                }
            }
        }

        connection.Close();

        Console.WriteLine("");
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║    ✅  Banco de dados configurado com sucesso!             ║");
        Console.WriteLine($"║        Total de statements executados: {statementCount}         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine("");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro: {ex.Message}");
    Environment.Exit(1);
}
