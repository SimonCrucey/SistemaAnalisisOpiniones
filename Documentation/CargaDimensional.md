# Carga dimensional y pruebas manuales

Esta guía reproduce la **demostración sintética**. Para la práctica académica definitiva con los originales, consulta el [procedimiento académico](Origen-Academico.md) y los [resultados comprobados](Resultados-Carga-Dimensional.md).

La extracción sigue siendo el modo predeterminado. La opción inicial `--load-dimensions` inicia otro Worker que lee un lote publicado y escribe únicamente en las dimensiones respaldadas por las fuentes. Usa PowerShell 7, SQL Server local, sqlcmd (ODBC) y SSMS. Abre la solución .slnx en una versión de Visual Studio compatible con ese formato y con .NET 8. Realiza las pruebas de escritura primero en la demostración.

Los comandos usan `-I` para activar `QUOTED_IDENTIFIER` en la sesión ODBC y `-f 65001` para entrada y salida UTF-8. Los scripts de preparación también fijan explícitamente las opciones `SET` de la sesión. En SSMS, abre los archivos guardados y activa **Consulta → Modo SQLCMD** para los dos scripts de preparación. Los caracteres de los catálogos deben verse correctamente, por ejemplo, `Reseña web`. Si la consola muestra caracteres incorrectos, usa PowerShell 7 y, en esa sesión, ejecuta `[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)` antes de repetir la consulta de verificación. La variante Go de sqlcmd habilita `QUOTED_IDENTIFIER` por defecto y los comandos de esta guía están escritos para la variante ODBC.

## Preparar la demostración

Desde la raíz de la solución:

```powershell
dotnet restore .\SistemaAnalisisOpiniones.slnx -m:1
dotnet build .\SistemaAnalisisOpiniones.slnx --no-restore -m:1
sqlcmd -S localhost -E -C -b -I -f 65001 -d master -i .\demo\Preparar-Demo.sql
sqlcmd -S localhost -E -C -b -I -f 65001 -d master -i .\demo\Preparar-DW-Demo.sql
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes_Demo_ITLA -i .\DataWarehouse\Verificar-Dimensiones.sql
```

Ejecuta el script del DW desde la raíz con sqlcmd ODBC, porque incluye el esquema compartido mediante una ruta relativa. Usa SSMS para las consultas de verificación. Los scripts de preparación entran en `master` para crear las bases que falten y cambian al nombre fijo de la base demo antes de preparar tablas y datos. Las operaciones de escritura comprueban la base actual. Si usas una instancia con nombre, usa el mismo servidor en SQLCMD, SSMS y las cadenas de conexión. Conecta al DW demo para la verificación. En una demo nueva espera 3 fuentes, 4 sentimientos y las demás tablas vacías. Captura el estado inicial.

En una segunda terminal, desde la misma raíz, inicia la API y déjala abierta. Para la demo usa terminales nuevas sin Social__AcademicDirectory ni Extraction__AcademicDirectory:

```powershell
dotnet run --no-build --project .\Api\SistemaAnalisisOpiniones.Api.csproj
```

En la primera terminal:

```powershell
Invoke-RestMethod 'http://127.0.0.1:5187/api/comentarios' | Format-Table id,dataset
$env:ConnectionStrings__TransactionDb = 'Server=localhost;Database=AnalisisOpiniones_Demo_ITLA;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;'
$env:ConnectionStrings__DataWarehouse = 'Server=localhost;Database=DW_OpinionesClientes_Demo_ITLA;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;'
$env:Extraction__SurveyPath = (Resolve-Path .\demo\encuestas-demo.csv).Path
$env:Extraction__StagingPath = Join-Path ([IO.Path]::GetTempPath()) ('stage-dimensiones-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj
if ($LASTEXITCODE -ne 0) { throw 'Falló la extracción.' }

$lote = Get-ChildItem -LiteralPath $env:Extraction__StagingPath -Directory |
    Where-Object { $_.Name -notlike '.pending-*' -and (Test-Path (Join-Path $_.FullName 'manifest.json')) } |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $lote) { throw 'No se encontró un lote publicado.' }
$env:Dimensions__BatchPath = $lote.FullName
$env:Dimensions__ProductsPath = (Resolve-Path .\demo\productos-dimensiones.csv).Path
$env:Dimensions__ClientsPath = (Resolve-Path .\demo\clientes-dimensiones.csv).Path
Get-Content (Join-Path $lote.FullName 'manifest.json') -Raw
```

El manifiesto demo esperado indica 3 encuestas, 2 reseñas y 3 comentarios. La API solo es necesaria para generar un lote. Para reutilizar uno existente, asigna su ruta absoluta a `Dimensions__BatchPath` y confirma que pertenece al mismo entorno que los catálogos y la conexión transaccional. El manifiesto actual no guarda el nombre de la base de origen.

## Cargar y comprobar

```powershell
dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj -- --load-dimensions
"Salida: $LASTEXITCODE"
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes_Demo_ITLA -i .\DataWarehouse\Verificar-Dimensiones.sql
dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj -- --load-dimensions
"Salida de reejecución: $LASTEXITCODE"
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes_Demo_ITLA -i .\DataWarehouse\Verificar-Dimensiones.sql
```

En una demo limpia se esperan 2 productos, 2 clientes, 3 fuentes, 4 sentimientos, 3 fechas, 0 eventos y 0 hechos. Fuentes y sentimientos vienen sembrados por el esquema. En la segunda carga se esperan cero inserciones y las mismas claves. Las consultas muestran metadatos, conteos, filas, duplicados y consistencia de fechas. Abre también el archivo en SSMS con el DW demo seleccionado para las capturas.

Prueba automatizada de dos cargas, conteos e idempotencia después de generar el lote y configurar las variables:

```powershell
.\tests\Validar-Dimensiones.ps1
```

La suite de la etapa anterior se conserva. Para ejecutar su modo sintético, usa `.	estsValidar-Extraccion.ps1 -Demo -Port 15187` con la conexión transaccional demo.

## Probar rollback en una base demo recién preparada

Haz esta prueba **antes de la primera carga exitosa**. La verificación inicial debe mostrar cero productos, fechas, clientes y hechos. En SSMS, usa la base demo y ejecuta:

```sql
USE [DW_OpinionesClientes_Demo_ITLA];
GO
IF DB_NAME() <> N'DW_OpinionesClientes_Demo_ITLA'
    THROW 50010, 'Prueba reservada al almacén demo.', 1;
IF EXISTS (SELECT 1 FROM dbo.DimProducto) OR EXISTS (SELECT 1 FROM dbo.DimFecha)
    THROW 50011, 'Usa la demostración antes de cargar dimensiones.', 1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_DemoRollback ON dbo.DimFecha
AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    THROW 50012, ''Fallo controlado de la demostración'', 1;
END;');
```

Ejecuta el Worker dimensional una vez. Espera código 1 y error SQL 50012. Como producto se escribe antes de fecha, el conteo de productos debe seguir en cero después del fallo. Comprueba también fechas, clientes y hechos con `Verificar-Dimensiones.sql`. Retira el disparador inmediatamente y ejecuta la carga normal:

```sql
USE [DW_OpinionesClientes_Demo_ITLA];
GO
IF DB_NAME() = N'DW_OpinionesClientes_Demo_ITLA'
    DROP TRIGGER IF EXISTS dbo.TR_DemoRollback;
```

Para probar el rechazo previo a la conexión al DW, pasa temporalmente una ruta existente cuyo nombre comience por .pending-. Espera código 1 y restaura la ruta del lote. Puedes usar un directorio aislado en TEMP en lugar de tocar staging real.

## Usar los datos académicos

Sigue el [flujo corregido con los archivos del profesor](Origen-Academico.md). El lote anterior de 703 registros incluía tres comentarios de ejemplo y carece del nuevo manifiesto de procedencia. Para cargar originales genera un lote nuevo y sigue la reconciliación previa sobre DW_OpinionesClientes. La prueba de rollback definitiva se ensaya en una transacción revertida sobre el DW original, después de verificar el respaldo.

## Reglas y solución de problemas

- En la demo las claves CSV y SQL siguen separadas. El flujo académico corregido usa un cruce verificable y guarda las equivalencias en el DW aislado.
- Cliente carga el identificador de origen respaldado por catálogo. País, edad y tipo quedan NULL. Los datos de contacto se excluyen de la carga y las capturas.
- Fecha se procesa incluso cuando faltan referencias. La clasificación ausente o desconocida se contabiliza como No clasificado y la desconocida queda registrada como incidencia.
- Evento queda sin carga por falta de fuente. FactOpiniones queda fuera de todas las escrituras.
- Código 1 con error de manifiesto indica incompatibilidad entre fuentes, estructura o conteos. Revisa el lote y su directorio.
- Un error SQL puede indicar servidor incorrecto, permisos, esquema faltante o el disparador de rollback. Revisa el número en consola sin publicar secretos. Un conflicto de atributos aborta toda la carga. Verifica el catálogo o la fila existente antes de reintentar.
- Una cancelación antes de confirmar debe dejar los cambios sin confirmar. El Worker registra código 130 cuando recibe la cancelación del host.

## Capturas para el PDF de entrega

1. Visual Studio con PublishedBatchReader, DimensionTransformer, DimensionLoader y DimensionWorker. Muestra el modo explícito, la transformación y el punto de confirmación de la transacción.
2. Consola con el nombre del lote y manifest.json. Muestra fuentes y conteos, sin datos de opinión.
3. SSMS con nombre del DW y los conteos iniciales de Verificar-Dimensiones.sql.
4. PowerShell con candidatos, incidencias, insertados, reutilizados, tiempo y mensaje de confirmación.
5. SSMS con las seis cuadrículas y los conteos finales. Muestra DimEvento vacía con su explicación. Para clientes usa la proyección que oculta IDs de origen.
6. Segunda ejecución con cero inserciones y verificación de duplicados y fechas en cero. Conserva las mismas claves mostradas.
7. Comprobación de FactOpiniones antes y después. En una demo nueva el conteo es cero.
8. Opcional para robustez, prueba de rollback con código 1 y conteos de destino iguales al estado anterior.
9. En el PDF académico añade el enlace a la versión de GitHub que contenga la carga, cuando autorices su publicación.

## Resultados manuales del flujo anterior

La ejecución académica reportada por el estudiante procesó 703 registros de staging. La primera carga insertó 474 filas y dejó 481 filas dimensionales en 581 ms. La segunda terminó en 727 ms, con 0 inserciones y 481 filas reutilizadas. La verificación reportó 0 claves duplicadas, 0 fechas inconsistentes y 0 productos sin clave o nombre. `FactOpiniones` permaneció vacía. El estudiante también comprobó el rollback en la demo. Los tiempos corresponden a esas ejecuciones.
