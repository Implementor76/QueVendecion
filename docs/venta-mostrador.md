# Venta de mostrador: primera versión (24 de octubre de 2026)

## Uso en Pinyex y en cualquier tienda

1. Ingresa como **Admin** de la tienda. En **Menú**, agrega «Copia blanco y negro, tamaño carta» con el precio de venta por cara y el costo unitario estimado. Este costo puede sumar papel, tóner, electricidad y reserva de mantenimiento. Deja el costo vacío si aún no se conoce; cero significa costo cero conocido. Marca el servicio como disponible y deja **Controlar inventario** desactivado. La forma de entrega exigida por el catálogo puede seleccionarse como «Ambas al mismo precio»; la venta presencial no pide ni usa modalidad o ventana de entrega. No se crea ningún producto automáticamente.
2. Desde **Mi tienda → Venta de mostrador**, elige ese servicio, escribe el número entero de **caras copiadas** (una hoja a dos caras equivale a dos), selecciona efectivo o transferencia y, si sirve, agrega una nota.
3. Revisa cantidad, importe y medio de pago. Pulsa **Registrar venta y cobro** solo después de recibir el pago. La pantalla muestra el número y el importe registrado. Si se pierde la respuesta, reintenta la captura pendiente: la misma clave devuelve la venta original. Un error de validación permite volver a editar; los errores inciertos conservan la misma clave.
4. Consulta **Pedidos** para ver el origen «Mostrador», estado Entregado, pago Pagado e historial. En **Inventario y ventas**, elige un rango que incluya la fecha local de la venta para ver cantidad de pedidos entregados, ingresos, costo histórico conocido y utilidad bruta estimada. Las cantidades de caras están en las partidas del historial. El resumen no es un corte de caja ni un reporte de unidades por producto.

Solo Admin ve el acceso y puede usar el endpoint. La tienda sale del token autenticado. Staff y administradores de otra tienda no pueden registrar ventas para Pinyex. Se listan únicamente productos disponibles sin control de existencias; el servidor vuelve a comprobar tienda, disponibilidad, inventario y precio. No hay cliente ficticio, cuenta, domicilio, archivo, fecha de servicio, descuento ni venta a crédito. Efectivo y transferencia son los medios admitidos. Las restricciones de horario web siguen aplicándose a pedidos públicos; no impiden una venta presencial administrativa.

## Persistencia y cálculo

La venta reutiliza `Pedidos` e `ItemsPedido`. `Origen=Web` (0) identifica pedidos existentes e históricos; `Origen=Mostrador` (1) identifica ventas presenciales. Una venta de mostrador se inserta directamente como Entregado y Pagado, con un evento de historial Entregado, usuario responsable e instante UTC. `EntregadoEnUtc` y `PagadoEnUtc` son el mismo instante de confirmación; `CobradoPorUsuarioId` identifica quién registró el cobro. El pedido no tiene cliente ni fechas de servicio. «Mis pedidos» del cliente filtra explícitamente origen Web.

Precio y costo unitario se copian a la partida al vender. El costo `NULL` sigue siendo desconocido y no entra en la utilidad; costo cero sí entra. El resumen existente suma una venta entregada una sola vez por pedido, aunque su pago esté registrado. Usa el instante de entrega y la zona horaria de la tienda para el rango. No se descuentan automáticamente papel, tóner u otros consumibles. El costo es estimación, no valuación contable.

La clave UUID de idempotencia y la huella del contenido se guardan con el pedido dentro de la misma transacción. El índice único `(TiendaId, ClaveIdempotencia)` impide dos registros con la misma clave. Mismo contenido devuelve el pedido previo incluso si después cambió el catálogo; contenido distinto se rechaza. La revisión incluye el precio visto por el administrador: si cambió antes de registrar, el servidor rechaza el intento y exige revisar el nuevo importe. El servidor nunca usa un precio enviado por el cliente como precio de venta.

## Esquema y despliegue futuro

La ampliación agrega a `Pedidos` las columnas `Origen INTEGER NOT NULL DEFAULT 0`, `ClaveIdempotencia`, `HuellaSolicitud`, `PagadoEnUtc`, `CobradoPorUsuarioId` y un índice único por tienda y clave. No modifica filas históricas ni rellena instantes de cobro anteriores. PostgreSQL tiene la migración aditiva `20261024162911_AgregarVentaMostrador`; el puente SQLite `MostradorSchemaUpgrade` es repetible. La selección de proveedor por entorno permanece intacta: la VM descrita actualmente usa Development con SQLite.

Preparación en Windows, desde la raíz del repositorio, hacia directorios nuevos fuera de los archivos versionados:

```powershell
dotnet test QueVendecion.sln --no-restore
dotnet publish src/QueVendecion.Api/QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o "$env:TEMP/qvd-mostrador-api"
dotnet publish tools/QueVendecion.SqliteUpgrade/QueVendecion.SqliteUpgrade.csproj -c Release -r linux-x64 --self-contained true -o "$env:TEMP/qvd-mostrador-upgrade"
npm --prefix src/QueVendecion.Web run build
```

Para una publicación futura autorizada, seguir [despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md): diagnosticar, detener escrituras y `quevendecion`, cerrar conexiones, hacer respaldo consistente con la API de SQLite o `.backup`, ensayar la herramienta sobre una copia y comprobar conteos, precios, costos, pedidos, `PRAGMA integrity_check` y `PRAGMA foreign_key_check`. Conservar el origen, los ajustes de Jax, la configuración y secretos del servidor. No reemplazar la base activa con una copia antigua ni copiar solo `.db` ignorando WAL. El ejecutable Linux de la herramienta se transfiere por separado; recibe origen y **destino inexistente**. Desplegar API y frontend de la misma versión solo en una ventana autorizada. Esta tarea no ejecutó ninguno de esos pasos en la VM.

## Validación local observada

Se usó API real en `127.0.0.1:5136`, Vite real en `127.0.0.1:5173`, Chrome y SQLite nueva bajo `.integration`, con dos tiendas y tres cuentas ficticias. No se abrió una base existente del usuario.

| Comprobación | Esperado | Observado |
| --- | --- | --- |
| 20 caras a $3, costo $0.70 | Venta $60, costo $14, utilidad $46 | Venta #1 Entregado/Pagado; SQLite guardó 20 × $3 y 20 × $0.70; resumen de esa venta $60/$14/$46 |
| Doble envío simultáneo de otra venta de una cara | Un pedido y un cobro | Dos respuestas HTTP 200 con el mismo ID; una sola fila. En conjunto, el resumen de las dos ventas fue $63/$14.70/$48.30 |
| Misma clave con contenido distinto | Rechazo | HTTP 400 `idempotencia_conflicto` |
| Otros productos y cantidades inválidas | Rechazo sin filas | Se rechazaron inventario, producto inactivo, producto de otra tienda y cero caras |
| Permisos y pago | Solo Admin propio, sin crédito | Staff HTTP 403; otra tienda HTTP 400/403; Fiado HTTP 400; cliente sin ventas de mostrador |
| Precio cambiado antes de confirmar | Exigir revisión | Prueba de servicio rechazó precio anterior; la partida anterior retuvo precio y costo históricos |
| Fuera de horario web | Mostrador disponible, web restringida | Mostrador HTTP 200; pedido público HTTP 400 `fuera_de_atencion` |
| Privacidad | Sin costo en datos del cliente | Menú público y DTO de pedido sin costo ni utilidad |
| Escritorio/móvil | Captura usable | Chrome a 1280 y 390 px, sin desbordamiento móvil ni excepciones JavaScript; historial con origen Mostrador |
| SQLite repetible | Conservar filas y claves | Herramienta ejecutada dos veces en copias; 2 tiendas, 3 usuarios, 4 productos, 3 pedidos y 3 partidas; `integrity_check=ok`, `foreign_key_check` vacío |

Además, las pruebas automatizadas verificaron costo nulo frente a cero, resumen, historial y migración aditiva sobre esquema heredado. Ejecutaron 77 pruebas .NET y 3 de frontend; build de Vite correcto. Las bases temporales y capturas bajo `.integration` quedan fuera de Git.

## Corrección del ensayo SQLite posterior (24 de octubre de 2026)

Un ensayo en la VM, con respaldo fresco de la base que ya contiene Pinyex, falló en la herramienta antes de publicar o detener producción. **El error no demuestra que la actualización cambiara datos.** Se reprodujo localmente con la versión anterior del ejecutable: `BusinessValues` seleccionaba únicamente las columnas presentes antes de ampliar el esquema, pero volvía a descubrirlas después. Así, el resultado posterior añadía `Pedidos.Origen` (y, en esquemas más antiguos, también `EntregadoEnUtc`), por lo que la comparación de cadenas fallaba por longitud/posición. La prueba integrada anterior había partido de una base creada con el esquema nuevo; por eso sus dos pasadas no detectaron este defecto de comparación.

La herramienta corregida fija el esquema original al crear la instantánea y compara **todas las columnas originales, fila por fila**, de tiendas, usuarios, clientes, productos, pedidos, partidas y movimientos existentes. Incluye costos, existencias e historial; no presupone números de tiendas, productos ni movimientos. Los valores se comparan mediante huellas SHA-256 para que un diagnóstico de diferencia indique solo tabla, ID y campo. Las columnas incorporadas se validan aparte: `Productos.TieneInventario` y `Pedidos.Origen` deben iniciar en `0`; las demás columnas agregadas deben iniciar en `NULL`. También comprueba que el índice único de idempotencia tenga las columnas esperadas, además de integridad y claves foráneas.

La reproducción local creó dos tiendas ficticias, cinco productos ficticios de Pinyex, cliente, usuario, pedidos, partidas con costos históricos y movimientos. La pluma tenía existencia 12 respaldada por un movimiento inicial de 11 y ajuste de +1. El ejecutable anterior falló con el mismo mensaje y línea del incidente; el corregido completó la primera actualización y una segunda pasada en otra copia. Se conservaron las filas y los saldos 11 → 12; `integrity_check=ok` y `foreign_key_check` vacío. Una prueba adicional modificó deliberadamente `Productos.Stock` en una copia y recibió `Productos Id=2 campo=Stock`, sin imprimir valores personales. **No se probó ni modificó la base real de la VM en esta corrección.**

Para reintentar el ensayo autorizado hay que **regenerar únicamente el paquete ejecutable `sqlite-upgrade` para `linux-x64`** a partir de este commit, con el comando `dotnet publish` de la sección anterior, y transferir todo su contenido. La compilación local del paquete corregido se comprobó en `%TEMP%\qvd-sqlite-upgrade-corrected`. No cambió el código de API ni frontend; sus paquetes preparados no necesitan regenerarse por esta corrección. El destino del ensayo anterior ya existe y la herramienta se niega a sobrescribirlo: usar una ruta nueva para la siguiente copia. Conservar el respaldo original; no usar la copia del ensayo fallido como candidata de publicación. No se ejecutó ningún comando en la VM durante esta investigación.
