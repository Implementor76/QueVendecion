# Prompt del Agente — Qué Vendeción

> Este archivo es un prompt para que cualquier agente (o tú mismo en una nueva sesión) continúe el proyecto correctamente. Léelo antes de escribir código.

---

## Qué es este proyecto

**Qué Vendeción** es una PWA mobile-first para ayudar a pequeños negocios a organizar sus pedidos. Piensa en un sistema tipo WhatsApp: el vendedor recibe pedidos por una URL pública y los gestiona desde un panel simple.

## Stack obligatorio

- **Frontend:** HTML + CSS + JavaScript + Tailwind v4 + Vite (SPA, sin frameworks Vue/React/Angular)
- **Backend:** ASP.NET Core Web API + Controllers + EF Core
- **DB:** el código selecciona SQLite fuera de `Production` y PostgreSQL en `Production`. En `srvubu01` se confirmó `Development` y SQLite activa en `/opt/apps/quevendecion/QueVendecion.db` (22 de octubre de 2026). La arquitectura objetivo sigue siendo PostgreSQL para producción.
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
| `Tienda` | Negocio. Tiene datos de contacto y `OperacionJson` para recepción, modalidad, ventanas y entrega. |
| `UsuarioTienda` | Usuarios del sistema. `Email`, `PasswordHash`, `Rol` (SuperAdmin=0, Admin=1, Staff=2), `TiendaId` |
| `Cliente` | Clientes que piden. `Nombre`, `Telefono`, `DeviceToken` (para notificaciones futuras) |
| `Producto` | Item del menú con precio, inventario opcional, fechas de servicio y `EntregaPermitida`; `NULL` en filas anteriores requiere asignación explícita. |
| `Pedido` | Orden con estado, pago, modalidad, fecha y ventana de servicio, entrega y dirección opcionales, total e historial de estados. Los pedidos anteriores conservan `NULL` en campos nuevos. |
| `ItemPedido` | Línea del pedido. `PedidoId`, `ProductoId`, `Cantidad`, `PrecioUnitario` |

## Enums

- `RolUsuario`: SuperAdmin=0, Admin=1, Staff=2
- `EstadoPedido`: Recibido, Confirmado, EnPreparacion, Listo, Entregado, Cancelado. Desde Confirmado puede pasarse directamente a Entregado; también se admite omitir etapas intermedias de preparación.
- `ModalidadPedido`: AlMomento, Programado; `FormaEntrega`: Recoleccion, Domicilio; `EntregaProducto`: Recoleccion, Domicilio, Ambas
- `FormaPago`: Efectivo, Transferencia, Fiado
- `EstadoPago`: Pendiente, Pagado, Parcial

## Flujo de usuarios

### SuperAdmin
- Crea tiendas + admin en un solo paso: `POST /api/superadmin/tienda` con `{ tienda: {...}, adminEmail, adminPassword }`
- El SuperAdmin usa el login JWT existente y la ruta `/superadmin` para administrar tiendas.

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
- `GET /api/public/tienda/{slug}/menu` — productos disponibles, filtrables por fecha y entrega
- `GET /api/public/tienda/{slug}/opciones` — fechas y ventanas disponibles
- `POST /api/public/tienda/{slug}/cliente` — registro/login cliente
- `GET /api/public/tienda/{slug}/cliente/{telefono}/pedidos` — historial del cliente
- `PUT /api/public/tienda/{slug}/cliente/{telefono}/device-token` — guardar token push
- `POST /api/public/tienda/{slug}/pedido` — crear pedido

### Auth
- `POST /api/auth/login` — login con email + password → JWT
- `POST /api/auth/cambiar-password` — cambiar password (requiere JWT)

### Admin (requiere JWT)
- `GET /api/tiendas/mi-tienda` — datos de mi tienda
- `PUT /api/tiendas/mi-tienda/operacion` — reglas de recepción y servicio
- `PUT /api/tiendas/{id}` — actualizar tienda
- `GET /api/tiendas/mi-tienda/productos` — productos de mi tienda
- `POST /api/tiendas/mi-tienda/productos` — crear producto
- `PUT /api/tiendas/mi-tienda/productos/{id}` — actualizar producto
- `DELETE /api/tiendas/mi-tienda/productos/{id}` — eliminar producto
- `GET /api/pedidos/mi-tienda` — pedidos de mi tienda (filtro por estado y fecha de servicio)
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
| `/superadmin` | Administración de tiendas | JWT SuperAdmin |
| `/tienda/:slug` | Tienda pública (cliente) | No (usa localStorage) |

## Cosas técnicas que NO olvidar

1. **Los datos se preservan siempre:** los cambios de esquema se despliegan con migraciones incrementales; nunca borrar ni recrear la base. Ver la política de producción abajo.
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

# Herramientas de migración (desde la raíz)
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/QueVendecion.Infrastructure --startup-project src/QueVendecion.Infrastructure
```

## Política obligatoria de datos y despliegue

QueVendecion ya contiene tiendas, usuarios, clientes, productos, pedidos, items
y otros datos que deben preservarse. **Nunca proponer ni ejecutar el borrado o
la recreación de una base como estrategia de actualización del esquema.** Esta
regla aplica también a desarrollo cuando existen datos.

- La selección actual del proveedor depende del entorno, no de la extensión del archivo de base. El estado efectivo del servidor debe diagnosticarse antes de elegir un procedimiento de despliegue.
- `InventarioSchemaUpgrade` es únicamente un puente controlado para la SQLite
  de desarrollo existente. No se permite en PostgreSQL ni es la estrategia
  permanente para evolucionar el esquema. No agregarle futuros cambios de negocio.
- PostgreSQL evoluciona mediante EF Core Migrations en
  `src/QueVendecion.Infrastructure/Migrations/PostgreSql`, con snapshot y factory
  de diseño. No usar `EnsureCreated` ni ejecutar migraciones desde el arranque
  del servidor. Una base anterior sin historial necesita un baseline verificado,
  no ejecutar directamente la migración inicial sobre sus tablas.
- Para inventario, agregar `TieneInventario NOT NULL DEFAULT false`, `Stock`
  nullable y `MovimientosInventario`; no eliminar ni reconstruir `Productos`.
  Los productos anteriores y sus datos deben permanecer intactos.
- No renumerar enums persistidos ni hacer rollback destructivo. Si un cambio
  requiere transformar datos, hacerlo en una migración incremental revisada.

Orden para un destino PostgreSQL confirmado:

1. Respaldar PostgreSQL y comprobar restauración en una base aislada.
2. Desplegar código, manteniendo las escrituras detenidas durante el cambio.
3. Aplicar las migraciones incrementales previamente revisadas.
4. Verificar historial, esquema, registros y relaciones.
5. Arrancar o reiniciar la aplicación.
6. Verificar integridad y funcionamiento con datos existentes.

Seguir [migraciones-produccion.md](migraciones-produccion.md) para baseline,
scripts, backup, migraciones y validaciones. Publicar binarios no sustituye
la migración de la base. No tocar producción ni restaurar backups sobre ella
sin autorización explícita.

Si el servicio efectivo conserva SQLite, usar el diagnóstico y el ensayo en copia
de [despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md). Las filas de producto
existentes requieren asignar manualmente la entrega; para Jax, el hielo de $25
corresponde a recolección y el de $30 a domicilio, previa comprobación por ID.

## Lo que falta (pendientes)

- **Notificaciones push:** El endpoint para guardar device token existe. Falta integrar Web Push (librería `web-push`), generar VAPID keys, registrar service worker para suscribir al usuario, y enviar notificaciones cuando cambie el estado de un pedido.
- **Seed de SuperAdmin:** Solo crea la cuenta inicial si aún no existe y se proporcionan `SeedSuperAdmin__Email` y `SeedSuperAdmin__Password` mediante configuración privada. No hay credenciales literales de seed.
- **Autenticación SuperAdmin:** No hay UI de login para SuperAdmin, se usa curl. Considerar un endpoint dedicado.
- **Roles Staff:** El enum Staff=2 existe pero no se usa. Pendiente para cuando haya empleados.
- **Producción:** La arquitectura objetivo es PostgreSQL, pero el proveedor real del servicio informado aún no está comprobado. Mantener separados ambos procedimientos hasta obtener el diagnóstico.
- **Testing:** Hay pruebas de servicios, inventario, concurrencia y HTTP; ejecutar `dotnet test` antes de publicar.
