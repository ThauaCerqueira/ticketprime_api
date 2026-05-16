-- ═══════════════════════════════════════════════════════════════════
-- V004: Constraints de integridade e índices de performance
-- ═══════════════════════════════════════════════════════════════════
--
-- ANTES: 
--   - Email sem UNIQUE constraint → duplicatas possíveis
--   - Capacidade/Preço sem CHECK → valores inválidos
--   - Perfil NULL → dados inconsistentes
--   - Faltando índices em DataEvento, Status, Perfil → full scans
--
-- AGORA:
--   - Todas as constraints com verificação de existência
--   - Índices covering para queries comuns
--   - Default values para colunas críticas
--
-- ═══════════════════════════════════════════════════════════════════

BEGIN TRANSACTION;

-- ── 1. UNIQUE constraint no Email ──────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Usuarios_Email')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Usuarios_Email ON Usuarios (Email) WHERE Email IS NOT NULL;
GO

-- ── 2. CHECK constraints ──────────────────────────────────────────

-- CapacidadeTotal > 0
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Eventos_CapacidadeTotal')
    ALTER TABLE Eventos ADD CONSTRAINT CK_Eventos_CapacidadeTotal CHECK (CapacidadeTotal > 0);
GO

-- PrecoPadrao >= 0
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Eventos_PrecoPadrao')
    ALTER TABLE Eventos ADD CONSTRAINT CK_Eventos_PrecoPadrao CHECK (PrecoPadrao >= 0);
GO

-- ── 3. Default values ─────────────────────────────────────────────

-- DataCompra default = GETDATE()
IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('Reservas') AND name = 'DF_Reservas_DataCompra')
    ALTER TABLE Reservas ADD CONSTRAINT DF_Reservas_DataCompra DEFAULT GETDATE() FOR DataCompra;
GO

-- Status default = 'Ativa'
IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('Reservas') AND name = 'DF_Reservas_Status')
    ALTER TABLE Reservas ADD CONSTRAINT DF_Reservas_Status DEFAULT 'Ativa' FOR Status;
GO

-- ── 4. Tornar Perfil NOT NULL ─────────────────────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Usuarios' AND COLUMN_NAME = 'Perfil' AND IS_NULLABLE = 'NO')
BEGIN
    UPDATE Usuarios SET Perfil = 'CLIENTE' WHERE Perfil IS NULL;
    ALTER TABLE Usuarios ALTER COLUMN Perfil VARCHAR(10) NOT NULL;
END
GO

-- ── 5. Índices de performance ─────────────────────────────────────

-- Eventos por data (eventos futuros, calendário)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Eventos_DataEvento')
    CREATE NONCLUSTERED INDEX IX_Eventos_DataEvento ON Eventos (DataEvento) 
        INCLUDE (Nome, CapacidadeTotal, PrecoPadrao, Status);
GO

-- Eventos por status (rascunho, publicado, cancelado)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Eventos_Status')
    CREATE NONCLUSTERED INDEX IX_Eventos_Status ON Eventos (Status) 
        INCLUDE (Nome, DataEvento, CapacidadeTotal);
GO

-- Cupons por expiração
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Cupons_DataExpiracao')
    CREATE NONCLUSTERED INDEX IX_Cupons_DataExpiracao ON Cupons (DataExpiracao) 
        INCLUDE (Codigo, PorcentagemDesconto, TotalUsado, LimiteUsos);
GO

-- Reservas por status (checkin, cancelamento)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Reservas_Status')
    CREATE NONCLUSTERED INDEX IX_Reservas_Status ON Reservas (Status) 
        INCLUDE (UsuarioCpf, EventoId, CodigoIngresso, ValorFinalPago);
GO

-- Usuários por perfil (admin queries)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Usuarios_Perfil')
    CREATE NONCLUSTERED INDEX IX_Usuarios_Perfil ON Usuarios (Perfil) 
        INCLUDE (Nome, Email);
GO

-- ── 6. Verificação ────────────────────────────────────────────────
SELECT 
    OBJECT_NAME(parent_object_id) AS Tabela,
    name AS Nome,
    type_desc AS Tipo
FROM sys.check_constraints
WHERE parent_object_id IN (OBJECT_ID('Eventos'), OBJECT_ID('Usuarios'))
UNION ALL
SELECT 
    OBJECT_NAME(parent_object_id) AS Tabela,
    name AS Nome,
    'UNIQUE INDEX' AS Tipo
FROM sys.indexes
WHERE is_unique = 1 AND is_primary_key = 0
    AND OBJECT_NAME(parent_object_id) IN ('Usuarios', 'Eventos', 'Reservas', 'Cupons')
ORDER BY Tabela, Tipo;
GO

COMMIT;
