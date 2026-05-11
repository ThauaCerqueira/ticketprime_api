# init-db.ps1
# Inicia o SQL Server em segundo plano e executa o script de inicialização
# após o servidor estar pronto.
# Alternativa em PowerShell para desenvolvedores Windows (substitui init-db.sh)

Write-Host "[init-db] Iniciando SQL Server..." -ForegroundColor Cyan

# Inicia o SQL Server em background (processo principal)
$sqlProcess = Start-Process -FilePath "/opt/mssql/bin/sqlservr" -NoNewWindow -PassThru

Write-Host "[init-db] Aguardando SQL Server inicializar..." -ForegroundColor Cyan

# Tenta conectar por até 120 segundos (60 tentativas x 2s)
$ready = $false
for ($i = 1; $i -le 60; $i++) {
    try {
        $result = & /opt/mssql-tools18/bin/sqlcmd `
            -S localhost -U sa -P "${env:SA_PASSWORD}" `
            -Q "SELECT 1" -C -l 2 2>$null
        
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
    } catch {
        # Ignora erros durante o warmup
    }
    
    Write-Host "[init-db] Tentativa ${i}/60: ainda não disponível. Aguardando 2s..." -ForegroundColor Yellow
    Start-Sleep -Seconds 2
}

if ($ready) {
    Write-Host "[init-db] SQL Server pronto. Executando script de inicialização..." -ForegroundColor Green
    & /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P "${env:SA_PASSWORD}" `
        -i /scripts/init.sql -C
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[init-db] Script executado com sucesso." -ForegroundColor Green
    } else {
        Write-Host "[init-db] ERRO: Falha ao executar o script SQL." -ForegroundColor Red
    }
} else {
    Write-Host "[init-db] AVISO: SQL Server não ficou pronto em 120s. Script não executado." -ForegroundColor Red
}

# Aguarda o processo do SQL Server (mantém o container vivo)
if ($sqlProcess -and !$sqlProcess.HasExited) {
    $sqlProcess.WaitForExit()
}
