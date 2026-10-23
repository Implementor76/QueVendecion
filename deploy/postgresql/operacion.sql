START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    DROP INDEX "IX_Pedidos_TiendaId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    ALTER TABLE "Tiendas" ADD "OperacionJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    ALTER TABLE "Productos" ADD "FechasServicioJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    ALTER TABLE "Pedidos" ADD "Entrega" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    ALTER TABLE "Pedidos" ADD "FechaServicio" date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    ALTER TABLE "Pedidos" ADD "HorarioConfirmado" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    ALTER TABLE "Pedidos" ADD "Modalidad" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    ALTER TABLE "Pedidos" ADD "VentanaSolicitada" character varying(11);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    CREATE INDEX "IX_Pedidos_TiendaId_FechaServicio_Estado" ON "Pedidos" ("TiendaId", "FechaServicio", "Estado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022054127_AgregarOperacionTienda') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261022054127_AgregarOperacionTienda', '9.0.21');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022055059_AgregarDireccionEntrega') THEN
    ALTER TABLE "Pedidos" ADD "DireccionEntrega" character varying(300);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022055059_AgregarDireccionEntrega') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261022055059_AgregarDireccionEntrega', '9.0.21');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022060414_AsignarEntregaYRegistrarEstados') THEN
    ALTER TABLE "Productos" ADD "EntregaPermitida" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022060414_AsignarEntregaYRegistrarEstados') THEN
    ALTER TABLE "Pedidos" ADD "HistorialEstadosJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022060414_AsignarEntregaYRegistrarEstados') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261022060414_AsignarEntregaYRegistrarEstados', '9.0.21');
    END IF;
END $EF$;
COMMIT;
