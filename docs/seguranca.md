# Segurança — TicketPrime

Este documento descreve as medidas de segurança implementadas no projeto TicketPrime.

---

## 🔐 Autenticação e Sessão

| Medida | Implementação |
|--------|---------------|
| **JWT com HMAC-SHA256** | Tokens assinados com chave de 256+ bits configurável via `Jwt:Key` |
| **Refresh Token em cookie httpOnly** | `ticketprime_refresh` com `HttpOnly=true, Secure=true, SameSite=Strict` — imune a XSS |
| **Blacklist de tokens** | `JwtBlacklistService` com suporte a `IDistributedCache` (Redis) |
| **Rate limiting por usuário** | 4 políticas: login (5/min), compra (3/min), escrita (10/min), geral (60/min) |
| **Senha temporária para admin** | No 1º deploy, senha aleatória é gerada; admin é forçado a trocar |
| **BCrypt work factor 11+** | Hash de senhas com custo configurável (mínimo 11) |

## 💳 Pagamentos (PCI-DSS)

| Medida | Implementação |
|--------|---------------|
| **Tokenização via iframes** | SDK MercadoPago v2 — dados do cartão NUNCA passam pelo backend |
| **Chave PIX criptografada** | AES-256-GCM em repouso no banco (`PixCryptoService`) |
| **SimulatedPaymentGateway bloqueado** | Se `ASPNETCORE_ENVIRONMENT=Production` e token MP ausente, app não inicia |
| **NUNCA logar dados sensíveis** | Logs de pagamento omitem response body do gateway (anônimos) |

## 🛡️ Headers HTTP de Segurança

Aplicados pelo `SecurityHeadersMiddleware` em todas as respostas:

| Header | Valor |
|--------|-------|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |
| `Content-Security-Policy` | `default-src 'self'; script-src 'self' https://sdk.mercadopago.com ...; style-src 'self' 'unsafe-inline' ...; frame-ancestors 'none'; form-action 'self'` |

## 🗄️ Auditoria e LGPD

| Medida | Implementação |
|--------|---------------|
| **Audit trail com hash SHA256** | `AuditLogService` com encadeamento hash blockchain-like |
| **LGPD Art. 18 I — Acesso** | Usuário pode baixar todos os dados pessoais em `/perfil` |
| **LGPD Art. 18 VI — Exclusão** | Anonimização de dados pessoais (histórico financeiro preservado por lei) |
| **Todas as transações financeiras registradas** | Compra, cancelamento, estorno com timestamp, IP e CPF |

## 🔒 Dados em Repouso

| Medida | Implementação |
|--------|---------------|
| **Senhas com bcrypt** | `BCrypt.Net.BCrypt` com hash + salt, work factor 11+ |
| **Chave PIX criptografada** | AES-256-GCM no banco |
| **Fotos criptografadas (E2E)** | Web Crypto API — ECDH P-256 + AES-GCM-256 (opcional) |
| **Connection strings** | Apenas via variáveis de ambiente ou User Secrets |

## 🐳 Segurança em Container

| Medida | Implementação |
|--------|---------------|
| **Usuário não-root** | Containers executam como `appuser`, não como root |
| **curl instalado** | Apenas para healthcheck (removível em hardening) |
| **script.sql NÃO incluído** | Não copiado para imagem de produção |
| **Recursos limitados** | `deploy.resources.limits` em todos os serviços |
| **Healthchecks** | Em todos os serviços (sqlserver, redis, api, frontend, minio) |

## 🌐 Rede e Proxy

| Medida | Implementação |
|--------|---------------|
| **TLS termination** | nginx com certificados Let's Encrypt (auto-renewal) |
| **SSL protocolos** | `TLSv1.2 TLSv1.3` apenas |
| **CORS** | Restrito a origens configuradas via `AllowedOrigins` |
| **Rate limiting no nginx** | 200 req/s por IP |
| **Server tokens ocultos** | `server_tokens off` no nginx |

## 📧 E-mail

| Medida | Implementação |
|--------|---------------|
| **SMTP obrigatório em produção** | App não inicia sem SMTP configurado |
| **ConsoleEmailService apenas em dev** | Emails exibidos no console, nunca enviados |
| **InMemoryEmailStore apenas em dev** | Painel `/admin/emails` não disponível em produção |

## ✅ Checklist de Segurança para Produção

- [ ] `Jwt:Key` com 32+ caracteres aleatórios (não a chave padrão)
- [ ] `SA_PASSWORD` forte (não a senha de exemplo)
- [ ] `MercadoPago:AccessToken` configurado (produção)
- [ ] `EmailSettings:SmtpHost` configurado
- [ ] `Redis:Connection` configurado
- [ ] Certificado TLS válido (Let's Encrypt)
- [ ] Domínio configurado no `AllowedOrigins`
- [ ] Backup automatizado via MinIO configurado
- [ ] Senha do admin trocada (não mais a padrão)
