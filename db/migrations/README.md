# Database Migrations

Este diretório contém migrações incrementais para o banco de dados TicketPrime.

## Como funciona

Diferente do `db/script.sql` (que cria o schema do zero), as migrações aqui são
**incrementais e versionadas** — cada arquivo representa uma mudança no schema
que pode ser aplicada com segurança em cima da versão anterior.

## Nomenclatura

```
V{numero}__{descricao}.sql
```

Exemplos:
- `V001__InitialSchema.sql` — Já existe como `db/script.sql`
- `V002__AddMeiaEntrada.sql` — Adiciona suporte a meia-entrada (Lei 12.933/2013)
- `V003__AddTicketTypes.sql` — Adiciona tipos de ingresso e lotes progressivos
- `V004__AddConstraintsAndIndexes.sql` — Constraints e índices de performance

## Como aplicar

### Via DbUp (recomendado para produção)

```bash
# Instalar ferramenta DbUp
dotnet tool install --global dotnet-dbup

# Aplicar migrações
dotnet dbup migrate \
  --connection "Server=localhost;Database=TicketPrime;User Id=sa;Password=...;TrustServerCertificate=True;" \
  --scripts ./db/migrations
```

### Via SSMS (para desenvolvimento)

Abra cada arquivo `.sql` no SSMS e execute (F5) em ordem numérica.

### Via container Docker

O `script.sql` principal é executado automaticamente na inicialização do container
SQL Server via `docker/init-db.sh`. Para aplicar migrações incrementais, execute:

```bash
# Conecte ao container SQL Server e aplique manualmente
docker exec -i ticketprime_sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -d TicketPrime -C \
  -i db/migrations/V004__AddConstraintsAndIndexes.sql
```

## Boas práticas

1. **Nunca edite uma migração já aplicada** — crie uma nova (V{proximo_numero}__...)
2. **Sempre inclua `IF NOT EXISTS`** nas alterações (migrações são idempotentes)
3. **Teste as migrações** em um banco de desenvolvimento antes de aplicar em produção
4. **Documente breaking changes** no início do arquivo de migração
