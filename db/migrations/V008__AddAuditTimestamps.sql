-- ═══════════════════════════════════════════════════════════════════════════════
-- Migration V008: Adiciona CreatedAt / UpdatedAt nas tabelas principais
--
-- Para LGPD e auditoria, saber quando um registro foi criado/modificado é
-- essencial. As colunas são populadas com DEFAULT = GETUTCDATE() na criação
-- e atualizadas via trigger no UPDATE.
-- ═══════════════════════════════════════════════════════════════════════════════

-- ── Eventos ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Eventos' AND COLUMN_NAME = 'CreatedAt')
BEGIN
    ALTER TABLE Eventos ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Eventos' AND COLUMN_NAME = 'UpdatedAt')
BEGIN
    ALTER TABLE Eventos ADD UpdatedAt DATETIME2 NULL;
END

-- ── Usuarios ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Usuarios' AND COLUMN_NAME = 'CreatedAt')
BEGIN
    ALTER TABLE Usuarios ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Usuarios' AND COLUMN_NAME = 'UpdatedAt')
BEGIN
    ALTER TABLE Usuarios ADD UpdatedAt DATETIME2 NULL;
END

-- ── Reservas ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Reservas' AND COLUMN_NAME = 'CreatedAt')
BEGIN
    ALTER TABLE Reservas ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Reservas' AND COLUMN_NAME = 'UpdatedAt')
BEGIN
    ALTER TABLE Reservas ADD UpdatedAt DATETIME2 NULL;
END

-- ── Cupons ──────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Cupons' AND COLUMN_NAME = 'CreatedAt')
BEGIN
    ALTER TABLE Cupons ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Cupons' AND COLUMN_NAME = 'UpdatedAt')
BEGIN
    ALTER TABLE Cupons ADD UpdatedAt DATETIME2 NULL;
END

-- ── Trigger de UpdatedAt para Eventos ──────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_Eventos_UpdatedAt')
BEGIN
    EXEC('
        CREATE TRIGGER TR_Eventos_UpdatedAt
        ON Eventos AFTER UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;
            UPDATE e SET UpdatedAt = GETUTCDATE()
            FROM Eventos e
            INNER JOIN inserted i ON i.Id = e.Id;
        END
    ');
END

-- ── Trigger de UpdatedAt para Usuarios ─────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_Usuarios_UpdatedAt')
BEGIN
    EXEC('
        CREATE TRIGGER TR_Usuarios_UpdatedAt
        ON Usuarios AFTER UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;
            UPDATE u SET UpdatedAt = GETUTCDATE()
            FROM Usuarios u
            INNER JOIN inserted i ON i.Cpf = u.Cpf;
        END
    ');
END

-- ── Trigger de UpdatedAt para Reservas ─────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_Reservas_UpdatedAt')
BEGIN
    EXEC('
        CREATE TRIGGER TR_Reservas_UpdatedAt
        ON Reservas AFTER UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;
            UPDATE r SET UpdatedAt = GETUTCDATE()
            FROM Reservas r
            INNER JOIN inserted i ON i.Id = r.Id;
        END
    ');
END

-- ── Trigger de UpdatedAt para Cupons ───────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_Cupons_UpdatedAt')
BEGIN
    EXEC('
        CREATE TRIGGER TR_Cupons_UpdatedAt
        ON Cupons AFTER UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;
            UPDATE c SET UpdatedAt = GETUTCDATE()
            FROM Cupons c
            INNER JOIN inserted i ON i.Codigo = c.Codigo;
        END
    ');
END
