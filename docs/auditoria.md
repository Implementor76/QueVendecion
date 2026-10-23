# Auditoría de Seguridad y Arquitectura — QuéVendecion

**Fecha:** 19 de julio, 2026
**Alcance:** API (.NET), Frontend (Vite/Vanilla JS), Dominio, Infraestructura
**Total de hallazgos:** 27

**Nota de vigencia (21 de octubre de 2026):** este documento conserva los
hallazgos de julio como registro histórico; sus referencias a código, rutas y
proveedor no son una evaluación actual. Para la revisión de tiendas, estados,
inventario y despliegue, véase [tiendas-hallazgos.md](tiendas-hallazgos.md) y
[despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md). Los hallazgos de
seguridad no se consideran cerrados por esta revisión funcional.

---

## Tabla de contenidos

1. [Resumen ejecutivo](#resumen-ejecutivo)
2. [Críticos](#criticos)
3. [Altos](#altos)
4. [Medios](#medios)
5. [Bajos](#bajos)
6. [Buenas prácticas existentes](#buenas-practicas-existenes)

---

## Resumen ejecutivo

La aplicación tiene vulnerabilidades de seguridad que deben resolverse antes de producción. Los problemas más urgentes son: un secret JWT hardcodeado en el código fuente, controllers sin autenticación que exponen operaciones admin al público, XSS masivo en el frontend, y cero validación de entrada en los DTOs.

| Severidad | Cantidad | Tema principal |
|-----------|----------|----------------|
| Crítico | 5 | Auth bypass, XSS, acceso admin sin autenticación |
| Alto | 8 | IDOR, sin roles, sin rate limiting, JWT longevo |
| Medio | 7 | Race conditions, null refs, sin transacciones, sin logging |
| Bajo | 8 | Configuración, versiones, SQLite, CORS |

---

## Críticos

### C1. Secret JWT hardcodeado en el código fuente

**Archivo:** `src/QueVendecion.Api/appsettings.json:13`, `Program.cs:28`, `AuthService.cs:80`

**Qué se encontró:**
La clave de firma JWT `QueVendecion_SecretKey_2024_MVP_!` está escrita literalmente en tres lugares:
- `appsettings.json` linea 13: `"Key": "QueVendecion_SecretKey_2024_MVP_!"`
- `Program.cs` linea 28: fallback `?? "QueVendecion_SecretKey_2024_MVP_!"`
- `AuthService.cs` linea 80: fallback idéntico

El archivo `appsettings.json` está commiteado en git (se verifica con `git log`).

**Problema/Impacto:**
Cualquier persona con acceso al repositorio puede forjar tokens JWT válidos para cualquier usuario, cualquier rol, cualquier tienda. Un atacante puede impersonar al SuperAdmin, crear tiendas, borrar productos, o acceder a datos de clientes. Es un bypass completo de autenticación.

**Cómo resolverlo:**
1. Mover la clave a User Secrets (`dotnet user-secrets set "Jwt:Key" "nueva-clave-aleatoria"`) o variables de entorno.
2. Eliminar los fallbacks hardcodeados en `Program.cs:28` y `AuthService.cs:80` (que null si no existe la config).
3. Generar una clave aleatoria de al menos 32 caracteres.
4. Hacer force-push para limpiar el historial de git.

```csharp
// Program.cs — eliminar el fallback, fallar si no existe
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured");
```

---

### C2. SuperAdminController sin autenticación

**Archivo:** `src/QueVendecion.Api/Controllers/SuperAdminController.cs:10-12`

**Qué se encontró:**
La clase `SuperAdminController` no tiene `[Authorize]` en la clase ni en ningún método. Los endpoints están completamente abiertos:
- `POST api/superadmin/tienda` — crea tiendas + usuarios admin
- `GET api/superadmin/tiendas` — lista todas las tiendas

**Problema/Impacto:**
Cualquiera en internet puede:
- Crear tiendas y cuentas admin con `curl -X POST http://target/api/superadmin/tienda`
- Enumerar todas las tiendas del sistema
- Crear una cuenta admin y tomar control total de una tienda

**Cómo resolverlo:**
```csharp
[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "SuperAdmin")]  // agregar esto
public class SuperAdminController : ControllerBase
```
Nota: el enum `RolUsuario.SuperAdmin` existe pero nunca se valida en ningún lado.

---

### C3. ProductosController sin autenticación

**Archivo:** `src/QueVendecion.Api/Controllers/ProductosController.cs:8-9`

**Qué se encontró:**
El controller `ProductosController` en la ruta `api/tiendas/{tiendaId}/productos` no tiene `[Authorize]`. Los 5 endpoints son públicos:
- `GET` — listar productos
- `GET {id}` — ver un producto
- `POST` — crear producto
- `PUT {id}` — modificar producto
- `DELETE {id}` — borrar producto

**Problema/Impacto:**
Un atacante puede:
- Leer el catálogo de cualquier tienda iterando IDs
- Crear productos falsos en cualquier tienda
- Cambiar precios (fraude financiero)
- Borrar todo el inventario de una tienda

```bash
curl -X DELETE http://target/api/tiendas/1/productos/5
```

**Cómo resolverlo:**
```csharp
[Authorize]  // agregar en la clase
[ApiController]
[Route("api/tiendas/{tiendaId}/[controller]")]
public class ProductosController : ControllerBase
```
Además, verificar que el `tiendaId` del path coincida con el `TiendaId` del token JWT del usuario.

---

### C4. ClientesController sin autenticación

**Archivo:** `src/QueVendecion.Api/Controllers/ClientesController.cs:7-9`

**Qué se encontró:**
`ClientesController` no tiene `[Authorize]`. Endpoints públicos:
- `GET api/clientes/telefono/{telefono}` — buscar cliente por teléfono
- `POST api/clientes` — crear cliente

**Problema/Impacto:**
Cualquiera puede:
- Buscar cualquier cliente por número de teléfono (PII expuesto)
- Crear registros de cliente arbitrarios
- Enumerar la base de clientes

**Cómo resolverlo:**
Eliminar este controller — la funcionalidad ya existe en `PublicController` (que es el endpoint público para clientes). Si se necesita para admin, agregar `[Authorize(Roles = "Admin,SuperAdmin")]`.

---

### C5. XSS masivo — datos del servidor inyectados en innerHTML

**Archivos:** Todas las páginas — `menu.js`, `pedidos.js`, `tienda.js`, `tiendaCliente.js`

**Qué se encontró:**
Los datos que vienen del servidor se insertan directamente en `innerHTML` sin ningún escaping. Ejemplos:

- `tiendaCliente.js:54`: `<p>La tienda "${slug}" no existe</p>` — el `slug` viene de la URL
- `tiendaCliente.js:70-71`: `tienda.nombre`, `tienda.direccion` en template literals
- `menu.js:148-149`: `p.nombre`, `p.descripcion` del servidor
- `pedidos.js:122-135`: `p.clienteNombre`, `i.productoNombre`
- `tienda.js:137-140`: `value="${tienda.nombre}"` — XSS en atributos HTML

**Problema/Impacto:**
Si un atacante logra insertar `<script>alert('hack')</script>` en el nombre de un producto o tienda (via C3), ese código se ejecuta en el navegador de todos los usuarios que visiten esa tienda. Con XSS se puede:
- Robar el token JWT de `localStorage`
- Tomar control de la sesión del admin
- Redirigir a phishing
- Inyectar keyloggers

El vector de ataque más peligroso es por la URL: `/tienda/"><img src=x onerror=alert(1)>` inyecta JS directamente.

**Cómo resolverlo:**
Crear una función de escaping y usarla en TODAS las interpolaciones de datos del servidor:

```javascript
// utils/sanitize.js
export function escapeHtml(str) {
  if (str == null) return '';
  const div = document.createElement('div');
  div.textContent = String(str);
  return div.innerHTML;
}
```

Luego reemplazar todas las interpolaciones:
```javascript
// Antes (vulnerable):
`<p>${tienda.nombre}</p>`

// Después (seguro):
`<p>${escapeHtml(tienda.nombre)}</p>`
```

---

## Altos

### H1. IDOR en PedidosController — sin verificación de ownership

**Archivo:** `src/QueVendecion.Api/Controllers/PedidosController.cs:31-53`

**Qué se encontró:**
Los endpoints `ObtenerPorId`, `ActualizarEstado` y `ActualizarEstadoPago` reciben un `id` de la URL y operan directamente sobre él, sin verificar que el pedido pertenezca a la tienda del usuario autenticado.

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<PedidoDto>> ObtenerPorId(int id)
{
    var pedido = await _pedidoService.ObtenerPorIdAsync(id);
    if (pedido == null) return NotFound();
    return pedido;  // No verifica pedido.TiendaId == GetTiendaId()
}
```

**Problema/Impacto:**
Un usuario autenticado de la tienda #2 puede:
- Ver pedidos de la tienda #1: `GET /api/pedidos/1`
- Cambiar el estado de pedidos de otra tienda: `PATCH /api/pedidos/1/estado`
- Marcar como pagados pedidos que no le pertenecen

**Cómo resolverlo:**
Verificar ownership después de obtener el pedido:
```csharp
var pedido = await _pedidoService.ObtenerPorIdAsync(id);
if (pedido == null) return NotFound();
if (pedido.TiendaId != GetTiendaId()) return Forbid();
```

---

### H2. IDOR en TiendasController — productos sin ownership check

**Archivo:** `src/QueVendecion.Api/Controllers/TiendasController.cs:58-72`

**Qué se encontró:**
`ActualizarProducto` y `EliminarProducto` aceptan un `id` de producto pero nunca verifican que ese producto pertenezca a la tienda del usuario autenticado.

**Problema/Impacto:**
Un admin de la tienda #2 puede modificar o borrar productos de la tienda #1 usando directamente el ID del producto.

**Cómo resolverlo:**
```csharp
[HttpPut("mi-tienda/productos/{id}")]
public async Task<ActionResult<ProductoDto>> ActualizarProducto(int id, [FromBody] CrearProductoDto dto)
{
    var tiendaId = GetTiendaId();
    var producto = await _productoService.ObtenerPorIdAsync(id);
    if (producto == null) return NotFound();
    if (producto.TiendaId != tiendaId) return Forbid();

    var actualizado = await _productoService.ActualizarAsync(id, dto);
    return actualizado;
}
```

---

### H3. Sin autorización por roles

**Archivos:** Todos los controllers

**Qué se encontró:**
El enum `RolUsuario` define `SuperAdmin`, `Admin` y `Staff`, y el token JWT incluye el rol como claim. Pero **ningún endpoint** usa `[Authorize(Roles = "...")]`. Un miembro de Staff puede:
- Cambiar estado de pedidos
- Modificar precios de productos
- Cambiar contraseña de admin
- Acceder a todo

**Problema/Impacto:**
Escalada de privilegios — un empleado temporal tiene los mismos poderes que el dueño de la tienda.

**Cómo resolverlo:**
Definir permisos por rol:
| Endpoint | SuperAdmin | Admin | Staff |
|----------|:----------:|:-----:|:-----:|
| CRUD tiendas | ✅ | ❌ | ❌ |
| CRUD productos | ✅ | ✅ | ❌ |
| Ver pedidos | ✅ | ✅ | ✅ |
| Cambiar estado pedido | ✅ | ✅ | ✅ |
| Cambiar estado pago | ✅ | ✅ | ❌ |
| Cambiar contraseña | ✅ | ✅ | ❌ |

---

### H4. Sin validación en DTOs

**Archivos:** Todos los DTOs en `src/QueVendecion.Application/DTOs/`

**Qué se encontró:**
Ningún DTO tiene DataAnnotation attributes. Sin `[Required]`, `[StringLength]`, `[Range]`, `[EmailAddress]`, ni `[RegularExpression]`. Ejemplos de lo que se acepta actualmente:

| DTO | Campo | Problema |
|-----|-------|----------|
| `CrearItemPedidoDto` | `Cantidad` | Acepta 0, negativos (fraude) |
| `CrearProductoDto` | `Precio` | Acepta negativos |
| `CrearPedidoDto` | `Items` | Acepta lista vacía (pedido sin productos) |
| `LoginDto` | `Password` | Acepta vacío |
| `RegistrarUsuarioDto` | `Password` | Sin mínimo de longitud |
| `CrearTiendaConAdminDto` | `AdminPassword` | Sin requisitos de complejidad |
| Todos los `Nombre` | — | Sin límite de longitud |
| `LoginDto` | `Email` | Sin validación de formato |

**Problema/Impacto:**
- Fraude: cantidades negativas generan créditos falsos
- Corrupción de datos: campos vacíos donde deberían tener contenido
- Ataques de denial of service: strings de 1MB en campos de texto
- Autenticación débil: passwords de un carácter

**Cómo resolverlo:**
Agregar DataAnnotation a cada DTO:
```csharp
public class CrearItemPedidoDto
{
    [Required]
    public int ProductoId { get; set; }

    [Range(1, 999)]
    public int Cantidad { get; set; }
}

public class CrearProductoDto
{
    [Required, StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [Range(0, 999999.99)]
    public decimal Precio { get; set; }
    // ...
}

public class RegistrarUsuarioDto
{
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
    // ...
}
```
Y agregar `[ApiController]` (ya está) que habilita la validación automática del ModelState.

---

### H5. Sin rate limiting

**Archivo:** `src/QueVendecion.Api/Program.cs` (archivo completo)

**Qué se encontró:**
No hay middleware de rate limiting configurado. Ningún endpoint tiene protección contra abuso. El endpoint de login (`POST api/auth/login`) es el más vulnerable.

**Problema/Impacto:**
- Brute force: intentos ilimitados de contraseña
- Credential stuffing: probar listas de credenciales filtradas
- Denial of service: saturar la BD con requests

**Cómo resolverlo:**
```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(5);
        opt.PermitLimit = 5;
    });
});

// En el endpoint de login:
[HttpPost("login")]
[EnableRateLimiting("auth")]
public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
```

---

### H6. JWT dura 7 días sin refresh ni revocación

**Archivo:** `src/QueVendecion.Infrastructure/Services/AuthService.cs:94`

**Qué se encontró:**
```csharp
expires: DateTime.UtcNow.AddDays(7)
```
El token dura una semana. No hay refresh tokens, no hay revocación, no hay blacklist. Cambiar la contraseña no invalida tokens existentes.

**Problema/Impacto:**
Si un token es robado (via XSS, robo de dispositivo, etc.), el atacante tiene acceso completo por 7 días sin posibilidad de invalidarlo.

**Cómo resolverlo:**
1. Reducir el expiry a 1-4 horas: `expires: DateTime.UtcNow.AddHours(4)`
2. Implementar refresh tokens (token de corta vida + token largo para refrescar)
3. Agregar un endpoint de logout que invalide el token (requiere blacklist o redis)

---

### H7. CDN sin SRI y sin Content Security Policy

**Archivo:** `src/QueVendecion.Web/index.html:14`

**Qué se encontró:**
El script de Lucide se carga desde unpkg.com sin atributos de integridad:
```html
<script src="https://unpkg.com/lucide@0.460.0/dist/umd/lucide.min.js"></script>
```
No hay meta tag de Content Security Policy en el HTML.

**Problema/Impacto:**
- Si unpkg.com es comprometido o el paquete es reemplazado (typosquatting), código malicioso se ejecuta en todos los usuarios
- Sin CSP, cualquier script inyectado por XSS se ejecuta sin restricciones

**Cómo resolverlo:**
```html
<!-- Agregar integrity y crossorigin -->
<script
  src="https://unpkg.com/lucide@0.460.0/dist/umd/lucide.min.js"
  integrity="sha384-GENERAR_HASH_AQUI"
  crossorigin="anonymous">
</script>

<!-- Agregar CSP -->
<meta http-equiv="Content-Security-Policy"
  content="default-src 'self'; script-src 'self' https://unpkg.com; style-src 'self' 'unsafe-inline'; img-src 'self' data:;">
```

Para generar el hash: `openssl dgst -sha384 lucide.min.js | awk '{print $2}' | xxd -r -p | base64`

---

### H8. JWT almacenado en localStorage

**Archivo:** `src/QueVendecion.Web/src/utils/auth.js:4,9`

**Qué se encontró:**
El token JWT se almacena en `localStorage`:
```javascript
export function setAuth(data) {
  localStorage.setItem(AUTH_KEY, JSON.stringify(data));
}
```
Y se envía en cada request:
```javascript
// api.js
headers['Authorization'] = `Bearer ${token}`;
```

**Problema/Impacto:**
`localStorage` es accesible por cualquier JavaScript que se ejecute en la página. Combinado con los XSS del C5, un atacante puede robar el token con:
```javascript
localStorage.getItem('qv_auth')
```

**Cómo resolverlo:**
Opción 1 (ideal): Mover JWT a cookies HttpOnly/Secure/SameSide=Strict. El servidor las envía automáticamente y JavaScript no puede acceder.
Opción 2 (rápido): Mantener en localStorage pero mitigar XSS con la función `escapeHtml()` del C5 y CSP del H7.

---

## Medios

### M1. Race conditions — creación de cliente y usuario (TOCTOU)

**Archivos:** `ClienteService.cs:26-43`, `AuthService.cs:46-75`

**Qué se encontró:**
Ambos métodos siguen el patrón "leer luego escribir" sin concurrencia:

```csharp
// ClienteService.CrearOCrearAsync
var cliente = await _context.Clientes
    .FirstOrDefaultAsync(c => c.Telefono == dto.Telefono);

if (cliente != null)
    return MapToDto(cliente);

// Si dos requests concurrentes llegan aquí al mismo tiempo...
cliente = new Cliente { ... };
_context.Clientes.Add(cliente);
await _context.SaveChangesAsync();  // Ambos crean el registro
```

**Problema/Impacto:**
Dos requests simultáneos con el mismo teléfono pueden crear clientes duplicados. Lo mismo aplica para el registro de usuarios con el mismo email.

**Cómo resolverlo:**
1. Agregar unique constraint en la BD (ya existe para email, verificar para teléfono)
2. Envolver en transacción con aislamiento serializable
3. O usar `INSERT OR IGNORE` de SQLite

---

### M2. NullReferenceException — crashes potenciales

**Archivos:** `TiendaService.cs:42,59`, `SuperAdminController.cs:27`, `PedidoService.cs:70-84`

**Qué se encontró:**
Múltiples puntos sin null check:
- `dto.Slug.ToLower().Replace(" ", "-")` — si `dto.Slug` es null, crashea
- `foreach (var itemDto in dto.Items)` — si `Items` es null, crashea
- `int.Parse(User.FindFirst("TiendaId")?.Value ?? "0")` — si el claim no existe, usa tienda 0

**Problema/Impacto:**
Excepciones no manejadas que devuelven 500 con stack trace al cliente, exponiendo implementación interna.

**Cómo resolverlo:**
1. DataAnnotations en DTOs para prevenir nulls
2. Validación explícita en servicios: `ArgumentNullException.ThrowIfNull(...)`
3. Agregar middleware global de excepciones en `Program.cs`

---

### M3. Sin transacciones — operaciones multi-paso sin atomicidad

**Archivos:** Todos los services, especialmente `SuperAdminController.cs:22-53`

**Qué se encontró:**
`SuperAdminController.CrearTiendaConAdmin` hace dos `SaveChangesAsync`:
```csharp
_context.Tiendas.Add(tienda);
await _context.SaveChangesAsync();  // 1. Tienda creada

_context.UsuariosTienda.Add(usuarioAdmin);
await _context.SaveChangesAsync();  // 2. Si esto falla, tienda sin admin
```

Lo mismo con `PedidoService.CrearAsync` — lee productos y los inserta sin transacción.

**Problema/Impacto:**
Si el segundo `SaveChanges` falla, queda estado parcial: una tienda sin admin, un pedido incompleto, etc.

**Cómo resolverlo:**
```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    _context.Tiendas.Add(tienda);
    await _context.SaveChangesAsync();

    _context.UsuariosTienda.Add(usuarioAdmin);
    await _context.SaveChangesAsync();

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

---

### M4. Sin logging — cero visibilidad

**Archivos:** Todos los controllers y services — ningún `ILogger`

**Qué se encontró:**
No hay ningún uso de `ILogger<T>` en toda la aplicación. No se registra:
- Intentos de login fallidos
- Cambios de estado de pedidos
- Creación/eliminación de productos
- Errores internos

**Problema/Impacto:**
Imposible detectar ataques, investigar incidentes, o diagnosticar problemas en producción.

**Cómo resolverlo:**
```csharp
// En cada controller:
private readonly ILogger<PedidosController> _logger;

public PedidosController(IPedidoService pedidoService, ILogger<PedidosController> logger)
{
    _pedidoService = pedidoService;
    _logger = logger;
}

// En cada operación crítica:
_logger.LogWarning("Intento de acceso no autorizado a pedido {PedidoId} por tienda {TiendaId}", id, tiendaId);
_logger.LogInformation("Pedido {PedidoId} cambio estado de {EstadoAnterior} a {EstadoNuevo}", id, anterior, nuevo);
_logger.LogError(ex, "Error al crear pedido para tienda {TiendaId}", dto.TiendaId);
```

---

### M5. Teléfonos de clientes en URLs (PII en logs)

**Archivos:** `ClientesController.cs:18`, `PublicController.cs:54,61`

**Qué se encontró:**
Los números de teléfono aparecen en la ruta de la URL:
```
GET api/clientes/telefono/555-1234
PUT api/public/tienda/mystore/cliente/555-1234/device-token
GET api/public/tienda/mystore/cliente/555-1234/pedidos
```

**Problema/Impacto:**
Los servidores web, proxies, CDNs y el historial del navegador registran las URLs completas. Los números de teléfono (PII) quedan en logs de infraestructura, violando posibles regulaciones de privacidad.

**Cómo resolverlo:**
Mover el teléfono a query parameters o al body del request:
```
GET api/public/tienda/mystore/cliente/pedidos?telefono=555-1234
```

---

### M6. Sin manejo global de excepciones

**Archivo:** `src/QueVendecion.Api/Program.cs`

**Qué se encontró:**
No hay `app.UseExceptionHandler()`, no hay `ProblemDetails` filter, no hay middleware de errores. Las excepciones no manejadas devuelven stack traces en desarrollo.

**Problema/Impacto:**
- En desarrollo: stack traces completos expuestos (rutas internos, líneas de código, tipos de exception)
- En producción: 500 genéricos sin información útil para debugging

**Cómo resolverlo:**
```csharp
// Program.cs
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.Map("/error", (HttpContext context) =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    return Results.Problem(
        detail: app.Environment.IsDevelopment() ? exception?.Message : "Internal server error",
        statusCode: 500
    );
});
```

---

### M7. Service worker sin SRI ni error handling

**Archivo:** `src/QueVendecion.Web/index.html:21-23`, `public/sw.js:1`

**Qué se encontró:**
```javascript
// index.html
if ('serviceWorker' in navigator && location.hostname !== 'localhost') {
  navigator.serviceWorker.register('/sw.js');  // sin .catch()
}
```
El service worker se registra sin manejo de errores y el cache tiene un nombre hardcodeado `qv-v2`.

**Problema/Impacto:**
Si falla el registro, no hay feedback. El usuario no tiene soporte offline sin saberlo. Si el cache no se actualiza correctamente, contenido stale se sirve.

**Cómo resolverlo:**
```javascript
navigator.serviceWorker.register('/sw.js')
  .then(reg => {
    reg.addEventListener('updatefound', () => {
      console.log('Nueva versión del service worker disponible');
    });
  })
  .catch(err => console.warn('Service worker no registrado:', err));
```

---

## Bajos

### L1. AllowedHosts wildcard

**Archivo:** `src/QueVendecion.Api/appsettings.json:8`

**Qué:** `"AllowedHosts": "*"` acepta cualquier header `Host`.
**Impacto:** En ciertas configuraciones de reverse proxy, habilita ataques de host header injection.
**Fix:** Configurar con los dominios reales en producción.

---

### L2. Versiones de NuGet con wildcard

**Archivos:** `QueVendecion.Api.csproj`, `QueVendecion.Infrastructure.csproj`

**Qué:** Paquetes con versiones tipo `4.*`, `9.*`, `8.19.*`.
**Impacto:** Builds no reproducibles — un `dotnet restore` futuro puede descargar una versión no probada o comprometida.
**Fix:** Fijar versiones exactas: `<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />`.

---

### L3. SQLite como base de datos de producción

**Archivos:** `QueVendecion.Infrastructure.csproj:14`, `appsettings.json:10`

**Qué:** La conexión apunta a `QueVendecion.db` (SQLite).
**Impacto:** SQLite no soporta concurrencia de escritura, no tiene connection pooling, no tiene control de acceso por usuario. Bajo carga produce `database is locked`. El archivo de BD puede ser exfiltrado fácilmente.
**Fix:** Migrar a PostgreSQL para producción.

---

### L4. EnsureCreated() en cada inicio

**Archivo:** `src/QueVendecion.Api/Program.cs:60`

**Qué:** `db.Database.EnsureCreated()` corre en cada arranque.
**Impacto:** En producción, `EnsureCreated()` no puede aplicar migraciones — solo crea la BD si no existe. También crea infraestructura automáticamente, lo cual es un anti-patrón de seguridad.
**Fix:** Usar EF Core Migrations con un paso de migración separado en el pipeline de deploy.

---

### L5. CORS solo HTTP

**Archivo:** `src/QueVendecion.Api/Program.cs:49`

**Qué:** Los orígenes CORS son solo `http://localhost:5173` y `http://localhost:3000`.
**Impacto:** En producción con HTTPS, la configuración actual no funcionaría. El patrón actual facilita olvidar actualizar para producción.
**Fix:** Usar configuración basada en ambiente:
```csharp
policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>())
```

---

### L6. Sin Data Protection key ring

**Archivo:** `src/QueVendecion.Api/Program.cs`

**Qué:** No hay `AddDataProtection()` configurado con almacenamiento persistente.
**Impacto:** Si la app se escala a múltiples instancias o se reinicia frecuentemente, las claves de protección se regeneran, invalidando tokens/cookies existentes.
**Fix:** Configurar con `AddDataProtection().PersistKeysToFileSystem()` o similar.

---

### L7. LoginDto tiene campo TiendaId no usado

**Archivo:** `src/QueVendecion.Application/DTOs/AuthDto.cs:7`

**Qué:** `LoginDto.TiendaId` existe pero `AuthService.LoginAsync` nunca lo usa — busca usuario solo por email.
**Impacto:** Confusión de API — el frontend envía un campo que no tiene efecto. Un developer futuro podría asumir que restringe el login por tienda.
**Fix:** Eliminar `TiendaId` de `LoginDto`.

---

### L8. Roles auto-asignables en registro

**Archivo:** `src/QueVendecion.Infrastructure/Services/AuthService.cs:59`, `AuthDto.cs:25`

**Qué:** `RegistrarUsuarioDto.Rol` acepta cualquier valor. El service lo usa directamente: `Rol = dto.Rol == "Admin" ? RolUsuario.Admin : RolUsuario.Staff`.
**Impacto:** Si `RegistrarAsync` se expone en un controller futuro, un usuario podría auto-promoverse a Admin.
**Fix:** Eliminar `Rol` del DTO y hardcodear el rol en el service, o validarlo contra el rol del usuario que hace la petición.

---

## Buenas prácticas existentes

| Práctica | Dónde |
|----------|-------|
| BCrypt para hashing de passwords | `AuthService.cs:31,58`, `SuperAdminController.cs:40` |
| `.gitignore` excluye `*.db` y `appsettings.Development.json` | `.gitignore` |
| EF Core con queries parametrizadas (sin SQL injection) | Todos los services |
| Unique indexes en campos críticos | `UsuarioTiendaConfiguration.cs:15` (email), `TiendaConfiguration.cs` (slug) |
| `IsRequired()` y `HasMaxLength()` en configuraciones EF | Todos los archivos en `Configurations/` |
| HTTPS redirect middleware | `Program.cs:69` |
| Enum `RolUsuario` definido | `Domain/Enums/RolUsuario.cs` |
