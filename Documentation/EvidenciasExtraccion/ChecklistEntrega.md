# Checklist de entrega de arquitectura y extracción

Revisión del 19 de noviembre de 2026. Las rutas de los entregables son relativas a la carpeta que contiene `SistemaAnalisisOpiniones.slnx`.

| Entregable | Ubicación | Estado |
| --- | --- | --- |
| Código fuente y solución .NET 8 | `SistemaAnalisisOpiniones.slnx`, `Api/`, `Data/`, `Process/` y consola `SistemaAnalisisOpiniones/` | Completo y probado. |
| Código en GitHub | `.gitignore` e instrucciones en `README.md` | Pendiente de autorización y publicación. |
| Diagrama de arquitectura y diagrama de flujo ETL | `Documentation/ArquitecturaExtraccion.md` y página 2 del PDF | Revisados. Representan extracción y staging, con transformación, carga y dashboard como etapas futuras. |
| Informe técnico | `Documentation/InformeTecnicoExtraccion.pdf` | Cuatro páginas revisadas visualmente. Diagramas legibles. El tiempo de 295 ms corresponde a la prueba automatizada. |
| Guía y pruebas | `README.md`, `tests/Validar-Extraccion.ps1`, `Documentation/ValidacionExtraccion.md` | Pruebas automatizadas documentadas. El reporte del estudiante y la inspección de staging constan en apartados propios. |
| Capturas de la demostración | `Documentation/EvidenciasExtraccion/` | Pendientes de incorporar. |

Los ZIP llamados `SistemaAnalisisOpiniones-Entrega.zip` corresponden a la práctica anterior. Para la entrega vigente, usa la solución de cuatro proyectos y los documentos de la tabla.

## Capturas manuales

- [ ] **01-visual-studio.png** — Explorador de soluciones con los cuatro proyectos y salida de compilación exitosa.
- [ ] **02-api.png** — URL local `/api/comentarios` con los tres objetos de ejemplo y su indicador académico. Puedes mostrar el HTTP 200 desde PowerShell o las herramientas del navegador.
- [ ] **03-worker-exitoso.png** — Consola con los tres conteos, `Lote publicado` y el tiempo que realmente muestre ese lote. Usa el log anterior si quieres acreditar los 600 ms.
- [ ] **04-manifiesto-y-ndjson.png** — En PowerShell, muestra los nombres de las fuentes, el contador de cada una en el manifiesto y el conteo de líneas de sus NDJSON. Mantén visible la referencia al lote de la captura anterior.
- [ ] **05-sql-y-staging.png** — En SSMS, muestra el conteo SQL de 200 reseñas y el resultado filtrado por `IdOpinion = 7`. En PowerShell muestra esa misma opinión de `resenas-sql.ndjson`. Limita ambos resultados a campos apropiados para compartir, como ID de opinión, código de fuente y puntaje. Puedes usar dos imágenes.
- [ ] **06-reejecucion.png** — En PowerShell, muestra dos directorios distintos con sus manifiestos. Si conservas ambos logs, incluye los dos mensajes de publicación.
- [ ] **07-csv-inexistente.png** — Muestra el error de `encuestas-csv`, `Falló el lote` y, si está visible, el código 1. Si muestras staging, identifica el directorio pendiente o verifica que no apareció un nuevo lote publicado.

Recorta nombres de cuenta, rutas personales, cadenas de conexión y comentarios con información personal. Conserva solo capturas aptas para compartir y revisa su inclusión antes de preparar un commit. Mantén los NDJSON originales en staging.
