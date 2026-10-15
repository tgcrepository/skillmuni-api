#!/usr/bin/env bash
# Creates .env for docker compose: random local passwords, a fresh RSA signing key pair, an HMAC key and an AES key.
# Nothing here is used outside your machine; Azure reads the same settings from Key Vault.
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -f .env ]]; then
  echo ".env already exists; delete it to regenerate." >&2
  exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
openssl genrsa -out "$tmp/private.pem" 2048 2>/dev/null
openssl rsa -in "$tmp/private.pem" -pubout -out "$tmp/public.pem" 2>/dev/null

b64() { base64 < "$1" | tr -d '\n'; }
secret() { openssl rand -base64 "$1" | tr -d '\n=+/'; }

cat > .env <<ENV
MYSQL_ROOT_PASSWORD=$(secret 24)
IDENTITY_DB_PASSWORD=$(secret 24)
LEARNER_DB_PASSWORD=$(secret 24)
CV_DB_PASSWORD=$(secret 24)
GAMIFICATION_DB_PASSWORD=$(secret 24)
ASSESSMENT_DB_PASSWORD=$(secret 24)
LEARNING_DB_PASSWORD=$(secret 24)
MUNI_DB_PASSWORD=$(secret 24)
PLATFORM_DB_PASSWORD=$(secret 24)
SQL_EDGE_PASSWORD=Sb1$(secret 18)
AUTH_PRIVATE_KEY_B64=$(b64 "$tmp/private.pem")
AUTH_PUBLIC_KEY_B64=$(b64 "$tmp/public.pem")
IDENTITY_HASH_KEY=$(openssl rand -base64 32 | tr -d '\n')
LEARNER_ENCRYPTION_KEY=$(openssl rand -base64 32 | tr -d '\n')
ENV
echo "Wrote .env. Next: docker compose up --build"
