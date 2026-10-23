# Tiendas: hallazgos, implementación y aprendizaje

Revisión local: 21 de octubre de 2026. No se publicó ni se modificó la base de producción.

**Actualización de revisión previa a publicación:** las secciones de implementación
inicial se conservan como registro de la primera etapa. La sección «Revisión
posterior» al final describe el comportamiento y las verificaciones vigentes.

## Hallazgos iniciales

- El repositorio tiene proyectos Domain, Application, Infrastructure, Api y Web; el frontend es JavaScript/Vite y el backend ASP.NET Core/EF Core. No se encontró `AGENTS.md` aplicable.
- El modelo anterior tenía tienda, producto e inventario opcional (`TieneInventario`, `Stock`, `MovimientoInventario`) y pedidos con creación, pago y estados, pero no configuración operativa, fecha de servicio, forma de entrega ni ventanas solicitadas. El inventario ya estaba conectado a alta de productos y pedido; se conservó.
- La página pública mostraba catálogo y carrito sin selección de fecha; la administración filtraba pedidos por estado. Los estados existentes son `Recibido`, `Confirmado`, `EnPreparacion`, `Listo`, `Entregado` y `Cancelado`. Los pagos existentes son efectivo, transferencia y fiado; sus estados son pendiente, pagado y parcial.
- El JWT incluye rol y `TiendaId`. Las rutas de `mi-tienda` y pedidos toman ese identificador del token. Se detectó una ruta alternativa de escritura de productos (`/api/tiendas/{tiendaId}/productos`) sin autorización; se exigió autenticación y coincidencia de tienda.
- `Program.cs` selecciona Npgsql en `Production` y SQLite fuera de ese entorno. En `Development` usa `EnsureCreated` y un puente aditivo para inventario; PostgreSQL usa migraciones antes del arranque. La información proporcionada sobre el servidor indica que la instancia utilizada conserva datos en `/opt/apps/quevendecion/QueVendecion.db`. **No se comprobó remotamente** el entorno o proveedor efectivo de ese proceso. La discrepancia debe resolverse antes de publicar, sin cambiar el proveedor como parte de esta etapa.
- El árbol de trabajo tenía cambios previos sin confirmar, incluidos archivos de inventario y migraciones. Se conservaron esos cambios.

## Cambios implementados

- `OperacionTiendaDto` y `OperacionJson` permiten configurar pedidos al momento, programados o ambos; recepción por día y hora; ventanas de servicio por día; anticipación mínima y máxima; corte expresado como días previos y hora local; recolección, domicilio, aviso público y zona horaria. Configuración vacía de recepción significa recepción continua y mantiene compatibles las tiendas existentes. El corte de un día previo a las 18:00 acepta solicitudes hasta las 18:00 exactas del día anterior.
- El administrador cambia la operación desde **Mi tienda** mediante `PUT /api/tiendas/mi-tienda/operacion`, disponible para rol `Admin` y limitado a la tienda del JWT. El formulario distingue recepción de recolección o entrega.
- Cada producto admite `fechasServicio`: lista vacía para catálogo habitual; lista con fechas concretas para productos programados. El menú público filtra por fecha y modalidad. El backend comprueba de nuevo todos los productos al confirmar el carrito.
- Un pedido nuevo guarda modalidad, fecha de servicio, ventana solicitada, forma de entrega, dirección si es a domicilio y horario confirmado o estimado por la tienda. Los campos son nulos para pedidos históricos; no se les asigna una fecha ficticia. El precio unitario histórico sigue guardado en cada línea.
- La validación usa `TimeZoneInfo` de la tienda, no la zona del servidor. Comprueba recepción, corte, anticipación, ventana, modalidad, forma de entrega, dirección y compatibilidad de todos los productos. Si ya pasó el corte, `/api/public/tienda/{slug}/opciones` ofrece otras fechas y ventanas válidas; el cliente escoge explícitamente. El pedido al momento no exige una hora exacta.
- La página pública muestra condiciones antes del catálogo, selecciona fecha cuando se necesita, recoge la dirección a domicilio y resume el horario solicitado. **Mis pedidos** distingue horario solicitado, horario confirmado y estado `Listo`; el pedido recibido indica que espera confirmación. Se sustituyó el ejemplo “Sin cebolla” por instrucciones generales. No se agregó ninguna promesa de notificaciones.
- En la primera implementación, la administración exigía transiciones consecutivas. La revisión posterior corrigió esa exigencia para permitir entregar directamente tras confirmar. Se conserva aquí el antecedente de la decisión.

## Archivos principales

- Modelo y contratos: `src/QueVendecion.Domain/Entities/{Tienda,Producto,Pedido}.cs`, `src/QueVendecion.Domain/Enums/ModalidadPedido.cs`, `src/QueVendecion.Application/DTOs/{OperacionDto,TiendaDto,ProductoDto,PedidoDto}.cs`.
- Reglas y persistencia: `src/QueVendecion.Infrastructure/Services/{OperacionReglas,TiendaService,ProductoService,PedidoService}.cs` y configuraciones EF.
- API: `src/QueVendecion.Api/Controllers/{Public,Tiendas,Pedidos,Productos}Controller.cs`.
- Interfaz: `src/QueVendecion.Web/src/pages/{tienda,menu,tiendaCliente,pedidos}.js` y `src/QueVendecion.Web/src/utils/api.js`.
- Esquema: `src/QueVendecion.Infrastructure/Data/OperacionSchemaUpgrade.cs`, migraciones PostgreSQL `20261022054127_AgregarOperacionTienda`, `20261022055059_AgregarDireccionEntrega` y `20261022060414_AsignarEntregaYRegistrarEstados`, y `deploy/postgresql/operacion.sql`.

## Ejemplos de configuración

Los ejemplos son valores para `operacion` de una tienda, con fechas de producto configuradas aparte. El número de `dia` sigue `DayOfWeek`: domingo 0, lunes 1, … sábado 6. Ajustar días y ventanas reales de cada negocio.

| Caso | Modalidades, recepción y entrega | Producto |
|---|---|---|
| Comida sobre pedido | `pedidosAlMomento=false`, `pedidosProgramados=true`, `diasPreviosCorte=1`, `horaCorte="18:00"`, recepción en los días deseados y `ventanasServicio` como jueves 13:00–15:00; `recoleccion=true`. | Platillos con `fechasServicio=["2026-10-22"]` y demás fechas anunciadas. |
| Antojitos | `pedidosAlMomento=true`, `pedidosProgramados=false`, recepción 12:00–22:00 en días abiertos; recolección o domicilio según opere. | Productos habituales, sin fechas; el administrador registra el tiempo estimado después de recibir el pedido. |
| Jax | Ambas modalidades y ambas formas de entrega; recepción y ventanas programadas según sus horarios. | Hielo y catálogo sin fechas, con inventario si se desea usar la función existente. |
| Pinyex | `pedidosAlMomento=false`, `pedidosProgramados=true`, recepción por ejemplo 06:00–19:00 y recolección 20:00–22:00; aviso: “Recolección a partir de las 20:00. Haz tu pedido durante el día y espera la confirmación de que está listo antes de pasar”. | Papelería habitual; las impresiones pueden anunciarse como producto, pero la carga de archivos queda pendiente. |

En todos los casos se configura `zonaHoraria`, por ejemplo `America/Hermosillo`, y una `anticipacionMaximaDias` apropiada. Si se habilitan pedidos programados, hay que configurar al menos una ventana de servicio.

## Esquema y pasos para una publicación posterior

1. **Antes de publicar**, verificar en el servicio real el entorno, proveedor y cadena de conexión efectiva; contrastarlos con la base SQLite indicada. Este trabajo no cambió esa selección. Tomar respaldo verificable de la base activa y ensayar la actualización en una copia.
2. Para SQLite bajo `Development`, el arranque aplica aditivamente inventario y operación. También existe `dotnet run --project src/QueVendecion.Api -- --upgrade-operation` con conexión SQLite de desarrollo configurada; el comando agrega columnas e índice faltantes sin borrar filas. No usarlo contra producción sin confirmar primero proveedor y entorno. Los pedidos anteriores quedan con columnas nuevas en `NULL`.
3. Para PostgreSQL confirmado con las migraciones previas de inventario ya aplicadas, revisar `deploy/postgresql/operacion.sql` y aplicarlo en una ventana de mantenimiento antes de arrancar la API nueva. Contiene las tres migraciones de esta etapa y registro en `__EFMigrationsHistory`. El arranque detecta migraciones pendientes y se detiene; no migra por sorpresa.
4. Publicar API y frontend juntos después de la actualización de esquema; probar una tienda piloto con ambas formas de entrega, un pedido programado, uno al momento y consulta de pedidos históricos. No se realizó ninguno de estos pasos en producción.

**Límite para el servidor descrito:** el código actual sigue escogiendo PostgreSQL en `Production`, mientras que la instancia descrita usa SQLite. Sin comprobar la configuración real y definir un camino de despliegue compatible con esa base, no hay un comando de publicación seguro para esa instancia. No se alteró el proveedor ni la conexión en esta tarea.

## Validación realizada

- `dotnet restore QueVendecion.sln`: correcto.
- `dotnet build QueVendecion.sln --no-restore`: correcto.
- `dotnet test QueVendecion.sln --no-restore`: **63 pruebas aprobadas, 0 fallidas**. Cubren corte exacto y fuera de corte, anticipación, fechas de producto, incompatibilidad, recepción inmediata dentro y fuera de horario, recolección/domicilio, dirección, recolección nocturna de Pinyex, historial sin fecha, aislamiento de pedidos entre tiendas y actualización SQLite repetible.
- `npm run build` en `src/QueVendecion.Web`: correcto (Vite, 16 módulos).
- La primera etapa realizó una prueba HTTP local con SQLite temporal. La revisión posterior amplió la verificación con pruebas visuales en navegador y ensayo de actualización sobre copias; ver resultados vigentes abajo.

## Aprendizaje y pendientes posteriores

- Las ventanas de recepción y de servicio necesitan reglas separadas; permitir pedidos durante el día no debe adelantar la hora de recolección. La fecha disponible de un producto se evalúa contra la fecha de servicio elegida.
- El inventario ya existe y funciona en el proyecto; esta etapa no agregó cupos de producción, costos ni utilidad. Más adelante se necesitan reglas de reserva/cupo por fecha y reportes de costo y margen, integrados con el inventario sin reemplazarlo.
- Para impresiones faltan carga y almacenamiento de archivos, validación de tamaño y tipo, acceso privado del administrador, política de conservación y relación del archivo con el pedido. No se implementaron esos módulos en esta etapa.

## Revisión posterior antes de publicación (21 de octubre de 2026)

### Documentación contrastada

| Documento revisado | Decisión o dato | Resultado frente al código y corrección |
|---|---|---|
| `Constitucion_Proyecto_QueVendecion.md`, `Documento_Fundacional_QueVendecion_MVP.md` | PostgreSQL para producción; SPA y API separadas; lenguaje y UX de negocio. | Arquitectura objetivo conservada. Se añadió nota: se informó un archivo SQLite y falta confirmar el proveedor efectivo del servicio. |
| `agent.md` | Estados, rutas, productos y política de despliegue. | Tenía `Preparando` en lugar de `EnPreparacion`, rutas y campos incompletos, y afirmaba PostgreSQL efectivo. Se actualizó al modelo y flujo actuales. |
| `guia-experiencias.md` | Preservar datos y probar backend/frontend. | Su checklist pedía eliminar la base vieja y decía que no había pruebas. Se corrigieron ambas indicaciones y se añadió la ruta de ensayo SQLite. |
| `actualizacion-inventario.md`, `Prompt_Codex_Corregir_Inventario_Alta_Producto.md` | Descuento transaccional, no sobreventa, reintegro único, esquema aditivo. | La implementación conserva transacción serializable y movimientos; `OperacionSchemaUpgrade` es un puente separado y documentado. El prompt se conserva como requerimiento histórico. |
| `migraciones-produccion.md`, `guia-deploy-ubuntu.md` | Migraciones PostgreSQL y preservación. | La guía solo aplica si se confirma PostgreSQL; se documentaron las tres migraciones de tiendas y el camino SQLite por copia. Se retiró una contraseña literal de ejemplo. |
| `auditoria.md` | Hallazgos históricos de seguridad y arquitectura. | Se marcó como auditoría de julio, no como estado actual. Esta revisión funcional no cierra sus hallazgos de seguridad. |
| `tiendas.md`, versión anterior de `tiendas-hallazgos.md` | Reglas de pedidos, inventario, operación y publicación. | La etapa inicial imponía preparación y listo antes de entregar; se corrigió. El reporte inicial queda como antecedente y estos resultados son los vigentes. |

### Estados e inventario

- Un pedido nuevo registra `Recibido`. El administrador puede confirmar y pasar de `Confirmado` a `Entregado` directamente si el producto está disponible. También puede usar `EnPreparacion` y `Listo` cuando aportan valor. Se puede cancelar cualquier estado activo; `Entregado` y `Cancelado` quedan cerrados.
- `HistorialEstadosJson` registra fecha UTC, estado anterior, nuevo estado y usuario cuando proviene del administrador. Las filas históricas conservan `NULL`, sin inventar transiciones pasadas. La interfaz administrativa muestra el historial.
- El descuento se realiza al crear el pedido dentro de la transacción existente. Entregar no vuelve a descontar. Cancelar reintegra una sola vez según movimientos registrados; repetir un estado no duplica efectos y no se permite cancelar tras la entrega.

### Productos existentes de Jax

- Se añadió `EntregaPermitida` por producto (`Recoleccion`, `Domicilio`, `Ambas`). Las filas existentes quedan `NULL`; el servidor rechaza nuevos pedidos con un producto sin asignar o incompatible y el catálogo público filtra según la forma elegida. El administrador ve qué productos requieren asignación.
- La transición debe identificar por ID los dos productos actuales de Jax, comprobar con el negocio su correspondencia y guardar **Bolsa de hielo para recoger, $25 → Recoleccion** y **Bolsa de hielo a domicilio, $30 → Domicilio**. No se modificó ningún producto ni precio existente. No inferir la forma de entrega a partir del nombre o precio en una actualización automática.
- La prueba de negocio creó ambos precios, confirmó combinaciones válidas y rechazó los cruces y productos heredados sin asignar. La línea del pedido conserva el precio unitario histórico.

### Pruebas visuales y funcionales locales

- Chrome en navegador real, pantalla móvil de 390 × 844 y escritorio; entorno local con SQLite de prueba y tienda ficticia. Se recorrieron cliente (selección de entrega antes del catálogo, fecha, ventana, dirección, error por dirección vacía, pedido e historial) y administrador (login, configuración, menú, confirmación, entrega directa e historial). Diez capturas de comprobación; cero excepciones de JavaScript.
- En móvil, al elegir domicilio solo apareció el hielo de $30; al elegir recolección aparecieron el hielo de $25 y un producto programado, con su fecha y ventana. La dirección vacía mostró «Captura la dirección de entrega». Se ajustó el texto antes de elegir forma de entrega, el alto del panel de pedido y el ajuste de acciones de estado en móvil.
- El recorrido visual usa datos ficticios y comprueba lo visible. No sustituye una prueba de aceptación con los datos y horarios reales de Jax.

### Esquema y ensayo de despliegue

- `Production` selecciona Npgsql; otros entornos seleccionan SQLite. Esta configuración **no se cambió**. Faltan del servicio real: entorno efectivo, proveedor/conexión efectiva sin divulgar secretos, ruta de base activa, esquema, historial de migraciones, conteos, integridad, permisos y estrategia de respaldo. Comandos de solo lectura y secuencia propuesta: [despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md).
- La herramienta `tools/QueVendecion.SqliteUpgrade` abre el origen en modo solo lectura, crea un destino nuevo mediante respaldo SQLite, aplica allí los cambios de inventario y operación, y comprueba conteos, integridad y relaciones. Rechaza sobrescribir el destino. No se ejecutó sobre el servidor.
- Ensayo local: 2 tiendas, 2 usuarios, 1 cliente, 3 productos, 3 pedidos y 3 líneas conservados. En una copia que simuló el esquema anterior, `EntregaPermitida` no existía en el origen y sí en el destino; precios 25 y 30 intactos, 3 pedidos históricos sin fecha inventada e integridad `ok`.
- Las migraciones PostgreSQL y `deploy/postgresql/operacion.sql` están preparados solo para un destino PostgreSQL confirmado. La publicación requiere una decisión posterior tras obtener el diagnóstico y ensayar con una copia de la base real.
- La revisión del arranque halló un seed de SuperAdmin con credenciales literales. Se sustituyó por `SeedSuperAdmin__Email` y `SeedSuperAdmin__Password` privados, solo cuando no existe la cuenta; las cuentas ya creadas no cambian. Un servicio nuevo sin esas variables no crea una cuenta predeterminada.

### Verificación final y pendientes

- `dotnet test QueVendecion.sln --no-restore`: 65 pruebas aprobadas, 0 fallidas.
- `npm run build` en `src/QueVendecion.Web`: correcto.
- Pendiente antes de publicar: diagnóstico del proceso y base efectivos; ensayo con copia real; asignación explícita de entrega a cada producto existente (especialmente los dos de Jax); aceptación de horarios, domicilio e historial con datos reales. No se publicó ni se modificó producción.

## Seguimiento tras publicación manual (22 de octubre de 2026)

La sección anterior registra los pendientes **antes** de la publicación. El usuario confirmó después que la versión de operación por tienda ya está en `srvubu01`: API `/opt/apps/quevendecion` puerto 5010, frontend `/var/www/quevendecion`, entorno efectivo `Development` y base activa SQLite `/opt/apps/quevendecion/QueVendecion.db`. Jax es `TiendaId=3`; `ProductoId=5` es domicilio/$30/`EntregaPermitida=1`, y `ProductoId=6` es recolección/$25/`EntregaPermitida=0`. Ambas formas están habilitadas y la zona es `America/Hermosillo`.

El pedido #7 de Jax se creó para recoger por $25, se confirmó y canceló, y terminó visualmente `Cancelado`. Los pedidos #5 y #6 permanecen visibles. **No se inspeccionó el historial de transiciones desplegado**. Tampoco se consultó directamente la VM en esta revisión.

### Incidente de hora

El pedido #7 se creó aproximadamente a las 22:11 de Hermosillo y la pantalla mostró 05:11 a. m. El código crea `CreadoEn`, `ActualizadoEn` e historial con instantes UTC. SQLite conserva el valor pero EF Core lo relee como `DateTimeKind.Unspecified`; al serializar el DTO, JSON omite `Z`. JavaScript interpreta esa cadena sin zona como hora local. Además, administración y **Mis pedidos** usaban la zona del navegador, no la zona configurada de la tienda. La diferencia de siete horas y el cambio de día encajan con ese recorrido; el instante exacto de #7 en la base desplegada queda por verificar.

`PedidoService` ahora marca como UTC esos valores al construir el DTO, incluidos los cambios de estado, sin alterar filas de la base. Las pantallas formatean los instantes con `Intl.DateTimeFormat` y la `zonaHoraria` de la tienda. `FechaServicio` sigue siendo una fecha civil `DateOnly`; `VentanaSolicitada` y `HorarioConfirmado` son horarios/textos locales y no se convierten como instantes. No se restaron horas manualmente ni se reescribieron fechas históricas.

Se añadieron pruebas de ida y vuelta por SQLite y serialización con `Z`, así como pruebas de presentación para `2026-10-22T05:11:00Z` → `21/10/2026 22:11` en Hermosillo y cruce de medianoche. La comprobación visual y del historial de #7 en la versión desplegada queda pendiente para una publicación posterior.

Validación local de esta corrección: `dotnet test QueVendecion.sln --no-restore` con 66 pruebas aprobadas; `npm test` con 2 pruebas aprobadas; `npm run build` correcto. El cambio de API y frontend debe publicarse junto en otra ventana autorizada.

### Incidentes de transferencia

La copia del frontend desde Windows dejó directorios inaccesibles para Nginx y produjo HTTP 500; se corrigieron directorios a `755` y archivos a `644`. Una copia de base como archivo omitió ajustes recientes de Jax, que se corrigieron directamente con respaldo previo y transacción. El procedimiento revisado de SQLite, WAL, cierre de conexiones y conservación de configuración está en [despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md).
