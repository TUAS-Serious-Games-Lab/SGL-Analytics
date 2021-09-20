CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920155909_InitialCreate') THEN
    CREATE TABLE "Applications" (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "ApiToken" character varying(64) NOT NULL,
        CONSTRAINT "PK_Applications" PRIMARY KEY ("Id")
    );
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920155909_InitialCreate') THEN
    CREATE TABLE "LogMetadata" (
        "Id" uuid NOT NULL,
        "AppId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "LocalLogId" uuid NOT NULL,
        "CreationTime" timestamp without time zone NOT NULL,
        "EndTime" timestamp without time zone NOT NULL,
        "UploadTime" timestamp without time zone NOT NULL,
        "FilenameSuffix" character varying(16) NOT NULL,
        "Complete" boolean NOT NULL,
        CONSTRAINT "PK_LogMetadata" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LogMetadata_Applications_AppId" FOREIGN KEY ("AppId") REFERENCES "Applications" ("Id") ON DELETE CASCADE
    );
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920155909_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Applications_Name" ON "Applications" ("Name");
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920155909_InitialCreate') THEN
    CREATE INDEX "IX_LogMetadata_AppId" ON "LogMetadata" ("AppId");
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920155909_InitialCreate') THEN
    CREATE INDEX "IX_LogMetadata_AppId_UserId" ON "LogMetadata" ("AppId", "UserId");
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920155909_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20210920155909_InitialCreate', '5.0.9');
    END IF;
END $$;
COMMIT;

