# Guia de Deploy - Ubuntu Server (via SSH)

Paso a paso para desplegar QueVendecion en una VM con Ubuntu Server.

---

## Requisitos

- Ubuntu Server 22.04+ (24.04 recomendado)
- Acceso SSH con usuario `root` o con `sudo`
- IP de la VM (ej. `192.168.1.50`)
- PostgreSQL corriendo en la BD (ej. `192.168.1.4:5432`)
- Repo clonado en tu maquina local

---

## 1. Preparar el servidor

```bash
# Conectate por SSH
ssh usuario@192.168.1.50

# Actualiza el sistema
sudo apt update && sudo apt upgrade -y

# (No necesitas .NET SDK si usas self-contained, pero si quieres tenerlo:)
sudo apt install -y wget apt-transport-https
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-9.0
dotnet --version

# Instala nginx (reverse proxy)
sudo apt install -y nginx

# Habilita nginx al inicio
sudo systemctl enable nginx
```

---

## 2. Publicar la API (desde tu maquina local)

En tu PC con Windows, desde la raiz del repo:

```powershell
# Publica la API como self-contained para linux-x64
dotnet publish src\QueVendecion.Api\QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o publish\api
```

---

## 3. Subir los archivos al servidor

```powershell
# Sube la API publicada
scp -r publish\api\* usuario@192.168.1.50:/opt/apps/quevendecion/
```

---

## 4. Configurar la API en el servidor

### 4.1 Crear el servicio systemd

```bash
# Copia el service file del repo al servidor
scp deploy\quevendecion.service usuario@192.168.1.50:/tmp/

# En el servidor
sudo cp /tmp/quevendecion.service /etc/systemd/system/
sudo nano /etc/systemd/system/quevendecion.service  # Edita los secrets
```

```ini
[Unit]
Description=QueVendecion API
After=network.target

[Service]
WorkingDirectory=/opt/apps/quevendecion
ExecStart=/opt/apps/quevendecion/QueVendecion.Api
Restart=always
RestartSec=10

# Ambiente
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5010

# Secrets — CAMBIAR estos valores
Environment=Jwt__Key=TU_SECRET_MINIMO_32_CARACTERES_AQUI
Environment=ConnectionStrings__DefaultConnection=Host=192.168.1.4;Port=5432;Database=quevendecion;Username=api_user;Password=C@r0l1na10

[Install]
WantedBy=multi-user.target
```

### 4.2 Activar y arrancar el servicio

```bash
# Recarga systemd
sudo systemctl daemon-reload

# Habilita el servicio para que arranque con el sistema
sudo systemctl enable quevendecion

# Inicia el servicio
sudo systemctl start quevendecion

# Verifica que esta corriendo
sudo systemctl status quevendecion
```

### 4.3 Verificar que la API responde

```bash
# Deberia responder en el puerto 5010
curl http://localhost:5010/api/public/tienda/test
```

---

## 5. Configurar nginx como reverse proxy

### 5.1 Crear la configuracion del sitio

```bash
sudo nano /etc/nginx/sites-available/quevendecion
```

Pega:

```nginx
server {
    listen 80;
    server_name TU_DOMINIO_O_IP;

    # API
    location /api/ {
        proxy_pass http://127.0.0.1:5010;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # Frontend (estaticos)
    location / {
        root /var/www/quevendecion;
        try_files $uri $uri/ /index.html;
    }

    # Cache de assets
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
        root /var/www/quevendecion;
        expires 30d;
        add_header Cache-Control "public, immutable";
    }
}
```

### 5.2 Activar la configuracion

```bash
# Activa el sitio
sudo ln -s /etc/nginx/sites-available/quevendecion /etc/nginx/sites-enabled/

# Elimina la config default si no la necesitas
sudo rm /etc/nginx/sites-enabled/default

# Verifica la sintaxis de nginx
sudo nginx -t

# Recarga nginx
sudo systemctl reload nginx
```

---

## 6. Publicar y subir el Frontend

### 6.1 Build del frontend (desde tu PC local)

```powershell
cd src\QueVendecion.Web
npm run build
cd ..\..
```

Esto genera la carpeta `dist/` con los archivos estaticos.

### 6.2 Subir al servidor

```powershell
# Crea el directorio
ssh usuario@192.168.1.50 "mkdir -p /var/www/quevendecion"

# Sube los archivos
scp -r src\QueVendecion.Web\dist\* usuario@192.168.1.50:/var/www/quevendecion/
```

---

## 7. Configurar la base de datos

Conectate al servidor donde esta PostgreSQL:

```bash
# Entra a PostgreSQL
sudo -u postgres psql

# Crea solo la base de datos (el usuario api_user ya existe)
CREATE DATABASE quevendecion OWNER api_user;
\q
```

> **Nota:** Las tablas se crean automaticamente al iniciar la API (`EnsureCreated()`).

---

## 8. Abrir el firewall

```bash
# Abre puertos HTTP y HTTPS
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Si usas SSH en un puerto distinto al 22
sudo ufw allow 22/tcp

# Activa el firewall
sudo ufw enable
```

---

## 9. Comandos utiles

```bash
# Ver logs en tiempo real
sudo journalctl -u quevendecion -f

# Reiniciar la API despues de un cambio
sudo systemctl restart quevendecion

# Ver estado del servicio
sudo systemctl status quevendecion

# Ver logs de nginx
sudo tail -f /var/log/nginx/error.log

# Recargar nginx despues de un cambio
sudo nginx -t && sudo systemctl reload nginx
```

---

## 10. Resumen de puertos

| Servicio | Puerto | Protocolo |
|----------|--------|-----------|
| nginx (frontend + proxy) | 80 | HTTP |
| API (Kestrel) | 5010 | HTTP |
| PostgreSQL | 5432 | TCP |
| SSH | 22 | TCP |

---

## 11. Checklist de deploy

- [ ] `.NET SDK 9.0` instalado en el servidor (opcional con self-contained)
- [ ] `nginx` instalado y configurado
- [ ] API publicada en `/opt/apps/quevendecion/`
- [ ] Frontend compilado en `/var/www/quevendecion/`
- [ ] `quevendecion.service` creado y activo
- [ ] `Jwt__Key` configurado con un secreto real
- [ ] `ConnectionStrings__DefaultConnection` con credenciales reales
- [ ] PostgreSQL accesible desde la VM
- [ ] Firewall configurado (puertos 80, 443, 22)
- [ ] nginx apuntando a `http://127.0.0.1:5010` para `/api/`
- [ ] CORS en `Program.cs` incluye la IP/dominio del frontend
- [ ] `curl http://localhost:5010` responde correctamente

---

## Troubleshooting

**La API no arranca:**
```bash
sudo journalctl -u quevendecion --no-pager -n 50
```
Revisa si el secreto JWT o la conexion a la BD estan mal configurados.

**nginx da 502 Bad Gateway:**
```bash
sudo systemctl status quevendecion
```
La API no esta corriendo. Verifica el servicio systemd.

**CORS error en el navegador:**
Revisa que la IP/dominio de tu frontend este en la lista de CORS en `Program.cs` linea 61.

**Puerto 5010 no responde externamente:**
```bash
sudo ufw status
curl http://localhost:5010
```
Si funciona local pero no externo, es el firewall.
