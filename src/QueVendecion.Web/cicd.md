# Publicacion y CI/CD

El ejemplo anterior de copiar binarios y reiniciar directamente ya no es valido
para un backend con datos persistentes. En `srvubu01` (22 de octubre de 2026)
el servicio usa SQLite en `Development`; seguir
[despliegue-sqlite-tiendas.md](../../docs/despliegue-sqlite-tiendas.md) para
preservar base, WAL y configuración. Los pasos PostgreSQL siguientes son la
ruta histórica para un destino PostgreSQL confirmado.

Seguir [migraciones-produccion.md](../../docs/migraciones-produccion.md):

1. Backup de PostgreSQL y ensayo de restauracion.
2. Detener escrituras y desplegar el codigo del release.
3. Aplicar migraciones incrementales revisadas (baseline verificado una sola vez).
4. Verificar esquema, historial y preservacion de datos.
5. Arrancar/reiniciar el servicio.
6. Verificar integridad, login, menu y pedidos existentes.

El pipeline debe detenerse ante cualquier error. No borrar ni recrear la base,
no ejecutar migraciones al arrancar la API y no publicar una SQLite de desarrollo
en produccion. Para frontend sin cambios de backend, ejecutar npm run build y
publicar dist; esto no actualiza el esquema de la base.
