# Evidencia de validación de la extracción

## Demostración reproducible con los archivos publicables

El 19 de noviembre de 2026 se restauraron y compilaron los cuatro proyectos desde una copia temporal de los 45 archivos candidatos a GitHub, con cero errores y cero advertencias de compilación. Se ejecutó dos veces `demo/Preparar-Demo.sql` en SQL Server local. Las dos ejecuciones devolvieron 2 reseñas web en `AnalisisOpiniones_Demo_ITLA`.

Se inició la API con el perfil local y se ejecutó el Worker con el CSV sintético y la conexión de demostración descritos en el README. El Worker publicó **8 registros en 612 ms**. Los manifiestos y los archivos NDJSON coincidieron en **3 encuestas, 2 reseñas y 3 comentarios**. La suite `tests/Validar-Extraccion.ps1 -Demo -Port 15187` devolvió código 0 y verificó también la reejecución y los siete casos de fallo. En modo de demostración se omite el dry-run histórico, que requiere los seis CSV académicos.

## Validación automatizada por Codex

Ejecución del 18 de noviembre de 2026 en Windows, con destino .NET 8. Solución restaurada y compilada con SDK .NET 10. Se ejecutó el script automatizado `Tests/Validar-Extraccion.ps1` con PowerShell 7, acceso de lectura a SQL Server y un proceso API en loopback. La suite devolvió código 0.

| Verificación | Resultado observado |
| --- | --- |
| Restauración y compilación | Completadas. 0 errores y tres advertencias NU1900 por indisponibilidad de la consulta de avisos de NuGet. |
| API real por HTTP | 3 comentarios, todos con el indicador «Ejemplo académico local». Los ID de staging coincidieron con los de la respuesta HTTP. |
| CSV | 500 encuestas en 58 ms. Se verificaron procedencia, cantidad y datos de fila. |
| SQL Server | 200 reseñas reales en 250 ms. Todas con código de origen `PLATAFORMA:Web`. |
| API a staging | 3 comentarios en 55 ms. |
| Worker integral | 703 registros en 295 ms con los tres extractores concurrentes. |
| Staging y reejecución | Tres NDJSON, manifiesto y conteos concordantes. Segundo directorio publicado y hash del primer manifiesto intacto. |
| CSV inválido | Encabezados inválidos, comilla sin cerrar y archivo inexistente. Los tres casos registraron error y devolvieron 1 sin publicar lote. |
| SQL inválido | Servidor inaccesible y cadena ausente. Ambos devolvieron 1 y no publicaron lote. |
| HTTP inválido | HTTP 404 documentado por el Worker. Código 1 y lote sin publicar. |
| Fallo de staging | Una ruta ocupada por un archivo provocó código 1 y un error registrado. |
| Regresión de la consola | 500 clientes, 200 productos, 105 fuentes, 900 opiniones, 206 aceptadas, 112 pendientes, 582 rechazadas y 968 incidencias. |

Los tiempos incluyen la escritura de cada fuente. El total también incluye la preparación y publicación del lote. Esta medición local es una observación de una ejecución, no un benchmark bajo carga. La prueba de la consola fue un dry-run. El Worker consultó SQL Server mediante `SELECT` y solo escribió archivos locales. Los archivos temporales de prueba contienen datos de origen y quedan fuera del repositorio.

La suite ejercita el ejecutable y la infraestructura local. Cancelación durante un lote, vencimiento HTTP con respuesta lenta, JSON malformado, pruebas de gran volumen y ejecución mediante el IDE quedan fuera de la evidencia de esta ejecución.

## Validación manual reportada por el estudiante

Reporte recibido el 19 de noviembre de 2026. En su ejecución, el Worker extrajo **500 encuestas CSV, 200 reseñas SQL y 3 comentarios API**. La consola mostró **703 registros en 600 ms**. El estudiante confirmó que el manifiesto coincidió con la consola y que los tres NDJSON tenían 500, 200 y 3 líneas. Comparó los campos visibles de `IdOpinion = 7` en staging y SQL Server y reportó que coincidían. Una segunda ejecución creó otro lote. Con un CSV inexistente observó un error de archivo no encontrado y el lote falló sin publicarse. Después restauró la configuración del CSV.

Los **600 ms** son de la ejecución manual reportada. Los **295 ms** de la tabla anterior corresponden a la ejecución automatizada. Quedan por adjuntar las capturas de la prueba manual.

## Inspección de archivos por Codex el 19 de noviembre

La inspección de solo lectura encontró tres lotes publicados en `staging`. En cada uno, los conteos del manifiesto y de los NDJSON coincidieron en **500, 200 y 3**. También se encontró un directorio `.pending-*` sin manifiesto. La inspección no asignó los lotes a una ejecución concreta ni reprodujo el tiempo, el error de CSV o la comparación de la opinión 7. Se contaron líneas sin copiar su contenido.
