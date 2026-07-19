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
