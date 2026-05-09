# Script para executar apenas Frontend (Blazor)

$rootPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║             🎨  Iniciando Frontend (Blazor)                ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "📱 Acesse em: http://localhost:5194" -ForegroundColor Green
Write-Host ""

cd "$rootPath/ui/TicketPrime.Web"
dotnet run
