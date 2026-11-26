-- Ejecutar con sqlcmd o con el modo SQLCMD de SSMS.
:ON ERROR EXIT
USE [master];
GO
IF DB_NAME() <> N'master'
    THROW 50002, N'Conecta a master para preparar la base demo.', 1;
IF DB_ID(N'AnalisisOpiniones_Demo_ITLA') IS NULL
    EXEC(N'CREATE DATABASE [AnalisisOpiniones_Demo_ITLA];');
GO
USE [AnalisisOpiniones_Demo_ITLA];
GO
IF DB_NAME() <> N'AnalisisOpiniones_Demo_ITLA'
    THROW 50001, 'La preparación solo puede ejecutarse en la base de demostración.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
    CREATE TABLE dbo.Clientes (
        IdCliente int IDENTITY PRIMARY KEY,
        Nombre varchar(100) NOT NULL,
        Email varchar(150) NOT NULL
    );

IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL
    CREATE TABLE dbo.Productos (
        IdProducto int IDENTITY PRIMARY KEY,
        Nombre varchar(150) NOT NULL,
        Categoria varchar(100) NOT NULL
    );

IF OBJECT_ID(N'dbo.FuenteDatos', N'U') IS NULL
    CREATE TABLE dbo.FuenteDatos (
        IdFuente int IDENTITY PRIMARY KEY,
        TipoFuente varchar(50) NOT NULL,
        FechaCarga datetime NOT NULL DEFAULT GETDATE(),
        CodigoFuente varchar(100) NOT NULL UNIQUE,
        NombreFuente varchar(150) NOT NULL
    );

IF OBJECT_ID(N'dbo.Opiniones', N'U') IS NULL
    CREATE TABLE dbo.Opiniones (
        IdOpinion int IDENTITY PRIMARY KEY,
        IdCliente int NOT NULL REFERENCES dbo.Clientes(IdCliente),
        IdProducto int NOT NULL REFERENCES dbo.Productos(IdProducto),
        IdFuente int NOT NULL REFERENCES dbo.FuenteDatos(IdFuente),
        Fecha datetime NOT NULL,
        Comentario varchar(max) NOT NULL,
        Clasificacion varchar(20) NOT NULL,
        PuntajeSatisfaccion decimal(5,2) NULL
    );

IF NOT EXISTS (SELECT 1 FROM dbo.Clientes WHERE Email = 'cliente-demo@example.invalid')
    INSERT dbo.Clientes (Nombre, Email) VALUES ('Cliente ficticio', 'cliente-demo@example.invalid');

IF NOT EXISTS (SELECT 1 FROM dbo.Productos WHERE Nombre = 'Producto ficticio de demostracion')
    INSERT dbo.Productos (Nombre, Categoria) VALUES ('Producto ficticio de demostracion', 'Demo ITLA');

IF NOT EXISTS (SELECT 1 FROM dbo.FuenteDatos WHERE CodigoFuente = 'PLATAFORMA:Web')
    INSERT dbo.FuenteDatos (TipoFuente, CodigoFuente, NombreFuente)
    VALUES ('Web', 'PLATAFORMA:Web', 'Resenas web de demostracion');

DECLARE @cliente int = (SELECT MIN(IdCliente) FROM dbo.Clientes WHERE Email = 'cliente-demo@example.invalid');
DECLARE @producto int = (SELECT MIN(IdProducto) FROM dbo.Productos WHERE Nombre = 'Producto ficticio de demostracion');
DECLARE @fuente int = (SELECT IdFuente FROM dbo.FuenteDatos WHERE CodigoFuente = 'PLATAFORMA:Web');

INSERT dbo.Opiniones (IdCliente, IdProducto, IdFuente, Fecha, Comentario, Clasificacion, PuntajeSatisfaccion)
SELECT @cliente, @producto, @fuente, demo.Fecha, demo.Comentario, demo.Clasificacion, demo.Puntaje
FROM (VALUES
    (CONVERT(datetime, '20261001', 112), 'Resena ficticia: buen servicio.', 'Positiva', 90.00),
    (CONVERT(datetime, '20261002', 112), 'Resena ficticia: entrega tardia.', 'Negativa', 35.00)
) AS demo(Fecha, Comentario, Clasificacion, Puntaje)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Opiniones AS o
    WHERE o.IdCliente = @cliente AND o.IdProducto = @producto AND o.IdFuente = @fuente
      AND o.Fecha = demo.Fecha AND o.Comentario = demo.Comentario
);

COMMIT TRANSACTION;

SELECT COUNT(*) AS ResenasWeb
FROM dbo.Opiniones
WHERE IdFuente = @fuente;
