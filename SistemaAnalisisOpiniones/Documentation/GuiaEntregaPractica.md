# Guía de entrega de la práctica ETL

Esta guía describe los componentes requeridos para la práctica, cómo ejecutar el ETL y cómo verificar los resultados mediante consultas SQL.

> **Estado:** la carga real, las cuatro tablas y las cinco consultas de validación ya fueron verificadas. Totales finales: 503 clientes, 203 productos, 108 fuentes y 212 opiniones; integridad y duplicados en 0. La segunda ejecución confirmó la idempotencia con 0 insertados y 206 duplicados omitidos. Solo falta revisar el ZIP final.

## 1. Alcance de la práctica

La práctica requiere:

1. Una aplicación que lea seis archivos CSV, valide y transforme los datos, detecte duplicados e inserte los registros respetando las relaciones.
2. Un script de base de datos con tablas, claves primarias (PK), claves foráneas (FK), procedimientos almacenados y vistas.
3. Evidencias de la ejecución del ETL y de las tablas pobladas.
4. Al menos cinco consultas SQL que permitan verificar los datos cargados y la integridad referencial.

No se incluyen sistemas adicionales de auditoría, aprobación de evidencias o recuperación de estados, ya que no forman parte del alcance requerido.

## 2. Componentes del proyecto

| Requisito                                             | Archivo o componente                                              |
| ----------------------------------------------------- | ----------------------------------------------------------------- |
| Lectura de los seis CSV                               | `ETL/Extract/CsvFileReader.cs` y `ETL/Extract/CsvEtlExtractor.cs` |
| Validación de estructura, campos obligatorios y tipos | `CsvFileReader` y `EtlPlanBuilder`                                |
| Limpieza y transformación                             | `ETL/Transform/CodeNormalizer.cs` y `EtlPlanBuilder.cs`           |
| Detección de duplicados                               | `EtlPlanBuilder` y `ETL/Load/SqlEtlWriter.cs`                     |
| Inserción respetando PK y FK                          | `ETL/Load/SqlEtlWriter.cs` y `DataBase/ScriptDB.sql`              |
| Tablas, claves, vistas y procedimientos               | `DataBase/ScriptDB.sql`                                           |
| Resumen de registros procesados                       | Salida de consola de `Program.cs`                                 |
| Consultas de validación                               | `DataBase/ConsultasValidacionEntrega.sql`                         |
| Capturas de evidencia                                 | `Documentation/Evidencias/`                                       |

## 3. Resultados esperados del dry-run

El dry-run valida los archivos CSV sin conectarse a SQL Server. Los resultados esperados son:

| Concepto               | Total |
| ---------------------- | ----: |
| Clientes leídos        |   500 |
| Productos leídos       |   200 |
| Fuentes leídas/creadas |   105 |
| Opiniones leídas       |   900 |
| Opiniones aceptadas    |   206 |
| Opiniones pendientes   |   112 |
| Opiniones rechazadas   |   582 |
| Errores de validación  |   968 |

Estos valores corresponden al procesamiento de los CSV y no deben confundirse con los totales de las tablas de SQL Server.

En una base nueva, los totales previstos después de una carga exitosa son:

| Tabla             | Total esperado |
| ----------------- | -------------: |
| `dbo.Clientes`    |            500 |
| `dbo.Productos`   |            200 |
| `dbo.FuenteDatos` |            105 |
| `dbo.Opiniones`   |            206 |

Si la base contiene datos de ejemplo u otros registros anteriores, los totales pueden ser mayores. Los valores reales deben confirmarse con las consultas de validación.

En la base local usada para la prueba ya existían 3 clientes, 3 productos, 3 fuentes y 6 opiniones. La primera carga insertó 500/200/105/206 y los totales finales fueron **503/203/108/212**. Las consultas 4 y 5 devolvieron 0. Una segunda ejecución insertó 0 registros y omitió 206 opiniones duplicadas.

## 4. Preparación de la base de datos

1. Tener .NET 8 instalado.
2. Disponer de una instancia de SQL Server autorizada.
3. Abrir `SistemaAnalisisOpiniones/DataBase/ScriptDB.sql` en SQL Server Management Studio (SSMS).
4. Ejecutar el script y comprobar que la base `AnalisisOpiniones` y los objetos requeridos estén disponibles.

El script define las tablas `dbo.Clientes`, `dbo.Productos`, `dbo.FuenteDatos` y `dbo.Opiniones`, sus relaciones, las vistas y los procedimientos almacenados del proyecto.

El script está diseñado para crear o actualizar los objetos de la base sin borrar los datos existentes. No obstante, debe revisarse antes de ejecutarlo en una base que contenga información importante.

## 5. Ejecución del ETL

Desde la raíz del proyecto, compilar la solución:

```powershell
dotnet build ".\SistemaAnalisisOpiniones.slnx"
```

### Validar los archivos CSV

```powershell
dotnet run --project ".\SistemaAnalisisOpiniones\SistemaAnalisisOpiniones.csproj" -- --dry-run
```

El modo `--dry-run` valida los CSV sin abrir una conexión a SQL Server.

### Ejecutar la carga

Configurar la conexión a la base de datos:

```powershell
$env:ETL_CONNECTION_STRING = "Server=localhost;Database=AnalisisOpiniones;Integrated Security=True;TrustServerCertificate=True;"
```

Después, ejecutar:

```powershell
dotnet run --project ".\SistemaAnalisisOpiniones\SistemaAnalisisOpiniones.csproj" -- --execute
```

La salida de consola debe permitir revisar, según la información que proporcione la aplicación:

- El modo de ejecución y la carpeta de datos.
- Los clientes, productos, fuentes y opiniones procesados.
- Los registros aceptados, pendientes y rechazados.
- Los errores de validación.
- Los registros insertados y los duplicados omitidos.

La captura de una ejecución real debe distinguirse de una captura del dry-run.

**Importante:** la salida original de la primera carga no se conservó como captura. La ejecución real quedó respaldada por el registro `01b-ejecucion-idempotente.txt`, que muestra una segunda ejecución con 0 insertados y 206 duplicados omitidos. No es necesario repetir la carga; los totales y la integridad se comprueban con las consultas.

## 6. Evidencias de la entrega

Todas las capturas deben guardarse en:

`SistemaAnalisisOpiniones/Documentation/Evidencias/`

### 6.1. Ejecución del ETL

- `01-dry-run.png`: validación de los CSV sin conexión a SQL Server. No debe presentarse como carga real.
- `01b-ejecucion-idempotente.txt`: registro de la segunda ejecución real, con 0 insertados y 206 duplicados omitidos.
- La primera carga insertó 500/200/105/206, según los totales verificados en las tablas y consultas.

### 6.2. Tablas pobladas

Las cuatro capturas de tablas deben mostrar los resultados obtenidos en SQL Server:

| Archivo            | Contenido                      |
| ------------------ | ------------------------------ |
| `02-clientes.png`  | Registros de `dbo.Clientes`    |
| `03-productos.png` | Registros de `dbo.Productos`   |
| `04-fuentes.png`   | Registros de `dbo.FuenteDatos` |
| `05-opiniones.png` | Registros de `dbo.Opiniones`   |

### 6.3. Cinco consultas de validación

Abrir `DataBase/ConsultasValidacionEntrega.sql` en SSMS y ejecutar cada consulta por separado. Capturar la cuadrícula de resultados después de cada ejecución.

| Archivo                        | Consulta                             | Resultado que se debe comprobar      |
| ------------------------------ | ------------------------------------ | ------------------------------------ |
| `06-consulta-1-totales.png`    | Conteo total de registros            | Totales reales de las cuatro tablas  |
| `07-consulta-2-fuentes.png`    | Distribución de opiniones por fuente | Fuentes que tienen opiniones         |
| `08-consulta-3-detalle.png`    | Muestra de opiniones                 | Cliente, producto y fuente resueltos |
| `09-consulta-4-integridad.png` | Integridad referencial               | Los tres conteos deben ser cero      |
| `10-consulta-5-duplicados.png` | Duplicados de clientes y opiniones   | Los dos conteos deben ser cero       |

Si una consulta devuelve resultados inesperados, se deben conservar los resultados y revisar la causa antes de afirmar que la validación fue exitosa.

## 7. Transformaciones aplicadas

La explicación de la entrega debe incluir las siguientes reglas:

- Normalización de identificadores externos de clientes y productos.
- Normalización de códigos de fuentes de datos.
- Conversión de calificaciones web a una escala de satisfacción de 0 a 100.
- Conversión de las calificaciones de encuestas a la escala de 0 a 100.
- Clasificación de las reseñas web como negativas, neutras o positivas según la calificación.
- Registro de comentarios sociales sin clasificación ni puntaje como pendientes, sin inventar valores.
- Rechazo y contabilización de filas inválidas.
- Reutilización de clientes y productos existentes cuando coinciden según las reglas de identificación.

## 8. Detección de duplicados

El proceso aplica las siguientes reglas, según la implementación del proyecto:

- **Clientes:** correo electrónico normalizado.
- **Productos:** nombre normalizado.
- **Opiniones dentro de los archivos:** identificador externo.
- **Opiniones existentes en la base de datos:** cliente, producto, fuente, fecha y comentario normalizado.

Si una opinión coincide con una existente según las claves de negocio, pero presenta una clasificación o un puntaje diferente, la carga se detiene y revierte la transacción para evitar datos contradictorios.

## 9. Lista final de entrega

- [x] Solución y código fuente del ETL.
- [x] Seis archivos CSV requeridos.
- [x] `DataBase/ScriptDB.sql`.
- [x] `DataBase/ConsultasValidacionEntrega.sql`.
- [x] README actualizado.
- [x] Guía de entrega actualizada.
- [x] Evidencia de ejecución del ETL.
- [x] Cuatro capturas de las tablas pobladas.
- [x] Cinco capturas de las consultas de validación.
- [x] Compilación final sin errores.
- [x] Archivo ZIP final revisado.

La validación de SQL Server y el ZIP final ya están completos. Solo queda adjuntar las capturas y el ZIP en la plataforma de entrega.
