# Migraciones y preservación de datos en PostgreSQL

La base existente contiene datos de negocio. No borrar bases, tablas ni columnas
para desplegar. No reconstruir `Productos`. Este procedimiento se aplica solo si
el diagnóstico confirma que el servicio usa PostgreSQL. El código selecciona
Npgsql en `Production` y SQLite en otros entornos; el proveedor efectivo del
servicio informado sigue sin comprobarse. Para SQLite, consultar
[despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md). El arranque en `Production` **verifica** migraciones pendientes, pero
no ejecuta DDL ni `EnsureCreated`. El proceso de despliegue aplica los cambios
antes de reiniciar la API. No se ha ejecutado nada contra producción al preparar
estos archivos.

## Migraciones versionadas

En `src/QueVendecion.Infrastructure/Migrations/PostgreSql`:

- `20260917143531_EsquemaAnteriorInventario`: esquema completo anterior, para
  instalaciones nuevas vacías. **No ejecutar su Up sobre la base existente.**
- `20260917143552_AgregarInventario`: `ALTER TABLE Productos ADD Stock integer NULL`,
  `ADD TieneInventario boolean NOT NULL DEFAULT false`, tabla de movimientos,
  claves foráneas e índices. No modifica las columnas ni los registros anteriores.
- `20261022054127_AgregarOperacionTienda`: configuración operativa, modalidad y
  fecha de servicio de pedidos, y fechas de servicio de productos.
- `20261022055059_AgregarDireccionEntrega`: dirección para pedidos a domicilio.
- `20261022060414_AsignarEntregaYRegistrarEstados`: entrega permitida nullable
  por producto e historial de estados nullable por pedido. No asigna entrega a
  productos anteriores ni cambia precios.
- Snapshot del modelo actual y factory de diseño exclusiva de PostgreSQL.
  La factory no inicia la API ni ejecuta seeds. No usar estas migraciones con SQLite.

Los `Down` destructivos están bloqueados: una reversión del esquema se resuelve
con una migración correctiva hacia adelante. No utilizar `database update 0`
ni restaurar un backup sobre producción como un paso rutinario de despliegue.

## Preparar y revisar los artefactos

Desde la raíz del repositorio (SDK .NET 9):

```powershell
dotnet tool restore
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes --project src/QueVendecion.Infrastructure --startup-project src/QueVendecion.Infrastructure --no-build
dotnet ef migrations script EsquemaAnteriorInventario AgregarInventario --idempotent --project src/QueVendecion.Infrastructure --startup-project src/QueVendecion.Infrastructure --no-build --output deploy/postgresql/inventario.sql
dotnet publish src/QueVendecion.Api/QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o publish/api
```

Revisar el SQL generado antes de publicarlo. El script incremental presupone
el baseline registrado; no usarlo sobre una base desconocida sin revisar historial.
Es idempotente por `__EFMigrationsHistory`: en ejecuciones posteriores omite la
migración aplicada. No pretende reparar un esquema modificado manualmente ni
registrar como aplicado un cambio que no existe.

Para cambios posteriores, modificar el modelo y generar una migración nueva:

```powershell
dotnet ef migrations add NombreDelCambio --project src/QueVendecion.Infrastructure --startup-project src/QueVendecion.Infrastructure --output-dir Migrations/PostgreSql
```

Revisar cualquier operación destructiva generada por EF; convertirla en una
estrategia gradual que conserve datos. No editar migraciones ya desplegadas.
Generar el script desde la última migración aplicada al destino hasta la nueva.

## Una sola vez: adoptar una base anterior sin historial

1. Respaldar y restaurar en una **base aislada**. Inspeccionar el esquema real
   con `pg_dump --schema-only` y compararlo con la inicial versionada.
2. Comprobar tablas, columnas, tipos, nulabilidad, defaults, claves, índices,
   relaciones, secuencias y valores de enums persistidos. Revisar también
   objetos adicionales del negocio: las comprobaciones automáticas no sustituyen
   la revisión del esquema completo.
3. Con escrituras detenidas, ejecutar `deploy/postgresql/baseline-existente.sql`
   **primero en esa copia**. Valida columnas, PK, identidad/secuencias, FK e índices,
   exige el esquema anterior sin inventario y un historial vacío. Solo crea
   `__EFMigrationsHistory` y registra la migración inicial; no ejecuta su `Up`.
4. Aplicar `inventario.sql`, ejecutar `verificar-inventario.sql` y comparar los
   datos con el backup. Repetir `inventario.sql` y verificar que no cambia datos.
5. Solo después de validar la copia, realizar la misma secuencia en producción
   dentro del flujo de mantenimiento descrito abajo.

Los scripts asumen esquema `public`. Si el esquema real difiere, si el historial
ya existe o si hay parte del inventario, detenerse y preparar una adaptación
incremental específica. **No borrar columnas/tablas ni inventar entradas del
historial para hacer pasar el baseline.** Una base que ya tiene todo el inventario
requiere comparar el modelo completo y preparar su adopción por separado.

Una instalación realmente vacía puede ejecutar
`deploy/postgresql/esquema-inicial-solo-base-vacia.sql` y luego `inventario.sql`.
Nunca usar esa ruta para actualizar una base existente.

## Flujo obligatorio de producción

Preparar credenciales mediante `.pgpass` o un servicio de conexión de PostgreSQL;
no guardar contraseñas en scripts ni en Git. Los ejemplos usan un servicio
libpq llamado `quevendecion`, configurado por el operador. `psql`/`pg_dump` no
usan el formato de conexión de Npgsql. Mantener las escrituras detenidas desde
el backup hasta terminar las verificaciones del esquema.

### 1. Backup de PostgreSQL

```bash
set -e
sudo systemctl stop quevendecion
umask 077
mkdir -p "$HOME/backups/quevendecion"
backup_path="$HOME/backups/quevendecion/$(date +%Y%m%d-%H%M%S).dump"
pg_dump --dbname='service=quevendecion' --format=custom --file="$backup_path"
pg_restore --list "$backup_path"
```

Comprobar códigos de salida y restauración real en una base aislada; listar el
archivo no demuestra que la restauración funcione. Guardar conteos e IDs y una
comparación de los datos de negocio antes/después. Si falla un paso, no continuar.

### 2. Desplegar código

Copiar los binarios y `deploy/postgresql/*.sql` del mismo release al servidor,
con el servicio todavía detenido. No copiar una SQLite de desarrollo sobre
archivos de producción. Mantener una copia del release anterior.

### 3. Aplicar migraciones incrementales

En la primera adopción de una base anterior, tras la revisión y ensayo del
baseline, ejecutar una sola vez:

```bash
psql 'service=quevendecion' -X -v ON_ERROR_STOP=1 -f baseline-existente.sql
```

Después, si el destino confirmado es PostgreSQL y aún falta inventario:

```bash
psql 'service=quevendecion' -X -v ON_ERROR_STOP=1 -f inventario.sql
```

Para la funcionalidad de tiendas, una vez aplicado y verificado inventario,
revisar y aplicar `deploy/postgresql/operacion.sql`, generado de
`AgregarInventario` a `AsignarEntregaYRegistrarEstados`. Incluye las tres
migraciones de esta etapa y el historial EF. No ejecutarlo sobre SQLite ni sobre
un esquema desconocido. Después comprobar conteos, precios, relaciones y
pedidos históricos; asignar explícitamente la entrega de productos existentes.

No ejecutar migradores simultáneos. Los scripts EF son transaccionales; un fallo
deja la migración pendiente. Investigar y corregir, sin reiniciar a ciegas ni
recrear la base. El baseline se registra en una transacción separada y no debe
repetirse si ya se completó: continuar con la incremental.

### 4. Verificar migración

```bash
psql 'service=quevendecion' -X -v ON_ERROR_STOP=1 -f verificar-inventario.sql
```

Verifica historial, tipos, default, nulabilidad, stock y relaciones. Comparar
conteos, IDs y campos previos con el backup/copia: tiendas, usuarios, clientes,
productos, pedidos e items deben permanecer intactos. En productos anteriores
al inventario, `TieneInventario=false` y `Stock=NULL`.

### 5. Arrancar/reiniciar

```bash
sudo systemctl start quevendecion
sudo systemctl status quevendecion
sudo journalctl -u quevendecion --no-pager -n 50
```

### 6. Verificar integridad y funcionamiento

Consultar una tienda y su menú **existentes** (HTTP 200, no basta un 404), iniciar
sesión, revisar pedidos anteriores y confirmar que los datos coinciden. Probar
alta de producto y pedido/cancelación de inventario en una tienda de pruebas
autorizada. Revisar logs y repetir las consultas de integridad pertinentes.

## SQLite de desarrollo

`InventarioSchemaUpgrade` conserva compatibilidad con la SQLite anterior y se
ejecuta solo en `Development`. Se limita a las dos columnas y la tabla de ese
incidente. `OperacionSchemaUpgrade` agrega por separado los campos de tiendas,
entrega e historial. Ninguno acepta PostgreSQL ni sustituye una estrategia de
migraciones SQLite a largo plazo. `EnsureCreated` solo permite crear una SQLite nueva de desarrollo;
no actualiza bases existentes. Una futura cadena de migraciones SQLite debe
tener su snapshot/baseline específico: no reutilizar la cadena PostgreSQL.

## Alcance de la validación local

Las pruebas verifican el SQL generado por Npgsql, que el cambio es aditivo,
el snapshot, el rechazo del puente SQLite en PostgreSQL y las regresiones SQLite.
La validación final de scripts, baseline y preservación sobre PostgreSQL requiere
una copia restaurada del motor real antes de producción.
