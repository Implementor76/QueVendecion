# Alta de productos e inventario

**Actualización posterior:** la etapa de costos e inventario por tienda cambia
la regla de desactivación descrita abajo. Una vez activado, el inventario no se
desactiva; las existencias cambian por compras y ajustes, con movimiento.
Consultar [costos-inventario.md](costos-inventario.md). Se conserva abajo el
contexto de la primera implementación.

## Corrección

Alta y edición usan el mismo comportamiento: al activar inventario, el stock
es obligatorio y debe ser entero no negativo; al desactivar se limpia y se
envía `null`. La edición carga el inventario del producto seleccionado.
El servicio valida stock nulo/negativo incluso si la solicitud no viene de la UI.

El POST devuelve `201 Created` con `Location` al GET individual. Ese GET solo
devuelve productos de la tienda del usuario autenticado.

Pedido, descuento y movimientos se guardan dentro de una transacción
`Serializable`. Las líneas repetidas se consolidan antes de validar stock.
SQLite serializa las escrituras; PostgreSQL puede abortar una transacción
concurrente. El API devuelve `409` en conflictos de serialización/bloqueo para
que el usuario actualice y reintente. No se reintenta automáticamente un pedido.
La cancelación también es transaccional y reintegra las salidas registradas una
sola vez. Un pedido cancelado no se puede reabrir: se crea uno nuevo, con una
nueva validación y descuento. Esta restricción evita vender nuevamente unidades
que ya se reintegraron.

## Esquema y despliegue

PostgreSQL utiliza EF Core Migrations: una inicial del esquema anterior y una
incremental que agrega TieneInventario con default false, Stock nullable y
MovimientosInventario, sin reconstruir Productos ni modificar sus datos previos.
La base existente necesita un baseline verificado si no tiene historial de EF.
El arranque de produccion verifica las migraciones, pero no las aplica.

Ver [migraciones-produccion.md](migraciones-produccion.md) para backup, baseline,
SQL, migracion incremental, verificacion y arranque. No borrar ni recrear bases.

SQLite de desarrollo conserva el puente controlado InventarioSchemaUpgrade,
limitado a este cambio de inventario. Usa PRAGMA table_info antes de consultar
con EF, agrega solo lo faltante dentro de una transaccion y se puede repetir.
No admite PostgreSQL ni debe ampliarse para futuros cambios del esquema.
En Development, dotnet run lo aplica antes del seed; --upgrade-inventory permite
actualizar y salir sin iniciar el servidor. Respaldar primero la base existente.

La etapa posterior de tiendas creó `OperacionSchemaUpgrade` como puente SQLite
separado. No se amplió `InventarioSchemaUpgrade`; el ensayo de operación y entrega
en una copia está en [despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md).

Las pruebas cubren inventario, concurrencia, rollback, compatibilidad SQLite,
HTTP 201/Location y forma aditiva del SQL PostgreSQL. La validacion de produccion
se realiza sobre una copia PostgreSQL restaurada antes de desplegar.
