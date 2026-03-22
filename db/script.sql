CREATE DATABASE TicketPrime;
GO

USE TicketPrime;
GO

CREATE TABLE Usuarios (
    Cpf CHAR(11) PRIMARY KEY,
    Nome VARCHAR(100),
    Email VARCHAR(100)
);

    FOREIGN KEY (UsuarioCpf) REFERENCES Usuarios(Cpf),
