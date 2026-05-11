# Architecture Decision Record (ADR)

## ADR-001: Uso de Minimal API com .NET 10

## Contexto
Projeto acadêmico TicketPrime, foco em simplicidade e agilidade.

## Decisão
Adotar Minimal API em vez de Controllers MVC tradicionais.

## Consequências
Prós:
- Menos boilerplate (`Program.cs` único vs Controllers + Startups)
- Roteamento inline facilita entendimento do fluxo completo

Contras:
- Perde organização por separação de concerns (mitigado com Services/Repositories separados)

---

## ADR-002: Dapper como Micro-ORM

## Contexto
Necessário acesso a banco de dados SQL Server com consultas parametrizadas anti-SQL Injection.

## Decisão
Usar Dapper em vez de Entity Framework Core.

## Consequências
Prós:
- SQL explícito permite otimização fina de queries
- Performance superior (mapeamento direto DataReader → objeto)
- Parâmetros nomeados (`@param`) previnem SQL Injection

Contras:
- Mais código manual (INSERT/SELECT escritos à mão)

---

## ADR-003: JWT Bearer para Autenticação

## Contexto
Necessário autenticação stateless entre frontend Blazor Server e API.

## Decisão
Token JWT com claims de CPF e Perfil (ADMIN/USER).

## Consequências
Prós:
- Sem estado no servidor (escalável)
- Claims embutidas no token (CPF, Perfil)

Contras:
- Revogação manual não é possível sem blacklist

---

## ADR-004: Separação em Camadas (Service + Repository)

## Contexto
Regras de negócio AV2 (R1-R4) precisam ser testáveis isoladamente.

## Decisão
- **Repository:** Apenas SQL + Dapper (sem regras de negócio)
- **Service:** Toda lógica de validação (R1-R4)
- **DTO:** Objetos de entrada/saída desacoplados das Models

## Consequências
Prós:
- Testes unitários via Mock de interfaces (Repository → Service)
- Substituição de banco sem alterar regras de negócio

Contras:
- Mais arquivos para gerenciar

---

## ADR-005: Cupom como Desconto Percentual com Valor Mínimo (R4)

## Contexto
Regra de negócio R4: desconto só se aplica se `PrecoPadrao >= ValorMinimoRegra`.

## Decisão
- Cupom tem `PorcentagemDesconto` (decimal) e `ValorMinimoRegra` (decimal)
- Desconto = `PrecoPadrao * (PorcentagemDesconto / 100)`
- `ValorFinalPago` = `PrecoPadrao - desconto` (se regra mínima atingida)

## Consequências
Prós:
- Desconto proporcional ao preço do ingresso
- Cupons de alto valor mínimo não se aplicam a eventos baratos

Contras:
- Desconto não é valor fixo, mas percentual

---

## ADR-006: INNER JOIN para GET /api/reservas/{cpf}

## Contexto
AV2 exige endpoint único que retorne dados consolidados de reserva + evento + usuário.

## Decisão
- Query única com `INNER JOIN Reservas → Eventos` e `INNER JOIN Reservas → Usuarios`
- Resultado mapeado para `ReservaDetalhadaDTO` (achatado, sem aninhamento)

## Consequências
Prós:
- Uma única ida ao banco (N+1 evitado)
- DTO plano é serializável diretamente como JSON

Contras:
- DTO precisa ser mantido em sincronia com a query SQL

---

## ADR-007: Limite de 2 Reservas por CPF por Evento (R2)

## Contexto
Regra de negócio R2 impede que um CPF reserve mais que 2 ingressos para o mesmo evento.

## Decisão
- Verificação via `COUNT(*)` na tabela Reservas filtrando por `UsuarioCpf` e `EventoId`
- Bloqueio na camada Service (não no banco) para mensagem de erro amigável

## Consequências
Prós:
- Mensagem clara para o usuário ("Você já atingiu o limite de 2 reservas")

Contras:
- Race condition teórica em requisições simultâneas (aceitável para projeto acadêmico)
