-- ═══════════════════════════════════════════════════════════════════════════════
-- Migration V006: Adiciona campos Cidade e Estado na tabela Eventos
--
-- Permite busca e filtro de eventos por localização geográfica na vitrine.
-- ═══════════════════════════════════════════════════════════════════════════════

-- Adiciona colunas Cidade e Estado
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Eventos' AND COLUMN_NAME = 'Cidade')
BEGIN
    ALTER TABLE Eventos ADD Cidade NVARCHAR(100) NOT NULL DEFAULT '';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Eventos' AND COLUMN_NAME = 'Estado')
BEGIN
    ALTER TABLE Eventos ADD Estado NVARCHAR(2) NOT NULL DEFAULT '';
END

-- Índice para busca por localização
-- ⚠ NOTA: O CREATE INDEX com WHERE exige QUOTED_IDENTIFIER ON (padrão do sqlcmd)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Eventos_Cidade_Estado')
BEGIN
    EXEC('
        SET QUOTED_IDENTIFIER ON;
        CREATE INDEX IX_Eventos_Cidade_Estado ON Eventos (Cidade, Estado)
        WHERE Cidade != '''' AND Estado != '''';
    ');
END
