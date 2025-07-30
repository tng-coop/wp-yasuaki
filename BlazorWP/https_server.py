#!/usr/bin/env python3
import os
import ssl
import tempfile
from http.server import HTTPServer, SimpleHTTPRequestHandler
from cryptography.hazmat.primitives.serialization import pkcs12, Encoding, PrivateFormat, NoEncryption

# ── CONFIGURATION ─────────────────────────────────────────────────────────────

# Base folder where this script lives (your project root)
BASE_DIR = os.path.dirname(os.path.abspath(__file__))

# Path to your PFX file
PFX_PATH = os.path.join(BASE_DIR, "cert", "aspnet.lan.pfx")
PFX_PASSWORD = b"yourpassword"  # Replace with actual password bytes

# Directory you want to serve (Blazor publish output)
PUBLISH_DIR = os.path.join(BASE_DIR, "bin", "Release", "net9.0", "publish", "wwwroot")

# Bind settings
HOST = "localhost"
PORT = 8000

# ── SANITY CHECKS ───────────────────────────────────────────────────────────────

if not os.path.isfile(PFX_PATH):
    raise FileNotFoundError(f"PFX not found at: {PFX_PATH}")

if not os.path.isdir(PUBLISH_DIR):
    raise FileNotFoundError(f"Publish directory not found at: {PUBLISH_DIR}")

# ── PREPARE CERTIFICATES ─────────────────────────────────────────────────────────

# Load PFX and extract key/certificates
with open(PFX_PATH, "rb") as f:
    pfx_data = f.read()
private_key, certificate, additional_certs = pkcs12.load_key_and_certificates(
    pfx_data,
    PFX_PASSWORD
)

# Write private key + cert chain to temporary PEM files
key_file = tempfile.NamedTemporaryFile(delete=False, suffix=".pem")
cert_file = tempfile.NamedTemporaryFile(delete=False, suffix=".pem")

# Private key PEM
key_file.write(private_key.private_bytes(
    encoding=Encoding.PEM,
    format=PrivateFormat.TraditionalOpenSSL,
    encryption_algorithm=NoEncryption()
))

# Certificate PEM (plus any CA chain)
cert_file.write(certificate.public_bytes(Encoding.PEM))
for ca in additional_certs or []:
    cert_file.write(ca.public_bytes(Encoding.PEM))

key_file.flush()
cert_file.flush()

# ── START SERVER ────────────────────────────────────────────────────────────────

# Change into the publish directory so SimpleHTTPRequestHandler serves it
os.chdir(PUBLISH_DIR)

# Create HTTP server
httpd = HTTPServer((HOST, PORT), SimpleHTTPRequestHandler)

# Wrap socket with TLS
ssl_context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
ssl_context.load_cert_chain(certfile=cert_file.name, keyfile=key_file.name)
httpd.socket = ssl_context.wrap_socket(httpd.socket, server_side=True)

print(f"Serving HTTPS on https://{HOST}:{PORT}/ (serving {PUBLISH_DIR})")
httpd.serve_forever()
