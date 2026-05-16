-- ═══════════════════════════════════════════════════════════════════
--  V005 - Foreign Keys, Índices e Constraints Faltantes
--  ══════════════════════════════════════════════════════════════════
--  ANTES:
--    - TicketTypeId e LoteId sem FK → dados órfãos
--    - StatusPagamento sem índice → webhook lento
--    - CodigoTransacaoGateway sem UNIQUE → pagamento duplicado
--  AGORA:
--    - FKs para TiposIngresso e Lotes
--    - Índices em StatusPagamento, DataCancelamento
--    - UNIQUE INDEX em CodigoTransacaoGateway e IdEstornoGateway
-- ═══════════════════════════════════════════════════════════════════

-- 1. FK: Reservas → TiposIngresso
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Reservas_TiposIngresso')
BEGIN
    DELETE FROM Reservas WHERE TicketTypeId IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM TiposIngresso WHERE Id = Reservas.TicketTypeId);
    ALTER TABLE Reservas ADD CONSTRAINT FK_Reservas_TiposIngresso
        FOREIGN KEY (TicketTypeId) REFERENCES TiposIngresso(Id);
    PRINT '[V005] FK_Reservas_TiposIngresso adicionada.';
END
GO

-- 2. FK: Reservas → Lotes
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Reservas_Lotes')
BEGIN
    DELETE FROM Reservas WHERE LoteId IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM Lotes WHERE Id = Reservas.LoteId);
    ALTER TABLE Reservas ADD CONSTRAINT FK_Reservas_Lotes
        FOREIGN KEY (LoteId) REFERENCES Lotes(Id);
    PRINT '[V005] FK_Reservas_Lotes adicionada.';
END
GO

-- 3. Índice em StatusPagamento (webhook)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservas_StatusPagamento')
    CREATE NONCLUSTERED INDEX IX_Reservas_StatusPagamento
        ON Reservas(StatusPagamento)
        WHERE StatusPagamento IS NOT NULL;
GO

-- 4. Índice em DataCancelamento (relatórios)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservas_DataCancelamento')
    CREATE NONCLUSTERED INDEX IX_Reservas_DataCancelamento
        ON Reservas(DataCancelamento)
        WHERE DataCancelamento IS NOT NULL;
GO

-- 5. UNIQUE INDEX em CodigoTransacaoGateway (evita duplicatas)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Reservas_CodigoTransacaoGateway')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Reservas_CodigoTransacaoGateway
        ON Reservas(CodigoTransacaoGateway)
        WHERE CodigoTransacaoGateway IS NOT NULL;
GO

-- 6. UNIQUE INDEX em IdEstornoGateway
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Reservas_IdEstornoGateway')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Reservas_IdEstornoGateway
        ON Reservas(IdEstornoGateway)
        WHERE IdEstornoGateway IS NOT NULL;
GO

PRINT '[V005] Migração concluída.';
GO
