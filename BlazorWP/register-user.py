#!/usr/bin/env python3

import os
import sys
import requests

# Load JWT token from environment variable
JWT_TOKEN = os.getenv("JWT_TOKEN")
if not JWT_TOKEN:
    print("Error: JWT_TOKEN environment variable not set.")
    sys.exit(1)


def fetch_invite_token():
    """
    Sends a POST request to the invite token endpoint and prints response status, headers, and body.
    """
    url = "https://yasuaki.com/wp-json/invite/v1/token"
    headers = {
        "Authorization": f"Bearer {JWT_TOKEN}",
        "Content-Type":  "application/json"
    }
    print(headers)
    try:
        response = requests.post(url, headers=headers)
        response.raise_for_status()
    except requests.exceptions.RequestException as e:
        print(f"HTTP request failed: {e}")
        if hasattr(e, 'response') and e.response is not None:
            print("Response Body:", e.response.text)
        sys.exit(1)

    # Print status and response details
    print("Status Code:", response.status_code)
    print("Response Headers:")
    for k, v in response.headers.items():
        print(f"{k}: {v}")
    print("\nResponse Body:")
    print(response.text)


if __name__ == "__main__":
    fetch_invite_token()

