# Publicar QueVendecion

Paso a paso para publicar la API (self-contained) y el frontend al servidor.

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
Environment=ConnectionStrings__DefaultConnection=Host=IP_BD;Port=5432;Database=quevendecion;Username=api_user;Password=C@r0l1na10
```

Para generar un secreto JWT rapido:

```bash
openssl rand -base64 48
```

---

## 5. Activar y arrancar la API

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
curl http://localhost:5010/api/public/tienda/test
```

Si ves un JSON (aunque sea vacio o error 404), la API esta viva.

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

## Comando rapido (publicar todo de un solo golpe)

```powershell
# Desde la raiz del repo
dotnet publish src\QueVendecion.Api\QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o publish\api
scp -r publish\api\* implementor@IP:/opt/apps/quevendecion/
cd src\QueVendecion.Web; npm run build; cd ..\..
scp -r src\QueVendecion.Web\dist\* implementor@IP:/var/www/quevendecion/
ssh implementor@IP "chmod +x /opt/apps/quevendecion/QueVendecion.Api && sudo systemctl restart quevendecion"
```
