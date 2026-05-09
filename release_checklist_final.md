# Definition of Done (DoD) / Release Checklist

> Marque **[x]** quando o item estiver concluído.

## 1. Requisitos Funcionais

### AV1 — Foundation
- [x] **POST /api/eventos** — Criar evento com nome, data, local, capacidade > 0, preço
- [x] **GET /api/eventos** — Listar todos os eventos
- [x] **POST /api/cupons** — Criar cupom com código único, porcentagem de desconto, valor mínimo
- [x] **POST /api/usuarios** — Cadastrar usuário com CPF, nome, email, senha, perfil
- [x] **Documentação:** User stories com BDD (6 cenários)
- [x] **README:** Comandos de execução (back, front, db)

### AV2 — Core System
- [x] **GET /api/reservas/{cpf}** — Listar reservas por CPF com INNER JOIN (dados do evento + usuário)
- [x] **POST /api/reservas** — Comprar ingresso com regras:
  - [x] R1: Validar existência do usuário e do evento
  - [x] R2: Máximo 2 reservas por CPF por evento
  - [x] R3: Não exceder CapacidadeTotal do evento
  - [x] R4: Aplicar cupom de desconto (se PrecoPadrao >= ValorMinimoRegra)
- [x] **ADR** — Architecture Decision Record (7 decisões documentadas)
- [x] **Matriz de Riscos** — 8 riscos identificados com mitigação
- [x] **Métricas** — Código, API, Negócio (12 métricas)
- [x] **SLO** — 5 objetivos de nível de serviço
- [x] **Error Budget** — Budget mensal com política (Green/Yellow/Red)
- [x] **Modelo de Ameaças STRIDE** — 6 ameaças com contramedidas
- [x] **SSDF** — Sem hardcoded secrets no código-fonte

## 2. Qualidade

### Testes Unitários
- [ ] Teste para R1: usuário não encontrado → InvalidOperationException
- [ ] Teste para R1: evento não encontrado → InvalidOperationException
- [ ] Teste para R1: evento já aconteceu → InvalidOperationException
- [ ] Teste para R2: limite de 2 reservas → InvalidOperationException
- [ ] Teste para R3: capacidade esgotada → InvalidOperationException
- [ ] Teste para R4: cupom válido com valor mínimo atingido → desconto aplicado
- [ ] Teste para R4: cupom válido mas valor mínimo não atingido → preço cheio
- [ ] Teste para R4: cupom inválido → InvalidOperationException
- [ ] Teste para GET /api/reservas/{cpf}: lista não vazia
- [ ] Teste para GET /api/reservas/{cpf}: CPF sem reservas → lista vazia

### Build
- [ ] `dotnet build` sem erros e sem warnings
- [ ] `dotnet test` — todas as unidades passando

## 3. Segurança (SSDF)

- [x] Connection string via `appsettings.json` + User Secrets (não hardcoded)
- [x] JWT Secret Key via `appsettings.json` + User Secrets
- [x] SQL Injection prevention: parâmetros `@nome` no Dapper (100% das queries)
- [x] Autenticação JWT em todos os endpoints sensíveis
- [x] Autorização por Role (ADMIN) em endpoints de criação
- [x] HTTPS forçado em produção
- [x] Logs não expõem dados sensíveis (senha, connection string)

## 4. Experiência do Usuário (Frontend Blazor)

- [ ] Tela de login funcional (JWT salvo em sessão)
- [ ] Listagem de eventos disponíveis
- [ ] Compra de ingresso com campo de cupom opcional
- [ ] Feedback visual para regras R1-R4 (mensagens de erro)
- [ ] Listagem de reservas do usuário
- [ ] Cancelamento de reserva

## 5. Documentação

- [x] `docs/adr.md` — 7 ADRs
- [x] `docs/operacao.md` — Riscos, Métricas, SLO, Error Budget, STRIDE
- [x] `docs/requisitos.md` — User stories com BDD
- [x] `README.md` — Instruções de execução
- [x] `release_checklist_final.md` — Este documento

---

## Histórico de Releases

| Versão | Data | Itens Concluídos |
|--------|------|------------------|
| v1.0 | Pendente | Release inicial |
