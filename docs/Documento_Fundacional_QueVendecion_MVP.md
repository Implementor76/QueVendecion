# Documento Fundacional - Qué Vendeción

## Objetivo

Ayudar a pequeños negocios que venden por pedido a organizar sus
pedidos.

## Arquitectura

Cliente (PWA/SPA) ↓ HTML + CSS + JavaScript + Tailwind + Vite

↓

ASP.NET Core Web API

↓

Application

↓

Domain

↓

Infrastructure (EF Core)

↓

SQLite (Desarrollo) / PostgreSQL (Producción)

**Nota de revisión (21 de octubre de 2026):** esta línea expresa el diseño
objetivo. La base efectiva del servicio informado y su entorno siguen pendientes
de diagnóstico; se reportó un archivo SQLite existente. La compatibilidad y el
ensayo en copia se describen en
[despliegue-sqlite-tiendas.md](despliegue-sqlite-tiendas.md).

## Formas de pago

Configurables por tienda.

Inicialmente: - Efectivo - Transferencia - Fiado

Cada pedido almacena: - Forma de pago - Estado de pago (Pendiente /
Pagado)

No existe módulo de cuentas por cobrar en el MVP.

## Diseño

Las decisiones visuales deberán basarse en referencias reales (capturas,
bocetos o imágenes) almacenadas en la documentación del proyecto para
mantener consistencia.
