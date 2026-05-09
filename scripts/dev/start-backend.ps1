# Script para executar apenas Backend (API)

$rootPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║              🔧  Iniciando Backend (API)                   ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "🌐 Swagger disponível em: http://localhost:5164/swagger" -ForegroundColor Green
Write-Host ""

cd "$rootPath/src"
dotnet run
