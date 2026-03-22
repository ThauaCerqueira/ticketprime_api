CREATE TABLE Usuarios (
Cpf CHAR(11) PRIMARY KEY,
Nome VARCHAR(100),
Email VARCHAR(100)
);

CREATE TABLE Eventos (
Id INT IDENTITY(1,1) PRIMARY KEY,
Nome VARCHAR(200),
CapacidadeTotal INT,
DataEvento DATETIME,
PrecoPadrao DECIMAL(10,2)
);

CREATE TABLE Cupons (
Codigo VARCHAR(50) PRIMARY KEY,
PorcentagemDesconto DECIMAL(5,2),
ValorMinimoRegra DECIMAL(10,2)
);

CREATE TABLE Reservas (
Id INT IDENTITY(1,1) PRIMARY KEY,
UsuarioCpf CHAR(11),
EventoId INT,
CupomUtilizado VARCHAR(50),
ValorFinalPago DECIMAL(10,2),

FOREIGN KEY (UsuarioCpf) REFERENCES Usuarios(Cpf),
FOREIGN KEY (EventoId) REFERENCES Eventos(Id),
FOREIGN KEY (CupomUtilizado) REFERENCES Cupons(Codigo)
);

USE TicketPrime;
GO

-- 1. Vinculando Reserva ao Usuário
ALTER TABLE Reservas ADD CONSTRAINT FK_Reservas_Usuarios 
FOREIGN KEY (UsuarioCpf) REFERENCES Usuarios(Cpf);

-- 2. Vinculando Reserva ao Evento
ALTER TABLE Reservas ADD CONSTRAINT FK_Reservas_Eventos 
FOREIGN KEY (EventoId) REFERENCES Eventos(Id);

-- 3. Vinculando Reserva ao Cupom (Permite Nulo)
ALTER TABLE Reservas ADD CONSTRAINT FK_Reservas_Cupons 
FOREIGN KEY (CupomUtilizado) REFERENCES Cupons(Codigo);
GO

USE TicketPrime;
GO

-- Adicionando a restrição de Chave Estrangeira
ALTER TABLE Reservas
ADD CONSTRAINT FK_Reservas_Usuarios 
FOREIGN KEY (UsuarioCpf) REFERENCES Usuarios(Cpf);
GO

-- Adiciona a coluna Senha na tabela Usuarios
ALTER TABLE Usuarios ADD Senha VARCHAR(100);