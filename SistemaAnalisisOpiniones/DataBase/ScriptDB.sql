-- =====================================================
-- SISTEMA DE ANALISIS DE OPINIONES DE CLIENTES
-- Base de datos: AnalisisOpiniones
-- Ejecutar una vez en SQL Server (SSMS o sqlcmd).
-- =====================================================

SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF DB_ID(N'AnalisisOpiniones') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE AnalisisOpiniones;');
END;
GO

USE AnalisisOpiniones;
GO

-- =====================================================
-- TABLAS BASE
-- =====================================================

IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes
    (
        IdCliente INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Clientes PRIMARY KEY,
        Nombre VARCHAR(100) NOT NULL,
        Email VARCHAR(150) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Productos
    (
        IdProducto INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Productos PRIMARY KEY,
        Nombre VARCHAR(150) NOT NULL,
        Categoria VARCHAR(100) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.FuenteDatos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FuenteDatos
    (
        IdFuente INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_FuenteDatos PRIMARY KEY,
        TipoFuente VARCHAR(50) NOT NULL,
        FechaCarga DATETIME NOT NULL
            CONSTRAINT DF_FuenteDatos_FechaCarga DEFAULT GETDATE(),
        CodigoFuente VARCHAR(100) NULL,
        NombreFuente VARCHAR(150) NULL
    );
END;
GO

-- Compatibilidad con bases creadas por versiones anteriores.
IF COL_LENGTH(N'dbo.FuenteDatos', N'CodigoFuente') IS NULL
BEGIN
    ALTER TABLE dbo.FuenteDatos ADD CodigoFuente VARCHAR(100) NULL;
END;
GO

IF COL_LENGTH(N'dbo.FuenteDatos', N'NombreFuente') IS NULL
BEGIN
    ALTER TABLE dbo.FuenteDatos ADD NombreFuente VARCHAR(150) NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_FuenteDatos_CodigoFuente'
      AND object_id = OBJECT_ID(N'dbo.FuenteDatos')
)
BEGIN
    CREATE UNIQUE INDEX UX_FuenteDatos_CodigoFuente
        ON dbo.FuenteDatos (CodigoFuente)
        WHERE CodigoFuente IS NOT NULL;
END;
GO

IF OBJECT_ID(N'dbo.Opiniones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Opiniones
    (
        IdOpinion INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Opiniones PRIMARY KEY,
        IdCliente INT NOT NULL,
        IdProducto INT NOT NULL,
        IdFuente INT NOT NULL,
        Fecha DATETIME NOT NULL,
        Comentario VARCHAR(MAX) NOT NULL,
        Clasificacion VARCHAR(20) NOT NULL,
        PuntajeSatisfaccion DECIMAL(5,2) NULL,
        CONSTRAINT FK_Opiniones_Clientes
            FOREIGN KEY (IdCliente) REFERENCES dbo.Clientes (IdCliente),
        CONSTRAINT FK_Opiniones_Productos
            FOREIGN KEY (IdProducto) REFERENCES dbo.Productos (IdProducto),
        CONSTRAINT FK_Opiniones_FuenteDatos
            FOREIGN KEY (IdFuente) REFERENCES dbo.FuenteDatos (IdFuente)
    );
END;
GO

-- =====================================================
-- VISTAS
-- =====================================================

CREATE OR ALTER VIEW dbo.vwOpinionesDetalle
AS
SELECT
    o.IdOpinion,
    o.Fecha,
    c.IdCliente,
    c.Nombre AS Cliente,
    c.Email,
    p.IdProducto,
    p.Nombre AS Producto,
    p.Categoria,
    f.IdFuente,
    f.CodigoFuente,
    f.NombreFuente AS Fuente,
    f.TipoFuente,
    o.Comentario,
    o.Clasificacion,
    o.PuntajeSatisfaccion
FROM dbo.Opiniones AS o
INNER JOIN dbo.Clientes AS c ON c.IdCliente = o.IdCliente
INNER JOIN dbo.Productos AS p ON p.IdProducto = o.IdProducto
INNER JOIN dbo.FuenteDatos AS f ON f.IdFuente = o.IdFuente;
GO

CREATE OR ALTER VIEW dbo.vwResumenSatisfaccion
AS
SELECT
    f.IdFuente,
    f.CodigoFuente,
    f.NombreFuente AS Fuente,
    f.TipoFuente,
    COUNT(o.IdOpinion) AS TotalOpiniones,
    CAST(AVG(o.PuntajeSatisfaccion) AS DECIMAL(5,2)) AS PromedioPuntaje,
    SUM(CASE WHEN o.Clasificacion = 'Positiva' THEN 1 ELSE 0 END) AS Positivas,
    SUM(CASE WHEN o.Clasificacion = 'Neutra' THEN 1 ELSE 0 END) AS Neutras,
    SUM(CASE WHEN o.Clasificacion = 'Negativa' THEN 1 ELSE 0 END) AS Negativas
FROM dbo.FuenteDatos AS f
LEFT JOIN dbo.Opiniones AS o ON o.IdFuente = f.IdFuente
GROUP BY f.IdFuente, f.CodigoFuente, f.NombreFuente, f.TipoFuente;
GO

-- =====================================================
-- PROCEDIMIENTOS ALMACENADOS
-- =====================================================

CREATE OR ALTER PROCEDURE dbo.usp_ResumenCarga
AS
BEGIN
    SET NOCOUNT ON;

    SELECT N'Clientes' AS Tabla, COUNT(*) AS Total FROM dbo.Clientes
    UNION ALL
    SELECT N'Productos', COUNT(*) FROM dbo.Productos
    UNION ALL
    SELECT N'Fuentes', COUNT(*) FROM dbo.FuenteDatos
    UNION ALL
    SELECT N'Opiniones', COUNT(*) FROM dbo.Opiniones;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ValidarIntegridad
AS
BEGIN
    SET NOCOUNT ON;

    SELECT N'Opiniones sin cliente' AS Validacion, COUNT(*) AS Total
    FROM dbo.Opiniones AS o
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Clientes AS c WHERE c.IdCliente = o.IdCliente)
    UNION ALL
    SELECT N'Opiniones sin producto', COUNT(*)
    FROM dbo.Opiniones AS o
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Productos AS p WHERE p.IdProducto = o.IdProducto)
    UNION ALL
    SELECT N'Opiniones sin fuente', COUNT(*)
    FROM dbo.Opiniones AS o
    WHERE NOT EXISTS (SELECT 1 FROM dbo.FuenteDatos AS f WHERE f.IdFuente = o.IdFuente)
    UNION ALL
    SELECT N'Correos de cliente duplicados', COUNT(*)
    FROM
    (
        SELECT Email
        FROM dbo.Clientes
        GROUP BY Email
        HAVING COUNT(*) > 1
    ) AS duplicados
    UNION ALL
    SELECT N'Opiniones duplicadas', COUNT(*)
    FROM
    (
        SELECT
            IdCliente,
            IdProducto,
            IdFuente,
            Fecha,
            Comentario,
            Clasificacion,
            PuntajeSatisfaccion
        FROM dbo.Opiniones
        GROUP BY
            IdCliente,
            IdProducto,
            IdFuente,
            Fecha,
            Comentario,
            Clasificacion,
            PuntajeSatisfaccion
        HAVING COUNT(*) > 1
    ) AS duplicadas;
END;
GO

PRINT N'Base de datos AnalisisOpiniones creada o actualizada correctamente.';
GO
