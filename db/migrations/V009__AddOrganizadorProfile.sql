-- ═══════════════════════════════════════════════════════════════════════════════
-- Migration V009: Adiciona campos de perfil do organizador
--
-- Bio, FotoUrl e BannerUrl para o perfil público do organizador.
-- ═══════════════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Usuarios' AND COLUMN_NAME = 'Bio')
BEGIN
    ALTER TABLE Usuarios ADD Bio NVARCHAR(500) NULL;
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Usuarios' AND COLUMN_NAME = 'FotoUrl')
BEGIN
    ALTER TABLE Usuarios ADD FotoUrl NVARCHAR(500) NULL;
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Usuarios' AND COLUMN_NAME = 'BannerUrl')
BEGIN
    ALTER TABLE Usuarios ADD BannerUrl NVARCHAR(500) NULL;
END
