:setvar WarehouseName "DW_OpinionesClientes_Demo_ITLA"
:ON ERROR EXIT
-- Adaptación del esquema dimensional académico para la demostración aislada.
USE [master];
GO
IF DB_NAME() <> N'master'
    THROW 50002, N'Conecta a master para preparar la base demo.', 1;
IF DB_ID(N'DW_OpinionesClientes_Demo_ITLA') IS NULL
BEGIN
    CREATE DATABASE DW_OpinionesClientes_Demo_ITLA;
END;
GO
USE [DW_OpinionesClientes_Demo_ITLA];
GO
:r ./DataWarehouse/Crear-Esquema-Dimensional.sql
