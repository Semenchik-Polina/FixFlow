#!/usr/bin/env bash
# Creates the application database and its login role (S22).
# Runs once, when the PostgreSQL cluster is empty (docker-entrypoint-initdb.d).
# The app role owns its database so EF Core migrations can create the per-module schemas,
# but it is not a superuser and can't create databases or roles.
# psql does the quoting (:"identifier", :'literal'), so values from .env are never spliced into SQL by the shell.
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set app_db="$FIXFLOW_DB_NAME" \
  --set app_user="$FIXFLOW_DB_USER" \
  --set app_password="$FIXFLOW_DB_PASSWORD" <<'SQL'
CREATE ROLE :"app_user" LOGIN PASSWORD :'app_password' NOSUPERUSER NOCREATEDB NOCREATEROLE;
CREATE DATABASE :"app_db" OWNER :"app_user";
REVOKE ALL ON DATABASE :"app_db" FROM PUBLIC;
SQL
