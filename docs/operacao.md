# Operação — TicketPrime

## Matriz de Riscos

| # | Risco | Probabilidade | Impacto | Mitigação |
|---|-------|:------------:|:-------:|-----------|
| R01 | Falha no banco de dados (SQL Server fora do ar) | Baixa | Alto | Connection string com failover; retry policy no Dapper; health check `/health` |
| R02 | SQL Injection | Muito Baixa | Crítico | Uso exclusivo de parâmetros nomeados `@param` no Dapper; sem concatenação de strings SQL |
| R03 | Token JWT expirado durante uso | Média | Médio | Frontend trata 401 e redireciona para login; token com 8h de validade |
| R04 | Race condition em compra simultânea do último ingresso | Baixa | Baixo | COUNT + verificação em nível de aplicação; em produção usar `SELECT ... WITH (UPDLOCK)` |
| R05 | CPF inválido sendo cadastrado | Média | Médio | Validação com `[Required]` e regex `^\d{11}$`;后端 pode validar dígito verificador |
| R06 | Cupom expirado ou já utilizado | Média | Baixo | Validação no service R4; cupons são reutilizáveis por design |
| R07 | Vazamento de connection string em logs | Baixa | Alto | Connection string lida de `appsettings.json` + User Secrets; nunca logada |
| R08 | Usuário não autenticado acessa endpoints protegidos | Muito Baixa | Alto | `RequireAuthorization()` em todos os endpoints sensíveis; JWT validado em cada requisição |

---

## Métricas

### Métricas de Código (coletadas via xUnit + Moq)

| Métrica | Alvo | Ferramenta |
|---------|:----:|-----------|
| Cobertura de testes unitários (Services) | ≥ 80% | xUnit + Coverage |
| Testes por regra de negócio (R1-R4) | ≥ 1 teste por regra | xUnit `[Fact]` / `[Theory]` |
| Testes de endpoint (rota + status code) | 100% dos endpoints | Integration tests (futuro) |

### Métricas de API (coletadas via logs/APM)

| Métrica | Descrição | Alvo |
|---------|-----------|:----:|
| Taxa de sucesso (200/201) | Percentual de requisições bem-sucedidas | ≥ 99.5% |
| Latência P95 (POST /api/reservas) | Tempo de resposta no percentil 95 | ≤ 500ms |
| Latência P95 (GET /api/eventos) | Tempo de resposta no percentil 95 | ≤ 200ms |
| Taxa de erro 5xx | Percentual de erros internos | ≤ 0.1% |
| Uptime da API | Disponibilidade do endpoint `/health` | ≥ 99.9% |

### Métricas de Negócio

| Métrica | Definição | Alvo |
|---------|-----------|:----:|
| Ingressos vendidos por evento | COUNT de Reservas por EventoId | ≤ CapacidadeTotal |
| Taxa de ocupação média | (Total ingressos vendidos / CapacidadeTotal soma) × 100 | ≥ 70% |
| Cupons utilizados | COUNT de Reservas com CupomUtilizado NOT NULL | Monitorar |
| Ticket médio | AVG(ValorFinalPago) por reserva | Monitorar |

---

## Service Level Objectives (SLO)

| Objetivo | Indicador | Período de medição | Target |
|----------|-----------|:------------------:|:------:|
| Disponibilidade da API | Uptime do endpoint `/health` | 30 dias corridos | 99.9% |
| Tempo de resposta POST reservas | Latência P95 | 7 dias corridos | ≤ 500ms |
| Tempo de resposta GET eventos | Latência P95 | 7 dias corridos | ≤ 200ms |
| Taxa de erro 5xx | Percentual de respostas HTTP 500 | 30 dias corridos | ≤ 0.1% |
| Integridade dos dados | Nenhuma reserva com EventoId inválido (FK) | Contínuo (banco) | 100% |

---

## Error Budget

| SLO | Error Budget (99.9% em 30 dias) |
|-----|:------------------------------:|
| Disponibilidade 99.9% | **43m 12s** de downtime permitido por mês |
| Taxa de erro 5xx ≤ 0.1% | Máximo de **0.1%** das requisições podem falhar em 30 dias |

### Consumo do Error Budget

| Mês | Downtime | Erro 5xx | Budget Restante | Ação Requerida |
|-----|:--------:|:--------:|:---------------:|----------------|
| Exemplo | 10 min | 0.05% | 77% | Nenhuma (dentro do orçamento) |
| ⚠️ Gatilho | > 30 min | > 0.08% | < 30% | Revisão de incidentes, pausar deploys |

### Política de Gestão

- **Green** (budget ≥ 70%): Deploys liberados normalmente
- **Yellow** (budget 30-70%): Deploys apenas em horário comercial com rollback preparado
- **Red** (budget < 30%): Congelamento de deploys; investigação de causa raiz obrigatória

---

## Modelo de Ameaças (Threat Modeling — STRIDE)

| Tipo | Ameaça | Contramedida |
|------|--------|-------------|
| **Spoofing** | Usuário falsifica CPF no token JWT | JWT assinado com chave secreta; validação em todo request |
| **Tampering** | Alteração de dados em trânsito | HTTPS obrigatório (configurado no launchSettings) |
| **Repudiation** | Usuário nega ter comprado ingresso | Logs de auditoria em todas as operações de reserva |
| **Information Disclosure** | Vazamento de dados via erro não tratado | Try/catch genérico retorna apenas `"Erro interno do servidor"` sem detalhes |
| **Denial of Service** | Flood de requisições POST /api/reservas | Rate limiting (futuro — middleware a implementar) |
| **Elevation of Privilege** | Usuário comum tenta criar evento (role ADMIN) | `RequireRole("ADMIN")` nos endpoints de criação |
