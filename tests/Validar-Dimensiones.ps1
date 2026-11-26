# Ejecutar personalmente después de preparar y configurar la demostración local.
$ErrorActionPreference = 'Stop'
if (-not $env:ConnectionStrings__DataWarehouse -or -not $env:Dimensions__BatchPath) {
    throw 'Configura primero las conexiones y el lote demo.'
}
$settings = [System.Data.Common.DbConnectionStringBuilder]::new()
$settings.ConnectionString = $env:ConnectionStrings__DataWarehouse
$databases = @('Database', 'Initial Catalog' | Where-Object { $settings.ContainsKey($_) } |
    ForEach-Object { $settings[$_] })
$servers = @('Server', 'Data Source' | Where-Object { $settings.ContainsKey($_) } |
    ForEach-Object { $settings[$_] })
if (@($databases).Count -ne 1 -or $databases -ne 'DW_OpinionesClientes_Demo_ITLA' -or
    @($servers).Count -ne 1 -or $servers -ne 'localhost') {
    throw 'La prueba automatizada requiere el DW de demostración en localhost.'
}

$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'Process/SistemaAnalisisOpiniones.Process.csproj'
function Get-DemoSnapshot {
    $sql = @'
SET NOCOUNT ON;
SELECT CONCAT(
 (SELECT COUNT(*) FROM dbo.DimProducto), '|', (SELECT COUNT(*) FROM dbo.DimFuente), '|',
 (SELECT COUNT(*) FROM dbo.DimSentimiento), '|', (SELECT COUNT(*) FROM dbo.DimFecha), '|',
 (SELECT COUNT(*) FROM dbo.DimCliente), '|', (SELECT COUNT(*) FROM dbo.DimEvento), '|',
 (SELECT COUNT(*) FROM dbo.FactOpiniones));
SELECT CONCAT('Producto:', ProductoKey, ':', ProductoOrigenID) FROM dbo.DimProducto ORDER BY ProductoKey;
SELECT CONCAT('Fuente:', FuenteKey, ':', Nombre) FROM dbo.DimFuente ORDER BY FuenteKey;
SELECT CONCAT('Sentimiento:', SentimientoKey, ':', Nombre) FROM dbo.DimSentimiento ORDER BY SentimientoKey;
SELECT CONCAT('Fecha:', FechaKey, ':', Fecha) FROM dbo.DimFecha ORDER BY FechaKey;
SELECT CONCAT('Cliente:', ClienteKey) FROM dbo.DimCliente ORDER BY ClienteKey;
'@
    $previousEncoding = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $snapshot = @(sqlcmd -S localhost -E -C -b -I -f 65001 -h -1 -W -d DW_OpinionesClientes_Demo_ITLA -Q $sql)
        $exitCode = $LASTEXITCODE
    } finally {
        [Console]::OutputEncoding = $previousEncoding
    }
    if ($exitCode) { throw 'No se pudo consultar el DW demo.' }
    $snapshot | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

dotnet run --no-build --project $project -- --load-dimensions
if ($LASTEXITCODE) { throw 'Falló la primera carga demo.' }
$first = @(Get-DemoSnapshot)
if ($first[0] -ne '2|3|4|3|2|0|0') { throw 'Conteos de demo inesperados.' }

$secondLog = @(dotnet run --no-build --project $project -- --load-dimensions)
if ($LASTEXITCODE) { throw 'Falló la reejecución demo.' }
$secondLog
if (@($secondLog | Select-String ': 0 insertados,').Count -ne 5) {
    throw 'La reejecución debe informar cero inserciones para las cinco dimensiones cargables.'
}
$second = @(Get-DemoSnapshot)
if (Compare-Object $first $second) { throw 'Cambió el estado de las dimensiones o el hecho.' }
'OK demo. Conteos esperados, claves conservadas y FactOpiniones vacía.'
