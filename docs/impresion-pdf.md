# Pedidos de impresión PDF (24 de octubre de 2026)

## Configuración y recorrido

Esta etapa añade una modalidad explícita al catálogo, sin crear ni cambiar tiendas o productos existentes. En **Menú**, el administrador crea o edita un producto sin control de existencias, con precio **por cara** y costo unitario completo estimado por cara. Activa **Este servicio consume papel**, elige un producto inventariable de la misma tienda, configura ambos lados si procede y activa **Impresión de PDF, blanco y negro, tamaño carta**. La entrega del producto debe admitir recolección y la tienda debe tenerla habilitada. El formulario de edición actual conserva la disponibilidad anterior; no presenta un control «Disponible». Para dar de alta un servicio nuevo, usa la disponibilidad del alta.

El cliente elige **Recoger**, la fecha y ventana cuando correspondan, adjunta un PDF y espera el conteo del servidor. Puede seleccionar todas las páginas o un rango como `1-3,5,8-10`; las páginas se imprimen en orden ascendente sin duplicados. Captura juegos y lados, revisa caras, hojas y precio, y envía el pedido. El archivo original se conserva; la tienda lo descarga desde el pedido administrativo para imprimirlo manualmente. Se imprime en blanco y negro, papel carta, con ajuste proporcional al área imprimible. El botón de descarga envía el archivo como adjunto, sin incrustarlo. El servicio de copias físicas sigue sin requerir PDF y el formulario de mostrador excluye los servicios que lo requieren.

Un archivo es una partida. Dos archivos del mismo servicio permanecen separados. Para cinco páginas, dos juegos consumen diez hojas por un lado o seis por ambos lados, y se cobran diez caras. La selección `1-3,5` tiene cuatro caras por juego. Cada juego y archivo empiezan en hoja nueva. El servidor deriva páginas, caras, hojas y precio: no acepta un conteo independiente del navegador. Agrupa las salidas del mismo papel para verificar la existencia total antes de guardar; el descuento ocurre una vez al crear el pedido. Confirmar o entregar no lo repite; cancelar mediante una transición permitida reintegra las salidas históricas una sola vez. Se conservan precio, costo, insumo, páginas, juegos, lados y hojas históricos. El costo estimado del servicio ya incluye papel; el resumen no vuelve a agregar el costo de la hoja. Las ventas entregadas y los pagos siguen siendo conceptos independientes.

## Identidad y privacidad

La cuenta de cliente anterior usa teléfono, que no prueba posesión del número. Para documentos se añadió una **sesión de documentos por dispositivo**: el registro devuelve un token aleatorio de 256 bits; solo su SHA-256 se guarda en la base, con tienda, cliente y vencimiento de 30 días. El cliente debe conservar ese dispositivo y sesión para consultar y descargar sus PDF; registrar de nuevo el mismo teléfono no otorga acceso a documentos de la sesión anterior. Los pedidos PDF tampoco aparecen en «Mis pedidos» desde otra sesión de ese teléfono. Esta limitación requiere una autenticación de cliente verificable para recuperación entre dispositivos en una etapa futura. Los pedidos web anteriores conservan su consulta existente.

La carga exige sesión válida. Al crear el pedido se vuelven a comprobar sesión, cliente, tienda, archivo temporal, huella SHA-256, páginas, disponibilidad, revisión de configuración, precio y stock. Las descargas comprueban autorización en cada solicitud: cliente dueño de la sesión y documento asociado, o personal Admin/Staff de la tienda del pedido. SuperAdmin no tiene acceso global a PDF. La API pública solo revela modalidad, precio y revisión opaca de configuración; no revela costos, ID del insumo, rutas ni documentos. Nombres de archivo se muestran escapados. Las rutas del disco se generan desde UUID del servidor; el nombre original nunca forma una ruta.

Los pedidos PDF requieren clave de idempotencia persistida en el pedido y huella de la solicitud. Un reintento igual devuelve el pedido original; la misma clave con otro contenido se rechaza. El navegador conserva la solicitud pendiente para reintentar tras perder la respuesta. El archivo se escribe primero en almacenamiento temporal con nombre generado, se valida y se registra. Al crear el pedido se verifica otra vez, se mueve a la carpeta final antes de confirmar la transacción SQL y se intenta compensar el movimiento si falla la escritura; si cae el proceso entre el movimiento y el commit, el reintento puede encontrar el archivo final y la limpieza elimina la carga temporal vencida. Nunca se considera válido un pedido con archivo parcial.

En PostgreSQL se bloquea la fila del documento al asociarla para serializar solicitudes sobre el mismo PDF. La compensación solo revierte un movimiento de archivo realizado por esa solicitud; un reintento que encontró el archivo ya finalizado no lo mueve de regreso.

## Archivo, límites y limpieza

`Documentos:Ruta` configura el directorio privado. En la VM, preparar `/var/lib/quevendecion/documentos`, fuera de `/opt/apps/quevendecion`, `wwwroot` y `/var/www`; dar lectura y escritura **solo** al usuario del servicio `quevendecion`, con permisos de directorio `700` y archivos `600` según la política del servidor. Nginx no debe publicar ni crear alias para esa ruta. En desarrollo el valor predeterminado está bajo la carpeta temporal del sistema; las pruebas usan una ruta temporal propia. Configuración inicial:

| Clave | Valor inicial | Límite de configuración |
| --- | ---: | ---: |
| `Documentos:MaxBytes` | 20 MiB | hasta 100 MiB |
| `Documentos:MaxPaginas` | 200 | hasta 500 |
| `Documentos:MaxTrabajosPorPedido` | 5 | hasta 20 |
| `Documentos:HorasTemporal` | 24 | 1 a 168 |
| `Documentos:DiasRetencion` | 30 | 1 a 365 |

Configurar también `client_max_body_size 22m` en la ubicación Nginx de la API. El endpoint usa límites ASP.NET de 22 MiB para cuerpo y multipart; el margen cubre el sobre multipart de un PDF de 20 MiB. El flujo copia con búfer acotado y verifica extensión, MIME, tamaño, estructura legible, ausencia de cifrado y cada página con [PdfPig 0.1.16](https://www.nuget.org/packages/PdfPig/0.1.16), con [licencia Apache 2.0](https://github.com/UglyToad/PdfPig/blob/master/LICENSE) apta para uso comercial. Los PDF siguen siendo archivos no confiables: no se ejecutan, convierten ni insertan en páginas HTML. Un PDF de gran complejidad dentro de los límites puede consumir tiempo de CPU; vigilar recursos y ajustar límites operativos si se observa abuso.

La limpieza se ejecuta al iniciar y cada hora. Cargas sin pedido vencen tras el plazo configurado. En pedidos Entregados o Cancelados, elimina el archivo después del plazo desde el instante **registrado de entrega o cancelación**; una edición posterior del pedido no desplaza el cierre. Conserva metadatos, instrucciones e historial y muestra «Archivo eliminado por política de retención». No elimina archivos de pedidos activos ni calcula cierres sin evidencia. Repite la limpieza sin rehacer movimientos y solo inspecciona nombres generados dentro de las dos carpetas administradas. Las copias de respaldo deben protegerse y tener una retención coordinada; no se eliminan con la limpieza en línea.

## Esquema y futura publicación

SQLite agrega `Productos.RequierePdf NOT NULL DEFAULT 0` y `RevisionImpresion NULL`, más tablas `DocumentosPdf` y `SesionesDocumento`. Los servicios históricos no exigen PDF y no reciben vínculos retroactivos. PostgreSQL usa la migración aditiva `20261024220710_AgregarImpresionPdf`. La herramienta `tools/QueVendecion.SqliteUpgrade` crea una copia, compara **todas** las columnas originales por fila y valida las columnas nuevas por separado; en la primera pasada las tablas nuevas quedan vacías y `RequierePdf=0`. En pasadas posteriores preserva también sus filas. No modifica el proveedor ni el entorno. La VM descrita en estos documentos sigue en `Development` con SQLite.

Generar API, frontend y ejecutable Linux de la herramienta **del mismo commit**, antes de una publicación autorizada:

```powershell
dotnet publish src/QueVendecion.Api/QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o "$env:TEMP/qvd-pdf-api"
dotnet publish tools/QueVendecion.SqliteUpgrade/QueVendecion.SqliteUpgrade.csproj -c Release -r linux-x64 --self-contained true -o "$env:TEMP/qvd-pdf-sqlite-upgrade"
npm --prefix src/QueVendecion.Web run build
```

Para el ensayo o cambio posterior: detener escrituras y `quevendecion`, cerrar conexiones SQLite, generar un respaldo consistente con la API `.backup` y, con el servicio detenido, respaldar **en el mismo punto** `/var/lib/quevendecion/documentos` con propietarios y permisos. Si se usa WAL, nunca copiar solo `.db`. Ejecutar la herramienta con el respaldo como origen y una **ruta nueva** como destino; repetir sobre esa copia y comprobar conteos, hashes originales, `PRAGMA integrity_check` y `PRAGMA foreign_key_check`. Ensayar API y descargas contra una copia aislada de la base **y** documentos. Mantener `appsettings*`, cadenas privadas, DB activa, WAL, documentos y respaldos fuera de cualquier reemplazo de binarios. Restaurar base y documentos **del mismo respaldo**: restaurar solo uno puede dejar archivos huérfanos o referencias sin archivo. Ver el procedimiento base en [despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md).

## Validación local observada

Las pruebas .NET cubren parser con PDF sintético válido de cinco páginas, contenido inválido, archivo corrupto, cifrado y exceso de tamaño; selección con rangos solapados, duplicados o inexistentes; dos archivos del mismo servicio y consumo agregado; falta de stock sin pedido, movimiento ni archivo asociado; reintento idempotente; costo histórico; cancelación con reintegro único; privacidad por cliente/tienda; retención y limpieza repetida. El ensayo SQLite crea tiendas, productos, cliente, pedidos, costos y movimientos ficticios, incluyendo existencia inicial 11 más ajuste +1, y aplica dos veces la herramienta. La segunda pasada conserva incluso una sesión y metadatos PDF ficticios insertados entre pasadas. Valida integridad y claves foráneas.

Resultado del conjunto: **98 pruebas correctas**, compilación Release sin advertencias y build Vite correcto.

La integración ejecutó API y Vite reales con SQLite y directorio de documentos temporales, cuenta y tienda ficticias, Chrome y un PDF sintético de cinco páginas. En móvil (390 px), la selección `1-3,5`, dos juegos, ambos lados mostró **8 caras, 4 hojas y $24**; el servidor creó el pedido #1 por $24, stock de papel **100 → 96**. «Mis pedidos» mostró archivo, selección e instrucciones. En escritorio (1280 px), administración mostró páginas, juegos, hojas y descarga autorizada; la respuesta fue `application/pdf` con `Content-Disposition: attachment`. Se confirmó el pedido desde esa pantalla y el historial mostró Recibido y Confirmado a las horas de la tienda ficticia (`America/Hermosillo`); la descarga de la sesión cliente respondió 200. No hubo errores JavaScript ni desbordamiento horizontal. La primera ejecución de navegador detectó HTTP 415 por un encabezado JSON sobrescrito en `api.js`; se corrigió y se repitió con HTTP 201. Todo se realizó localmente: no se accedió a la VM ni se usaron bases o documentos reales.
