:ON ERROR EXIT
-- Ejecutar con -d DW_OpinionesClientes, después de verificar un respaldo recuperable.
-- Crea solo objetos auxiliares. La reconciliación se invoca aparte desde Process.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF DB_NAME() <> N'DW_OpinionesClientes' OR @@TRANCOUNT <> 0
    THROW 50120, N'Conecta al DW original en una sesión sin transacción.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM dbo.FactOpiniones WITH (TABLOCKX, HOLDLOCK))
        THROW 50121, N'FactOpiniones debe permanecer vacía.', 1;

    IF OBJECT_ID(N'dbo.EquivalenciaProducto', N'U') IS NULL
        CREATE TABLE dbo.EquivalenciaProducto (
            SqlOrigenID nvarchar(100) NOT NULL PRIMARY KEY,
            CsvOrigenID nvarchar(100) NOT NULL UNIQUE REFERENCES dbo.DimProducto(ProductoOrigenID)
        );
    IF OBJECT_ID(N'dbo.EquivalenciaCliente', N'U') IS NULL
        CREATE TABLE dbo.EquivalenciaCliente (
            SqlOrigenID nvarchar(100) NOT NULL PRIMARY KEY,
            CsvOrigenID nvarchar(100) NOT NULL UNIQUE REFERENCES dbo.DimCliente(ClienteOrigenID)
        );

    EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.usp_ReconciliarDimensionesOriginales
    @Plan nvarchar(max),
    @Mode tinyint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF DB_NAME() <> N''DW_OpinionesClientes'' OR @@TRANCOUNT <> 0 OR @Mode IS NULL OR @Mode NOT IN (0, 1, 2)
        THROW 50110, N''Contexto o modo inválido para reconciliar.'', 1;

    DECLARE @Products TABLE (
        SqlKey nvarchar(100) COLLATE DATABASE_DEFAULT PRIMARY KEY,
        CsvKey nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL UNIQUE,
        Nombre nvarchar(200) NOT NULL, Categoria nvarchar(100) NULL
    );
    DECLARE @Clients TABLE (
        SqlKey nvarchar(100) COLLATE DATABASE_DEFAULT PRIMARY KEY,
        CsvKey nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL UNIQUE
    );
    DECLARE @Dates TABLE (FechaKey int PRIMARY KEY);
    DECLARE @ProductActions TABLE (OldKey int PRIMARY KEY, SqlKey nvarchar(100), CsvKey nvarchar(100), KeepCsv bit);
    DECLARE @ClientActions TABLE (OldKey int PRIMARY KEY, SqlKey nvarchar(100), CsvKey nvarchar(100), KeepCsv bit);
    DECLARE @Actions TABLE (Tabla nvarchar(40), Accion nvarchar(40), Filas int);
    DECLARE @locked bigint;

    INSERT @Products SELECT SqlKey, CsvKey, Name, Category FROM OPENJSON(@Plan, ''$.Products'')
        WITH (SqlKey nvarchar(100), CsvKey nvarchar(100), Name nvarchar(200), Category nvarchar(100));
    INSERT @Clients SELECT SqlKey, CsvKey FROM OPENJSON(@Plan, ''$.Clients'')
        WITH (SqlKey nvarchar(100), CsvKey nvarchar(100));
    INSERT @Dates SELECT CONVERT(int, value) FROM OPENJSON(@Plan, ''$.Dates'');
    IF (SELECT COUNT(*) FROM @Products) <> 20 OR (SELECT COUNT(*) FROM @Clients) <> 39
        OR (SELECT COUNT(*) FROM @Dates WHERE FechaKey IN (20261001, 20261002, 20261003)) <> 3
        OR (SELECT COUNT(*) FROM @Dates) <> 3
        OR EXISTS (SELECT 1 FROM @Products WHERE SqlKey NOT LIKE N''SQL:%'' OR CsvKey NOT LIKE N''CSV:PROD:%'')
        OR EXISTS (SELECT 1 FROM @Clients WHERE SqlKey NOT LIKE N''SQL:%'' OR CsvKey NOT LIKE N''CSV:CLI:%'')
        THROW 50111, N''El plan no coincide con el lote histórico auditado.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.FactOpiniones WITH (TABLOCKX, HOLDLOCK))
            THROW 50112, N''FactOpiniones no está vacía. Se canceló la reconciliación.'', 1;

        -- Retener los bloqueos hasta confirmar o revertir impide usar un plan obsoleto.
        SELECT @locked = COUNT_BIG(*) FROM dbo.DimProducto WITH (TABLOCKX, HOLDLOCK);
        SELECT @locked = COUNT_BIG(*) FROM dbo.DimCliente WITH (TABLOCKX, HOLDLOCK);
        SELECT @locked = COUNT_BIG(*) FROM dbo.DimFecha WITH (TABLOCKX, HOLDLOCK);
        SELECT @locked = COUNT_BIG(*) FROM dbo.EquivalenciaProducto WITH (TABLOCKX, HOLDLOCK);
        SELECT @locked = COUNT_BIG(*) FROM dbo.EquivalenciaCliente WITH (TABLOCKX, HOLDLOCK);

        IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N''dbo.FactOpiniones'')
            AND is_disabled = 0 AND is_not_trusted = 0
            AND referenced_object_id IN (OBJECT_ID(N''dbo.DimProducto''), OBJECT_ID(N''dbo.DimCliente''),
                OBJECT_ID(N''dbo.DimFecha''), OBJECT_ID(N''dbo.DimFuente''), OBJECT_ID(N''dbo.DimSentimiento''),
                OBJECT_ID(N''dbo.DimEvento''))) <> 6
            THROW 50113, N''Las seis FK del hecho deben estar habilitadas y ser confiables.'', 1;
        IF EXISTS (
            SELECT 1 FROM sys.foreign_keys
            WHERE referenced_object_id IN (OBJECT_ID(N''dbo.DimProducto''), OBJECT_ID(N''dbo.DimCliente''), OBJECT_ID(N''dbo.DimFecha''))
              AND NOT (parent_object_id = OBJECT_ID(N''dbo.FactOpiniones'')
                OR (parent_object_id = OBJECT_ID(N''dbo.EquivalenciaProducto'') AND referenced_object_id = OBJECT_ID(N''dbo.DimProducto''))
                OR (parent_object_id = OBJECT_ID(N''dbo.EquivalenciaCliente'') AND referenced_object_id = OBJECT_ID(N''dbo.DimCliente''))))
           OR EXISTS (SELECT 1 FROM sys.triggers WHERE is_disabled = 0 AND parent_id IN
              (OBJECT_ID(N''dbo.DimProducto''), OBJECT_ID(N''dbo.DimCliente''), OBJECT_ID(N''dbo.DimFecha''),
               OBJECT_ID(N''dbo.EquivalenciaProducto''), OBJECT_ID(N''dbo.EquivalenciaCliente'')))
            THROW 50114, N''Hay dependencias o disparadores no previstos.'', 1;

        IF EXISTS (SELECT 1 FROM dbo.DimProducto AS d WHERE d.ProductoOrigenID LIKE N''SQL:%''
                   AND NOT EXISTS (SELECT 1 FROM @Products AS p WHERE p.SqlKey = d.ProductoOrigenID))
           OR EXISTS (SELECT 1 FROM dbo.DimCliente AS d WHERE d.ClienteOrigenID LIKE N''SQL:%''
                      AND NOT EXISTS (SELECT 1 FROM @Clients AS p WHERE p.SqlKey = d.ClienteOrigenID))
            THROW 50115, N''Existen claves SQL fuera de la lista histórica verificada.'', 1;

        IF EXISTS (
            SELECT 1 FROM @Products AS p JOIN dbo.DimProducto AS d ON d.ProductoOrigenID IN (p.SqlKey, p.CsvKey)
            WHERE d.Nombre COLLATE Latin1_General_100_BIN2 <> p.Nombre COLLATE Latin1_General_100_BIN2
               OR d.Categoria COLLATE Latin1_General_100_BIN2 <> p.Categoria COLLATE Latin1_General_100_BIN2
               OR (d.Categoria IS NULL AND p.Categoria IS NOT NULL)
               OR (d.Categoria IS NOT NULL AND p.Categoria IS NULL))
           OR EXISTS (
            SELECT 1 FROM @Clients AS p JOIN dbo.DimCliente AS d ON d.ClienteOrigenID = p.SqlKey
            WHERE d.Pais IS NOT NULL OR d.Edad IS NOT NULL OR d.TipoCliente IS NOT NULL)
           OR EXISTS (
            SELECT 1 FROM @Dates AS p JOIN dbo.DimFecha AS d ON d.FechaKey = p.FechaKey
            WHERE YEAR(d.Fecha) * 10000 + MONTH(d.Fecha) * 100 + DAY(d.Fecha) <> p.FechaKey
               OR d.Anio <> YEAR(d.Fecha) OR d.Trimestre <> DATEPART(quarter, d.Fecha)
               OR d.Mes <> MONTH(d.Fecha) OR d.Dia <> DAY(d.Fecha))
            THROW 50116, N''Un candidato tiene atributos distintos de la carga auditada.'', 1;

        INSERT @ProductActions
        SELECT d.ProductoKey, p.SqlKey, p.CsvKey, CASE WHEN c.ProductoKey IS NULL THEN 0 ELSE 1 END
        FROM @Products p JOIN dbo.DimProducto d ON d.ProductoOrigenID = p.SqlKey
        LEFT JOIN dbo.DimProducto c ON c.ProductoOrigenID = p.CsvKey;
        INSERT @ClientActions
        SELECT d.ClienteKey, p.SqlKey, p.CsvKey, CASE WHEN c.ClienteKey IS NULL THEN 0 ELSE 1 END
        FROM @Clients p JOIN dbo.DimCliente d ON d.ClienteOrigenID = p.SqlKey
        LEFT JOIN dbo.DimCliente c ON c.ClienteOrigenID = p.CsvKey;

        IF EXISTS (
            SELECT 1 FROM @ProductActions a JOIN dbo.EquivalenciaProducto e ON e.SqlOrigenID = a.SqlKey OR e.CsvOrigenID IN (a.CsvKey, a.SqlKey)
            WHERE e.SqlOrigenID <> a.SqlKey OR e.CsvOrigenID <> a.CsvKey)
           OR EXISTS (
            SELECT 1 FROM @ClientActions a JOIN dbo.EquivalenciaCliente e ON e.SqlOrigenID = a.SqlKey OR e.CsvOrigenID IN (a.CsvKey, a.SqlKey)
            WHERE e.SqlOrigenID <> a.SqlKey OR e.CsvOrigenID <> a.CsvKey)
            THROW 50117, N''Existe una equivalencia incompatible con el plan.'', 1;

        INSERT @Actions
        SELECT N''DimProducto'', N''Conservar PK y cambiar clave'', COUNT(*) FROM @ProductActions WHERE KeepCsv = 0
        UNION ALL SELECT N''DimProducto'', N''Eliminar alias redundante'', COUNT(*) FROM @ProductActions WHERE KeepCsv = 1
        UNION ALL SELECT N''DimCliente'', N''Conservar PK y cambiar clave'', COUNT(*) FROM @ClientActions WHERE KeepCsv = 0
        UNION ALL SELECT N''DimCliente'', N''Eliminar alias redundante'', COUNT(*) FROM @ClientActions WHERE KeepCsv = 1
        UNION ALL SELECT N''DimFecha'', N''Eliminar fecha de ejemplo'', COUNT(*) FROM dbo.DimFecha d JOIN @Dates p ON p.FechaKey = d.FechaKey;

        IF @Mode <> 0
        BEGIN
            UPDATE d SET ProductoOrigenID = a.CsvKey
            FROM dbo.DimProducto d JOIN @ProductActions a ON a.OldKey = d.ProductoKey WHERE a.KeepCsv = 0;
            UPDATE d SET ClienteOrigenID = a.CsvKey
            FROM dbo.DimCliente d JOIN @ClientActions a ON a.OldKey = d.ClienteKey WHERE a.KeepCsv = 0;

            INSERT dbo.EquivalenciaProducto (SqlOrigenID, CsvOrigenID)
            SELECT a.SqlKey, a.CsvKey FROM @ProductActions a WHERE NOT EXISTS
                (SELECT 1 FROM dbo.EquivalenciaProducto e WHERE e.SqlOrigenID = a.SqlKey);
            INSERT dbo.EquivalenciaCliente (SqlOrigenID, CsvOrigenID)
            SELECT a.SqlKey, a.CsvKey FROM @ClientActions a WHERE NOT EXISTS
                (SELECT 1 FROM dbo.EquivalenciaCliente e WHERE e.SqlOrigenID = a.SqlKey);

            DELETE d FROM dbo.DimProducto d JOIN @ProductActions a ON a.OldKey = d.ProductoKey WHERE a.KeepCsv = 1;
            DELETE d FROM dbo.DimCliente d JOIN @ClientActions a ON a.OldKey = d.ClienteKey WHERE a.KeepCsv = 1;
            DELETE d FROM dbo.DimFecha d JOIN @Dates p ON p.FechaKey = d.FechaKey;

            IF EXISTS (SELECT 1 FROM dbo.FactOpiniones)
                THROW 50118, N''La tabla de hechos debe permanecer vacía.'', 1;
        END;

        IF @Mode = 2 COMMIT TRANSACTION;
        ELSE ROLLBACK TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
    SELECT Tabla, Accion, Filas FROM @Actions;
END;';
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
