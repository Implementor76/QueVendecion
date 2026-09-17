# Publicar Cambios

Guia rapida para deploy segun lo que cambió.

---

## Cambió solo la API (backend)

```powershell
dotnet publish src\QueVendecion.Api\QueVendecion.Api.csproj -c Release -r linux-x64 --self-contained true -o publish\api
scp -r publish\api\* implementor@192.168.1.3:/opt/apps/quevendecion/
```

En el servidor:

```bash
chmod +x /opt/apps/quevendecion/QueVendecion.Api
sudo systemctl restart quevendecion
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
chmod +x /opt/apps/quevendecion/QueVendecion.Api
sudo systemctl restart quevendecion
```

---

## Cambió el enum de estados o estructura de la BD

Si cambiaste un enum, una entidad o agregaste una migración, borrá la BD y dejá que se recree:

```bash
ssh implementor@192.168.1.3
sudo rm /opt/apps/quevendecion/QueVendecion.db
sudo systemctl restart quevendecion
```

> **Cuidado:** Esto borra todos los datos (tiendas, pedidos, usuarios). Solo hacer en desarrollo/pruebas.

---

## Verificar que funciona

```bash
ssh implementor@192.168.1.3

# Verificar que la API esta corriendo
sudo systemctl status quevendecion

# Probar un endpoint
curl -k https://pedidos.implementor.site/api/public/tienda/test
```

---

## Ver logs si algo falla

```bash
sudo journalctl -u quevendecion --no-pager -n 30
```
