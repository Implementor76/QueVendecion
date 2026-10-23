# Guía de Experiencias — Qué Vendeción

> Lecciones aprendidas, errores comunes y cosas que no olvidar. Si algo te parece obvio, probablemente te va a morder después.

---

## Errores que ya pasaron y cómo evitarlos

### 1. El 400 al crear pedido con enums
**Qué pasó:** El frontend envía `"formaPago": "Transferencia"` (string), pero ASP.NET por defecto espera el valor numérico `1` del enum. Resultado: validación falla, 400 Bad Request.

**Solución:** Agregar `JsonStringEnumConverter` en `Program.cs`:
```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```
**Regla:** Siempre que tengas enums en DTOs que se reciben del frontend, configura el conversor.

### 2. El puerto del API no es 5000
**Qué pasó:** Vite apuntaba a `http://localhost:5000` pero el API en realidad escucha en `http://localhost:5136` (según `launchSettings.json`). Las llamadas a la API fallaban silenciosamente.

**Solución:** Verificar el puerto real en `src/QueVendecion.Api/Properties/launchSettings.json` y configurar el proxy de Vite correctamente en `vite.config.js`.

### 3. DB vieja con schema nuevo
**Qué pasó:** `EnsureCreated()` no actualiza tablas existentes. Si agregas un campo nuevo a una entidad, la DB vieja no lo tiene y el ORM falla.

**Solución:** Preservar la base y aplicar cambios incrementales. En PostgreSQL usar EF Core Migrations; si la base anterior no tiene historial, hacer primero un baseline verificado según [migraciones-produccion.md](migraciones-produccion.md). Los puentes SQLite de inventario y operación son acotados; ensayar la actualización en una copia siguiendo [despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md). Nunca borrar ni recrear la base para actualizar el esquema.

### 4. `QueVendecion.Web` no está en el .sln
**Qué pasó:** Se asumió que `dotnet build` incluía el frontend. No — el frontend es un proyecto Node independiente con su propio `package.json`.

**Solución:** Para compilar todo hay que correr ambos:
```bash
dotnet build QueVendecion.sln               # Backend
cd src/QueVendecion.Web && npm run build    # Frontend
```

### 5. Services en Infrastructure, no en Application
**Qué pasó:** Se intentó poner los servicios en `Application/Services/`. Clean Architecture dice que `Application` solo tiene interfaces + DTOs, y `Infrastructure` tiene las implementaciones (porque dependen de EF Core).

**Solución:** Servicios van en `src/QueVendecion.Infrastructure/Services/`. La UI (Api) referencia Infrastructure para registrar dependencias en DI.

---

## Cosas que parecen obvias pero no lo son

### El JWT trae el TiendaId
El token del admin contiene el `TiendaId` como claim. El frontend lo extrae con `getTiendaId()` y los endpoints lo usan para filtrar datos. **Nunca** pedir el tiendaId al usuario — ya viene en el token.

### El login solo pide email
El admin ingresa email + password. El tiendaId se resuelve en el backend desde el email. Esto simplifica la UX (el admin no necesita "saber" a qué tienda pertenece).

### Los clientes se identifican por teléfono
El nombre y teléfono del cliente se guardan en localStorage bajo la key `qvd_client_{slug}`. Cada tienda tiene su propio espacio. Si el cliente cambia de teléfono, pierde el historial — aceptable para el MVP.

### El formulario de producto no cierra después de agregar
A diferencia de un modal, el form de agregar producto se queda abierto y muestra un mensaje de éxito. Esto es intencional — el vendedor suele agregar varios productos seguidos. El input de nombre se enfoca automáticamente.

### El bottom nav del cliente está en `tiendaCliente.js`
Toda la lógica del cliente (login, menú, pedidos, cuenta) está en un solo archivo. Es grande (~300 líneas) pero mantenerlo junto facilita la coherencia del UX. Si crece mucho, separar en módulos.

---

## Checklist antes de commit

- [ ] `dotnet build QueVendecion.sln` pasa sin errores
- [ ] `npm run build` en `src/QueVendecion.Web` pasa sin errores
- [ ] No hay secretos ni credenciales literales; JWT y seed inicial se inyectan por configuración privada
- [ ] Los endpoints nuevos están en el controller correcto (públicos vs admin vs superadmin)
- [ ] Si cambió el esquema, se respaldó la base existente y se probó la actualización incremental en una copia
- [ ] Los enums se envían como strings, no números
- [ ] El proxy de Vite apunta al puerto correcto del API

---

## Arquitectura de archivos (referencia rápida)

```
src/
├── QueVendecion.Domain/
│   ├── Entities/          # Tienda, Producto, Pedido, Cliente, ItemPedido, UsuarioTienda
│   └── Enums/             # RolUsuario, EstadoPedido, FormaPago, EstadoPago, ModalidadPedido, EntregaProducto
├── QueVendecion.Application/
│   ├── DTOs/              # PedidoDto, ProductoDto, TiendaDto, ClienteDto, etc.
│   └── Interfaces/        # ITiendaService, IProductoService, IPedidoService, etc.
├── QueVendecion.Infrastructure/
│   ├── Data/              # QueVendecionDbContext
│   └── Services/          # TiendaService, ProductoService, PedidoService, etc.
├── QueVendecion.Api/
│   ├── Controllers/       # TiendasController, PedidosController, PublicController, etc.
│   ├── Program.cs         # DI, JWT, CORS, verificación del esquema (producción migra en deploy)
│   └── appsettings.json   # ConnectionStrings, Jwt config
└── QueVendecion.Web/
    ├── src/
    │   ├── main.js        # Registro de rutas
    │   ├── pages/         # home, login, tienda, menu, pedidos, tiendaCliente
    │   ├── utils/         # api.js, auth.js, clientAuth.js, router.js
    │   └── style.css      # Tema de colores Tailwind v4
    ├── vite.config.js     # Proxy al API
    └── public/            # manifest.json, service worker, favicon
```

---

## Notas para futuras sesiones

### Notificaciones Push (próximo paso)
- Ya existe `DeviceToken` en la entidad `Cliente`
- Ya existe el endpoint `PUT /api/public/tienda/{slug}/cliente/{telefono}/device-token`
- Falta: instalar paquete `WebPush` en el backend, generar VAPID keys, crear servicio de envío, registrar service worker en el frontend, suscribir al usuario al cargar la tienda, y enviar notificación cuando el admin cambie el estado de un pedido.

### Seed de SuperAdmin
- Si no existe ninguno, el arranque solo crea SuperAdmin cuando recibe `SeedSuperAdmin__Email` y `SeedSuperAdmin__Password` por configuración privada. La contraseña exige al menos 12 caracteres. Si ya existe la cuenta, no se cambia.

### Testing
- Hay pruebas de servicios, inventario, operación, migraciones y HTTP. Ejecutar `dotnet test` y compilar el frontend antes de publicar.

### Producción
- PostgreSQL es la arquitectura objetivo. El proveedor efectivo del servicio informado debe diagnosticarse antes de desplegar; existe un procedimiento de ensayo para SQLite existente.
- Dockerizar el API
- nginx como reverse proxy
- HTTPS obligatorio
- Dominio y SSL

---

## Reglas de comunicación con el usuario

- Tono: WhatsApp-level, directo, sin formalidades excesivas
- Si el usuario pide algo que rompe la arquitectura, explicar por qué y proponer alternativa
- Si aparece una nueva regla de negocio, pausar y preguntar si actualizar la documentación
- Siempre mostrar el plan antes de implementar cambios grandes
- Hacer commit después de cada milestone funcional
