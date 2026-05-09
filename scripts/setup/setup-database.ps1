# Script para configurar o Banco de Dados (SQL Server)
# Executa o script SQL localizado em db/script.sql

param(
    [string]$Server = "localhost,1433",
    [string]$Database = "master",
    [string]$UserId = "sa",
    [string]$Password = "TicketPrime@2024!"
)

$connectionString = "Server=$Server;Database=$Database;User Id=$UserId;Password=$Password;TrustServerCertificate=True;"
$sqlScriptPath = ".\db\script.sql"

$cyan = @{ ForegroundColor = 'Cyan' }
$green = @{ ForegroundColor = 'Green' }
$yellow = @{ ForegroundColor = 'Yellow' }
$red = @{ ForegroundColor = 'Red' }

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" @cyan
Write-Host "║      🔧  Configurando Banco de Dados (SQL Server)          ║" @cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" @cyan
Write-Host ""

if (-not (Test-Path $sqlScriptPath)) {
    Write-Host "❌ Erro: Arquivo script.sql não encontrado em $sqlScriptPath" @red
    exit 1
}

try {
    Add-Type -AssemblyName System.Data.SqlClient
    
    Write-Host "📖 Lendo script SQL..." @yellow
    $sqlScript = Get-Content -Path $sqlScriptPath -Raw
    
    Write-Host "🔄 Dividindo script por statements (GO)..." @yellow
    $sqlStatements = $sqlScript -split '\bGO\b'
    
    Write-Host "🔌 Conectando ao SQL Server: $Server..." @yellow
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Host "✅ Conectado!" @green
    
    Write-Host ""
    Write-Host "⏳ Executando statements..." @yellow
    $statementCount = 0
    
    foreach ($statement in $sqlStatements) {
        if ($statement.Trim().Length -gt 0) {
            $command = $connection.CreateCommand()
            $command.CommandText = $statement.Trim()
            $command.CommandTimeout = 60
            
            try {
                $result = $command.ExecuteNonQuery()
                $statementCount++
                Write-Host "   ✓ Statement $statementCount executado" @green
            }
            catch {
                Write-Host "   ❌ Erro no statement $($statementCount + 1): $($_.Exception.Message)" @red
            }
        }
    }
    
    $connection.Close()
    
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════╗" @green
    Write-Host "║    ✅  Banco de dados configurado com sucesso!             ║" @green
    Write-Host "║        Total de statements executados: $statementCount        ║" @green
    Write-Host "╚════════════════════════════════════════════════════════════╝" @green
    Write-Host ""
}
catch {
    Write-Host "❌ Erro: $($_.Exception.Message)" @red
    exit 1
}
