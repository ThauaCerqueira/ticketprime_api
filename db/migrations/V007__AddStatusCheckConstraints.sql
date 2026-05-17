-- ═══════════════════════════════════════════════════════════════════════════════
-- Migration V007: Adiciona CHECK constraints nos campos Status
--
-- Reforça a integridade dos dados no nível do banco, garantindo que apenas
-- valores válidos sejam armazenados. Complementa os novos enums C# (Enums.cs)
-- que fornecem segurança de tipo em tempo de compilação.
-- ═══════════════════════════════════════════════════════════════════════════════

-- Eventos.Status: 'Rascunho', 'Publicado', 'Cancelado'
IF NOT EXISTS (SELECT * FROM sys.check_constraints 
               WHERE name = 'CK_Eventos_Status')
BEGIN
    ALTER TABLE Eventos ADD CONSTRAINT CK_Eventos_Status
        CHECK (Status IN ('Rascunho', 'Publicado', 'Cancelado'));
END

-- Reservas.Status: 'Ativa', 'Usada', 'Cancelada', 'Aguardando Pagamento'
IF NOT EXISTS (SELECT * FROM sys.check_constraints 
               WHERE name = 'CK_Reservas_Status')
BEGIN
    ALTER TABLE Reservas ADD CONSTRAINT CK_Reservas_Status
        CHECK (Status IN ('Ativa', 'Usada', 'Cancelada', 'Aguardando Pagamento'));
END

-- FilaEspera.Status: 'Ativo', 'Notificado', 'Expirado'
IF NOT EXISTS (SELECT * FROM sys.check_constraints 
               WHERE name = 'CK_FilaEspera_Status')
BEGIN
    ALTER TABLE FilaEspera ADD CONSTRAINT CK_FilaEspera_Status
        CHECK (Status IN ('Ativo', 'Notificado', 'Expirado'));
END

-- DocumentosMeiaEntrada.Status: 'Pendente', 'Verificado', 'Rejeitado'
IF NOT EXISTS (SELECT * FROM sys.check_constraints 
               WHERE name = 'CK_DocumentosMeiaEntrada_Status')
BEGIN
    ALTER TABLE DocumentosMeiaEntrada ADD CONSTRAINT CK_DocumentosMeiaEntrada_Status
        CHECK (Status IN ('Pendente', 'Verificado', 'Rejeitado'));
END
