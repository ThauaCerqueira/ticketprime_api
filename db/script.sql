IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'TicketPrimeDB')
BEGIN
    CREATE DATABASE TicketPrimeDB;
END
GO

USE TicketPrimeDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Eventos] (
        [Id]               INT             IDENTITY (1, 1) NOT NULL,
        [Nome]             NVARCHAR (150)  NOT NULL,
        [CapacidadeTotal]  INT             NOT NULL,
        [DataEvento]       DATETIME2 (7)   NOT NULL,
        [PrecoPadrao]      DECIMAL (18, 2) NOT NULL,
        
        CONSTRAINT [PK_Eventos] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO