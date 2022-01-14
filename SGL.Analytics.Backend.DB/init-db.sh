#!/bin/sh
set -e

sed -i 's/host all all all md5/host all all all scram-sha-256/' /var/lib/postgresql/data/pg_hba.conf

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB"<<-EOSQL
	-- Create users that don't exist yet
	DO
	\$do\$
	BEGIN
		IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE  rolname = 'sgla_users') THEN
			CREATE USER sgla_users WITH ENCRYPTED PASSWORD '${USERREG_PW}';
		END IF;
	END
	\$do\$;

	DO
	\$do\$
	BEGIN
		IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE  rolname = 'sgla_users_admin') THEN
			CREATE USER sgla_users_admin WITH ENCRYPTED PASSWORD '${USERREG_ADM_PW}';
		END IF;
	END
	\$do\$;

	DO
	\$do\$
	BEGIN
		IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE  rolname = 'sgla_logs') THEN
			CREATE USER sgla_logs WITH ENCRYPTED PASSWORD '${LOGS_PW}';
		END IF;
	END
	\$do\$;

	DO
	\$do\$
	BEGIN
		IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE  rolname = 'sgla_logs_admin') THEN
			CREATE USER sgla_logs_admin WITH ENCRYPTED PASSWORD '${LOGS_ADM_PW}';
		END IF;
	END
	\$do\$;

	-- Create databases that don't exist yet
	SELECT 'CREATE DATABASE sgla_users' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'sgla_users')\gexec
	SELECT 'CREATE DATABASE sgla_logs' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'sgla_logs')\gexec

	REVOKE ALL ON DATABASE sgla_users FROM public;
	REVOKE ALL ON DATABASE sgla_logs FROM public;

	\c sgla_users
	REVOKE ALL ON SCHEMA public FROM public;
	GRANT CONNECT ON DATABASE sgla_users TO sgla_users, sgla_users_admin;
	GRANT USAGE ON SCHEMA public TO sgla_users;
	GRANT ALL ON SCHEMA public TO sgla_users_admin;
	ALTER DEFAULT PRIVILEGES FOR USER sgla_users_admin IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO sgla_users;
	ALTER DEFAULT PRIVILEGES FOR USER sgla_users_admin IN SCHEMA public GRANT ALL ON SEQUENCES TO sgla_users;
	REASSIGN OWNED BY sgla_users TO sgla_users_admin;

	\c sgla_logs
	REVOKE ALL ON SCHEMA public FROM public;
	GRANT CONNECT ON DATABASE sgla_logs TO sgla_logs, sgla_logs_admin;
	GRANT USAGE ON SCHEMA public TO sgla_logs;
	GRANT ALL ON SCHEMA public TO sgla_logs_admin;
	ALTER DEFAULT PRIVILEGES FOR USER sgla_logs_admin IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO sgla_logs;
	ALTER DEFAULT PRIVILEGES FOR USER sgla_logs_admin IN SCHEMA public GRANT ALL ON SEQUENCES TO sgla_logs;
	REASSIGN OWNED BY sgla_logs TO sgla_logs_admin;
EOSQL
