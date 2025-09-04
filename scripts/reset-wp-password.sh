#!/bin/bash
# Usage: ./reset-wp-password.sh <username>

USER=$1

if [ -z "$USER" ]; then
  echo "Usage: $0 <username>"
  exit 1
fi

# Generate a random 20-character password
P=$(wp eval 'echo wp_generate_password(20);')

# Update the WordPress user password
wp user update "$USER" --user_pass="$P"

if [ $? -eq 0 ]; then
  echo "✅ Password updated successfully for user: $USER"
  echo "$P" | tohome   # adjust this to your own password-safe method
else
  echo "❌ Failed to update password for user: $USER"
  exit 1
fi
