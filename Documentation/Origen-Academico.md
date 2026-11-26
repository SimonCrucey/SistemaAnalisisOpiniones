# Flujo académico con los archivos originales

La validación manual de este flujo obtuvo **900 opiniones y 486 filas dimensionales**. Consulta los [resultados definitivos](Resultados-Carga-Dimensional.md).

La extracción académica combina 500 encuestas del CSV, 200 reseñas de AnalisisOpiniones y 200 comentarios sociales servidos por la API local. El Worker lee los seis CSV del profesor para verificar SHA-256 y cotejar cada reseña SQL con su IdReview original. El manifiesto recoge el ambiente, la base de origen y los seis hashes. La API usa la huella de social_comments.csv en una cabecera y el extractor HTTP valida la cabecera para evitar mezclar los ambientes. Los CSV originales y los lotes quedan fuera de Git.

La API anterior solo servía tres comentarios de prueba. Esos tres se mantienen para la demo. En modo académico la API conserva el identificador, el cliente cuando existe, el producto, la plataforma, la fecha y el comentario originales. Escucha en loopback para la práctica y rechaza clientes de red no locales. Usa la respuesta académica solo en tu equipo. En las capturas muestra conteos y no el contenido personal.

## Antes de empezar

Abre PowerShell 7 y sitúate en la raíz que contiene SistemaAnalisisOpiniones.slnx. Conserva los seis originales descomprimidos juntos, por ejemplo en SistemaAnalisisOpiniones/Data dentro de esa raíz. El conjunto admitido se identifica por sus hashes, así que conserva los bytes originales. Instala los runtimes .NET y ASP.NET Core 8 y usa sqlcmd ODBC. Los comandos de preparación usan -I para QUOTED_IDENTIFIER y -f 65001 para UTF-8. Abre SSMS para inspeccionar los resultados. Si usas Visual Studio, abre el .slnx con soporte para .NET 8 y usa su terminal para seguir estos pasos.

```powershell
dotnet restore .\SistemaAnalisisOpiniones.slnx -m:1
dotnet build .\SistemaAnalisisOpiniones.slnx --no-restore -m:1
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes -i .\DataWarehouse\Verificar-Dimensiones.sql
```

Usa únicamente AnalisisOpiniones como origen de lectura y DW_OpinionesClientes como destino. Prepara un respaldo completo verificado del DW antes de instalar el procedimiento. Detén otros procesos de carga mientras haces la reconciliación. El usuario que instala necesita permisos de creación de tablas y procedimientos en el DW. Mantén la conexión transaccional con permisos de lectura.

Si el evaluador parte de una instancia propia sin las bases académicas, puede restaurar sus copias o preparar `AnalisisOpiniones` con `SistemaAnalisisOpiniones/DataBase/ScriptDB.sql` e importar los seis archivos con la [consola histórica](../SistemaAnalisisOpiniones/Documentation/GuiaEntregaPractica.md). Para el DW usa el esquema original de la práctica de modelado. En un DW sin la carga histórica, instala las tablas auxiliares y carga el lote original directamente. La reconciliación está reservada al DW que contiene la carga anterior y requiere su lote histórico. Para una ejecución autocontenida desde un clon, usa la [demostración sintética](CargaDimensional.md).

## Extraer el lote académico

Primera terminal PowerShell, desde la raíz. Mantén la API activa:

```powershell
$env:Social__AcademicDirectory = (Resolve-Path .\SistemaAnalisisOpiniones\Data).Path
dotnet run --no-build --project .\Api\SistemaAnalisisOpiniones.Api.csproj -- --urls http://127.0.0.1:5187
```

Segunda terminal, desde la misma raíz. Si los originales están en otra carpeta, ajusta ambas variables de ruta a esa carpeta. Conecta a la instancia que contiene las 200 reseñas verificadas.

```powershell
$r = Invoke-WebRequest 'http://127.0.0.1:5187/api/comentarios'
$c = $r.Content | ConvertFrom-Json
"Comentarios: $(@($c).Count)"
"Clientes ausentes: $(@($c | Where-Object { $null -eq $_.idCliente }).Count)"
$r.Headers['X-Source-Sha256']
$c | Group-Object fuente | Select-Object Name,Count

$env:ConnectionStrings__TransactionDb = 'Server=localhost;Database=AnalisisOpiniones;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;'
$env:Extraction__AcademicDirectory = (Resolve-Path .\SistemaAnalisisOpiniones\Data).Path
$env:Extraction__ApiEndpoint = 'http://127.0.0.1:5187/api/comentarios'
$env:Extraction__StagingPath = Join-Path ([IO.Path]::GetTempPath()) ('staging-originales-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj
if ($LASTEXITCODE -ne 0) { throw 'Falló la extracción académica.' }

$lote = Get-ChildItem -LiteralPath $env:Extraction__StagingPath -Directory |
    Where-Object { $_.Name -notlike '.pending-*' -and (Test-Path (Join-Path $_.FullName 'manifest.json')) } |
    Select-Object -First 1
if (-not $lote) { throw 'No se encontró un lote publicado.' }
$env:Dimensions__BatchPath = $lote.FullName
Get-Content (Join-Path $lote.FullName 'manifest.json') -Raw | ConvertFrom-Json | ConvertTo-Json -Depth 5
```

Espera 200 comentarios, 88 clientes ausentes, 77 Instagram, 68 Facebook y 55 Twitter. El manifiesto debe indicar academic, AnalisisOpiniones, los seis hashes y 500/200/200. El lote total debe ser 900. Para un lote ya publicado, asigna su ruta absoluta a Dimensions__BatchPath. Regenera lotes anteriores sin información de procedencia. Los 703 del informe previo describen aquella prueba con tres comentarios de ejemplo.

## Reconciliar el DW original y cargar las dimensiones

En la segunda terminal, conserva el lote nuevo de 900. Selecciona también un lote histórico auditado de 703 para identificar los registros obsoletos. El proceso exige que sus tres NDJSON coincidan con sus huellas históricas.

```powershell
$env:ConnectionStrings__DataWarehouse = 'Server=localhost;Database=DW_OpinionesClientes;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;'
$env:Dimensions__ProductsPath = Join-Path $env:Extraction__AcademicDirectory 'products.csv'
$env:Dimensions__ClientsPath = Join-Path $env:Extraction__AcademicDirectory 'clients.csv'

$env:Dimensions__LegacyBatchPath = (Resolve-Path -LiteralPath '.\staging\20261119T201838585Z-aee17ccf75ba47aa97af60f170cb759a').Path

# Ejecuta solo después de verificar el respaldo y revisar el SQL.
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes -i .\DataWarehouse\Instalar-Reconciliacion.sql
if ($LASTEXITCODE -ne 0) { throw 'Falló la instalación de equivalencias.' }
dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj -- --preview-dimension-migration
if ($LASTEXITCODE -ne 0) { throw 'Falló la previsualización.' }
```

Si esa carpeta histórica no existe, selecciona otra copia del mismo lote auditado. Compara el resumen con el estado inicial. Si los conteos difieren de los esperados, revisa la causa antes de aplicar. Con el estado auditado se esperan 14 productos y 37 clientes renombrados conservando PK, 6 productos y 2 clientes redundantes retirados y 3 fechas de ejemplo retiradas. El proceso rechaza hechos presentes, dependencias no previstas, claves históricas desconocidas y conflictos.

```powershell
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes -i .\DataWarehouse\Verificar-Dimensiones.sql
dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj -- --rehearse-dimension-migration
if ($LASTEXITCODE -ne 0) { throw 'Falló el ensayo.' }
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes -i .\DataWarehouse\Verificar-Dimensiones.sql
```

El ensayo ejecuta las operaciones previstas dentro de la transacción y termina en ROLLBACK. Compara las cinco huellas SHA‑256 y los conteos de las dos verificaciones para confirmar que también se conservaron claves y atributos. El modo previsualización no ejecuta DML. Después de revisar el resultado y solo cuando decidas confirmar:

```powershell
dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj -- --apply-dimension-migration
if ($LASTEXITCODE -ne 0) { throw 'Falló la reconciliación.' }
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes -i .\DataWarehouse\Verificar-Dimensiones.sql

dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj -- --load-dimensions
if ($LASTEXITCODE -ne 0) { throw 'Falló la primera carga.' }
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes -i .\DataWarehouse\Verificar-Dimensiones.sql

dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj -- --load-dimensions
if ($LASTEXITCODE -ne 0) { throw 'Falló la reejecución.' }
sqlcmd -S localhost -E -C -b -I -f 65001 -d DW_OpinionesClientes -i .\DataWarehouse\Verificar-Dimensiones.sql
```

También puedes ejecutar Verificar-Dimensiones.sql desde SSMS con el DW aislado seleccionado. Los siguientes resultados proceden del análisis de solo lectura de los seis originales y de las equivalencias verificadas de SQL. Son expectativas de ejecución:

| Tabla | Filas finales esperadas | Inserciones esperadas tras reconciliar el estado auditado |
| --- | ---: | ---: |
| DimProducto | 95 | 0 insertadas |
| DimCliente | 65 | 1 insertada |
| DimFecha | 319 | 15 insertadas |
| DimFuente | 3 | 0 insertadas, 3 reutilizadas |
| DimSentimiento | 4 | 0 insertadas, 4 reutilizadas |
| DimEvento | 0 | Sin origen de eventos |
| FactOpiniones | 0 | Fuera del alcance |
| EquivalenciaProducto | 95 | 75 insertadas |
| EquivalenciaCliente | 65 | 26 insertadas |

Se esperan 470 filas dimensionales tras reconciliar, 16 inserciones dimensionales en la carga y 486 filas dimensionales finales. La reconciliación registra 59 equivalencias y la carga añade 101, para 160 en total. En la segunda carga se esperan 0 inserciones y 486 filas dimensionales reutilizadas, más las 160 equivalencias reutilizadas. Verifica cero duplicados y fechas inconsistentes. Los logs separan 88 clientes ausentes de 473 clientes sin catálogo y muestran 407 referencias de producto sin catálogo. Los recuentos de sentimiento esperados son 255 positivos, 240 negativos, 205 neutros y 200 no clasificados.

## Verificar fallos, rollback y regresión

- Extracción académica reproducible: cierra la API manual o usa otro puerto y ejecuta **.\tests\Validar-Extraccion.ps1 -Port 15187** con Extraction__AcademicDirectory y ConnectionStrings__TransactionDb ya definidos. La suite inicia la API académica, compara los campos con social_comments.csv y con staging, verifica las huellas, los reintentos, el HTTP 404 y el dry-run histórico. Sus archivos de prueba van a TEMP.
- Prueba de rollback: usa el ensayo transaccional anterior sobre DW_OpinionesClientes. Comprueba que conteos y claves se conservan al terminar. El entorno sintético queda como material de la práctica anterior.
- Si el Worker rechaza un lote antiguo o un hash, comprueba que los seis archivos provengan del ZIP correcto y genera otro lote. Si rechaza una reseña SQL, coteja los catálogos y el contenido con los originales. La comparación exacta usa cliente, producto, fecha, comentario, clasificación y puntaje. Cualquier ausencia, conflicto o diferencia de ese grupo impide publicar el lote original.
- Si falla la carga, comprueba que el destino sea DW_OpinionesClientes y revisa el código SQL o el mensaje de conflicto. Si una equivalencia es ambigua, conserva la referencia original en staging y revisa los catálogos antes de volver a intentar.

## Decisiones de procedencia

El canal de transporte REST se mantiene como tipo de origen de Red social en DimFuente. Instagram, Facebook y Twitter se conservan como plataforma en staging y en conteos separados de consola. El catálogo fuente_datos.csv conserva los códigos Fxxx, el tipo y la fecha de carga. Sus 100 filas se verifican como insumo y ya existen en SQL transaccional, pero los originales no proporcionan una FK entre esos códigos y cada opinión. No se generan asociaciones por tipo.

Producto y Cliente usan claves canónicas CSV. El nombre y la categoría identifican el producto mediante un cruce único. El correo identifica al cliente mediante otro cruce único y queda en memoria. Las tablas de equivalencia guardan solo las claves CSV y SQL y se confirman con la misma transacción. Si un identificador SQL no tiene correspondencia unívoca, se registra como incidencia y no crea una entidad paralela. El NDJSON de reseñas conserva también IdReviewOriginal. Un comentario sin cliente permanece sin cliente y sus otros atributos siguen utilizándose. Los sentimientos sociales se contabilizan como No clasificado.

Para las capturas, conserva el manifiesto por lote, los conteos por plataforma, las salidas inicial y final de SSMS, los logs de la primera y segunda carga, los conteos de equivalencias, el hecho vacío y el resultado del rollback demo. La versión anterior del DW queda intacta. Los resultados manuales de esta versión quedan por registrar.
