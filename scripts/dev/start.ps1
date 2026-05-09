# Script para iniciar todos os serviços (Docker + Backend + Frontend)
# Uso: .\start.ps1 ou .\start.ps1 -Kill para encerrar

param(
    [switch]$Kill = $false
)

$green = @{ ForegroundColor = 'Green' }
$yellow = @{ ForegroundColor = 'Yellow' }
$red = @{ ForegroundColor = 'Red' }
$cyan = @{ ForegroundColor = 'Cyan' }

if ($Kill) {
    Write-Host ""
    Write-Host "❌ Encerrando todos os processos..." @red
    Write-Host ""
    
    docker-compose down --remove-orphans 2>$null
    Get-Process | Where-Object {$_.ProcessName -match "(dotnet|src|TicketPrime)" } | Stop-Process -Force -ErrorAction SilentlyContinue
    
    Write-Host "✅ Todos os processos encerrados!" @green
    Write-Host ""
    exit
}

$rootPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" @cyan
Write-Host "║     🚀  Iniciando TicketPrime (Dev - Todos os Serviços)   ║" @cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" @cyan
Write-Host ""

# 1. Subir Docker Compose (SQL Server)
Write-Host "1️⃣  📦 Iniciando Docker Compose (SQL Server)..." @yellow
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$rootPath'; docker-compose up -d" -WindowStyle Normal

Write-Host "⏳ Aguardando SQL Server ficar pronto..." @yellow
Start-Sleep -Seconds 15

# 2. Iniciar Backend (API)
Write-Host "2️⃣  🔧 Iniciando Backend (API)..." @yellow
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$rootPath/src'; dotnet run" -WindowStyle Normal

Write-Host "⏳ Aguardando Backend compilar e iniciar... (pode levar alguns segundos)..." @yellow
Start-Sleep -Seconds 10

# 3. Iniciar Frontend (Blazor)
Write-Host "3️⃣  🎨 Iniciando Frontend (Blazor)..." @yellow
Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$rootPath/ui/TicketPrime.Web'; dotnet run" -WindowStyle Normal

Write-Host "⏳ Aguardando Frontend compilar e iniciar... (pode levar alguns segundos)..." @yellow
Start-Sleep -Seconds 10

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" @green
Write-Host "║         ✅  Todos os serviços foram iniciados!             ║" @green
Write-Host "╚════════════════════════════════════════════════════════════╝" @green
Write-Host ""
Write-Host "🌐 Acesso aos Serviços:" @yellow
Write-Host "   📱 Frontend:      http://localhost:5194" @green
Write-Host "   🔌 API Swagger:   http://localhost:5164/swagger" @green
Write-Host "   💾 SQL Server:    localhost:1433" @green
Write-Host ""
Write-Host "🧪 Credenciais de Teste:" @yellow
Write-Host "   CPF: 00000000000" @green
Write-Host "   Senha: admin123" @green
Write-Host ""
Write-Host "⚠️  Para encerrar tudo, execute: .\start.ps1 -Kill" @red
Write-Host ""
