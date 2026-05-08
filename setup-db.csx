#!/usr/bin/env dotnet-script

#r "nuget: Microsoft.Data.SqlClient, 5.1.0"

using Microsoft.Data.SqlClient;

var connectionString = "Server=localhost,1433;Database=master;User Id=sa;Password=TicketPrime@2024!;TrustServerCertificate=True;";
var scriptPath = @"db/script.sql";

try
{
    if (!File.Exists(scriptPath))
    {
        Console.WriteLine($"Error: Script file not found at {scriptPath}");
        Environment.Exit(1);
    }

    var sqlScript = File.ReadAllText(scriptPath);
    var statements = sqlScript.Split(new[] { "\nGO\n", "\nGO\r\n", "\r\nGO\r\n" }, StringSplitOptions.RemoveEmptyEntries);

    using (var connection = new SqlConnection(connectionString))
    {
        connection.Open();
        Console.WriteLine("✓ Connected to SQL Server");

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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ Warning: {ex.Message}");
                }
            }
        }

        connection.Close();
    }

    Console.WriteLine("✓ Database setup completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
    Environment.Exit(1);
}
