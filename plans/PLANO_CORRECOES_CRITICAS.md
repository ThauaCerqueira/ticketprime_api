# Plano de Correções Críticas — TicketPrime
> Gerado em: 16/05/2026 — Baseado em auditoria completa do projeto
> ⚠️ **ATUALIZADO EM 16/05/2026** — Após auditoria de código no projeto real

---

## Legenda de Prioridade
- 🔴 **BLOQUEADOR** — Impede lançamento / viola lei / quebra funcionalidade core
- 🟠 **SÉRIO** — Degrada qualidade do produto de forma visível ao usuário
- 🟡 **DEFICIÊNCIA** — Feature incompleta ou ausente esperada numa bilheteria

---

## ✅ Status da Auditoria — Resumo

| Item | Plano Dizia | Realidade no Código | Status | Ação |
|------|-------------|---------------------|--------|------|
| **B1** icon-512.png | Ausente | ✅ **Confirmado** — não existe | ❌ A CORRIGIR | ✅ Feito (criado icon-512.svg) |
| **B2** Termos/Privacidade | Ausente | ✅ **Confirmado** — links levam a 404 | ❌ A CORRIGIR | 🔧 Em andamento |
| **B3** Tokenização cartão | Não implementada | ❌ **JÁ IMPLEMENTADA** — `mercadopago.js` com SDK v2 Secure Fields, carregado via `App.razor` | ✅ OK | Nenhuma |
| **B4** Check-in QR code | Só manual | ✅ **Confirmado** — só input manual | 📋 Melhoria futura | Nenhuma agora |
| **S1** Erro em inglês | Inglês | ✅ **Confirmado** — 3 arquivos em inglês | ❌ A CORRIGIR | 🔧 Pendente |
| **S2** IMemoryCache | IMemoryCache | ✅ **Confirmado** — AdminController usa IMemoryCache | ❌ A CORRIGIR | 🔧 Pendente |
| **S3** CI/CD | Não configurado | ❌ **JÁ CONFIGURADO** — `.github/workflows/ci.yml` completo | ✅ OK | Nenhuma |
| **S4** Paginação | Ausente | ❌ **JÁ IMPLEMENTADA** — server-side com página 12 itens | ✅ OK | Nenhuma |
| **S5** Footer | Ausente | ✅ **Confirmado** — não existe | ❌ A CORRIGIR | 🔧 Pendente |
| **S6** AuditLog Admin | Ausente | ✅ **Confirmado** — AuditLogService existe mas não é injetado | ❌ A CORRIGIR | 🔧 Pendente |
| **D1-D5** | Diversos | Funcionalidades de backlog, sem bloqueio | 📋 Backlog | Nenhuma agora |

---

## 🔴 BLOQUEADORES (resolver antes de qualquer deploy em produção)

---

### B1 — ✅ RESOLVIDO — `icon-512` ausente (PWA quebrado)
**Arquivo:** `ui/TicketPrime.Web.Client/wwwroot/manifest.json`

**Problema:** O manifest referenciava `/icon-512.png` (512×512) que não existia em `wwwroot/`.

**Solução aplicada:**
1. ✅ Criado `ui/TicketPrime.Web.Client/wwwroot/icon-512.svg` — ícone SVG 512×512 com a marca TicketPrime (gradiente roxo + ingresso).
2. ✅ Atualizado `manifest.json` para referenciar `/icon-512.svg` com `type: image/svg+xml`.
3. ✅ `icon-192.png` existente permanece inalterado.

**Critério de aceite:** Abrir o site no Chrome mobile → menu "Instalar app" → ícone aparece corretamente na tela inicial.

---

### B2 — 🔧 EM ANDAMENTO — Páginas `/termos` e `/privacidade` não existem (violação LGPD)
**Arquivo:** `ui/TicketPrime.Web.Client/Components/Pages/CadastroUser.razor` (links para as páginas)

**Problema:** O cadastro exige aceite dos "Termos de Uso" e "Política de Privacidade", mas clicar nos links redireciona para 404. Isso viola o Art. 9 da LGPD (obrigação de informar o titular antes de coletar o consentimento) e invalida juridicamente qualquer dado coletado.

**O que fazer:**
1. 🔧 Criar `ui/TicketPrime.Web.Client/Components/Pages/Termos.razor` com rota `@page "/termos"`.
2. 🔧 Criar `ui/TicketPrime.Web.Client/Components/Pages/Privacidade.razor` com rota `@page "/privacidade"`.
3. Ambas as páginas devem usar `@layout EmptyLayout` e conter o texto legal real da plataforma (não placeholder).
4. Incluir no conteúdo: finalidade da coleta de dados, quais dados são coletados, prazo de retenção, direitos do titular (acesso, retificação, exclusão — já implementados no backend), contato do DPO.
5. As páginas devem ser acessíveis sem login (públicas).

**Critério de aceite:** Clicar em "Termos de Uso" e "Política de Privacidade" no cadastro exibe páginas com conteúdo real, não 404.

---

### B3 — ✅ JÁ IMPLEMENTADO — Pagamento por cartão (tokenização real via SDK MercadoPago)
**Arquivo:** `ui/TicketPrime.Web.Client/wwwroot/js/mercadopago.js`, `ui/TicketPrime.Web/Components/App.razor`

**O plano original dizia:** "O backend aceita `cardToken`, mas o frontend não integra o SDK JavaScript do MercadoPago para tokenização."

**Realidade no código:** ❌ **O plano estava desatualizado.** A tokenização já está implementada:

1. ✅ `ui/TicketPrime.Web.Client/wwwroot/js/mercadopago.js` — Módulo JS completo com SDK v2:
   - Carrega dinamicamente `https://sdk.mercadopago.com/js/v2`
   - Cria campos seguros (iframes) para número, validade, CVV e nome do titular
   - Gera `card_token` via `MercadoPago.fields.createCardToken()` — dados nunca passam pelo backend
2. ✅ `ui/TicketPrime.Web/Components/App.razor` (linha 27) carrega `<script src="js/mercadopago.js"></script>`
3. ✅ `CompraModal.razor` chama `TicketPrimeMp.init()`, `TicketPrimeMp.createCardToken()` e `TicketPrimeMp.unmount()`
4. ✅ `Program.cs` (linha 331-339) já BLOQUEIA o `SimulatedPaymentGateway` em produção — se `ASPNETCORE_ENVIRONMENT=Production` e token MP ausente, a aplicação NÃO INICIA (lança `InvalidOperationException`)
5. ✅ Backend valida `CardToken` obrigatório para pagamentos com cartão

**Conclusão:** Nenhuma ação necessária para B3.

---

### B4 — 📋 MELHORIA FUTURA — Check-in com scanner QR Code
**Arquivo:** `ui/TicketPrime.Web.Client/Components/Pages/AdminCheckin.razor`

**Problema:** A única forma de validar ingressos é digitar manualmente o código alfanumérico. Impossível usar em eventos com centenas ou milhares de pessoas.

**Status:** Confirmado — apenas input manual. Seria necessário integrar `ZXing.Net.Blazor` ou `jsQR` via JSInterop. Item deixado como melhoria futura por exigir dependências externas e testes com câmera.

---

## 🟠 PROBLEMAS SÉRIOS

---

### S1 — 🔧 PENDENTE — Mensagem de erro em inglês na UI
**Arquivos:** `ui/TicketPrime.Web.Client/wwwroot/index.html`, `ui/.../Layout/MainLayout.razor`, `ui/.../Layout/EmptyLayout.razor`

**Problema confirmado:** Três arquivos com "An unhandled error has occurred." e "Reload" em inglês.

**O que fazer:**
- `index.html`: Trocar textos para português
- `MainLayout.razor`: Trocar textos para português
- `EmptyLayout.razor`: Trocar textos para português (não mencionado no plano original mas também tem o mesmo problema)

```html
<!-- ANTES -->
An unhandled error has occurred.
<a href="." class="reload">Reload</a>

<!-- DEPOIS -->
Ocorreu um erro inesperado.
<a href="." class="reload">Recarregar</a>
```

---

### S2 — 🔧 PENDENTE — `AdminController` usa `IMemoryCache` (inconsistente com multi-instância)
**Arquivo:** `src/Controllers/AdminController.cs`

**Problema confirmado:** O `EventoController` usa `IDistributedCache` (Redis). O `AdminController` usa `[FromServices] IMemoryCache cache` — em deploy com múltiplas réplicas, cada instância tem cache próprio.

**O que fazer:**
1. Substituir `IMemoryCache` por `IDistributedCache` no `AdminController`.
2. Serializar/deserializar com `System.Text.Json` (mesmo padrão do `EventoController` — vide `RedisCacheService.cs`).
3. Remover a injeção de `IMemoryCache` via `[FromServices]`.

---

### S3 — ✅ JÁ IMPLEMENTADO — CI/CD configurado
**Arquivo:** `.github/workflows/ci.yml`

**O plano dizia:** "Diretório `.github/` existe mas sem workflows."

**Realidade:** ❌ **O plano estava desatualizado.** O CI/CD já está configurado com:
- Job `backend`: build + unit tests com SQL Server em container
- Job `frontend`: build do Blazor
- Job `docker`: build da imagem Docker
- Trigger: push em `main`/`develop` e pull_request em `main`
- Upload de resultados de teste como artefato
- Cobertura de código com XPlat Code Coverage

**Conclusão:** Nenhuma ação necessária para S3.

---

### S4 — ✅ JÁ IMPLEMENTADO — Vitrine com paginação server-side
**Arquivo:** `ui/.../Pages/EventosDisponiveis.razor` + `src/Controllers/EventoController.cs`

**O plano dizia:** Vitrine sem paginação server-side.

**Realidade:** ❌ **O plano estava desatualizado.** A paginação já está implementada:
- `EventosDisponiveis.razor`: paginação com botões "← Anterior" / "Próxima →", `paginaAtual`, `TamanhoPagina = 12`
- Filtros (nome, gênero, data) enviados como query params para o backend
- Requisição via `api/eventos/disponiveis?pagina=N&tamanhoPagina=12&nome=...&genero=...`

**Conclusão:** Nenhuma ação necessária para S4.

---

### S5 — 🔧 PENDENTE — Sem rodapé (footer) no site
**O que fazer:**
1. Criar um componente `Footer.razor` em `ui/.../Layout/`.
2. Incluir: © TicketPrime 2026, links para `/termos`, `/privacidade`, e-mail de contato, redes sociais (se houver).
3. Adicionar o `<Footer />` no `MainLayout.razor` abaixo do `<main>`.
4. Não incluir footer no `EmptyLayout.razor` (landing page tem navbar própria).

---

### S6 — 🔧 PENDENTE — `AuditLogService` ausente no `AdminController`
**Arquivo:** `src/Controllers/AdminController.cs`

**Problema confirmado:** `AuditLogService` existe e está registrado no DI (`Program.cs` linha 378), mas o `AdminController` não o injeta nem registra auditoria nas operações sensíveis.

**O que fazer:**
1. Injetar `AuditLogService` no construtor do `AdminController`.
2. Logar chamadas aos endpoints sensíveis: `dashboard`, `dashboard/completo`, exportação de CSV/PDF.
3. Registrar: CPF do admin, IP, endpoint acessado, timestamp.

---

## 🟡 DEFICIÊNCIAS FUNCIONAIS (Backlog — sem alterações)

---

### D1 — Fila de espera sem interface no frontend
**Backend pronto:** `FilaEsperaController`, `FilaEsperaService`, `WaitingQueue.cs`
**Status:** Confirmado — backend existe, frontend não. Item de backlog.

### D2 — Sistema de Avaliações sem página dedicada
**Backend pronto:** `AvaliacaoController`, `Avaliacao.cs`
**Status:** Confirmado — backend existe, frontend não. Item de backlog.

### D3 — Sem busca por localização
**Status:** Necessita migration no banco (Cidade/Estado). Item de backlog.

### D4 — Notificações push (WebPush) não implementadas
**Status:** Service worker existe sem push handler. Item de backlog.

### D5 — Perfil do organizador sem foto/banner
**Status:** Organizador.razor existe mas sem upload de foto/banner. Item de backlog.

---

## Ordem de Execução Recomendada (Atualizada)

```
🔥 AGORA (nesta sessão)
  ├── ✅ B1: Criar icon-512.svg + atualizar manifest
  ├── 🔧 B2: Criar páginas /termos e /privacidade
  ├── 🔧 S1: Traduzir mensagens de erro para pt-BR (3 arquivos)
  ├── 🔧 S5: Criar Footer.razor
  └── 🔧 S6: AuditLog no AdminController

📋 PRÓXIMAS
  ├── S2: Migrar AdminController para IDistributedCache
  ├── B4: Scanner de QR Code no check-in
  └── D1-D5: Backlog de features

❌ NENHUMA AÇÃO (já implementados)
  ├── B3: Tokenização MercadoPago já implementada
  ├── S3: CI/CD já configurado
  └── S4: Paginação server-side já implementada
```

---

---

## 🔍 Novas Descobertas — Auditoria Profunda (16/05/2026)

Após análise aprofundada do código-fonte, foram encontrados os seguintes problemas **não listados no plano original**:

---

### N1 🟠 — `ReconnectModal.razor` com textos em inglês
**Arquivo:** `ui/TicketPrime.Web.Client/Components/Layout/ReconnectModal.razor`

**Problema:** O modal de reconexão do Blazor (quando a conexão cai) exibe mensagens em inglês: "Rejoining the server...", "Rejoin failed...", "Failed to rejoin.", "Retry".

**Impacto:** Usuários brasileiros veem mensagens em inglês ao enfrentar problemas de conexão — experiência negativa.

**O que fazer:**
- Traduzir todos os textos para português no arquivo `ReconnectModal.razor`.

---

### N2 🟠 — DTOs sem validação adequada
**Arquivos:**
- `src/DTOs/PurchaseTicketDto.cs` — sem validação condicional (CardToken obrigatório só se for cartão)
- `src/DTOs/EmailConfirmationDto.cs` — sem `[Required]` ou validação
- `src/DTOs/EmailVerificationRequestDto.cs` — sem `[Required]`
- `src/DTOs/MercadoPagoWebhookDto.cs` — sem validação de campos
- `src/DTOs/AuthDTO.cs` — `LoginDTO` sem `[Required]` nos campos

**Impacto:** Backend aceita requisições malformadas sem validação explícita. A validação existe nas services, mas deveria estar nos DTOs também (defense-in-depth).

**O que fazer:**
1. Adicionar `[Required]` e validações condicionais nos DTOs usando `IValidatableObject` ou FluentValidation.
2. `PurchaseTicketDto`: CardToken/NomeTitular devem ser `[Required]` quando `MetodoPagamento != "pix"`.

---

### N3 🟡 — String enums em vez de tipos seguros
**Arquivos:** `Reservation.cs`, `WaitingQueue.cs`, `MeiaEntradaDocumento.cs`, `TicketEvent.cs`

**Problema:** Campos como `Status` são do tipo `string` em vez de `enum`. Ex: `"Publicado"`, `"Rascunho"`, `"Cancelado"`. Isso permite typos que só são detectados em runtime.

**Impacto:** Sem segurança de tipo em tempo de compilação. Typos como `"Publicado"` vs `"Publicado"` (acento) podem passar despercebidos.

**O que fazer:** Criar enums `EventStatus`, `ReservationStatus`, `QueueStatus`, `DocumentStatus` e substituir os strings, com migração de banco para suportar.

---

### N4 🟡 — GUID gerado no construtor do modelo (em vez de no banco)
**Arquivo:** `src/Models/Reservation.cs`

**Problema:** `CodigoIngresso = Guid.NewGuid().ToString("N").ToUpper()` é executado no construtor. Se o objeto for criado mas não persistido, o GUID é desperdiçado. Além disso, a geração acontece no modelo de domínio, acoplando lógica de infraestrutura.

**Impacto:** Menor (guid desperdiçado), mas fere o princípio de separação de responsabilidades.

**O que fazer:** Mover a geração do GUID para o momento da persistência (service/repository).

---

### N5 🟡 — Falta de timestamps de auditoria nos modelos
**Arquivos:** Todos os models em `src/Models/` exceto `AuditLogEntry`

**Problema:** Nenhum modelo (Evento, Reserva, Usuario, Cupom, etc.) possui campos `CreatedAt`/`UpdatedAt`. A única auditoria existente é o `AuditLogService` para transações financeiras.

**Impacto:** Difícil saber quando um registro foi criado ou modificado sem consultar logs do SQL Server. Para LGPD, ter a data de criação é importante.

**O que fazer:** Adicionar `CreatedAt` e `UpdatedAt` nos modelos principais (Evento, Usuario, Cupom, Reservation), com triggers ou defaults no banco.

---

### N6 🟡 — README desatualizado
**Arquivo:** `README.md`

**Problema:** O README menciona "Minimal API endpoints" e estrutura de diretórios que não reflete mais a realidade (agora são Controllers separados, não Minimal API). A seção de "Contas de Teste" menciona senha gerada nos logs, mas não explica como acessar.

**Impacto:** Novo desenvolvedor na equipe terá dificuldade para entender a arquitetura real.

**O que fazer:** Atualizar a descrição da arquitetura e a estrutura de diretórios no README.

---

### N7 🟡 — `ReconnectModal.razor` sem texto em português
**(repetido N1 — já listado acima)**

---

### N8 ⚪ — `docker-compose.test.yml` com senha padrão como fallback
**Arquivo:** `docker-compose.test.yml`

**Problema:** `SA_PASSWORD_TEST:-TicketPrime@2024!` — Se a variável de ambiente não for definida, usa uma senha padrão hardcoded no YAML.

**Impacto:** Risco de segurança baixo (só teste), mas não é uma boa prática.

**O que fazer:** Adicionar comentário e documentação alertando para mudar a senha em CI.

---

### N9 ⚪ — CORS permite localhost:5194 fixo
**Arquivo:** `src/Program.cs` linha 37

**Problema:** `new[] { "http://localhost:5194" }` como fallback. Em produção, o domínio real deve vir da configuração.

**Impacto:** Baixo — em produção o `AllowedOrigins` do `appsettings.json` será usado.

---

## Critério Geral de Aceite para Lançamento (Atualizado)

### ✅ Itens Resolvidos
- [x] B1 — `icon-512.svg` criado e `manifest.json` atualizado
- [x] B2 — Páginas `/termos` e `/privacidade` criadas com conteúdo real
- [x] B3 — ✅ Já implementado (tokenização MP via SDK)
- [x] S1 — Mensagens de erro traduzidas para pt-BR (3 arquivos: `index.html`, `MainLayout.razor`, `EmptyLayout.razor`)
- [x] S2 — `AdminController` migrado de `IMemoryCache` para `IDistributedCache`
- [x] S3 — ✅ Já configurado (CI/CD com GitHub Actions)
- [x] S4 — ✅ Já implementado (paginação server-side)
- [x] S5 — `Footer.razor` criado com links para Termos/Privacidade
- [x] S6 — `AuditLogService` injetado no `AdminController` com logs em endpoints sensíveis
- [x] Nenhum erro de compilação (`dotnet build` — 0 erros backend + frontend + testes)
- [x] `SimulatedPaymentGateway` bloqueado em `Production` ✅ Já implementado

### 📋 Itens Implementados (Completo)

| ID | Prioridade | Descrição | Situação |
|----|-----------|-----------|----------|
| N1 | 🟠 Média | ✅ `ReconnectModal.razor` traduzido para pt-BR | Concluído |
| N2 | 🟠 Média | ✅ Validação adicionada nos DTOs críticos | Concluído |
| N4 | 🟡 Baixa | ✅ GUID movido do construtor do modelo para o service (ReservaService) | Concluído |
| N6 | 🟡 Baixa | ✅ README.md atualizado | Concluído |
| N8 | ⚪ Mínima | ✅ Documentação adicionada no `docker-compose.test.yml` sobre senha padrão | Concluído |
| UX1 | 🟠 Crítico | ✅ ChavePIX recuperável na página do ingresso | Concluído |
| UX2 | 🟠 Crítico | ✅ Polling de status PIX a cada 15s no modal de compra | Concluído |
| UX4 | 🟡 Médio | ✅ Botão favoritar na vitrine e detalhe do evento | Concluído |
| UX5 | 🟡 Médio | ✅ QR Code condicional ao status do pagamento | Concluído |
| UX6 | 🟡 Baixa | ✅ "Lembrar-me" no login (checkbox + JWT de 7 dias) | Concluído |
| UX7 | 🟡 Baixa | ✅ Documentação da fila de espera e email no SETUP_DEV.md | Concluído |
| UX9 | ⚪ Mínima | ✅ Badge "⚡ X restantes" nos cards da vitrine (quando ≤ 20 vagas) | Concluído |

### 📋 Backlog (para próximas sprints)

| ID | Prioridade | Descrição | Esforço Estimado |
|----|-----------|-----------|------------------|
| N3 | 🟡 Baixa | Substituir string enums por enums reais | 3-4h + migration |
| N4 | 🟡 Baixa | Mover GUID generation para service layer | 30 min |
| N5 | 🟡 Baixa | Adicionar CreatedAt/UpdatedAt nos modelos | 2-3h + migration |
| N8 | ⚪ Mínima | Documentar senha padrão no docker-compose.test.yml | 5 min |

---

## 🔍 Auditoria de Experiência do Usuário (UX) — Jornada Completa

> Analisado em: 16/05/2026 — Percorrendo todo o fluxo: descoberta → cadastro → compra → pagamento → pós-compra → cancelamento

### ✅ Pontos Positivos (o que já funciona bem)

| Aspecto | Detalhes |
|---------|----------|
| **Vitrine** | Busca por nome, filtro por gênero/data, paginação server-side (12 cards), skeleton loading, SEO com Schema.org |
| **Detalhe do evento** | Info grid completo, galeria de fotos, tipos de ingresso (setores), lotes progressivos, política de reembolso, links para calendário (Google + .ics) |
| **Cadastro** | Validação com FluentValidation, máscara de CPF, verificação de e-mail obrigatória, aceite de Termos/Privacidade |
| **Login** | Input com máscara de CPF, link "Esqueceu a senha?", rate limiting (5 tentativas/min) |
| **Compra - Cartão** | Tokenização segura via MercadoPago SDK (iframes) — PCI-DSS compliance, campos com estilo consistente |
| **Compra - PIX** | QR Code gerado via QRCoder, botão "Copiar chave PIX" com clipboard API, fallback manual |
| **Meia-entrada** | Upload de documento comprobatório com validação de tipo/tamanho, suporte a múltiplos formatos |
| **Seguro** | Opção de seguro de devolução (15%) com explicação clara do benefício |
| **Ingresso digital** | QR Code para check-in, código alfanumérico, status visual (Ativa/Usada/Cancelada), links para calendário |
| **Cancelamento** | Termo de devolução com breakdown de valores, checkbox de aceite obrigatório, estorno via gateway |
| **Transferência** | Modal para transferir ingresso para outro CPF |
| **Avaliações** | Modal com estrelas + comentário, nota média no evento |
| **LGPD** | Seção completa: baixar dados (Art. 18 I), excluir conta (Art. 18 VI), dados financeiros preservados por lei |
| **Segurança** | JWT + refresh token httpOnly, rate limiting por usuário, blacklist de tokens, CSRF protection, audit trail com hash SHA256 |
| **Tema** | Dark/light mode automático (prefers-color-scheme) + manual, persistido em localStorage |
| **Responsividade** | Layout adaptável para mobile/tablet/desktop |

### ❌ Problemas de UX Encontrados

---

#### UX1 🟠 — Chave PIX não recuperável após fechar modal de compra
**Arquivos:** `ui/.../Shared/CompraModal.razor`, `src/DTOs/ReservationDetailDto.cs`, `ui/.../Pages/MeuIngresso.razor`

**Problema:** Quando o usuário compra via PIX, o QR Code é exibido no modal de sucesso. Se ele fechar o modal sem copiar a chave, **não há como recuperá-la**. O `ReservationDetailDto` (usado em MeuIngresso.razor) **não possui campo `ChavePix`**. A chave PIX fica criptografada no banco mas nunca é exposta novamente.

**Impacto:** Usuário perde acesso ao código PIX e não consegue pagar. A compra fica pendente para sempre.

**O que fazer:**
1. Adicionar campo `ChavePix` no `ReservationDetailDto`.
2. No `MeuIngresso.razor`, exibir o QR Code PIX e a chave quando o status da reserva for "Aguardando Pagamento".
3. O backend já descriptografa a ChavePix no serviço — só precisa incluí-la no DTO de detalhes.

---

#### UX2 🟠 — Sem acompanhamento de status do PIX (polling)
**Arquivo:** `ui/.../Shared/CompraModal.razor`

**Problema:** Após gerar o QR Code PIX, não há polling de status. O modal não informa se o pagamento foi confirmado. O usuário pode fechar o modal pensando que a compra foi concluída, mas na verdade ela só será efetivada após o pagamento PIX ser confirmado (via webhook).

**Impacto:** Usuário pode perder o prazo do PIX (vencimento em 15-30 min no MercadoPago real).

**O que fazer:**
1. Adicionar polling a cada 15s no modal de sucesso PIX para verificar se o pagamento foi confirmado.
2. Exibir timer de vencimento do PIX.
3. Quando confirmado, atualizar o modal para "Pagamento confirmado! ✅".

---

#### UX3 🟠 — Sem confirmação por e-mail em desenvolvimento
**Arquivos:** `src/Service/EmailTemplateService.cs`, `src/Program.cs`

**Problema:** Em desenvolvimento (sem SMTP configurado), os e-mails são logados no console (`ConsoleEmailService`). O usuário **nunca recebe**:
- E-mail de confirmação de cadastro (link de verificação)
- E-mail de confirmação de compra
- E-mail de cancelamento
- E-mail de redefinição de senha

**Impacto:** Em desenvolvimento, o fluxo de verificação de e-mail não pode ser testado, e a fila de espera não notifica ninguém.

**O que fazer:** Para desenvolvimento, exibir os e-mails simulados em uma página acessível `/admin/emails` ou pelo menos logar com mais destaque no console (já é feito, mas vale documentar).

---

#### UX4 🟡 — Botão "Favoritar" ausente na vitrine e no detalhe do evento
**Arquivos:** `ui/.../Pages/EventosDisponiveis.razor`, `ui/.../Pages/DetalheEvento.razor`

**Problema:** O backend `FavoritoController` e a página `Favoritos.razor` existem, mas **não há botão de favoritar** em lugar nenhum da interface. O usuário não consegue adicionar eventos aos favoritos.

**Impacto:** Funcionalidade de favoritos invisível — ninguém usa.

**O que fazer:**
1. Adicionar ícone de coração ❤️ no canto dos cards da vitrine.
2. Adicionar botão de favoritar na página de detalhes do evento.
3. Usar o endpoint `POST /api/favoritos/{eventoId}` e alternar visualmente.

---

#### UX5 🟡 — QR Code do ingresso visível mesmo antes do PIX ser pago
**Arquivo:** `ui/.../Pages/MeuIngresso.razor`

**Problema:** Se a compra foi PIX e ainda não foi confirmada, o QR Code do ingresso (para check-in) já é exibido. O usuário pode tentar usar um ingresso não pago.

**Impacto:** Confusão operacional — porteiro pode receber ingresso não pago.

**O que fazer:** Exibir o QR Code do ingresso apenas quando `Status == "Ativa"` (paga e confirmada). Para `Status == "Aguardando Pagamento"`, exibir QR Code PIX e mensagem "Aguardando confirmação do pagamento".

---

#### UX6 🟡 — Sem "Lembrar-me" no login
**Arquivo:** `ui/.../Pages/Login.razor`

**Problema:** O JWT expira em 30 minutos e não há opção "Lembrar-me" no login. O refresh token rotaciona mas mantém a sessão viva por até 30 dias apenas se o usuário usar o app continuamente.

**Impacto:** Usuário precisa fazer login repetidamente se não usar a plataforma por mais de algumas horas (mobile, por exemplo).

**O que fazer:** Adicionar checkbox "Lembrar-me" que estende o `ExpireMinutes` do JWT para 7 dias em vez de 30 minutos.

---

#### UX7 🟡 — Fila de espera sem notificação em desenvolvimento
**Arquivo:** `src/Service/FilaEsperaService.cs`

**Problema:** Quando uma vaga é liberada por cancelamento, o sistema tenta notificar o próximo da fila **por e-mail**. Sem SMTP configurado, ninguém é notificado.

**Impacto:** Fila de espera não funciona em desenvolvimento.

**O que fazer:** Documentar no `SETUP_DEV.md` que a fila de espera requer SMTP. (Não é blocker — em produção terá SMTP.)

---

#### UX8 ⚪ — Sem busca por localização (cidade/estado)
**Arquivo:** `ui/.../Pages/EventosDisponiveis.razor`

**Problema:** Para uma plataforma de eventos, não ter filtro por localização é uma limitação significativa. O modelo `TicketEvent` não possui campos `Cidade`/`Estado`.

**Impacto:** Usuário não consegue filtrar eventos por região.

---

#### UX9 ⚪ — Sem badge de "ingressos restantes" no card da vitrine
**Arquivo:** `ui/.../Pages/EventosDisponiveis.razor`

**Problema:** Os cards da vitrine mostram preço e capacidade total, mas não mostram vagas restantes nem um badge de "Últimos ingressos" quando está quase esgotado.

**Impacto:** Menos urgência e conversão.

---

## Ordem de Execução Recomendada (Atualizada)

```
✅ TODOS OS ITENS IMPLEMENTADOS
  ├── 🟠🔴 B1, B2, S1, S2, S5, S6, N1, N2, UX1, UX2 (10 críticos/médios)
  ├── 🟡⚪ N4, N6, UX4, UX5, UX6, UX7, UX9 (7 baixos/mínimos)
  └── N8 (docker-compose.test.yml doc)

📋 PRÓXIMA SPRINT (se desejar)
  ├── UX3 🟠 Email funcional em dev (ConsoleEmailService → painel /admin/emails)
  ├── UX8 ⚪ Busca por localização (cidade/estado)
  ├── N3 🟡 String enums → enums reais
  ├── N5 🟡 Timestamps de auditoria (CreatedAt/UpdatedAt)
  └── B4 (QR scanner check-in), D1-D5 (Features)
```
