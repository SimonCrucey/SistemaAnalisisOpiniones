# Sistema de Análisis de Opiniones de Clientes

## Carga de dimensiones

La validación académica definitiva procesó **900 opiniones originales** y dejó **486 filas dimensionales y 160 equivalencias** en `DW_OpinionesClientes`. La segunda carga registró cero inserciones. Consulta los [resultados definitivos](Documentation/Resultados-Carga-Dimensional.md).

Para trabajar con los seis CSV del profesor, sigue el [flujo académico de originales](Documentation/Origen-Academico.md). La API local lee `social_comments.csv`, el Worker comprueba la procedencia de las reseñas SQL y el lote contiene 500 encuestas, 200 reseñas y 200 comentarios. La carga corregida utiliza `DW_OpinionesClientes`. Antes de cargar el lote original completo, sigue la previsualización y reconciliación descritas en la guía.

El Worker ofrece el modo `--load-dimensions` para validar un lote publicado y cargar las dimensiones respaldadas por los catálogos. Usa las conexiones `ConnectionStrings__TransactionDb` y `ConnectionStrings__DataWarehouse`, y las rutas `Dimensions__BatchPath`, `Dimensions__ProductsPath` y `Dimensions__ClientsPath`. El modo de extracción sigue siendo el predeterminado.

La [guía de carga dimensional](Documentation/CargaDimensional.md) incluye requisitos, preparación del almacén demo, comandos de extracción y carga, verificación SQL, pruebas y limitaciones. `DimEvento` queda sin carga por ausencia de una fuente verificable. La transformación dimensional termina en las dimensiones y deja `FactOpiniones` fuera de las escrituras.

Práctica de Big Data del ITLA. Un Worker Service .NET 8 extrae encuestas CSV, reseñas de SQL Server y comentarios de una API REST. Cada ejecución guarda tres archivos NDJSON y un manifiesto en staging. La transformación, la carga analítica y el dashboard corresponden a etapas futuras.

## Proyectos

| Proyecto | Responsabilidad |
| --- | --- |
| `Api` | API REST local con tres comentarios académicos de ejemplo. |
| `Data` | Extractores CSV, SQL y HTTP, interfaz común y lector CSV compartido. |
| `Process` | Worker Service, configuración, logs y publicación de lotes en staging. |
| `SistemaAnalisisOpiniones` | Consola histórica que valida, transforma y puede cargar los seis CSV académicos. |

Abre `SistemaAnalisisOpiniones.slnx` para cargar los cuatro proyectos. Consulta los [diagramas de arquitectura y flujo](Documentation/ArquitecturaExtraccion.md) y la [validación histórica de extracción](Documentation/ValidacionExtraccion.md). Para preparar y ejecutar también la **carga dimensional sintética**, sigue la [guía de demostración](Documentation/CargaDimensional.md).

## Requisitos

- Windows y PowerShell 7 para los comandos y la suite.
- SDK de .NET compatible con `.slnx` y `net8.0`, y runtimes .NET y ASP.NET Core 8. Comprueba con `dotnet --list-sdks` y `dotnet --list-runtimes`.
- SQL Server local y **sqlcmd (ODBC)** o SSMS en modo SQLCMD. Los comandos de consola usan `-I` para activar `QUOTED_IDENTIFIER` y `-f 65001` para leer y mostrar UTF-8. Necesitas permiso para crear la base de demostración y una identidad con permiso SELECT sobre `dbo.Opiniones` y `dbo.FuenteDatos` para ejecutar el Worker.
- Para usar el IDE, una versión de Visual Studio que admita `.slnx` y .NET 8.

## Demostración con los archivos del repositorio

Ejecuta los comandos desde la carpeta que contiene `SistemaAnalisisOpiniones.slnx`.

### 1. Compilar y preparar SQL Server

```powershell
dotnet restore .\SistemaAnalisisOpiniones.slnx -m:1
dotnet build .\SistemaAnalisisOpiniones.slnx --no-restore -m:1
sqlcmd -S localhost -E -C -b -I -f 65001 -d master -i .\demo\Preparar-Demo.sql
```

El script crea y puebla solo `AnalisisOpiniones_Demo_ITLA`. En una base de demostración recién creada, devuelve `ResenasWeb = 2`. Puedes repetirlo y verificar que el conteo sigue en 2. En SSMS también puedes abrir el script, activar **Consulta → Modo SQLCMD** y ejecutarlo. Si usas una instancia con nombre, sustituye `localhost` en SQLCMD y en la conexión del Worker por el mismo nombre de instancia. El ejemplo usa autenticación de Windows y confianza en el certificado del servidor local.

### 2. Iniciar la API

```powershell
dotnet run --no-build --project .\Api\SistemaAnalisisOpiniones.Api.csproj
```

Deja abierta esta terminal. La API usa el perfil local en `127.0.0.1:5187`. En otra terminal, desde la misma carpeta, comprueba que responde:

```powershell
Invoke-RestMethod 'http://127.0.0.1:5187/api/comentarios' | Format-Table id, dataset
```

Debes ver `demo-1`, `demo-2` y `demo-3` con el indicador `Ejemplo académico local`.

### 3. Configurar y ejecutar el Worker

En la segunda terminal:

```powershell
$env:ConnectionStrings__TransactionDb = 'Server=localhost;Database=AnalisisOpiniones_Demo_ITLA;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;'
$env:Extraction__SurveyPath = (Resolve-Path .\demo\encuestas-demo.csv).Path
$env:Extraction__StagingPath = Join-Path (Get-Location).Path 'staging'
dotnet run --no-build --project .\Process\SistemaAnalisisOpiniones.Process.csproj
```

Las variables se aplican a esa sesión de PowerShell. El CSV sintético usa los encabezados reales de `SurveyCsvExtractor`. La ruta absoluta evita que el Worker busque el archivo en su directorio de compilación. Mantén las credenciales fuera del repositorio. Para un servidor remoto usa una identidad de solo lectura, cifrado y un certificado confiable. La configuración de rutas, endpoint y tiempos de espera se encuentra en `Process/appsettings.json`. Se puede sobrescribir el endpoint con `Extraction__ApiEndpoint`, que admite HTTPS remoto o HTTP en loopback.

### 4. Revisar el lote publicado

Espera el mensaje `Lote publicado`. En una ejecución de demostración debes obtener **3 encuestas CSV, 2 reseñas SQL y 3 comentarios API**, para un total de **8 registros**. Los **703 registros** del informe anterior corresponden a 500 encuestas originales, 200 reseñas SQL y tres ejemplos REST. El flujo corregido con los originales procesa **900**.

```powershell
$lote = Get-ChildItem -LiteralPath $env:Extraction__StagingPath -Directory |
    Where-Object { $_.Name -notlike '.pending-*' -and (Test-Path (Join-Path $_.FullName 'manifest.json')) } |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $lote) { throw 'No se encontró un lote publicado.' }
$manifiesto = Get-Content (Join-Path $lote.FullName 'manifest.json') -Raw | ConvertFrom-Json
$manifiesto.sources | ForEach-Object {
    $archivo = Join-Path $lote.FullName ($_.source + '.ndjson')
    [pscustomobject]@{
        Fuente = $_.source
        Manifiesto = $_.records
        Lineas = @(Get-Content -LiteralPath $archivo -Encoding UTF8).Count
    }
} | Format-Table -AutoSize
```

Cada línea NDJSON contiene `source` y `record`. El manifiesto incluye la fecha de finalización UTC, el conteo y el tiempo por fuente. Una reejecución crea otro directorio. Un fallo durante la extracción devuelve código 1 y puede dejar archivos parciales bajo `.pending-*`. Usa los directorios publicados con manifiesto.

## Validaciones

Tras compilar en Debug y preparar la base sintética, conserva `ConnectionStrings__TransactionDb` apuntando a `AnalisisOpiniones_Demo_ITLA`. Detén la API manual o usa otro puerto para la suite, que inicia su propia instancia:

```powershell
.\tests\Validar-Extraccion.ps1 -Demo -Port 15187
```

La suite comprueba la extracción, los tres NDJSON, el manifiesto, la reejecución y los casos de fallo. El modo académico original se ejecuta sin `-Demo`, con conexión a la base académica y los seis CSV autorizados en `SistemaAnalisisOpiniones/Data/`. Requiere `Extraction__AcademicDirectory`, espera 500/200/200, compara los comentarios con el CSV original y ejecuta la regresión de la consola.

## Consola histórica

Para comprobar la consola con los seis CSV académicos autorizados, usa el modo de simulación:

```powershell
dotnet run --project .\SistemaAnalisisOpiniones\SistemaAnalisisOpiniones.csproj -- --dry-run
```

La carga histórica se documenta en la [guía anterior](SistemaAnalisisOpiniones/Documentation/GuiaEntregaPractica.md). La demostración vigente ejecuta el Worker y solo escribe archivos de staging.

