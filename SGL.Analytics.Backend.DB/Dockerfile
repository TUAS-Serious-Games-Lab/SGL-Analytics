FROM postgres:13 as final
ENV POSTGRES_INITDB_ARGS="--auth-host=scram-sha-256"
COPY ["init-db.sh", "/docker-entrypoint-initdb.d/init-db.sh"]
