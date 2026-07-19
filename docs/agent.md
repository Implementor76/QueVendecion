# Prompt del Agente — Qué Vendeción

> Este archivo es un prompt para que cualquier agente (o tú mismo en una nueva sesión) continúe el proyecto correctamente. Léelo antes de escribir código.

---

## Qué es este proyecto

**Qué Vendeción** es una PWA mobile-first para ayudar a pequeños negocios a organizar sus pedidos. Piensa en un sistema tipo WhatsApp: el vendedor recibe pedidos por una URL pública y los gestiona desde un panel simple.

## Stack obligatorio

- **Frontend:** HTML + CSS + JavaScript + Tailwind v4 + Vite (SPA, sin frameworks Vue/React/Angular)
- **Backend:** ASP.NET Core Web API + Controllers + EF Core
- **DB:** SQLite (dev) / PostgreSQL (prod)
- **Colores:** `#312c51` (primary), `#48426d` (primary-light), `#f0c38e` (accent), `#f1aa9b` (accent-warm), `#f8f6f0` (surface)
- **Lenguaje de negocio:** Tienda, Cliente, Menú, Pedido, Fiado, Confirmado

## Arquitectura

```
QueVendecion.Domain/        → Entidades + Enums (puro C#, sin dependencias)
QueVendecion.Application/   → DTOs + Interfaces de servicio
QueVendecion.Infrastructure/→ EF Core DbContext + Implementaciones de servicio + Migrations
QueVendecion.Api/           → Controllers + Program.cs + JWT
QueVendecion.Web/           → Vite + Tailwind v4 (proyecto Node independiente, NO está en .sln)
```

## Modelo de datos (entidades)

| Entidad | Descripción |
|---|---|
| `Tienda` | Negocio. Tiene `Nombre`, `Slug` (único), `Direccion`, `Telefono`, `Descripcion` |
| `UsuarioTienda` | Usuarios del sistema. `Email`, `PasswordHash`, `Rol` (SuperAdmin=0, Admin=1, Staff=2), `TiendaId` |
| `Cliente` | Clientes que piden. `Nombre`, `Telefono`, `DeviceToken` (para notificaciones futuras) |
| `Producto` | Items del menú. `Nombre`, `Descripcion`, `Precio`, `Disponible`, `TiendaId` |
| `Pedido` | Orden. `TiendaId`, `ClienteId?`, `ClienteNombre?`, `Estado` (Recibido→Confirmado→Preparando→Listo→Entregado/Cancelado), `FormaPago`, `EstadoPago`, `Total`, `Observaciones` |
| `ItemPedido` | Línea del pedido. `PedidoId`, `ProductoId`, `Cantidad`, `PrecioUnitario` |

## Enums

- `RolUsuario`: SuperAdmin=0, Admin=1, Staff=2
- `EstadoPedido`: Recibido, Confirmado, Preparando, Listo, Entregado, Cancelado
- `FormaPago`: Efectivo, Transferencia, Fiado
- `EstadoPago`: Pendiente, Pagado, Parcial

## Flujo de usuarios

### SuperAdmin (no tiene UI propia)
- Crea tiendas + admin en un solo paso: `POST /api/superadmin/tienda` con `{ tienda: {...}, adminEmail, adminPassword }`
- No hay login de SuperAdmin en el MVP, se usa curl/Postman

### Admin (dueño de tienda)
- Se registra con email + password (creado por SuperAdmin)
- Login: `POST /api/auth/login` → devuelve JWT con `TiendaId`
- Ve su panel en `/mi-tienda` con tabs: Datos de tienda, Menú, Pedidos
- Endpoints usan JWT: `/api/tiendas/mi-tienda/*`, `/api/pedidos/mi-tienda/*`
- Puede cambiar su password

### Cliente (quien pide)
- Entra a `/tienda/{slug}` (URL pública)
- Se registra con nombre + teléfono → se guarda en localStorage y en backend
- Navega entre tabs: Menú, Mis Pedidos, Mi Cuenta
- Hace pedidos desde el menú → `POST /api/public/tienda/{slug}/pedido`
- Ve su historial: `GET /api/public/tienda/{slug}/cliente/{telefono}/pedidos`

## Endpoints API

### Públicos (sin auth)
- `GET /api/public/tienda/{slug}` — datos de la tienda
- `GET /api/public/tienda/{slug}/menu` — productos disponibles
- `POST /api/public/tienda/{slug}/cliente` — registro/login cliente
- `GET /api/public/tienda/{slug}/cliente/{telefono}/pedidos` — historial del cliente
- `PUT /api/public/tienda/{slug}/cliente/{telefono}/device-token` — guardar token push
- `POST /api/public/tienda/{slug}/pedido` — crear pedido

### Auth
- `POST /api/auth/login` — login con email + password → JWT
- `POST /api/auth/cambiar-password` — cambiar password (requiere JWT)

### Admin (requiere JWT)
- `GET /api/tiendas/mi-tienda` — datos de mi tienda
- `PUT /api/tiendas/{id}` — actualizar tienda
- `GET /api/tiendas/mi-tienda/productos` — productos de mi tienda
- `POST /api/tiendas/mi-tienda/productos` — crear producto
- `PUT /api/tiendas/mi-tienda/productos/{id}` — actualizar producto
- `DELETE /api/tiendas/mi-tienda/productos/{id}` — eliminar producto
- `GET /api/pedidos/mi-tienda` — pedidos de mi tienda (filtro por estado)
- `PATCH /api/pedidos/{id}/estado` — cambiar estado del pedido
- `PATCH /api/pedidos/{id}/pago` — cambiar estado de pago

### SuperAdmin (requiere JWT)
- `POST /api/superadmin/tienda` — crear tienda + admin

## Frontend (rutas)

| Ruta | Página | Requiere auth |
|---|---|---|
| `/` | Home (elección admin/cliente) | No |
| `/login` | Login admin | No |
| `/mi-tienda` | Panel admin | JWT |
| `/menu` | CRUD productos | JWT |
| `/pedidos` | Gestión pedidos | JWT |
| `/tienda/:slug` | Tienda pública (cliente) | No (usa localStorage) |

## Cosas técnicas que NO olvidar

1. **La DB se recrea con `EnsureCreated()`** — borrar `QueVendecion.db` si cambia el schema
2. **El proxy de Vite apunta a `http://localhost:5136`** — el puerto real del API
3. **`QueVendecion.Web` NO está en el .sln** — es un proyecto Node independiente
4. **Los enums se envían como strings** — `JsonStringEnumConverter` está configurado en Program.cs
5. **El JWT trae `TiendaId` como claim** — el frontend lo usa para filtrar datos del admin
6. **El login del admin solo pide email+password** — el tiendaId viene del token
7. **El formulario de producto se queda abierto después de agregar** — muestra mensaje de éxito, enfoca el input de nombre
8. **Los clientes se identifican por teléfono** — localStorage por tienda (`qvd_client_{slug}`)
9. **Los colores del tema están en `style.css`** con `@theme` de Tailwind v4
10. **Las referencias visuales están en `docs/`** (Referencia1.jpg, Referencia2.jpg)

## Comandos útiles

```bash
# Backend
dotnet build QueVendecion.sln          # Build completo
cd src/QueVendecion.Api && dotnet run  # Arrancar API (puerto 5136)

# Frontend
cd src/QueVendecion.Web && npm run dev    # Dev server (puerto 5173)
cd src/QueVendecion.Web && npm run build  # Build producción

# DB
# Borrar para recrear schema:
del src\QueVendecion.Api\QueVendecion.db
```

## Lo que falta (pendientes)

- **Notificaciones push:** El endpoint para guardar device token existe. Falta integrar Web Push (librería `web-push`), generar VAPID keys, registrar service worker para suscribir al usuario, y enviar notificaciones cuando cambie el estado de un pedido.
- **Seed de SuperAdmin:** Actualmente se crea SuperAdmin manualmente. Considerar seed automático en el primer run.
- **Autenticación SuperAdmin:** No hay UI de login para SuperAdmin, se usa curl. Considerar un endpoint dedicado.
- **Roles Staff:** El enum Staff=2 existe pero no se usa. Pendiente para cuando haya empleados.
- **Producción:** Pasar a PostgreSQL, configurar Docker + nginx.
- **Testing:** No hay tests unitarios ni de integración aún.
