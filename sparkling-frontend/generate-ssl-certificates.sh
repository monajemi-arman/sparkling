#!/bin/bash
# Generate self-signed SSL cert and key as fullchain.pem + privkey.pem

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CERT="$SCRIPT_DIR/fullchain.pem"
KEY="$SCRIPT_DIR/privkey.pem"

# Remove old certs if they exist
rm -f "$CERT" "$KEY"

# Generate private key and self-signed cert
openssl req -x509 -nodes -newkey rsa:2048 \
  -keyout "$KEY" \
  -out "$CERT" \
  -days 365 \
  -subj "/C=US/ST=Denial/L=Springfield/O=Dis/CN=localhost"

echo "✅ Self-signed certificate generated:"
echo "  - Certificate: $CERT"
echo "  - Key: $KEY"
