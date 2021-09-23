﻿CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    CREATE TABLE "Applications" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "ApiToken" text NOT NULL,
        CONSTRAINT "PK_Applications" PRIMARY KEY ("Id")
    );
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    CREATE TABLE "ApplicationUserPropertyDefinitions" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "AppId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Type" integer NOT NULL,
        "Required" boolean NOT NULL,
        CONSTRAINT "PK_ApplicationUserPropertyDefinitions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ApplicationUserPropertyDefinitions_Applications_AppId" FOREIGN KEY ("AppId") REFERENCES "Applications" ("Id") ON DELETE CASCADE
    );
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    CREATE TABLE "UserRegistrations" (
        "Id" uuid NOT NULL,
        "AppId" uuid NOT NULL,
        "Username" text NOT NULL,
        "HashedSecret" text NOT NULL,
        CONSTRAINT "PK_UserRegistrations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserRegistrations_Applications_AppId" FOREIGN KEY ("AppId") REFERENCES "Applications" ("Id") ON DELETE CASCADE
    );
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    CREATE TABLE "ApplicationUserPropertyInstances" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "DefinitionId" integer NOT NULL,
        "UserId" uuid NOT NULL,
        "IntegerValue" integer NULL,
        "FloatingPointValue" double precision NULL,
        "StringValue" text NULL,
        "DateTimeValue" timestamp without time zone NULL,
        "GuidValue" uuid NULL,
        "JsonValue" text NULL,
        CONSTRAINT "PK_ApplicationUserPropertyInstances" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ApplicationUserPropertyInstances_ApplicationUserPropertyDef~" FOREIGN KEY ("DefinitionId") REFERENCES "ApplicationUserPropertyDefinitions" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ApplicationUserPropertyInstances_UserRegistrations_UserId" FOREIGN KEY ("UserId") REFERENCES "UserRegistrations" ("Id") ON DELETE CASCADE
    );
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Applications_Name" ON "Applications" ("Name");
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_ApplicationUserPropertyDefinitions_AppId_Name" ON "ApplicationUserPropertyDefinitions" ("AppId", "Name");
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_ApplicationUserPropertyInstances_DefinitionId_UserId" ON "ApplicationUserPropertyInstances" ("DefinitionId", "UserId");
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    CREATE INDEX "IX_ApplicationUserPropertyInstances_UserId" ON "ApplicationUserPropertyInstances" ("UserId");
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_UserRegistrations_AppId_Username" ON "UserRegistrations" ("AppId", "Username");
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20210920162505_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20210920162505_InitialCreate', '5.0.9');
    END IF;
END $$;
COMMIT;
