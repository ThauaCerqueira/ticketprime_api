<#
.SYNOPSIS
    Remove completamente do histórico do Git o arquivo appsettings.Development.json
    que continha a chave JWT vazada (TicketPrimeDev_SuperSecretKey_32CharsMinimum_2024!).

.DESCRIPTION
    Este script usa git-filter-repo (recomendado) ou git filter-branch (fallback)
    para remover permanentemente o arquivo appsettings.Development.json de todo
    o histórico do repositório.

    ═══════════════════════════════════════════════════════════════════
    ⚠️  ATENÇÃO: Isso reescreve o histórico do Git!
    ═══════════════════════════════════════════════════════════════════
    - Todos os colaboradores precisarão fazer um clone fresco do repositório.
    - Pull requests abertos serão afetados.
    - Commits assinados (GPG) podem perder a assinatura.
    - Faça um BACKUP completo do repositório antes de executar.
      (Ex: copiar a pasta inteira ou criar um bundle: git bundle create backup.bundle --all)

.PARAMETER Force
    Pula a confirmação do usuário.

.EXAMPLE
    .\scripts\git-cleanup-jwt-secret.ps1

.EXAMPLE
    .\scripts\git-cleanup-jwt-secret.ps1 -Force

.NOTES
    Autor: TicketPrime Security Team
    Data: 2026-05-16
#>

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host @"

╔══════════════════════════════════════════════════════════════════╗
║    🔐  LIMPEZA DE SEGREDO NO HISTÓRICO DO GIT                ║
╠══════════════════════════════════════════════════════════════════╣
║                                                                ║
║  Arquivo alvo: src/appsettings.Development.json                ║
║  Segredo:      Chave JWT "TicketPrimeDev_..."                  ║
║                                                                ║
║  ⚠️  ATENÇÃO: Isso reescreve o histórico do Git!              ║
║  Faça backup antes de continuar.                               ║
║                                                                ║
╚══════════════════════════════════════════════════════════════════╝

"@

# ── Verifica se está em um repositório Git ────────────────────────────
if (-not (Test-Path ".git")) {
    Write-Error "Erro: Este diretório não é um repositório Git. Execute o script na raiz do repositório."
    exit 1
}

# ── Verifica se há modificações não commitadas ───────────────────────
$status = git status --porcelain
if ($status) {
    Write-Host "⚠️  Há modificações não commitadas no repositório:" -ForegroundColor Yellow
    Write-Host $status -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Recomenda-se commitar ou stash as mudanças antes de continuar." -ForegroundColor Yellow
    if (-not $Force) {
        $choice = Read-Host "Deseja continuar mesmo assim? (s/N)"
        if ($choice -ne "s" -and $choice -ne "S") {
            Write-Host "Operação cancelada pelo usuário." -ForegroundColor Red
            exit 0
        }
    }
}

# ── Tenta usar git-filter-repo (recomendado) ─────────────────────────
$useFilterRepo = $false
try {
    $null = git filter-repo --version 2>$null
    $useFilterRepo = $true
    Write-Host "✓ git-filter-repo encontrado!" -ForegroundColor Green
}
catch {
    Write-Host "ℹ️  git-filter-repo não encontrado. Usando filter-branch (mais lento)." -ForegroundColor Yellow
    Write-Host "  Para instalar: https://github.com/newren/git-filter-repo#installation" -ForegroundColor Yellow
}

# ── Confirmação final ────────────────────────────────────────────────
if (-not $Force) {
    Write-Host ""
    Write-Host "⚠️  Isso reescreverá o histórico do Git permanentemente!" -ForegroundColor Red
    Write-Host "  Todos os colaboradores precisarão clonar o repositório novamente." -ForegroundColor Red
    $confirm = Read-Host "Tem certeza que deseja continuar? (s/N)"
    if ($confirm -ne "s" -and $confirm -ne "S") {
        Write-Host "Operação cancelada pelo usuário." -ForegroundColor Red
        exit 0
    }
}

Write-Host ""
Write-Host "🔄 Removendo src/appsettings.Development.json do histórico..." -ForegroundColor Yellow

if ($useFilterRepo) {
    # ── Método 1: git-filter-repo (rápido e seguro) ─────────────────
    git filter-repo --path src/appsettings.Development.json --invert-paths --force
}
else {
    # ── Método 2: git filter-branch (fallback) ──────────────────────
    git filter-branch --force --index-filter `
        "git rm --cached --ignore-unmatch src/appsettings.Development.json" `
        --prune-empty --tag-name-filter cat -- --all
}

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ SEGREDO REMOVIDO DO HISTÓRICO COM SUCESSO!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📋 PRÓXIMOS PASSOS:" -ForegroundColor Cyan
    Write-Host "  1. Verifique se o histórico está limpo:" -ForegroundColor Cyan
    Write-Host "     git log --all --follow -p -- src/appsettings.Development.json" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  2. Force o push para atualizar o remoto:" -ForegroundColor Cyan
    Write-Host "     git push origin --force --all" -ForegroundColor Cyan
    Write-Host "     git push origin --force --tags" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  3. Avise a equipe para clonar novamente:" -ForegroundColor Cyan
    Write-Host "     git clone <url-do-repositorio>" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  4. 🔐 Configure a chave JWT via variável de ambiente (recomendado):" -ForegroundColor Green
    Write-Host "     \$env:Jwt__Key = \"$( -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | % {[char]$_}) )\"" -ForegroundColor Green
    Write-Host ""
    Write-Host "     Ou via User Secrets (desenvolvimento):" -ForegroundColor Green
    Write-Host "     dotnet user-secrets set Jwt:Key \"$( -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | % {[char]$_}) )\"" -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "❌ FALHA AO REMOVER SEGREDO DO HISTÓRICO!" -ForegroundColor Red
    Write-Host "  Erro ao executar o comando Git. Verifique as mensagens acima." -ForegroundColor Red
    exit 1
}
