FROM postgres:13-alpine as final
ENV POSTGRES_INITDB_ARGS="--auth-host=scram-sha-256"
COPY ["init-db.sh", "/docker-entrypoint-initdb.d/init-db.sh"]
HEALTHCHECK --start-period=30s --interval=60s CMD psql -U postgres -c "SELECT 1;"
