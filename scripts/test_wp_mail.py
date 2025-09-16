#!/usr/bin/env bash
set -euo pipefail

cd /var/www/html/wordpress/

SUBJ="WP->MailHog test $(date +%s)"

echo "[*] Sending via wp-cli (subject: $SUBJ)"
wp eval "var_dump( wp_mail('inbox@example.test', '$SUBJ', 'Hello from wp-cli at ' . date('c')) );"

echo "[*] Waiting for message in MailHog..."
MSG_ID=""
for i in {1..20}; do
  MSG_ID=$(curl -s http://127.0.0.1:8025/api/v2/messages | jq -r --arg s "$SUBJ" '.items[] | select(.Content.Headers.Subject[0]==$s) | .ID' | head -n1)
  [ -n "$MSG_ID" ] && break
  sleep 1
done

if [ -z "$MSG_ID" ]; then
  echo "❌ Did not find message with subject: $SUBJ"
  exit 1
fi

echo "[*] Got MailHog ID: $MSG_ID"
curl -s "http://127.0.0.1:8025/api/v1/messages/$MSG_ID" | jq '{From: .Content.Headers.From[0], To: .Content.Headers.To[0], Subject: .Content.Headers.Subject[0], Body: .MIME.Parts[0].Body}'
