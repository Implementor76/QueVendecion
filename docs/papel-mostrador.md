# Venta de inventario en mostrador y papel para copias (24 de octubre de 2026)

**Etapa posterior:** [impresión PDF con archivo adjunto](impresion-pdf.md). La descripción de pedidos web sin adjuntos que sigue corresponde al alcance de esta etapa de papel.

## Configuración y uso

La función sirve a cualquier tienda. No reconoce productos por nombre o ID y no crea ni cambia registros existentes. Para Pinyex, el administrador puede dar de alta una hoja carta como producto con inventario, precio de venta de $1 y costo de referencia de $0.198 por hoja (paquete de 500 a $99). Las existencias iniciales se capturan al crear el producto; compras y ajustes posteriores pasan por **Inventario y ventas**. Para copias, crea o edita un producto **sin control de inventario**, con precio de $3 **por cara** y el costo completo estimado por cara. En **Menú → Editar producto**, activa **Este servicio consume papel**, elige la hoja inventariable de la misma tienda y decide si admite ambos lados. No hay un control «Disponible» visible en ese formulario: al editar se conserva el valor de disponibilidad que ya tenía el producto.

En **Mi tienda → Venta de mostrador**, selecciona el producto. Para hojas sueltas u otro producto inventariable captura **Cantidad de unidades**. Para un servicio con papel captura **caras por juego**, **juegos** y **un lado / ambos lados**. Revisa las caras cobradas, hojas a descontar, precio unitario y total; selecciona efectivo o transferencia y pulsa **Registrar venta y cobro** una vez recibido el pago. La venta queda Entregada y Pagada. El historial administrativo indica origen Mostrador y los movimientos de la hoja muestran el pedido, saldos y usuario. El resumen de **Inventario y ventas** la cuenta una vez como ingreso pagado y calcula utilidad solo para líneas con costo conocido.

La tienda puede seguir vendiendo servicios sin papel vinculado con la captura anterior de una cantidad simple. El catálogo web también admite servicios con papel, sin archivos adjuntos: el cliente captura caras, juegos y lados antes de agregar el servicio. Se mantienen horarios, modalidades, entrega y restricciones web. El servidor reserva las hojas al crear el pedido y las reintegra una sola vez si se cancela mediante una transición permitida. No se pueden cancelar ventas de mostrador ya entregadas. La venta de prueba #8 corregida manualmente en producción no se reabre ni reprocesa.

## Cálculo y trazabilidad

Las cantidades son enteras positivas y se limitan a 100 000 caras por pedido. Cada juego comienza en hoja nueva:

| Captura | Caras cobradas | Hojas descontadas |
| --- | ---: | ---: |
| 10 caras, 1 juego, un lado | 10 | 10 |
| 10 caras, 1 juego, ambos lados | 10 | 5 |
| 3 caras, 2 juegos, ambos lados | 6 | 4 |

El precio de venta de la hoja **no** se agrega al total del servicio. El costo de referencia del servicio ya incluye papel, tóner, electricidad y mantenimiento, si así lo estimó la tienda. La salida del insumo modifica stock, pero no suma otra partida de costo en el resumen. La diferencia por uso dúplex no recalcula automáticamente ese costo. El costo nulo es desconocido; cero es conocido. Precio y costo por partida quedan congelados al registrar, aunque luego se edite el catálogo. Las partidas nuevas guardan caras por juego, juegos, lados, insumo y hojas históricas. Las operaciones anteriores no reciben consumos inferidos ni movimientos retroactivos. Los movimientos registran cantidad, saldo anterior y nuevo, pedido, usuario administrativo cuando corresponde, motivo y fecha UTC; la pantalla usa la zona horaria de la tienda.

El servidor comprueba tienda, disponibilidad, existencia, cantidades, precio vigente y opciones de papel dentro de la transacción. La salida de un producto vendido y el papel para un servicio se agrupan por stock para impedir sobreventa, pero conservan movimientos separados y motivos claros. Confirmar o entregar no vuelve a descontar. Al cancelar, las salidas originales del pedido determinan el reintegro aunque el vínculo del servicio cambie. La idempotencia de mostrador incluye producto, cantidad, caras, juegos, lados, insumo y opción dúplex esperados, precio, pago y nota; el mismo contenido devuelve la venta previa sin descontar otra vez. Las huellas de ventas anteriores siguen reconociéndose en reintentos sin papel. Un cambio de precio o de configuración del servicio antes de confirmar exige revisar de nuevo.

La API pública solo indica si el servicio consume papel y admite ambos lados; no entrega el ID del insumo, costos ni utilidad. La configuración completa se consulta por rutas administrativas con autorización por tienda. No se permiten autorreferencias, cadenas de servicios ni insumos de otra tienda. Para borrar un insumo referenciado, desvincúlalo primero de todos los servicios; los productos con movimientos o pedidos históricos siguen protegidos por las reglas de inventario.

## Esquema y publicación futura

SQLite agrega a `Productos` `PapelInsumoId` nullable y `AdmiteDobleCara NOT NULL DEFAULT 0`; a `ItemsPedido`, `CarasPorJuego`, `Juegos`, `DobleCara`, `PapelInsumoIdHistorico` y `HojasConsumidas`, todas nullable. Agrega índice del insumo y disparadores de integridad de referencias. Las filas históricas quedan con vínculo y captura nulos, y dúplex desactivado. PostgreSQL aplica la migración aditiva `20261024194902_AgregarPapelEnServicios`. No se cambia el proveedor ni el entorno: la VM descrita en la documentación opera con Development y SQLite.

Para un despliegue **posteriormente autorizado**, detener escrituras y el servicio, cerrar conexiones y obtener una copia consistente mediante la API de respaldo SQLite; nunca copiar solo `.db` de una base con WAL activo. Ejecutar `tools/QueVendecion.SqliteUpgrade` contra el respaldo hacia un destino nuevo, repetir el ensayo en otra copia si se necesita, comprobar conteos, costos, precios, existencias, pedidos, partidas, movimientos, `PRAGMA integrity_check` y `PRAGMA foreign_key_check`. La herramienta fija las columnas originales antes de ampliar el esquema y compara todas esas columnas fila por fila; valida por separado los nuevos valores iniciales (`AdmiteDobleCara=0`, los demás `NULL`). Preservar configuración, base activa, WAL y respaldos fuera del reemplazo de binarios. Véase [despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md). Se deben generar **API, frontend y herramienta SQLite** de este mismo commit:

```powershell
dotnet publish src/QueVendecion.Api/QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o "$env:TEMP/qvd-papel-api"
dotnet publish tools/QueVendecion.SqliteUpgrade/QueVendecion.SqliteUpgrade.csproj -c Release -r linux-x64 --self-contained true -o "$env:TEMP/qvd-papel-sqlite-upgrade"
npm --prefix src/QueVendecion.Web run build
```

No se accedió a la VM ni a sus bases durante esta etapa.

## Validación local observada

La herramienta SQLite se probó desde esquemas anteriores con tiendas y productos ficticios representativos de Pinyex, pedidos, costos, existencias y movimiento inicial de 11 más ajuste +1. Dos ejecuciones conservaron los registros, pasaron integridad y claves foráneas. Las pruebas .NET cubren venta de 10 hojas, los tres cálculos de la tabla, falta de stock sin filas parciales, reintento idempotente, concurrencia, cancelación web tras cambiar el vínculo, costo histórico, utilidad sin duplicar papel, aislamiento y compatibilidad de huellas anteriores.

La integración usó API y Vite reales en `127.0.0.1:5136` y `127.0.0.1:5173`, Chrome y SQLite nueva bajo `.integration`, con tienda, cuenta y cliente ficticios. En navegador, una venta de 10 hojas llevó el stock de 100 a 90; una venta de 3 caras por juego, 2 juegos, ambos lados, cobró $18 y descontó 4 hojas (90 → 86). Un pedido web posterior con la misma captura descontó otras 4 (86 → 82). Las pantallas a 1280 y 390 px mostraron la revisión y vista previa correctas, sin desbordamiento móvil ni excepciones JavaScript. Las capturas y la base de prueba están excluidas de Git.

La función no incluye desglose de insumos, merma automática, devolución de ventas cobradas, adjuntos, impresión remota, impuestos ni contabilidad. Las hojas dañadas se registran mediante el ajuste existente con motivo.
