# Resultados de la carga dimensional

La validación manual se realizó con los seis CSV originales, la base transaccional `AnalisisOpiniones` y el almacén `DW_OpinionesClientes`. Estos resultados corresponden al flujo académico definitivo.

| Etapa | Resultado observado |
| --- | --- |
| Extracción | **900 opiniones originales en 622 ms**. 500 encuestas CSV, 200 reseñas de SQL Server y 200 comentarios originales servidos por API REST. |
| Ensayo transaccional | La reconciliación se ensayó con rollback. Las comprobaciones posteriores confirmaron que el estado se conservó. |
| Reconciliación definitiva | **438 ms**. El almacén quedó con 470 filas dimensionales antes de la carga. |
| Carga dimensional | **461 ms**, con 16 filas dimensionales nuevas y 470 reutilizadas. |
| Reejecución del mismo lote | **450 ms**, con **0 inserciones**. Se reutilizaron 486 filas dimensionales y 160 equivalencias. |
| Integridad | Cero claves de negocio duplicadas, cero fechas inconsistentes y cero productos sin clave o nombre. Las cinco huellas SHA-256 de productos, clientes, fechas y las dos tablas de equivalencias coincidieron después de la reejecución. |

| Tabla | Filas finales |
| --- | ---: |
| `DimProducto` | 95 |
| `DimCliente` | 65 |
| `DimFecha` | 319 |
| `DimFuente` | 3 |
| `DimSentimiento` | 4 |
| `DimEvento` | 0 |
| **Total dimensional** | **486** |
| `EquivalenciaProducto` | 95 |
| `EquivalenciaCliente` | 65 |
| **Total de equivalencias** | **160** |
| `FactOpiniones` | 0 |

`DimEvento` quedó vacía porque los datos originales carecen de eventos. La carga de `FactOpiniones` corresponde a otra etapa. Los tiempos son mediciones de esa validación manual en el entorno académico.

Los **703 registros** documentados en la práctica anterior corresponden a un lote histórico con tres comentarios REST de ejemplo. La extracción definitiva usa las 200 filas de `social_comments.csv`. La [guía académica](Origen-Academico.md) documenta el procedimiento y la [guía sintética](CargaDimensional.md) permite reproducir el flujo sin los archivos del profesor.
