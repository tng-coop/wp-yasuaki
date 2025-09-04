#!/bin/bash

scriptdir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
certdir="${scriptdir}/../cert"
mkdir -p "${certdir}"
cd "${certdir}" || exit 1

set -euo pipefail

# --- Variables ---
CERT_PASSWORD="yourpassword"
CA_NAME="AspNetLanDevelopmentCA"
DOMAIN="aspnet.lan"
SHARED_CERT_DIR="/srv/shared/aspnet/cert"
SHARE_GROUP="aspnet"

# --- Cleanup existing certificates ---
rm -f "${DOMAIN}-ca."* "${DOMAIN}."* *.pfx *.pem *.csr *.srl *.ext

# --- Reset NSS DB ---
rm -rf "$HOME/.pki/nssdb"
mkdir -p "$HOME/.pki/nssdb"
certutil -d sql:"$HOME/.pki/nssdb" -N --empty-password \
  && echo "✅ NSS DB reset."

# --- Generate Root CA (10 years) ---
openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
  -keyout "${DOMAIN}-ca.key" -out "${DOMAIN}-ca.crt" \
  -subj "/CN=${CA_NAME}" \
  -addext "basicConstraints=critical,CA:true" \
  -addext "keyUsage=critical,keyCertSign,cRLSign" \
  && echo "✅ Root CA generated."

# --- Trust CA system-wide & in NSS DB (so you can sign the leaf) ---
sudo cp "${DOMAIN}-ca.crt" /usr/local/share/ca-certificates/"${CA_NAME}".crt
sudo update-ca-certificates || true
certutil -d sql:"$HOME/.pki/nssdb" \
  -A -t "CT,C,C" -n "${CA_NAME}" \
  -i "${DOMAIN}-ca.crt" \
  && echo "✅ Root CA trusted."

# --- Generate CSR for the leaf cert ---
openssl req -newkey rsa:4096 -nodes \
  -keyout "${DOMAIN}.key" -out "${DOMAIN}.csr" \
  -subj "/CN=${DOMAIN}" \
  && echo "✅ Leaf CSR and key generated."

# --- Create an extensions file for the leaf cert ---
cat > "${DOMAIN}.ext" <<EOF
basicConstraints=CA:FALSE
subjectAltName=DNS:${DOMAIN},DNS:wp.lan,DNS:localhost,IP:127.0.0.1,IP:::1
keyUsage=digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
EOF
echo "✅ Extensions file (${DOMAIN}.ext) written:"
cat "${DOMAIN}.ext"

# --- Sign leaf cert with CA (≤ 825 days for iOS/macOS compliance) ---
openssl x509 -req -in "${DOMAIN}.csr" \
  -CA "${DOMAIN}-ca.crt" -CAkey "${DOMAIN}-ca.key" \
  -CAcreateserial -out "${DOMAIN}.crt" \
  -days 825 -sha256 \
  -extfile "${DOMAIN}.ext" \
  && echo "✅ Leaf certificate signed (${DOMAIN}.crt)."

# --- Bundle into PFX & PEM for ease of use elsewhere ---
openssl pkcs12 -export -out "${DOMAIN}.pfx" \
  -inkey "${DOMAIN}.key" -in "${DOMAIN}.crt" \
  -passout pass:"${CERT_PASSWORD}" \
  && echo "✅ PFX bundle created (${DOMAIN}.pfx)."

openssl pkcs12 -in "${DOMAIN}.pfx" -out "${DOMAIN}.pem" \
  -nodes -passin pass:"${CERT_PASSWORD}" \
  && echo "✅ PEM bundle created (${DOMAIN}.pem)."

# --- Ensure the share group exists ---
sudo groupadd -f "${SHARE_GROUP}"

# --- Prepare shared directory and publish ALL cert artifacts ---
sudo mkdir -p "${SHARED_CERT_DIR}"
sudo chown root:"${SHARE_GROUP}" "${SHARED_CERT_DIR}"
sudo chmod 2770 "${SHARED_CERT_DIR}"

# copy everything now that it's all been generated
sudo cp \
  "${DOMAIN}-ca."* \
  "${DOMAIN}."* \
  "${SHARED_CERT_DIR}/"

echo "✅ All certificate files copied to ${SHARED_CERT_DIR} (group: ${SHARE_GROUP})"

echo
echo "✅ iOS-compatible certificates generated for:"
echo "   • DNS: ${DOMAIN}"
echo "   • DNS: wp.lan"
echo "   • DNS: localhost"
echo "   • IP: 127.0.0.1 and ::1"
