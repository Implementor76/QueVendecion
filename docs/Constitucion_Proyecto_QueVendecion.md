# Constitución del Proyecto - Qué Vendeción

## Arquitectura Tecnológica Oficial (MVP)

### Frontend

-   HTML5
-   CSS3
-   JavaScript (ES2023+)
-   Tailwind CSS v4
-   Vite
-   SPA + PWA
-   Sin frameworks (Vue, React, Angular o similares) durante el MVP.

### Backend

-   ASP.NET Core Web API.
-   Controllers tradicionales.
-   Comunicación exclusivamente mediante API HTTP.
-   El frontend y el backend evolucionan de forma independiente.

### Base de datos

Desarrollo: - SQLite

Producción: - PostgreSQL

El dominio nunca dependerá de un motor específico de base de datos.

**Nota de revisión (21 de octubre de 2026):** PostgreSQL sigue siendo la meta
arquitectónica indicada aquí. La instancia informada conserva un archivo SQLite;
su entorno y proveedor efectivos aún no se han comprobado. La selección actual
del código depende del entorno y no se modificó en esta revisión. Véase
[despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md) para diagnosticar y
ensayar en copia antes de decidir la publicación.

### Infraestructura

-   Ubuntu Server
-   Docker
-   nginx
-   PostgreSQL
-   Prometheus
-   Grafana

## Principios de Desarrollo

-   Primero entender el negocio.
-   Después diseñar.
-   Después programar.
-   La tecnología sirve al producto, no al revés.

## Principios de UX

-   Mobile First.
-   Una persona que sabe usar WhatsApp debe poder usar el sistema.
-   El humor puede usarse en textos secundarios, nunca en acciones
    críticas.

## Lenguaje del Negocio

Utilizar siempre el lenguaje del vendedor: - Tienda - Cliente - Menú -
Pedido - Fiado - Confirmado
