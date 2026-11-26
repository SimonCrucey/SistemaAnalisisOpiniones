-- Selecciona el almacén al conectar (SSMS o sqlcmd -d).
SET NOCOUNT ON;
IF DB_NAME() NOT IN (N'DW_OpinionesClientes', N'DW_OpinionesClientes_Demo_ITLA')
    THROW 50001, 'Conecta al almacén dimensional antes de consultar.', 1;

SELECT DB_NAME() AS Almacen;
SELECT 'DimProducto' AS Tabla, COUNT_BIG(*) AS Filas FROM dbo.DimProducto
UNION ALL SELECT 'DimFuente', COUNT_BIG(*) FROM dbo.DimFuente
UNION ALL SELECT 'DimSentimiento', COUNT_BIG(*) FROM dbo.DimSentimiento
UNION ALL SELECT 'DimFecha', COUNT_BIG(*) FROM dbo.DimFecha
UNION ALL SELECT 'DimCliente', COUNT_BIG(*) FROM dbo.DimCliente
UNION ALL SELECT 'DimEvento', COUNT_BIG(*) FROM dbo.DimEvento
UNION ALL SELECT 'FactOpiniones', COUNT_BIG(*) FROM dbo.FactOpiniones;

SELECT TOP (20) ProductoKey, ProductoOrigenID, Nombre, Categoria FROM dbo.DimProducto ORDER BY ProductoKey;
SELECT FuenteKey, Nombre, TipoOrigen FROM dbo.DimFuente ORDER BY FuenteKey;
SELECT SentimientoKey, Nombre FROM dbo.DimSentimiento ORDER BY SentimientoKey;
SELECT TOP (20) FechaKey, Fecha, Anio, Trimestre, Mes, Dia FROM dbo.DimFecha ORDER BY Fecha;
-- Evita mostrar identificadores de cliente de origen académico en capturas públicas.
SELECT TOP (20) ClienteKey, CASE WHEN ClienteOrigenID LIKE 'CSV:%' THEN 'CSV'
    WHEN ClienteOrigenID LIKE 'SQL:%' THEN 'SQL' ELSE 'Otro' END AS Origen,
    Pais, Edad, TipoCliente FROM dbo.DimCliente ORDER BY ClienteKey;
SELECT TOP (20) EventoKey, Nombre, TipoEvento, FechaInicio, FechaFin FROM dbo.DimEvento ORDER BY EventoKey;

SELECT 'DimProducto' AS Tabla, COUNT_BIG(*) AS ClavesDuplicadas FROM
    (SELECT ProductoOrigenID FROM dbo.DimProducto GROUP BY ProductoOrigenID HAVING COUNT(*) > 1) d
UNION ALL SELECT 'DimFuente', COUNT_BIG(*) FROM
    (SELECT Nombre FROM dbo.DimFuente GROUP BY Nombre HAVING COUNT(*) > 1) d
UNION ALL SELECT 'DimSentimiento', COUNT_BIG(*) FROM
    (SELECT Nombre FROM dbo.DimSentimiento GROUP BY Nombre HAVING COUNT(*) > 1) d
UNION ALL SELECT 'DimFecha', COUNT_BIG(*) FROM
    (SELECT Fecha FROM dbo.DimFecha GROUP BY Fecha HAVING COUNT(*) > 1) d
UNION ALL SELECT 'DimCliente', COUNT_BIG(*) FROM
    (SELECT ClienteOrigenID FROM dbo.DimCliente GROUP BY ClienteOrigenID HAVING COUNT(*) > 1) d;

SELECT COUNT_BIG(*) AS FechasInconsistentes FROM dbo.DimFecha
WHERE FechaKey <> YEAR(Fecha) * 10000 + MONTH(Fecha) * 100 + DAY(Fecha)
   OR Anio <> YEAR(Fecha) OR Trimestre <> DATEPART(quarter, Fecha)
   OR Mes <> MONTH(Fecha) OR Dia <> DAY(Fecha);
SELECT COUNT_BIG(*) AS ProductosSinClaveONombre FROM dbo.DimProducto
WHERE LEN(TRIM(ProductoOrigenID)) = 0 OR LEN(TRIM(Nombre)) = 0;

SELECT t.name AS Tabla, c.name AS Columna, TYPE_NAME(c.user_type_id) AS Tipo,
       c.max_length AS LongitudBytes, c.is_nullable AS AdmiteNulo, c.is_identity AS Identidad
FROM sys.tables AS t INNER JOIN sys.columns AS c ON c.object_id = t.object_id
WHERE t.schema_id = SCHEMA_ID('dbo') AND (t.name LIKE 'Dim%' OR t.name = 'FactOpiniones')
ORDER BY t.name, c.column_id;

SELECT OBJECT_NAME(i.object_id) AS Tabla, i.name AS Indice, i.is_primary_key AS EsPK,
       i.is_unique AS EsUnico, c.name AS Columna, ic.key_ordinal AS Posicion
FROM sys.indexes AS i
INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE OBJECT_SCHEMA_NAME(i.object_id) = 'dbo'
  AND (OBJECT_NAME(i.object_id) LIKE 'Dim%' OR OBJECT_NAME(i.object_id) = 'FactOpiniones')
  AND ic.key_ordinal > 0
ORDER BY Tabla, Indice, Posicion;

SELECT OBJECT_NAME(parent_object_id) AS Tabla, name AS Restriccion, definition AS Regla,
       is_disabled AS Desactivada, is_not_trusted AS NoConfiable
FROM sys.check_constraints
WHERE OBJECT_SCHEMA_NAME(parent_object_id) = 'dbo'
  AND (OBJECT_NAME(parent_object_id) LIKE 'Dim%' OR OBJECT_NAME(parent_object_id) = 'FactOpiniones');

SELECT name AS ClaveForanea, OBJECT_NAME(referenced_object_id) AS Dimension,
       is_disabled AS Desactivada
FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.FactOpiniones');

IF DB_NAME() = N'DW_OpinionesClientes'
   AND OBJECT_ID(N'dbo.EquivalenciaProducto', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.EquivalenciaCliente', N'U') IS NOT NULL
BEGIN
    SELECT 'EquivalenciaProducto' AS Tabla, COUNT_BIG(*) AS Filas FROM dbo.EquivalenciaProducto
    UNION ALL SELECT 'EquivalenciaCliente', COUNT_BIG(*) FROM dbo.EquivalenciaCliente;
    SELECT TOP (10) SqlOrigenID, CsvOrigenID FROM dbo.EquivalenciaProducto ORDER BY SqlOrigenID;
    -- Comparar estas huellas antes y después del ensayo comprueba también claves y atributos.
    SELECT 'DimProducto' AS Tabla, CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT ProductoKey, ProductoOrigenID, Nombre, Categoria FROM dbo.DimProducto ORDER BY ProductoKey
            FOR JSON PATH, INCLUDE_NULL_VALUES)), 2) AS SHA256
    UNION ALL SELECT 'DimCliente', CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT ClienteKey, ClienteOrigenID, Pais, Edad, TipoCliente FROM dbo.DimCliente ORDER BY ClienteKey
            FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
    UNION ALL SELECT 'DimFecha', CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT FechaKey, Fecha, Anio, Trimestre, Mes, Dia FROM dbo.DimFecha ORDER BY FechaKey
            FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
    UNION ALL SELECT 'EquivalenciaProducto', CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT SqlOrigenID, CsvOrigenID FROM dbo.EquivalenciaProducto ORDER BY SqlOrigenID FOR JSON PATH)), 2)
    UNION ALL SELECT 'EquivalenciaCliente', CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT SqlOrigenID, CsvOrigenID FROM dbo.EquivalenciaCliente ORDER BY SqlOrigenID FOR JSON PATH)), 2);
END;
