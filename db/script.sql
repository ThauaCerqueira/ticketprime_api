-- 1. Criação do Banco de Dados
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'TicketPrimeDB')
BEGIN
    CREATE DATABASE TicketPrimeDB;
END
GO

USE TicketPrimeDB;
GO

-- 2. Tabela de Usuários (Deve vir antes pois outras podem depender dela)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Usuarios]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Usuarios] (
        [Cpf]   CHAR(11)      NOT NULL,
        [Nome]  VARCHAR(100)  NOT NULL,
        [Email] VARCHAR(100)  NOT NULL,
        
        CONSTRAINT [PK_Usuarios] PRIMARY KEY CLUSTERED ([Cpf] ASC)
    );
END
GO

-- 3. Tabela de Eventos
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Eventos] (
        [Id]              INT              IDENTITY (1, 1) NOT NULL,
        [Nome]            NVARCHAR (150)   NOT NULL,
        [CapacidadeTotal] INT              NOT NULL,
        [DataEvento]      DATETIME2 (7)    NOT NULL,
        [PrecoPadrao]     DECIMAL (18, 2)  NOT NULL,
        
        CONSTRAINT [PK_Eventos] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- 4. Exemplo de Tabela de Reservas (Onde a Foreign Key faria sentido)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Reservas]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Reservas] (
        [Id]         INT      IDENTITY (1, 1) NOT NULL,
        [UsuarioCpf] CHAR(11) NOT NULL,
        [EventoId]   INT      NOT NULL,
        [DataCompra] DATETIME DEFAULT GETDATE(),

        CONSTRAINT [PK_Reservas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reservas_Usuarios] FOREIGN KEY ([UsuarioCpf]) REFERENCES [Usuarios]([Cpf]),
        CONSTRAINT [FK_Reservas_Eventos] FOREIGN KEY ([EventoId]) REFERENCES [Eventos]([Id])
    );
END
GO
