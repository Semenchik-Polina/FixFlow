#!/usr/bin/env sh
# Exports the ASP.NET Core HTTPS development certificate for the Compose `full` profile (plan T0.11, P19).
#   ./scripts/dev-cert.sh
# Writes certs/fixflow-dev.pem and certs/fixflow-dev.key (untracked). The key has no password: the certificate is for
# localhost only, and the API container reads it from a read-only bind mount.
# Run it again when the certificate expires (yearly) or after `dotnet dev-certs https --clean`.
set -eu

certs="$(cd "$(dirname "$0")/.." && pwd)/certs"
mkdir -p "$certs"

# Creates the certificate if there is none and trusts it. On Linux trust is best effort (OpenSSL, plus browsers when
# certutil is installed); without it the browser shows a warning for https://localhost:8443.
dotnet dev-certs https --trust || echo "warning: the development certificate could not be trusted" >&2

dotnet dev-certs https --export-path "$certs/fixflow-dev.pem" --format Pem --no-password

# The container's non-root user (uid 1654) reads them through the bind mount, whatever the host's umask. This makes
# the key readable by every local user, which is fine on a single-user machine; on a shared one, anyone could then
# impersonate localhost to you.
chmod 755 "$certs"
chmod 644 "$certs/fixflow-dev.pem" "$certs/fixflow-dev.key"

echo "Exported to $certs"
