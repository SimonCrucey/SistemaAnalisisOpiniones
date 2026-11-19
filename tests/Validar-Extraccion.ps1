# Ejecutar con PowerShell 7 desde cualquier carpeta, después de compilar Debug.
param([int]$Port = 5187, [switch]$Demo)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $env:ConnectionStrings__TransactionDb) {
    throw 'Configure ConnectionStrings__TransactionDb antes de ejecutar las pruebas.'
}

function Assert-Result($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-Worker([string]$Stage, [string]$Csv, [string]$Endpoint) {
    $script:workerLog = & dotnet $worker "--Extraction:StagingPath=$Stage" "--Extraction:SurveyPath=$Csv" "--Extraction:ApiEndpoint=$Endpoint" 2>&1
    return $LASTEXITCODE
}

function Test-Failure([string]$Name, [string]$Stage, [string]$Csv, [string]$Endpoint, [string]$LogPattern) {
    Assert-Result ((Invoke-Worker $Stage $Csv $Endpoint) -eq 1) "$Name debía devolver 1."
    Assert-Result (($workerLog -join '\n') -match $LogPattern) "$Name debía registrar $LogPattern."
    if (Test-Path $Stage -PathType Container) {
        $published = @(Get-ChildItem $Stage -Directory -Force | Where-Object Name -notlike '.pending-*')
        Assert-Result ($published.Count -eq 0) "$Name publicó un lote incompleto."
    }
    "OK $Name. Código 1, error registrado y sin lote publicado."
}

$root = Split-Path $PSScriptRoot
$worker = Join-Path $root 'Process/bin/Debug/net8.0/SistemaAnalisisOpiniones.Process.dll'
$legacy = Join-Path $root 'SistemaAnalisisOpiniones/bin/Debug/net8.0/SistemaAnalisisOpiniones.dll'
$survey = Join-Path $root 'SistemaAnalisisOpiniones/Data/surveys_part1.csv'
if ($Demo) {
    $survey = Join-Path $root 'demo/encuestas-demo.csv'
}
$stage = Join-Path ([IO.Path]::GetTempPath()) ('opiniones-tests-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $stage
$url = "http://127.0.0.1:$Port"
$endpoint = "$url/api/comentarios"

$api = Start-Process -FilePath dotnet -ArgumentList "Api/bin/Debug/net8.0/SistemaAnalisisOpiniones.Api.dll --urls $url" -WorkingDirectory $root -WindowStyle Hidden -PassThru
try {
    $apiReady = $false
    for ($i = 0; $i -lt 30 -and -not $apiReady; $i++) {
        if ($api.HasExited) { throw 'La API local terminó durante el arranque.' }
        try {
            $comments = Invoke-RestMethod $endpoint -TimeoutSec 2
            $apiReady = @($comments).Count -eq 3
        } catch {
            Start-Sleep -Milliseconds 250
        }
    }
    Assert-Result $apiReady 'No se pudo iniciar la API local.'
    Assert-Result (@($comments | Where-Object dataset -ne 'Ejemplo académico local').Count -eq 0) 'Falta identificar los ejemplos académicos.'
    "OK API HTTP local y contrato de ejemplos académicos."

    $success = Join-Path $stage 'success'
    Assert-Result ((Invoke-Worker $success $survey $endpoint) -eq 0) ($workerLog -join '\n')
    $first = Get-ChildItem $success -Directory | Select-Object -First 1
    $manifest = Get-Content (Join-Path $first.FullName 'manifest.json') -Raw | ConvertFrom-Json
    $expected = @{
        'encuestas-csv' = $(if ($Demo) { 3 } else { 500 })
        'resenas-sql' = $(if ($Demo) { 2 } else { 200 })
        'comentarios-api' = 3
    }
    Assert-Result ($manifest.sources.Count -eq 3) 'El manifiesto debe representar las tres fuentes.'
    foreach ($source in $manifest.sources) {
        $name = $source.source
        $rows = @(Get-Content (Join-Path $first.FullName "$name.ndjson") -Encoding utf8 | ForEach-Object { $_ | ConvertFrom-Json })
        Assert-Result ($rows.Count -eq $expected[$name] -and $source.records -eq $rows.Count) "Cantidad inesperada en $name."
        Assert-Result (@($rows | Where-Object source -ne $name).Count -eq 0) "Origen incorrecto en $name."
        "OK $name. $($rows.Count) registros NDJSON en $($source.elapsedMilliseconds) ms."
        switch ($name) {
            'encuestas-csv' {
                Assert-Result ($rows[0].record.fields.IdOpinion -and $rows[0].record.line -eq 2) 'Se perdió la procedencia del CSV.'
            }
            'resenas-sql' {
                Assert-Result (@($rows | Where-Object { $_.record.codigoFuente -ne 'PLATAFORMA:Web' }).Count -eq 0) 'Se extrajeron registros SQL ajenos a reseñas web.'
            }
            'comentarios-api' {
                Assert-Result (($rows.record.id -join ',') -eq ($comments.id -join ',')) 'El staging API difiere de la respuesta real.'
            }
        }
    }
    $workerLog | Select-String 'Total \d+ registros en (\d+) ms' | ForEach-Object { "OK Worker integral. $($_.Matches[0].Value)." }
    $firstHash = (Get-FileHash (Join-Path $first.FullName 'manifest.json')).Hash
    Assert-Result ((Invoke-Worker $success $survey $endpoint) -eq 0) ($workerLog -join '\n')
    Assert-Result (@(Get-ChildItem $success -Directory).Count -eq 2) 'La reejecución debía generar un segundo lote.'
    Assert-Result ((Get-FileHash (Join-Path $first.FullName 'manifest.json')).Hash -eq $firstHash) 'Se modificó el primer manifiesto.'
    'OK reejecución y conservación del primer lote.'

    $invalid = Join-Path $stage 'invalid.csv'
    Set-Content $invalid "IdOpinion,IdCliente"
    Test-Failure 'CSV con encabezados inválidos' (Join-Path $stage 'bad-headers') $invalid $endpoint 'encabezados'
    Get-Content $survey -TotalCount 1 | Set-Content $invalid
    Add-Content $invalid '"campo sin cerrar'
    Test-Failure 'CSV con comilla sin cerrar' (Join-Path $stage 'bad-quote') $invalid $endpoint 'comilla sin cerrar'
    Test-Failure 'CSV inexistente' (Join-Path $stage 'missing-test') (Join-Path $stage 'missing.csv') $endpoint 'Error en encuestas-csv'
    Test-Failure 'API HTTP 404' (Join-Path $stage 'bad-api') $survey "$url/no-existe" 'HTTP NotFound'

    $savedConnection = $env:ConnectionStrings__TransactionDb
    try {
        $env:ConnectionStrings__TransactionDb = 'Server=tcp:127.0.0.1,1;Database=AnalisisOpiniones;Integrated Security=True;Connect Timeout=1;ConnectRetryCount=0;'
        Test-Failure 'SQL sin conexión' (Join-Path $stage 'bad-sql') $survey $endpoint 'SQL Server error'
        Remove-Item Env:ConnectionStrings__TransactionDb
        Test-Failure 'SQL sin configuración' (Join-Path $stage 'missing-sql') $survey $endpoint 'configure ConnectionStrings__TransactionDb'
    } finally {
        $env:ConnectionStrings__TransactionDb = $savedConnection
    }

    $blocked = Join-Path $stage 'blocked'
    Set-Content $blocked 'archivo donde se esperaba directorio'
    Test-Failure 'Staging sin directorio disponible' $blocked $survey $endpoint 'Falló el lote'

    if ($Demo) {
        'OK modo demostración. La regresión histórica requiere los seis CSV académicos.'
        return
    }

    $legacyOutput = & dotnet $legacy --dry-run
    Assert-Result ($LASTEXITCODE -eq 0) 'Falló el dry-run de la consola histórica.'
    foreach ($value in '500', '200', '105', '900', '206', '112', '582', '968') {
        Assert-Result (($legacyOutput -join '\n') -match ": $value\b") "Falta el contador histórico $value."
    }
    'OK regresión anterior. 500 clientes, 200 productos, 105 fuentes, 900 opiniones, 206 aceptadas, 112 pendientes, 582 rechazadas y 968 incidencias.'
} finally {
    if (-not $api.HasExited) { Stop-Process -Id $api.Id }
}
