# Script para encerrar todos os processos

$green = @{ ForegroundColor = 'Green' }
$yellow = @{ ForegroundColor = 'Yellow' }
$red = @{ ForegroundColor = 'Red' }
$cyan = @{ ForegroundColor = 'Cyan' }

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" @cyan
Write-Host "║         ❌  Encerrando todos os processos...               ║" @cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" @cyan
Write-Host ""

$rootPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path

Write-Host "• Parando Docker Compose..." @yellow
cd $rootPath
docker-compose down --remove-orphans 2>$null

Write-Host "• Encerrando processos dotnet..." @yellow
Get-Process | Where-Object {$_.ProcessName -match "(dotnet|src|TicketPrime)" } | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "✅ Todos os processos foram encerrados!" @green
Write-Host ""
