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
