IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'TicketPrime')
BEGIN
    CREATE DATABASE TicketPrime;
END
GO

USE TicketPrime;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Usuarios]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Usuarios] (
        [Cpf]   CHAR(11)      NOT NULL,
        [Nome]  VARCHAR(100)  NOT NULL,
        [Email] VARCHAR(100)  NOT NULL,
        [Senha] VARCHAR(25)   NOT NULL,
        [Perfil] VARCHAR(10)  NOT NULL DEFAULT 'CLIENTE',
        
        CONSTRAINT [PK_Usuarios] PRIMARY KEY CLUSTERED ([Cpf] ASC)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Eventos] (
        [Id]                        INT              IDENTITY (1, 1) NOT NULL,
        [Nome]                      NVARCHAR (200)   NOT NULL,
        [CapacidadeTotal]           INT              NOT NULL,
        [DataEvento]                DATETIME2 (7)    NOT NULL,
        [PrecoPadrao]               DECIMAL (18, 2)  NOT NULL,
        [LimiteIngressosPorUsuario] INT              NOT NULL DEFAULT 6,
        [Local]                     NVARCHAR (500)   NOT NULL DEFAULT '',
        [Descricao]                 NVARCHAR (2000)  NULL,
        [GeneroMusical]             NVARCHAR (100)   NOT NULL DEFAULT '',
        [EventoGratuito]            BIT              NOT NULL DEFAULT 0,
        [Status]                    VARCHAR (20)     NOT NULL DEFAULT 'Rascunho',
        
        CONSTRAINT [PK_Eventos] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [CK_Eventos_Status] CHECK ([Status] IN ('Rascunho', 'Publicado', 'Encerrado', 'Cancelado'))
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Reservas]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Reservas] (
        [Id]              INT            IDENTITY (1, 1) NOT NULL,
        [UsuarioCpf]      CHAR(11)       NOT NULL,
        [EventoId]        INT            NOT NULL,
        [DataCompra]      DATETIME       DEFAULT GETDATE(),
        [CupomUtilizado]  NVARCHAR(20)   NULL,
        [ValorFinalPago]  DECIMAL(18, 2) NOT NULL DEFAULT 0,

        CONSTRAINT [PK_Reservas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reservas_Usuarios] FOREIGN KEY ([UsuarioCpf]) REFERENCES [Usuarios]([Cpf]),
        CONSTRAINT [FK_Reservas_Eventos] FOREIGN KEY ([EventoId]) REFERENCES [Eventos]([Id]),
        CONSTRAINT [FK_Reservas_Cupons] FOREIGN KEY ([CupomUtilizado]) REFERENCES [Cupons]([Codigo])
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Cupons]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Cupons] (
        [Codigo]              NVARCHAR(20)   NOT NULL,
        [PorcentagemDesconto] DECIMAL(5, 2)  NOT NULL,
        [ValorMinimoRegra]    DECIMAL(18, 2) NOT NULL,

        CONSTRAINT [PK_Cupons] PRIMARY KEY CLUSTERED ([Codigo] ASC),
        CONSTRAINT [CK_Cupons_Desconto] CHECK ([PorcentagemDesconto] >= 0 AND [PorcentagemDesconto] <= 100),
        CONSTRAINT [CK_Cupons_ValorMinimo] CHECK ([ValorMinimoRegra] >= 0)
    );
END
GO

-- Adiciona coluna Perfil caso o banco já exista sem ela
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Usuarios]') AND name = 'Perfil')
BEGIN
    ALTER TABLE Usuarios ADD Perfil VARCHAR(10) NOT NULL DEFAULT 'CLIENTE';
END
GO

-- Adiciona coluna LimiteIngressosPorUsuario na tabela Eventos (caso não exista)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]') AND name = 'LimiteIngressosPorUsuario')
BEGIN
    ALTER TABLE Eventos ADD LimiteIngressosPorUsuario INT NOT NULL DEFAULT 6;
END
GO

-- Adiciona colunas na tabela Reservas (caso a tabela já exista sem elas)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservas]') AND name = 'CupomUtilizado')
BEGIN
    ALTER TABLE Reservas ADD CupomUtilizado NVARCHAR(20) NULL;
    ALTER TABLE Reservas ADD CONSTRAINT [FK_Reservas_Cupons]
        FOREIGN KEY ([CupomUtilizado]) REFERENCES [Cupons]([Codigo]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Reservas]') AND name = 'ValorFinalPago')
BEGIN
    ALTER TABLE Reservas ADD ValorFinalPago DECIMAL(18, 2) NOT NULL DEFAULT 0;
END
GO

-- ─── Novos campos da tabela Eventos (compatibilidade com bancos existentes) ──────────────────

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]') AND name = 'Local')
BEGIN
    ALTER TABLE Eventos ADD [Local] NVARCHAR(500) NOT NULL DEFAULT '';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]') AND name = 'Descricao')
BEGIN
    ALTER TABLE Eventos ADD [Descricao] NVARCHAR(2000) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]') AND name = 'GeneroMusical')
BEGIN
    ALTER TABLE Eventos ADD [GeneroMusical] NVARCHAR(100) NOT NULL DEFAULT '';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]') AND name = 'EventoGratuito')
BEGIN
    ALTER TABLE Eventos ADD [EventoGratuito] BIT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]') AND name = 'Status')
BEGIN
    ALTER TABLE Eventos ADD [Status] VARCHAR(20) NOT NULL DEFAULT 'Rascunho';
    ALTER TABLE Eventos ADD CONSTRAINT [CK_Eventos_Status]
        CHECK ([Status] IN ('Rascunho', 'Publicado', 'Encerrado', 'Cancelado'));
END
GO

-- Expande Nome de 150 para 200 chars (caso a coluna já exista com tamanho menor)
-- Executar apenas se a precisão atual for 150
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Eventos]')
      AND name = 'Nome'
      AND max_length = 300   -- NVARCHAR(150) ocupa 300 bytes
)
BEGIN
    ALTER TABLE Eventos ALTER COLUMN [Nome] NVARCHAR(200) NOT NULL;
END
GO

-- ─── Tabela de fotos criptografadas ──────────────────────────────────────────

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EventoFotos]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[EventoFotos] (
        [Id]                    INT              IDENTITY (1, 1) NOT NULL,
        [EventoId]              INT              NOT NULL,
        [CiphertextBase64]      NVARCHAR (MAX)   NOT NULL,
        [IvBase64]              NVARCHAR (200)   NOT NULL,
        [ChaveAesCifradaBase64] NVARCHAR (MAX)   NOT NULL,
        [ChavePublicaOrgJwk]    NVARCHAR (MAX)   NOT NULL,
        [HashNomeOriginal]      NVARCHAR (100)   NOT NULL,
        [TipoMime]              VARCHAR  (50)    NOT NULL,
        [TamanhoBytes]          BIGINT           NOT NULL DEFAULT 0,
        [Criptografada]         BIT              NOT NULL DEFAULT 1,
        [DataUpload]            DATETIME2 (7)    NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_EventoFotos] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_EventoFotos_Eventos] FOREIGN KEY ([EventoId])
            REFERENCES [Eventos]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_EventoFotos_TamanhoBytes] CHECK ([TamanhoBytes] >= 0)
    );
END
GO

-- ─── Cria o usuário admin padrão ─────────────────────────────────────────────

-- Cria o usuário admin padrão
IF NOT EXISTS (SELECT * FROM Usuarios WHERE Cpf = '00000000000')
BEGIN
    INSERT INTO Usuarios (Cpf, Nome, Email, Senha, Perfil)
    VALUES ('00000000000', 'Administrador', 'admin@ticketprime.com', 'admin123', 'ADMIN');
END
GO