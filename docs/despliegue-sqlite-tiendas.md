# Preparación de despliegue con SQLite existente

Este documento conserva el plan previo a la publicación. **Actualización del 22 de octubre de 2026:** la versión de operación por tienda se publicó manualmente en `srvubu01`. El servicio efectivo corre en `Development` con la SQLite activa `/opt/apps/quevendecion/QueVendecion.db`; la API está en el puerto 5010 y el frontend en `/var/www/quevendecion`. La selección de proveedor en el código sigue siendo `Production` → Npgsql y otros entornos → SQLite. Los diagnósticos pendientes de las secciones históricas deben leerse a la luz de estos datos confirmados.

## Diagnóstico pendiente en el servidor

Ejecutar solo lecturas. Los comandos muestran nombres de variables o valores de entorno no secretos y nunca imprimen cadenas de conexión, contraseñas o JWT. Revisar primero que la ruta del proceso corresponda al servicio esperado.

```bash
pid=$(systemctl show quevendecion --property=MainPID --value)
test "$pid" -gt 0
printf 'Proceso: %s\n' "$pid"
readlink -f "/proc/$pid/exe"
readlink -f "/proc/$pid/cwd"
sudo sh -c 'tr "\0" "\n" < "/proc/$1/environ" | grep -E "^(ASPNETCORE_ENVIRONMENT|DOTNET_ENVIRONMENT)=" || true' sh "$pid"
sudo sh -c 'tr "\0" "\n" < "/proc/$1/environ" | cut -d= -f1 | sort -u' sh "$pid" | grep -E '^(ConnectionStrings__|ASPNETCORE_|DOTNET_)' || true
sudo lsof -p "$pid" -Fn | grep -F '/opt/apps/quevendecion/QueVendecion.db' || true
file /opt/apps/quevendecion/QueVendecion.db
sqlite3 -readonly /opt/apps/quevendecion/QueVendecion.db 'PRAGMA integrity_check; PRAGMA user_version; SELECT name FROM sqlite_master WHERE type="table" ORDER BY name;'
sqlite3 -readonly /opt/apps/quevendecion/QueVendecion.db 'SELECT count(*) FROM Tiendas; SELECT count(*) FROM Productos; SELECT count(*) FROM Pedidos;'
```

Si la cadena de conexión se define en archivos, **no imprimirlos** en la consola ni adjuntarlos a un reporte. Un operador con acceso debe confirmar en privado solo si el proveedor efectivo es SQLite o PostgreSQL y qué archivo o base usa el proceso. Si las variables de entorno no indican el entorno, registrar “no establecido” y confirmar el valor efectivo por una vía administrativa segura. `lsof` sin coincidencia no prueba por sí solo que SQLite no se use: las conexiones pueden abrirse y cerrarse.

## Ensayo local realizado

`tools/QueVendecion.SqliteUpgrade` recibe origen y ruta de copia nueva. Abre el origen en modo solo lectura, usa la API de respaldo de SQLite para crear una copia consistente, actualiza **solo la copia** con los puentes de inventario y operación, verifica conteos, integridad y claves foráneas. Se niega a sobrescribir un destino existente. Es una herramienta de transición para la base SQLite informada; no modifica la selección del proveedor en la API ni sustituye un plan de migraciones SQLite a largo plazo.

```powershell
dotnet run --project tools/QueVendecion.SqliteUpgrade -- <origen.db> <copia-nueva.db>
```

### Preparar el ejecutable para Ubuntu sin código fuente

La publicación histórica de la API se hace en la PC de desarrollo con
`-r linux-x64 --self-contained true`; el frontend se compila con Vite y se copia
como `dist`. La VM solo recibe artefactos. Para generar también la herramienta
SQLite desde la raíz del repositorio en Windows:

```powershell
dotnet publish src\QueVendecion.Api\QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o publish\api
Push-Location src\QueVendecion.Web
npm run build
Pop-Location
dotnet publish tools\QueVendecion.SqliteUpgrade\QueVendecion.SqliteUpgrade.csproj -c Release -r linux-x64 --self-contained true -o publish\sqlite-upgrade
```

El tercer comando genera `publish/sqlite-upgrade/QueVendecion.SqliteUpgrade` y
sus dependencias, sin requerir .NET SDK ni código fuente en la VM. Confirmar
primero que `uname -m` en Ubuntu devuelva `x86_64`; de otro modo elegir el RID
apropiado. Si se transfiere para un ensayo autorizado, copiar **todo el
contenido** de `publish/sqlite-upgrade` a un subdirectorio separado de la API,
por ejemplo `/opt/apps/quevendecion/tools/sqlite-upgrade`, y conceder permiso de
ejecución al archivo principal:

**Atención al repositorio local:** `publish/api` está versionado en Git. Ejecutar
el comando histórico sobre esa ruta modifica cientos de binarios seguidos por
Git. Para preparar artefactos sin ensuciar el árbol, sustituir `-o publish\api`
y `-o publish\sqlite-upgrade` por carpetas nuevas bajo `$env:TEMP` en la PC y
usar esas carpetas como origen al transferir. La ubicación local no cambia el
contenido ni el destino final en Ubuntu.

```powershell
ssh usuario@SERVIDOR "mkdir -p /opt/apps/quevendecion/tools/sqlite-upgrade"
scp -r publish\sqlite-upgrade\* usuario@SERVIDOR:/opt/apps/quevendecion/tools/sqlite-upgrade/
```

```bash
chmod +x /opt/apps/quevendecion/tools/sqlite-upgrade/QueVendecion.SqliteUpgrade
/opt/apps/quevendecion/tools/sqlite-upgrade/QueVendecion.SqliteUpgrade ORIGEN.db COPIA-NUEVA.db
```

Los comandos de transferencia y ejecución son una referencia para una etapa
posterior: no se ejecutaron contra la VM. No mezclar las dependencias de la
herramienta con los archivos publicados de la API ni usar el archivo de base
activo como destino. El `publish/api` histórico incluye archivos `appsettings*`;
antes de copiarlo hay que revisar qué configuración está en el servidor para no
sobrescribirla. El procedimiento de publicación depende del diagnóstico inicial.

Se ensayó sobre una base SQLite local con 2 tiendas, 2 usuarios, 1 cliente, 3 productos, 3 pedidos y 3 líneas. Se construyó además una copia de esquema anterior a esta etapa, se actualizó a otra copia y se conservaron esos conteos. El origen siguió sin `EntregaPermitida`; la copia actualizada sí la tiene. Los precios de prueba 25 y 30 permanecieron iguales; los tres pedidos previos quedaron con `FechaServicio=NULL`; `PRAGMA integrity_check` devolvió `ok`. **Esto prueba la actualización local, no el contenido ni la configuración de `srvubu01`.**

## Secuencia propuesta para una publicación posterior

1. Obtener el diagnóstico anterior. Si el proceso efectivo usa `Production` y la API publicada sigue seleccionando Npgsql, detener esta ruta de despliegue: la versión nueva no abrirá SQLite con esa configuración. Resolver esa incompatibilidad como decisión separada; este trabajo no la cambió.
2. Identificar con el administrador las filas de productos existentes y su forma correcta de entrega. Los productos previos quedan con `EntregaPermitida=NULL` y no son vendibles por el flujo nuevo hasta que el administrador los asigne. Para Jax, verificar **por ID** que la bolsa de $25 sea `Recoleccion` y la de $30 `Domicilio`; no deducirlo ni actualizarlo automáticamente por nombre o precio. Programar esta asignación como paso del cambio, antes de abrir pedidos nuevos.
3. Con la versión y el entorno compatibles, ensayar la herramienta sobre una copia real tomada mediante respaldo SQLite. Comparar conteos, IDs, precios, relaciones, integridad y consultas de pedidos históricos. Ejecutar pruebas funcionales contra la copia actualizada. No enviar capturas con datos personales a repositorios o tickets públicos.
4. En la futura ventana autorizada: detener escrituras y servicio, generar una copia fresca a partir del archivo activo, volver a verificarla, conservar el original como respaldo inmutable, sustituir la ruta de la base por la copia actualizada y ajustar permisos al usuario del servicio. Publicar API y frontend de la misma versión, iniciar y validar historial, catálogo, precios y pedidos piloto. La copia debe generarse **después** de detener escrituras para no perder pedidos entre el ensayo y el cambio.
5. Si cualquier comparación, arranque o prueba falla, mantener el servicio detenido y recuperar el archivo original respaldado junto con los binarios anteriores. No hacer rollback del esquema sobre la única copia de datos.

La ruta PostgreSQL del proyecto sigue documentada en [migraciones-produccion.md](migraciones-produccion.md); aplicarla solo si el diagnóstico confirma que esa es la base efectiva o como parte de una migración de proveedor autorizada por separado.

## Incidentes observados en la publicación manual

- La transferencia del frontend desde Windows dejó directorios sin permiso de acceso para Nginx y produjo HTTP 500. Se corrigieron directorios a `755` y archivos estáticos a `644`. En la próxima publicación, comprobar permiso de recorrido desde `/var` hasta `dist`, propietario/grupo del contenido, `nginx -t`, respuesta HTTP y registro de errores. Los comandos de reparación, una vez confirmado el directorio de destino, son `find /var/www/quevendecion -type d -exec chmod 755 {} +` y `find /var/www/quevendecion -type f -exec chmod 644 {} +`.
- Copiar una base actualizada como archivo no incorporó los últimos ajustes de Jax. Tras respaldo, se corrigieron directamente en la base activa dentro de una transacción. Estado confirmado: Jax `TiendaId=3`, `ProductoId=5` domicilio/$30/`EntregaPermitida=1`, `ProductoId=6` recolección/$25/`EntregaPermitida=0`; ambas entregas habilitadas y `America/Hermosillo`. No se debe volver a sustituir esa base con una copia anterior.
- El pedido #7 de Jax se creó para recoger por $25 y terminó `Cancelado` tras confirmar y cancelar. Los pedidos #5 y #6 siguen visibles. El historial de estados de #7 desplegado no se inspeccionó. La pantalla mostró `05:11` para un pedido creado cerca de las `22:11` de Hermosillo; la corrección de presentación se documenta en [tiendas-hallazgos.md](tiendas-hallazgos.md).

## Transferencia consistente de SQLite y conservación del servidor

La base activa de `srvubu01` es la fuente de verdad. No copiar una `.db` de Windows sobre ella: puede estar desactualizada y, si hay escritura en modo WAL, los últimos commits pueden residir en `QueVendecion.db-wal`. Tampoco copiar solo el archivo `.db` desde un origen vivo sin comprobar un checkpoint o usar la API de respaldo SQLite.

1. Para ensayar con datos actuales mientras el servicio sigue activo, crear **en la VM** una instantánea consistente con `sqlite3 /opt/apps/quevendecion/QueVendecion.db ".backup '/ruta/privada/ensayo.db'"` o con la API de respaldo SQLite. Verificar `PRAGMA integrity_check`, `PRAGMA foreign_key_check`, conteos, IDs y ajustes de Jax en esa instantánea. Una instantánea tomada antes de nuevos pedidos no sirve como reemplazo final.
2. Para cambiar la base activa en una ventana autorizada, detener primero las escrituras y el servicio. Confirmar que no quedan conexiones al archivo ni a sus sidecars `-wal`/`-shm` (`lsof`). Con la base cerrada, crear una instantánea final mediante `.backup` y verificarla; el mecanismo incorpora las páginas confirmadas que estuvieran en WAL. Si se usa copia de archivos en vez de `.backup`, hacer checkpoint con SQLite después de cerrar los escritores, comprobar resultado y copiar el conjunto consistente de `.db` y sidecars necesarios. No eliminar ni ignorar WAL por intuición.
3. Conservar la base original y su respaldo fuera del directorio que se sobrescribirá. Comparar la instantánea final con la activa en conteos, últimos pedidos y configuración de Jax **antes** de sustituir cualquier archivo. Para cambios de datos puntuales como la asignación de entrega, preferir una transacción revisada contra la base activa, con respaldo previo, sin reemplazarla con una copia vieja.
4. Al publicar la API, preservar `QueVendecion.db`, sus sidecars y los `appsettings*.json`/configuración efectiva del servidor; no copiar esos archivos desde el artefacto local. Mantener el entorno `Development`, la conexión y el puerto 5010 ya confirmados. Copiar binarios a una ruta preparada y comprobar diferencias antes de activar la versión. En el frontend, transferir archivos estáticos y verificar los permisos de directorios `755` y archivos `644` para Nginx.

Estos pasos describen una futura operación; en esta revisión no se accedió a la VM ni se modificó su base.
