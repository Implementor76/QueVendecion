# Publicar QueVendecion

Paso a paso para publicar la API (self-contained) y el frontend al servidor.

**Estado de `srvubu01` (22 de octubre de 2026):** usa `Development` y SQLite
activa en `/opt/apps/quevendecion/QueVendecion.db`. Los comandos PostgreSQL de
esta guía son históricos y no aplican a ese servicio. Seguir
[despliegue-sqlite-tiendas.md](../docs/despliegue-sqlite-tiendas.md) para
instantánea consistente, WAL, preservación de la base y `appsettings*` del
servidor. No usar `scp publish\api\*` sobre el directorio activo: puede
sobrescribir su configuración. Tras copiar el frontend, comprobar directorios
`755` y archivos `644` para Nginx.

**Antes de copiar código:** detener escrituras, respaldar PostgreSQL y comprobar
restauración. Seguir [migraciones-produccion.md](../docs/migraciones-produccion.md).
El orden es backup → código → migraciones → verificación → arranque → validación
funcional. Nunca borrar ni recrear una base existente.

---

## 1. Publicar la API (desde tu PC local)

Desde la raiz del repo en Windows:

```powershell
dotnet publish src\QueVendecion.Api\QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o publish\api
```

> Self-contained = incluye el runtime de .NET. No necesitas instalar .NET en el servidor.

---

## 2. Subir la API al servidor

```powershell
# Sube todo el contenido de publish\api a /opt/apps/quevendecion/
scp -r publish\api\* implementor@IP:/opt/apps/quevendecion/
```

Reemplaza `IP` con la IP de tu servidor.

---

## 3. Dar permisos de ejecucion

```bash
# Por SSH al servidor
ssh implementor@IP

chmod +x /opt/apps/quevendecion/QueVendecion.Api
```

---

## 4. Configurar secrets en el service

El service file ya esta en `deploy/quevendecion.service`. Copialo al servidor y edita los valores reales:

```powershell
scp deploy\quevendecion.service implementor@IP:/tmp/
```

```bash
# En el servidor
sudo cp /tmp/quevendecion.service /etc/systemd/system/
```

Edita los secrets:

```bash
sudo nano /etc/systemd/system/quevendecion.service
```

Cambia estas lineas:

```ini
Environment=Jwt__Key=GENERAR_UN_SECRETO_LARGO_AQUI
Environment=ConnectionStrings__DefaultConnection=Host=IP_BD;Port=5432;Database=quevendecion;Username=USUARIO;Password=VALOR_PRIVADO
```

Para generar un secreto JWT rapido:

```bash
openssl rand -base64 48
```

---

## 5. Activar y arrancar la API

Antes del arranque, aplicar el baseline una sola vez si la base antigua no tiene
historial y su esquema fue verificado. Aplicar luego el SQL incremental del mismo
release y verificarlo, según la guía de migraciones. Mantener la API detenida
hasta que todos los pasos terminen correctamente:

```bash
set -e
psql 'service=quevendecion' -X -v ON_ERROR_STOP=1 -f inventario.sql
psql 'service=quevendecion' -X -v ON_ERROR_STOP=1 -f verificar-inventario.sql
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable quevendecion
sudo systemctl start quevendecion

# Verificar que esta corriendo
sudo systemctl status quevendecion
```

---

## 6. Verificar que responde

```bash
curl --fail http://localhost:5010/api/public/tienda/SLUG_EXISTENTE/menu
```

Usar una tienda existente y exigir HTTP 200; un 404 no verifica que los datos
se preservaron. Comprobar también login y pedidos anteriores.

---

## 7. Publicar el Frontend

### Build

```powershell
cd src\QueVendecion.Web
npm run build
cd ..\..
```

### Subir

```powershell
# Crear directorio en el servidor (solo la primera vez)
ssh implementor@IP "mkdir -p /var/www/quevendecion"

scp -r src\QueVendecion.Web\dist\* implementor@IP:/var/www/quevendecion/
```

---

## 8. Logs

```bash
# Logs de la API en tiempo real
sudo journalctl -u quevendecion -f

# Ultimas 50 lineas
sudo journalctl -u quevendecion --no-pager -n 50
```

---

## Publicaciones posteriores

Repetir el flujo de [migraciones-produccion.md](../docs/migraciones-produccion.md).
No usar un comando de copia/reinicio que omita backup, migración y verificación.
