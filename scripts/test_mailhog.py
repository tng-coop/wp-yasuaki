#!/usr/bin/env python3
import os
import sys
import time
import socket
import base64
import smtplib
import json
from urllib.parse import quote
from email.message import EmailMessage

try:
    import requests
except ImportError:
    print("Missing dependency: requests\nInstall with: pip install requests", file=sys.stderr)
    sys.exit(1)

# --- Config (env overrides) ---
MH_HOST      = os.getenv("MH_HOST", "127.0.0.1")
MH_SMTP_PORT = int(os.getenv("MH_SMTP_PORT", "1025"))
MH_HTTP_PORT = int(os.getenv("MH_HTTP_PORT", "8025"))
MAIL_FROM    = os.getenv("MAIL_FROM", "wp-test@local.test")
MAIL_TO      = os.getenv("MAIL_TO", "inbox@example.test")
WAIT_SECS    = int(os.getenv("WAIT_SECS", "20"))
CLEAR_FIRST  = os.getenv("CLEAR_FIRST", "true").lower() == "true"

MH_BASE = f"http://{MH_HOST}:{MH_HTTP_PORT}"

def die(msg: str, code: int = 1):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(code)

def mh_delete_all():
    try:
        requests.delete(f"{MH_BASE}/api/v1/messages", timeout=5)
    except requests.RequestException:
        # Non-fatal
        pass

def send_via_smtp(subject: str):
    """Send a minimal email to MailHog via SMTP (no TLS/auth)."""
    msg = EmailMessage()
    msg["From"] = MAIL_FROM
    msg["To"] = MAIL_TO
    msg["Subject"] = subject
    msg.set_content(f"Hello from Python SMTP -> MailHog! {time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}")

    with smtplib.SMTP(host=MH_HOST, port=MH_SMTP_PORT, timeout=10) as s:
        # No TLS/auth, MailHog listens in clear on 1025
        s.send_message(msg)

def poll_for_subject(subject: str, timeout_secs: int) -> str:
    """Return the first message ID whose Subject matches exactly, or raise on timeout."""
    print(f"[*] Waiting for message with Subject: {subject}", file=sys.stderr)
    t0 = time.time()
    while True:
        try:
            r = requests.get(f"{MH_BASE}/api/v2/messages", timeout=5)
            r.raise_for_status()
            data = r.json()
        except requests.RequestException:
            data = {}

        items = (data or {}).get("items", []) or []
        for it in items:
            try:
                subj = it["Content"]["Headers"]["Subject"][0]
            except Exception:
                subj = None
            if subj == subject:
                return it.get("ID") or it.get("Id") or ""

        if time.time() - t0 > timeout_secs:
            raise TimeoutError(f"Timed out after {timeout_secs}s waiting for subject {subject!r}")
        time.sleep(1)

def fetch_msg_json(msg_id: str) -> dict | None:
    """Try v2 single-message endpoint first; fall back to v1."""
    eid = quote(msg_id, safe="")
    # try v2
    try:
        r = requests.get(f"{MH_BASE}/api/v2/messages/{eid}", timeout=5)
        if r.status_code == 200:
            return r.json()
    except requests.RequestException:
        pass
    # fallback v1
    try:
        r = requests.get(f"{MH_BASE}/api/v1/messages/{eid}", timeout=5)
        if r.status_code == 200:
            return r.json()
    except requests.RequestException:
        pass
    return None

def get_header(doc: dict, name: str):
    """Fetch header 'name' with fallbacks for v1/v2 shapes."""
    # Preferred (v2/v1 structured)
    try:
        val = doc["Content"]["Headers"][name][0]
        if isinstance(val, str):
            return val
    except Exception:
        pass
    # v1 raw fallbacks
    if name.lower() == "from":
        return doc.get("Raw", {}).get("From")
    if name.lower() == "to":
        raw_to = doc.get("Raw", {}).get("To")
        if isinstance(raw_to, list):
            return ", ".join(raw_to)
        return raw_to
    return None

def extract_plaintext(doc: dict) -> tuple[str, str]:
    """
    Return (encoding, body_text). Tries text/plain MIME part, then Content.Body.
    If empty, caller can try raw download.
    """
    parts = (doc.get("MIME", {}) or {}).get("Parts", []) or []
    # find first text/plain part
    for p in parts:
        try:
            ct = p["Headers"]["Content-Type"][0]
        except Exception:
            ct = ""
        if isinstance(ct, str) and "text/plain" in ct.lower():
            enc = (p.get("Headers", {}).get("Content-Transfer-Encoding", ["7bit"]) or ["7bit"])[0]
            body = p.get("Body") or p.get("Content") or ""
            return enc or "7bit", body or ""
    # fallback to Content.Body
    try:
        body = doc["Content"]["Body"]
        if isinstance(body, str):
            return "7bit", body
    except Exception:
        pass
    return "7bit", ""

def fetch_raw_eml(msg_id: str) -> str:
    """Fetch raw RFC822 and return as string, or empty if not available."""
    try:
        eid = quote(msg_id, safe="")
        r = requests.get(f"{MH_BASE}/api/v1/messages/{eid}/download", timeout=5)
        if r.status_code == 200:
            return r.text
    except requests.RequestException:
        pass
    return ""

def print_summary(doc: dict):
    summary = {
        "ID": doc.get("ID") or doc.get("Id"),
        "From": get_header(doc, "From"),
        "To": get_header(doc, "To"),
        "Subject": get_header(doc, "Subject"),
        "Date": get_header(doc, "Date"),
    }
    print("[*] Message summary:")
    print(json.dumps(summary, indent=2))

def print_plaintext(doc: dict, msg_id: str):
    enc, body = extract_plaintext(doc)
    print("[*] Plaintext body:")
    if body:
        if isinstance(enc, str) and enc.lower() == "base64":
            try:
                print(base64.b64decode(body).decode("utf-8", errors="replace"))
            except Exception:
                # print undecoded if something odd
                print(body)
        else:
            print(body)
        return

    # last resort: parse raw eml after header/body divider
    raw = fetch_raw_eml(msg_id)
    if raw:
        saw_blank = False
        for line in raw.splitlines():
            if saw_blank:
                print(line)
            elif line.strip() == "":
                saw_blank = True

def main():
    if CLEAR_FIRST:
        print("[*] Clearing MailHog inbox...")
        mh_delete_all()

    subject = f"MailHog CLI test {int(time.time())}"
    print(f"[*] Sending via Python SMTP -> {MH_HOST}:{MH_SMTP_PORT}")
    try:
        send_via_smtp(subject)
    except (socket.error, smtplib.SMTPException) as e:
        die(f"SMTP send failed: {e}")

    try:
        msg_id = poll_for_subject(subject, WAIT_SECS)
    except TimeoutError as e:
        die(str(e))

    print(f"[*] Got message ID: {msg_id}")
    doc = fetch_msg_json(msg_id)
    if not doc:
        die("Could not fetch message JSON (v2 and v1 failed).")

    print_summary(doc)
    print_plaintext(doc, msg_id)
    print("Done ✅")

if __name__ == "__main__":
    main()
