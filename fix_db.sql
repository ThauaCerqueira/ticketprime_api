-- Fix 1: Add missing columns to Reservas table (for TicketType and Lote support)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reservas' AND COLUMN_NAME = 'TicketTypeId')
    ALTER TABLE Reservas ADD TicketTypeId INT NULL;
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reservas' AND COLUMN_NAME = 'LoteId')
    ALTER TABLE Reservas ADD LoteId INT NULL;
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reservas' AND COLUMN_NAME = 'CodigoTransacaoGateway')
    ALTER TABLE Reservas ADD CodigoTransacaoGateway NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reservas' AND COLUMN_NAME = 'IdEstornoGateway')
    ALTER TABLE Reservas ADD IdEstornoGateway NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reservas' AND COLUMN_NAME = 'DataEstorno')
    ALTER TABLE Reservas ADD DataEstorno DATETIME NULL;
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reservas' AND COLUMN_NAME = 'ChavePix')
    ALTER TABLE Reservas ADD ChavePix NVARCHAR(MAX) NULL;
GO

-- Fix 2: Set email verified for our test user
UPDATE Usuarios SET EmailVerificado = 1 WHERE Cpf = '04885687306';
GO

-- Check the result
SELECT Cpf, Nome, Email, EmailVerificado FROM Usuarios WHERE Cpf = '04885687306';
GO

-- Fix 3: Add Slug column to Usuarios (opaque public identifier, replaces CPF in URLs)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Usuarios' AND COLUMN_NAME = 'Slug')
    ALTER TABLE Usuarios ADD Slug VARCHAR(32) NULL;
GO

-- Ensure Slug has a unique index for fast lookups (only for non-null slugs)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Usuarios_Slug')
    CREATE UNIQUE NONCLUSTERED INDEX IX_Usuarios_Slug ON Usuarios (Slug) WHERE Slug IS NOT NULL;
GO

-- Generate slugs for existing organizers (ADMIN or ORGANIZADOR profiles) that don't have one yet
UPDATE Usuarios
SET Slug = LOWER(SUBSTRING(CONVERT(VARCHAR(40), HASHBYTES('SHA1', Cpf), 2), 1, 16))
WHERE Slug IS NULL AND Perfil IN ('ADMIN', 'ORGANIZADOR');
GO

-- Verify the migration
SELECT Cpf, Nome, Perfil, Slug FROM Usuarios WHERE Perfil IN ('ADMIN', 'ORGANIZADOR');
GO

-- ═══════════════════════════════════════════════════════════════════
-- FIX 4: Missing Constraints — integridade referencial e de domínio
-- ═══════════════════════════════════════════════════════════════════
-- ANTES: Nenhuma dessas constraints existia, permitindo dados
-- inconsistentes (email duplicado, capacidade zero, preço negativo).
-- AGORA: Todas adicionadas com verificação de existência.
-- ═══════════════════════════════════════════════════════════════════

-- 4a: UNIQUE constraint no Email da tabela Usuarios
-- ANTES: Dois usuários podiam ter o mesmo email → confusão, LGPD violation
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Usuarios_Email')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Usuarios_Email ON Usuarios (Email) WHERE Email IS NOT NULL;
GO

-- 4b: CHECK constraint CapacidadeTotal > 0 na tabela Eventos
-- ANTES: Capacidade = 0 ou negativa era permitida
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Eventos_CapacidadeTotal')
    ALTER TABLE Eventos ADD CONSTRAINT CK_Eventos_CapacidadeTotal CHECK (CapacidadeTotal > 0);
GO

-- 4c: CHECK constraint PrecoPadrao >= 0 na tabela Eventos
-- ANTES: Preço negativo era possível
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Eventos_PrecoPadrao')
    ALTER TABLE Eventos ADD CONSTRAINT CK_Eventos_PrecoPadrao CHECK (PrecoPadrao >= 0);
GO

-- 4d: Tornar Perfil NOT NULL na tabela Usuarios (se não houver NULLs)
-- ANTES: Perfil podia ser NULL
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Usuarios' AND COLUMN_NAME = 'Perfil' AND IS_NULLABLE = 'NO')
BEGIN
    -- Primeiro, preenche NULLs existentes com 'CLIENTE'
    UPDATE Usuarios SET Perfil = 'CLIENTE' WHERE Perfil IS NULL;
    ALTER TABLE Usuarios ALTER COLUMN Perfil VARCHAR(10) NOT NULL;
END
GO

-- 4e: Adicionar default value para DataCompra na Reservas
IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('Reservas') AND name = 'DF_Reservas_DataCompra')
    ALTER TABLE Reservas ADD CONSTRAINT DF_Reservas_DataCompra DEFAULT GETDATE() FOR DataCompra;
GO

-- 4f: Adicionar default value para Status na Reservas
IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('Reservas') AND name = 'DF_Reservas_Status')
    ALTER TABLE Reservas ADD CONSTRAINT DF_Reservas_Status DEFAULT 'Ativa' FOR Status;
GO

-- ═══════════════════════════════════════════════════════════════════
-- FIX 5: Missing Indexes — performance
-- ═══════════════════════════════════════════════════════════════════
-- ANTES: Consultas por data, status, e perfil usavam full scan.
-- AGORA: Índices criados para queries comuns.
-- ═══════════════════════════════════════════════════════════════════

-- 5a: Index para queries de data (eventos futuros, calendário)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Eventos_DataEvento')
    CREATE NONCLUSTERED INDEX IX_Eventos_DataEvento ON Eventos (DataEvento) INCLUDE (Nome, CapacidadeTotal, PrecoPadrao, Status);
GO

-- 5b: Index para filtro por status (rascunho, publicado, cancelado)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Eventos_Status')
    CREATE NONCLUSTERED INDEX IX_Eventos_Status ON Eventos (Status) INCLUDE (Nome, DataEvento, CapacidadeTotal);
GO

-- 5c: Index para busca de cupons por expiração
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Cupons_DataExpiracao')
    CREATE NONCLUSTERED INDEX IX_Cupons_DataExpiracao ON Cupons (DataExpiracao) INCLUDE (Codigo, PorcentagemDesconto, TotalUsado, LimiteUsos);
GO

-- 5d: Index para busca de reservas por status (checkin, cancelamento)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Reservas_Status')
    CREATE NONCLUSTERED INDEX IX_Reservas_Status ON Reservas (Status) INCLUDE (UsuarioCpf, EventoId, CodigoIngresso, ValorFinalPago);
GO

-- 5e: Index para busca de usuários por perfil (admin queries)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Usuarios_Perfil')
    CREATE NONCLUSTERED INDEX IX_Usuarios_Perfil ON Usuarios (Perfil) INCLUDE (Nome, Email);
GO

-- ═══════════════════════════════════════════════════════════════════
-- FIX 6: Admin password reset on first deploy
-- ═══════════════════════════════════════════════════════════════════
-- SEGURANÇA: Em produção, a senha 'admin123' (pública no README) DEVE
-- ser trocada. Este script força a troca de senha no primeiro login
-- marcando a conta com senha temporária.
-- ═══════════════════════════════════════════════════════════════════
-- ATENÇÃO: Descomente a linha abaixo em produção para forçar troca
-- UPDATE Usuarios SET SenhaTemporaria = 1 WHERE Cpf = '00000000191';
GO

-- ═══════════════════════════════════════════════════════════════════
-- FIX 7: Verify all constraints
-- ═══════════════════════════════════════════════════════════════════
SELECT 
    OBJECT_NAME(parent_object_id) AS Tabela,
    name AS ConstraintName,
    type_desc AS Tipo
FROM sys.check_constraints
WHERE parent_object_id IN (OBJECT_ID('Eventos'), OBJECT_ID('Usuarios'))
UNION ALL
SELECT 
    OBJECT_NAME(parent_object_id) AS Tabela,
    name AS IndexName,
    'UNIQUE INDEX' AS Tipo
FROM sys.indexes
WHERE is_unique = 1 AND is_primary_key = 0
    AND OBJECT_NAME(parent_object_id) IN ('Usuarios', 'Eventos', 'Reservas', 'Cupons')
ORDER BY Tabela, Tipo;
GO
