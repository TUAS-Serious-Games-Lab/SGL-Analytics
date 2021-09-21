FROM postgres:13
ENV POSTGRES_INITDB_ARGS="--auth-host=scram-sha-256"
COPY ["SGL.Analytics.Backend.DB/init-db.sh", "/docker-entrypoint-initdb.d/init-db.sh"]
