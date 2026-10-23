# Publicar Cambios

Guia rapida para deploy segun lo que cambió.

**`srvubu01` actual (22 de octubre de 2026):** `Development` con SQLite activa.
Los ejemplos `psql` siguientes solo aplican a un destino PostgreSQL confirmado;
no ejecutarlos en `srvubu01`. El procedimiento vigente está en
[despliegue-sqlite-tiendas.md](../docs/despliegue-sqlite-tiendas.md). Preservar
base, WAL y `appsettings*` al copiar API; verificar permisos `755` de directorios
y `644` de archivos del frontend para Nginx. Los `scp` de este documento que
copian todo `publish\api` no preservan por sí solos esa configuración.

Para cualquier cambio de backend, seguir primero
[migraciones-produccion.md](../docs/migraciones-produccion.md): backup verificado
de PostgreSQL, detener escrituras, desplegar, aplicar migraciones incrementales,
verificar esquema/datos, arrancar y comprobar funcionamiento. No copiar binarios
sobre un servicio activo ni reiniciarlo antes de migrar.

---

## Cambió solo la API (backend)

```powershell
dotnet publish src\QueVendecion.Api\QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o publish\api
scp -r publish\api\* implementor@192.168.1.3:/opt/apps/quevendecion/
```

En el servidor:

```bash
set -e
chmod +x /opt/apps/quevendecion/QueVendecion.Api
# Con backup y código desplegado, aplicar y verificar el SQL del release:
psql 'service=quevendecion' -X -v ON_ERROR_STOP=1 -f inventario.sql
psql 'service=quevendecion' -X -v ON_ERROR_STOP=1 -f verificar-inventario.sql
# Solo si los pasos anteriores terminaron correctamente:
sudo systemctl start quevendecion
```

---

## Cambió solo el Frontend

```powershell
cd src\QueVendecion.Web
npm run build
cd ..\..
scp -r src\QueVendecion.Web\dist\* implementor@192.168.1.3:/var/www/quevendecion/
```

No necesita reiniciar nada.

---

## Cambió la API y el Frontend

```powershell
dotnet publish src\QueVendecion.Api\QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o publish\api
scp -r publish\api\* implementor@192.168.1.3:/opt/apps/quevendecion/
cd src\QueVendecion.Web
npm run build
cd ..\..
scp -r src\QueVendecion.Web\dist\* implementor@192.168.1.3:/var/www/quevendecion/
```

En el servidor:

```bash
set -e
chmod +x /opt/apps/quevendecion/QueVendecion.Api
psql 'service=quevendecion' -X -v ON_ERROR_STOP=1 -f inventario.sql
psql 'service=quevendecion' -X -v ON_ERROR_STOP=1 -f verificar-inventario.sql
# Solo después de verificar la migración:
sudo systemctl start quevendecion
```

---

## Cambió el enum de estados o estructura de la BD

Generar una migración incremental con EF Core, revisar su SQL y probarla sobre
una copia restaurada. No renumerar valores de enums persistidos. Nunca borrar
ni recrear la base para actualizarla, tampoco cuando desarrollo contiene datos.

La base PostgreSQL anterior sin historial necesita primero el baseline verificado
de [migraciones-produccion.md](../docs/migraciones-produccion.md). No ejecutar
la migración inicial sobre tablas existentes. El puente de SQLite queda reservado
al inventario antiguo en desarrollo.

---

## Verificar que funciona

```bash
ssh implementor@192.168.1.3

# Verificar que la API esta corriendo
sudo systemctl status quevendecion

# Reemplazar SLUG_EXISTENTE por una tienda real: debe devolver HTTP 200
curl --fail https://pedidos.implementor.site/api/public/tienda/SLUG_EXISTENTE/menu
```

---

## Ver logs si algo falla

```bash
sudo journalctl -u quevendecion --no-pager -n 30
```
