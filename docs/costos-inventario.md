# Costos e inventario por tienda (23 de octubre de 2026)

Esta etapa sirve a cualquier tienda. No crea Pinyex ni carga productos reales.
Se conserva el descuento de unidades al **crear** un pedido; funciona como
separación de mercancía. Confirmar o entregar no descuenta de nuevo. Cancelar
reintegra las salidas registradas una sola vez, dentro de una transacción.

## Reglas de producto y stock

- `CostoUnitario` es opcional y tiene cuatro decimales. `NULL` significa
  desconocido; cero es un costo explícito. Una compra actualiza el costo de
  referencia al último costo unitario capturado. Puede registrarse un costo en
  la edición del producto. Esto no es valuación contable por promedio ni PEPS.
- `ExistenciaMinima` es opcional y solo aplica con control de inventario. La
  administración avisa cuando el saldo es menor o igual al mínimo.
- La creación o activación del control admite existencia inicial y registra un
  movimiento si es positiva. Después, la edición del producto exige que el
  saldo enviado coincida con el actual. Compra y ajuste son las vías para
  cambiarlo. El ajuste requiere motivo y nunca permite saldo negativo.
- Un producto con control activo no puede desactivarlo. Un producto con pedidos
  o movimientos tampoco puede borrarse: se marca no disponible. Así se conserva
  la posibilidad de reintegrar pedidos pendientes.
- Las existencias y movimientos heredados permanecen iguales. No se inventan
  movimientos anteriores. La trazabilidad de compra, ajuste y usuario comienza
  al operar con esta versión. Las salidas de clientes no tienen usuario
  administrativo; una cancelación administrativa sí lo registra.

La API pública y los pedidos del cliente no contienen costo ni utilidad. Las
rutas autenticadas `mi-tienda` entregan esos datos solo para la tienda del JWT.
Las líneas nuevas de pedido guardan precio y costo de referencia vigentes. Las
líneas históricas sin costo permanecen en `NULL` aunque cambie el producto.

## Resumen de ventas

El rango se interpreta en la zona horaria de la tienda, según el **instante de
entrega**, no la fecha del pedido ni la fecha de servicio. Solo cuentan pedidos
`Entregado`. Si una transición histórica `Entregado` tiene instante en el
historial, se usa ese dato; si no hay evidencia, se cuenta como entregado sin
instante y queda fuera de la cifra del rango. Se muestran aparte los pedidos
por atender, que son el total abierto de la tienda y no llevan filtro de rango.

La utilidad bruta estimada suma `(precio histórico - costo histórico) × cantidad`
solo para líneas con costo conocido. La venta de líneas sin costo se muestra
fuera de ese cálculo, no como ganancia. El estado de pago es independiente: el
resumen separa entregado y pagado de entregado pendiente de cobro. Los importes
finales se muestran con dos decimales. Esta etapa no incluye impuestos,
facturación ni contabilidad.

## Esquema y publicación futura

SQLite utiliza `CostosSchemaUpgrade`, que agrega columnas nullable e índice sin
reconstruir tablas ni rellenar datos. La herramienta
`tools/QueVendecion.SqliteUpgrade` crea una copia consistente con la API de
respaldo SQLite, aplica los puentes en ella, verifica integridad, claves
foráneas, conteos, precios, pedidos y existencias. En PostgreSQL se aplica la
migración `20261023064705_AgregarCostosYResumen` después de las anteriores;
el arranque solo comprueba migraciones pendientes. La elección de proveedor por
entorno sigue intacta.

Ensayo local: origen `src/QueVendecion.Api/QueVendecion.db` abierto solo lectura;
destino nuevo bajo `.trial`. La copia actualizada conservó 3 tiendas, 3
usuarios, 1 cliente, 2 productos, 3 pedidos y 5 líneas. `integrity_check` y
`foreign_key_check` pasaron. Este ensayo no prueba el estado de `srvubu01`.

Validación local: 71 pruebas .NET y 3 pruebas frontend pasaron, al igual que
la compilación de la API, la herramienta SQLite y el build de Vite. En Chrome
con API simulada se revisaron las vistas de inventario, compra, movimientos,
resumen, menú administrativo y catálogo del cliente en 390 px y 1280 px. Se
comprobó el aviso de stock bajo, el costo privado en administración, la
selección de entrega del cliente y la lista de Mis pedidos vacía. La simulación
visual no equivale a una prueba contra la VM ni valida datos reales.

La VM publicada sigue en `Development`, con SQLite activa en
`/opt/apps/quevendecion/QueVendecion.db`, API en el puerto 5010 y frontend en
`/var/www/quevendecion`. En una futura ventana autorizada: detener escrituras,
cerrar el servicio, comprobar conexiones y WAL, crear respaldo consistente de
la base **activa** mediante `.backup` o la API SQLite, ensayar el upgrade sobre
esa copia final, comparar filas clave, y conservar el original para reversión.
No transferir solo el archivo `.db` desde una base viva ni sustituir la base
activa por una copia local vieja. Mantener los `appsettings*`, el entorno, la
base y sus sidecars fuera de la sobreescritura de binarios. Publicar API y
frontend de la misma versión; verificar permisos `755` de directorios y `644`
de archivos estáticos para Nginx. Ver el contexto y comandos anteriores en
[despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md).

Para generar artefactos en la PC de desarrollo, usar carpetas temporales nuevas
fuera de `publish/api` (esa ruta histórica está versionada), y no copiar
`appsettings*.json` desde el artefacto al servidor:

```powershell
dotnet publish src/QueVendecion.Api/QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o "$env:TEMP/qvd-costos-api"
dotnet publish tools/QueVendecion.SqliteUpgrade/QueVendecion.SqliteUpgrade.csproj -c Release -r linux-x64 --self-contained true -o "$env:TEMP/qvd-costos-upgrade"
Push-Location src/QueVendecion.Web
npm run build
Pop-Location
```

La herramienta recibe un origen y un destino **nuevo**; el origen se abre solo
lectura y se respalda con la API SQLite. Una prueba local se invoca así:

```powershell
dotnet run --project tools/QueVendecion.SqliteUpgrade -- <origen.db> <copia-nueva.db>
```

En una futura publicación autorizada, el origen debe ser la base activa de la
VM capturada después de detener escrituras. Primero comprobar `systemctl`
para el nombre real de la unidad, `lsof` para conexiones a `.db`, `-wal` y
`-shm`, y `PRAGMA integrity_check`/`foreign_key_check` en el respaldo. La
herramienta no reemplaza la base activa: entrega la copia actualizada para
comparación y un cambio controlado. Ninguno de estos comandos se ejecutó en
`srvubu01` durante esta etapa.
