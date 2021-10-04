#!/bin/bash
set -e

sed -i 's/host all all all md5/host all all all scram-sha-256/' /var/lib/postgresql/data/pg_hba.conf

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB"<<-EOSQL
	CREATE USER sgla_users WITH ENCRYPTED PASSWORD '${USERREG_PW}';
	CREATE DATABASE sgla_users;
	GRANT ALL PRIVILEGES ON DATABASE sgla_users TO sgla_users;
	CREATE USER sgla_logs WITH ENCRYPTED PASSWORD '${LOGS_PW}';
	CREATE DATABASE sgla_logs;
	GRANT ALL PRIVILEGES ON DATABASE sgla_logs TO sgla_logs;
EOSQL
