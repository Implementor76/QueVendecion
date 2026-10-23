-- One-time baseline for the existing PRE-INVENTORY PostgreSQL schema.
-- Run only after backup, restore rehearsal and schema review. No business data changes.
-- This records the initial migration WITHOUT running its CREATE TABLE statements.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL search_path = public;
SET LOCAL lock_timeout = '10s';
SELECT pg_advisory_xact_lock(hashtext('quevendecion-schema'));
LOCK TABLE "Clientes", "Tiendas", "Pedidos", "Productos", "UsuariosTienda", "ItemsPedido" IN ACCESS EXCLUSIVE MODE;
DO $baseline$
BEGIN
    IF EXISTS (
        SELECT 1 FROM (VALUES
            ('Clientes', 'Id', 'integer', true),
            ('Clientes', 'Nombre', 'character varying(100)', true),
            ('Clientes', 'Telefono', 'character varying(20)', false),
            ('Clientes', 'DeviceToken', 'text', false),
            ('Clientes', 'CreadoEn', 'timestamp with time zone', true),
            ('Tiendas', 'Id', 'integer', true),
            ('Tiendas', 'Nombre', 'character varying(100)', true),
            ('Tiendas', 'Slug', 'character varying(50)', true),
            ('Tiendas', 'Direccion', 'character varying(200)', false),
            ('Tiendas', 'Telefono', 'character varying(20)', false),
            ('Tiendas', 'CreadaEn', 'timestamp with time zone', true),
            ('Pedidos', 'Id', 'integer', true),
            ('Pedidos', 'TiendaId', 'integer', true),
            ('Pedidos', 'ClienteId', 'integer', false),
            ('Pedidos', 'ClienteNombre', 'character varying(100)', false),
            ('Pedidos', 'Estado', 'integer', true),
            ('Pedidos', 'FormaPago', 'integer', true),
            ('Pedidos', 'EstadoPago', 'integer', true),
            ('Pedidos', 'Total', 'numeric(18,2)', true),
            ('Pedidos', 'Observaciones', 'character varying(500)', false),
            ('Pedidos', 'CreadoEn', 'timestamp with time zone', true),
            ('Pedidos', 'ActualizadoEn', 'timestamp with time zone', false),
            ('Productos', 'Id', 'integer', true),
            ('Productos', 'TiendaId', 'integer', true),
            ('Productos', 'Nombre', 'character varying(100)', true),
            ('Productos', 'Descripcion', 'character varying(500)', false),
            ('Productos', 'Precio', 'numeric(18,2)', true),
            ('Productos', 'Disponible', 'boolean', true),
            ('Productos', 'ImagenUrl', 'character varying(500)', false),
            ('Productos', 'CreadoEn', 'timestamp with time zone', true),
            ('UsuariosTienda', 'Id', 'integer', true),
            ('UsuariosTienda', 'TiendaId', 'integer', true),
            ('UsuariosTienda', 'Nombre', 'character varying(100)', true),
            ('UsuariosTienda', 'Email', 'character varying(150)', true),
            ('UsuariosTienda', 'PasswordHash', 'text', true),
            ('UsuariosTienda', 'Rol', 'integer', true),
            ('UsuariosTienda', 'Activo', 'boolean', true),
            ('UsuariosTienda', 'CreadoEn', 'timestamp with time zone', true),
            ('ItemsPedido', 'Id', 'integer', true),
            ('ItemsPedido', 'PedidoId', 'integer', true),
            ('ItemsPedido', 'ProductoId', 'integer', true),
            ('ItemsPedido', 'Cantidad', 'integer', true),
            ('ItemsPedido', 'PrecioUnitario', 'numeric(18,2)', true)
        ) AS expected(tbl, col, typ, required)
        LEFT JOIN pg_namespace ns ON ns.nspname = 'public'
        LEFT JOIN pg_class c ON c.relnamespace = ns.oid AND c.relname = expected.tbl AND c.relkind = 'r'
        LEFT JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = expected.col AND NOT a.attisdropped AND a.attnum > 0
        WHERE a.attname IS NULL OR format_type(a.atttypid, a.atttypmod) <> expected.typ OR a.attnotnull <> expected.required
    ) THEN
        RAISE EXCEPTION 'Baseline refused: missing or incompatible columns. Compare the full schema with EsquemaAnteriorInventario.';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Productos' AND column_name IN ('Stock', 'TieneInventario'))
       OR to_regclass('public."MovimientosInventario"') IS NOT NULL THEN
        RAISE EXCEPTION 'Baseline refused: inventory already exists or is partial. Review this database separately; never drop columns to force baseline.';
    END IF;
    IF EXISTS (
        SELECT 1 FROM (VALUES ('Clientes'), ('Tiendas'), ('Pedidos'), ('Productos'), ('UsuariosTienda'), ('ItemsPedido')) AS expected(tbl)
        WHERE NOT EXISTS (
            SELECT 1 FROM pg_constraint pk
            JOIN pg_attribute a ON a.attrelid = pk.conrelid AND a.attnum = ANY(pk.conkey)
            WHERE pk.conrelid = to_regclass(format('public.%I', expected.tbl))
              AND pk.contype = 'p' AND cardinality(pk.conkey) = 1 AND a.attname = 'Id'
        ) OR NOT EXISTS (
            SELECT 1 FROM information_schema.columns c WHERE c.table_schema = 'public'
            AND c.table_name = expected.tbl AND c.column_name = 'Id'
            AND (c.is_identity = 'YES' OR c.column_default LIKE 'nextval(%')
        )
    ) THEN RAISE EXCEPTION 'Baseline refused: primary key or identity/sequence differs.'; END IF;
    IF EXISTS (
        SELECT 1 FROM (VALUES ('Pedidos', 'ClienteId', 'Clientes', 'a'), ('Pedidos', 'TiendaId', 'Tiendas', 'c'), ('Productos', 'TiendaId', 'Tiendas', 'c'), ('UsuariosTienda', 'TiendaId', 'Tiendas', 'c'), ('ItemsPedido', 'PedidoId', 'Pedidos', 'c'), ('ItemsPedido', 'ProductoId', 'Productos', 'c')) AS expected(tbl, col, parent, deletion)
        WHERE NOT EXISTS (
            SELECT 1 FROM pg_constraint fk
            JOIN pg_attribute a ON a.attrelid = fk.conrelid AND a.attnum = ANY(fk.conkey)
            JOIN pg_attribute target ON target.attrelid = fk.confrelid AND target.attnum = ANY(fk.confkey)
            WHERE fk.conrelid = to_regclass(format('public.%I', expected.tbl)) AND fk.contype = 'f'
              AND fk.confrelid = to_regclass(format('public.%I', expected.parent))
              AND cardinality(fk.conkey) = 1 AND cardinality(fk.confkey) = 1
              AND a.attname = expected.col AND target.attname = 'Id' AND fk.confdeltype::text = expected.deletion
              AND fk.convalidated
        )
    ) THEN RAISE EXCEPTION 'Baseline refused: missing or incompatible foreign keys.'; END IF;
    IF EXISTS (
        SELECT 1 FROM (VALUES ('ItemsPedido', 'PedidoId', false), ('ItemsPedido', 'ProductoId', false), ('Pedidos', 'ClienteId', false), ('Pedidos', 'TiendaId', false), ('Productos', 'TiendaId', false), ('Tiendas', 'Slug', true), ('UsuariosTienda', 'Email', true), ('UsuariosTienda', 'TiendaId', false)) AS expected(tbl, col, unique_index)
        WHERE NOT EXISTS (
            SELECT 1 FROM pg_index ix
            JOIN pg_attribute a ON a.attrelid = ix.indrelid AND a.attnum = ix.indkey[0]
            WHERE ix.indrelid = to_regclass(format('public.%I', expected.tbl))
              AND a.attname = expected.col AND ix.indnkeyatts = 1 AND ix.indisunique = expected.unique_index
              AND ix.indisvalid AND ix.indpred IS NULL AND ix.indexprs IS NULL
        )
    ) THEN RAISE EXCEPTION 'Baseline refused: missing or incompatible indexes.'; END IF;
END $baseline$;
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
    "ProductVersion" character varying(32) NOT NULL
);
DO $history$
BEGIN
    IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory") THEN
        RAISE EXCEPTION 'Baseline refused: migration history is not empty. Inspect it before proceeding.';
    END IF;
END $history$;
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260917143531_EsquemaAnteriorInventario', '9.0.20');
COMMIT;
