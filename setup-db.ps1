$connectionString = "Server=localhost,1433;Database=master;User Id=sa;Password=TicketPrime@2024!;TrustServerCertificate=True;"
$sqlScriptPath = "c:\Users\giuli\Downloads\ticketprime_api\db\script.sql"

try {
    Add-Type -AssemblyName System.Data.SqlClient
    
    # Read the SQL script
    $sqlScript = Get-Content -Path $sqlScriptPath -Raw
    
    # Split the script by GO statements
    $sqlStatements = $sqlScript -split '\bGO\b'
    
    # Execute each statement
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    foreach ($statement in $sqlStatements) {
        if ($statement.Trim().Length -gt 0) {
            $command = $connection.CreateCommand()
            $command.CommandText = $statement.Trim()
            $command.CommandTimeout = 30
            
            try {
                $result = $command.ExecuteNonQuery()
                Write-Host "Statement executed successfully"
            }
            catch {
                Write-Host "Error in statement: $($_.Exception.Message)"
            }
        }
    }
    
    $connection.Close()
    Write-Host "Database setup completed successfully!"
}
catch {
    Write-Host "Error: $($_.Exception.Message)"
    exit 1
}
