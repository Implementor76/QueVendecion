\set ON_ERROR_STOP on
BEGIN READ ONLY;
SET LOCAL search_path = public;
DO $verify$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20260917143552_AgregarInventario') THEN
        RAISE EXCEPTION 'Falta la migracion AgregarInventario en el historial.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Productos'
          AND column_name = 'TieneInventario' AND data_type = 'boolean'
          AND is_nullable = 'NO' AND column_default = 'false')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Productos'
          AND column_name = 'Stock' AND data_type = 'integer' AND is_nullable = 'YES')
       OR to_regclass('public."MovimientosInventario"') IS NULL THEN
        RAISE EXCEPTION 'El esquema de inventario no coincide con la migracion.';
    END IF;
    IF EXISTS (SELECT 1 FROM "Productos" WHERE "TieneInventario" AND ("Stock" IS NULL OR "Stock" < 0)) THEN
        RAISE EXCEPTION 'Hay stock invalido.';
    END IF;
    IF EXISTS (SELECT 1 FROM "Productos" p LEFT JOIN "Tiendas" t ON t."Id" = p."TiendaId" WHERE t."Id" IS NULL)
       OR EXISTS (SELECT 1 FROM "UsuariosTienda" u LEFT JOIN "Tiendas" t ON t."Id" = u."TiendaId" WHERE t."Id" IS NULL)
       OR EXISTS (SELECT 1 FROM "Pedidos" p LEFT JOIN "Tiendas" t ON t."Id" = p."TiendaId" WHERE t."Id" IS NULL)
       OR EXISTS (SELECT 1 FROM "Pedidos" p LEFT JOIN "Clientes" c ON c."Id" = p."ClienteId" WHERE p."ClienteId" IS NOT NULL AND c."Id" IS NULL)
       OR EXISTS (SELECT 1 FROM "ItemsPedido" i LEFT JOIN "Pedidos" p ON p."Id" = i."PedidoId" LEFT JOIN "Productos" pr ON pr."Id" = i."ProductoId" WHERE p."Id" IS NULL OR pr."Id" IS NULL)
       OR EXISTS (SELECT 1 FROM "MovimientosInventario" m LEFT JOIN "Productos" p ON p."Id" = m."ProductoId" LEFT JOIN "Pedidos" pe ON pe."Id" = m."PedidoId" WHERE p."Id" IS NULL OR (m."PedidoId" IS NOT NULL AND pe."Id" IS NULL)) THEN
        RAISE EXCEPTION 'Se detectaron relaciones huerfanas. No arrancar la aplicacion.';
    END IF;
END $verify$;
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
SELECT 'Tiendas' AS tabla, COUNT(*) AS registros FROM "Tiendas"
UNION ALL SELECT 'UsuariosTienda', COUNT(*) FROM "UsuariosTienda"
UNION ALL SELECT 'Clientes', COUNT(*) FROM "Clientes"
UNION ALL SELECT 'Productos', COUNT(*) FROM "Productos"
UNION ALL SELECT 'Pedidos', COUNT(*) FROM "Pedidos"
UNION ALL SELECT 'ItemsPedido', COUNT(*) FROM "ItemsPedido"
UNION ALL SELECT 'MovimientosInventario', COUNT(*) FROM "MovimientosInventario";
COMMIT;
