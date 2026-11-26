-- Invocado por los scripts SQLCMD de preparación desde la raíz de la solución.
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/* ==========================================================
   1. DIMENSIONES
   ========================================================== */
IF OBJECT_ID(N'dbo.DimProducto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DimProducto (
        ProductoKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimProducto PRIMARY KEY,
        ProductoOrigenID NVARCHAR(100) NOT NULL,
        Nombre NVARCHAR(200) NOT NULL,
        Categoria NVARCHAR(100) NULL,
        CONSTRAINT UQ_DimProducto_Origen UNIQUE (ProductoOrigenID)
    );
END;
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.DimCliente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DimCliente (
        ClienteKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimCliente PRIMARY KEY,
        ClienteOrigenID NVARCHAR(100) NULL,
        Pais NVARCHAR(100) NULL,
        Edad INT NULL,
        TipoCliente NVARCHAR(80) NULL,
        CONSTRAINT UQ_DimCliente_Origen UNIQUE (ClienteOrigenID),
        CONSTRAINT CK_DimCliente_Edad CHECK (Edad BETWEEN 0 AND 120)
    );
END;
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.DimFecha', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DimFecha (
        FechaKey INT NOT NULL CONSTRAINT PK_DimFecha PRIMARY KEY, -- YYYYMMDD
        Fecha DATE NOT NULL,
        Anio SMALLINT NOT NULL,
        Trimestre TINYINT NOT NULL,
        Mes TINYINT NOT NULL,
        Dia TINYINT NOT NULL,
        CONSTRAINT UQ_DimFecha_Fecha UNIQUE (Fecha),
        CONSTRAINT CK_DimFecha_Trimestre CHECK (Trimestre BETWEEN 1 AND 4),
        CONSTRAINT CK_DimFecha_Mes CHECK (Mes BETWEEN 1 AND 12),
        CONSTRAINT CK_DimFecha_Dia CHECK (Dia BETWEEN 1 AND 31)
    );
END;
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.DimFuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DimFuente (
        FuenteKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimFuente PRIMARY KEY,
        Nombre NVARCHAR(40) NOT NULL,
        TipoOrigen NVARCHAR(40) NOT NULL,
        CONSTRAINT UQ_DimFuente_Nombre UNIQUE (Nombre)
    );
END;
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.DimSentimiento', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DimSentimiento (
        SentimientoKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimSentimiento PRIMARY KEY,
        Nombre NVARCHAR(20) NOT NULL,
        CONSTRAINT UQ_DimSentimiento_Nombre UNIQUE (Nombre),
        CONSTRAINT CK_Sentimiento_Nombre CHECK
            (Nombre IN (N'Positivo', N'Negativo', N'Neutro', N'No clasificado'))
    );
END;
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.DimEvento', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DimEvento (
        EventoKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimEvento PRIMARY KEY,
        Nombre NVARCHAR(150) NOT NULL,
        TipoEvento NVARCHAR(60) NULL,
        FechaInicio DATE NULL,
        FechaFin DATE NULL,
        CONSTRAINT CK_Evento_Fechas CHECK
            (FechaFin IS NULL OR FechaInicio IS NULL OR FechaFin >= FechaInicio)
    );
END;
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/* ==========================================================
   2. TABLA CENTRAL DE HECHOS
   Granularidad: una fila = una opinion procesada.
   ========================================================== */
IF OBJECT_ID(N'dbo.FactOpiniones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FactOpiniones (
        OpinionKey BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FactOpiniones PRIMARY KEY,
        OpinionOrigenID NVARCHAR(120) NOT NULL,
        ProductoKey INT NOT NULL,
        ClienteKey INT NULL,
        FechaKey INT NOT NULL,
        FuenteKey INT NOT NULL,
        SentimientoKey INT NOT NULL,
        EventoKey INT NULL,
        FechaProcesamiento DATETIME2(0) NOT NULL
            CONSTRAINT DF_FactOpiniones_FechaProcesamiento DEFAULT SYSUTCDATETIME(),
        TextoOpinion NVARCHAR(MAX) NULL,
        Calificacion DECIMAL(4,2) NULL,
        -- Columna calculada: NO se inserta ni se actualiza manualmente.
        CantidadOpiniones AS CONVERT(INT, 1) PERSISTED,

        CONSTRAINT CK_FactOpiniones_Calificacion CHECK (Calificacion BETWEEN 1 AND 5),
        CONSTRAINT UQ_Opinion_Fuente UNIQUE (FuenteKey, OpinionOrigenID),
        CONSTRAINT FK_Opinion_Producto FOREIGN KEY (ProductoKey)
            REFERENCES dbo.DimProducto(ProductoKey),
        CONSTRAINT FK_Opinion_Cliente FOREIGN KEY (ClienteKey)
            REFERENCES dbo.DimCliente(ClienteKey),
        CONSTRAINT FK_Opinion_Fecha FOREIGN KEY (FechaKey)
            REFERENCES dbo.DimFecha(FechaKey),
        CONSTRAINT FK_Opinion_Fuente FOREIGN KEY (FuenteKey)
            REFERENCES dbo.DimFuente(FuenteKey),
        CONSTRAINT FK_Opinion_Sentimiento FOREIGN KEY (SentimientoKey)
            REFERENCES dbo.DimSentimiento(SentimientoKey),
        CONSTRAINT FK_Opinion_Evento FOREIGN KEY (EventoKey)
            REFERENCES dbo.DimEvento(EventoKey)
    );
END;
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/* ==========================================================
   3. INDICES PARA CONSULTAS ANALITICAS
   ========================================================== */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FactOpiniones')
      AND name = N'IX_FactOpiniones_Fecha_Producto'
)
BEGIN
    CREATE INDEX IX_FactOpiniones_Fecha_Producto
        ON dbo.FactOpiniones(FechaKey, ProductoKey);
END;
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FactOpiniones')
      AND name = N'IX_FactOpiniones_Fuente_Sentimiento'
)
BEGIN
    CREATE INDEX IX_FactOpiniones_Fuente_Sentimiento
        ON dbo.FactOpiniones(FuenteKey, SentimientoKey);
END;
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/* ==========================================================
   4. CATALOGOS INICIALES (sin duplicados al reejecutar)
   ========================================================== */
INSERT INTO dbo.DimFuente (Nombre, TipoOrigen)
SELECT v.Nombre, v.TipoOrigen
FROM (VALUES
    (N'Encuesta',   N'CSV'),
    (N'Reseña web', N'Base relacional'),
    (N'Red social', N'API REST')
) AS v(Nombre, TipoOrigen)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.DimFuente d WHERE d.Nombre = v.Nombre
);
GO
IF DB_NAME() <> N'$(WarehouseName)' OR DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50001, N'La preparación solo puede ejecutarse en el almacén aislado indicado.', 1;

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

INSERT INTO dbo.DimSentimiento (Nombre)
SELECT v.Nombre
FROM (VALUES
    (N'Positivo'),
    (N'Negativo'),
    (N'Neutro'),
    (N'No clasificado')
) AS v(Nombre)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.DimSentimiento d WHERE d.Nombre = v.Nombre
);
GO

/* ==========================================================
   5. VERIFICACION DE LA ESTRUCTURA
   ========================================================== */
SELECT DB_NAME() AS BaseDeDatos,
       (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo')
        AND name IN (N'FactOpiniones', N'DimProducto', N'DimCliente',
                     N'DimFecha', N'DimFuente', N'DimSentimiento', N'DimEvento')) AS TablasEsperadas,
       (SELECT COUNT(*) FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.FactOpiniones') AND is_disabled = 0) AS RelacionesActivas,
       (SELECT COUNT(*) FROM dbo.DimFuente) AS FuentesRegistradas,
       (SELECT COUNT(*) FROM dbo.DimSentimiento) AS SentimientosRegistrados,
       (SELECT COUNT(*) FROM dbo.FactOpiniones) AS OpinionesRegistradas;

SELECT c.name AS ColumnaCalculada, c.is_computed,
       cc.definition AS Formula
FROM sys.columns c
INNER JOIN sys.computed_columns cc
    ON cc.object_id = c.object_id AND cc.column_id = c.column_id
WHERE c.object_id = OBJECT_ID(N'dbo.FactOpiniones')
  AND c.name = N'CantidadOpiniones';
GO

/* RESULTADO ESPERADO EN UNA BASE NUEVA:
   TablasEsperadas = 7; RelacionesActivas = 6;
   FuentesRegistradas = 3; SentimientosRegistrados = 4;
   OpinionesRegistradas = 0; CantidadOpiniones is_computed = 1.

   No se insertan opiniones ficticias. Antes de cargar hechos, un ETL debe
   poblar las dimensiones necesarias (incluida DimFecha).
   OpinionOrigenID debe ser unico dentro de cada FuenteKey.
*/
