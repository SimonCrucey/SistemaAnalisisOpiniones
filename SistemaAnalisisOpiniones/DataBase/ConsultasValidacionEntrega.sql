-- =====================================================
-- CONSULTAS DE VALIDACION DE LA ENTREGA (SOLO LECTURA)
-- Base de datos: AnalisisOpiniones
-- =====================================================

USE AnalisisOpiniones;
GO

-- 1. Conteo total de registros cargados.
SELECT N'Clientes' AS Tabla, COUNT(*) AS Total FROM dbo.Clientes
UNION ALL
SELECT N'Productos', COUNT(*) FROM dbo.Productos
UNION ALL
SELECT N'Fuentes', COUNT(*) FROM dbo.FuenteDatos
UNION ALL
SELECT N'Opiniones', COUNT(*) FROM dbo.Opiniones;
GO

-- 2. Distribucion de opiniones por fuente.
SELECT
    CodigoFuente,
    Fuente,
    TipoFuente,
    TotalOpiniones,
    PromedioPuntaje,
    Positivas,
    Neutras,
    Negativas
FROM dbo.vwResumenSatisfaccion
WHERE TotalOpiniones > 0
ORDER BY TotalOpiniones DESC, Fuente;
GO

-- 3. Muestra de opiniones con cliente, producto y fuente resueltos.
SELECT TOP (20)
    IdOpinion,
    Fecha,
    Cliente,
    Producto,
    Categoria,
    Fuente,
    Clasificacion,
    PuntajeSatisfaccion,
    Comentario
FROM dbo.vwOpinionesDetalle
ORDER BY IdOpinion;
GO

-- 4. Integridad referencial: todas las filas deben mostrar cero.
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
WHERE NOT EXISTS (SELECT 1 FROM dbo.FuenteDatos AS f WHERE f.IdFuente = o.IdFuente);
GO

-- 5. Duplicados por reglas de negocio.
SELECT N'Correos de cliente duplicados' AS Validacion, COUNT(*) AS Total
FROM
(
    SELECT Email
    FROM dbo.Clientes
    GROUP BY Email
    HAVING COUNT(*) > 1
) AS clientes_duplicados
UNION ALL
SELECT N'Comentarios duplicados por cliente, producto, fuente y fecha', COUNT(*)
FROM
(
    SELECT
        IdCliente,
        IdProducto,
        IdFuente,
        CAST(Fecha AS DATE) AS Fecha,
        Comentario
    FROM dbo.Opiniones
    GROUP BY
        IdCliente,
        IdProducto,
        IdFuente,
        CAST(Fecha AS DATE),
        Comentario
    HAVING COUNT(*) > 1
) AS opiniones_duplicadas;
GO
