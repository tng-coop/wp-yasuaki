#!/usr/bin/env bash
# BlazorWP/scripts/setup-apache-direct.sh
set -euo pipefail

DOMAIN="${DOMAIN:-aspnet.lan}"
DOMAIN_ALIASES="${DOMAIN_ALIASES:-}"   # space-separated, e.g. "wp.lan other.lan"
PORT="${PORT:-8444}"
SRC_WP_DIR="${WP_DIR:-$GITHUB_WORKSPACE/wordpress}"           # source in workspace
DST_WP_DIR="/var/www/wordpress"                                # final docroot
CERT_DIR="${CERT_DIR:-$GITHUB_WORKSPACE/BlazorWP/cert}"

# Use your generated cert names
CRT="${CERT_CERT_FILE:-$CERT_DIR/aspnet.lan.crt}"
KEY="${CERT_KEY_FILE:-$CERT_DIR/aspnet.lan.key}"

echo "== setup-apache-direct =="
echo "DOMAIN=$DOMAIN  PORT=$PORT"
echo "SRC_WP_DIR=$SRC_WP_DIR"
echo "DST_WP_DIR=$DST_WP_DIR"
echo "CERT_DIR=$CERT_DIR"
echo "CRT=$CRT"
echo "KEY=$KEY"

[[ -d "$SRC_WP_DIR" ]] || { echo "Missing source WP_DIR: $SRC_WP_DIR" >&2; exit 1; }
[[ -f "$CRT" && -f "$KEY" ]] || { echo "Missing certs: $CRT / $KEY" >&2; exit 1; }


# Install Apache + mod_php
sudo apt-get update -y
sudo apt-get install -y apache2 libapache2-mod-php8.3
sudo a2enmod rewrite ssl mime dir

# Ensure host resolves locally
if ! grep -qE "^[^#]*\\s$DOMAIN(\\s|$)" /etc/hosts; then
  echo "127.0.0.1  $DOMAIN" | sudo tee -a /etc/hosts >/dev/null
fi
if [[ -n "$DOMAIN_ALIASES" ]]; then
  for h in $DOMAIN_ALIASES; do
    grep -qE "^[^#]*\\s$h(\\s|$)" /etc/hosts || echo "127.0.0.1  $h" | sudo tee -a /etc/hosts >/dev/null
  done
fi

# Default .htaccess (pretty permalinks/REST)
cat > "$SRC_WP_DIR/.htaccess" <<'HT'
<IfModule mod_rewrite.c>
RewriteEngine On
RewriteBase /
RewriteRule ^index\\.php$ - [L]
RewriteCond %{REQUEST_FILENAME} !-f
RewriteCond %{REQUEST_FILENAME} !-d
RewriteRule . /index.php [L]
</IfModule>
HT

# Deploy to /var/www/wordpress (clean, predictable perms)
sudo rsync -a --delete "$SRC_WP_DIR/" "$DST_WP_DIR/"
sudo chown -R www-data:www-data "$DST_WP_DIR"

# Listen on custom HTTPS port (avoid duplicate)
grep -q "Listen $PORT" /etc/apache2/ports.conf || echo "Listen $PORT" | sudo tee -a /etc/apache2/ports.conf >/dev/null

# SSL vhost
sudo tee /etc/apache2/sites-available/wp-ssl.conf >/dev/null <<CONF
<VirtualHost *:${PORT}>
  ServerName ${DOMAIN}
  $( [[ -n "$DOMAIN_ALIASES" ]] && echo "ServerAlias ${DOMAIN_ALIASES}" )
  DocumentRoot "${DST_WP_DIR}"

  SSLEngine on
  SSLCertificateFile      "${CRT}"
  SSLCertificateKeyFile   "${KEY}"

  # Pass Basic Auth header for WP Application Passwords
  SetEnvIf Authorization "(.*)" HTTP_AUTHORIZATION=\$1

  <Directory "${DST_WP_DIR}">
    AllowOverride All
    Require all granted
    Options -Indexes
  </Directory>

  ErrorLog  \${APACHE_LOG_DIR}/wp-ssl-error.log
  CustomLog \${APACHE_LOG_DIR}/wp-ssl-access.log combined
</VirtualHost>
CONF

sudo a2ensite wp-ssl.conf
sudo a2dissite 000-default.conf || true
sudo systemctl restart apache2

# Flush permalinks as www-data (docroot owned by Apache)
sudo -u www-data -E wp option update permalink_structure '/%postname%/' --path="$DST_WP_DIR"
sudo -u www-data -E wp rewrite flush --hard --path="$DST_WP_DIR"

echo "== Apache is serving WordPress at https://${DOMAIN}:${PORT}/ (aliases: ${DOMAIN_ALIASES:-none}) =="