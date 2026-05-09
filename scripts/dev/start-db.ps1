# Script para executar apenas o Banco de Dados (Docker Compose - SQL Server)

$rootPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path
$rootPath = Join-Path $rootPath -ChildPath ".." | Resolve-Path

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║            📦  Iniciando Banco de Dados (SQL Server)       ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Aguarde a inicialização... (pode levar alguns segundos)" -ForegroundColor Yellow
Write-Host ""
Write-Host "💾 SQL Server será acessível em: localhost:1433" -ForegroundColor Green
Write-Host ""

cd $rootPath
docker-compose up
