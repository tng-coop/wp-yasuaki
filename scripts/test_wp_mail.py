#!/usr/bin/env python3
import os
import sys
import time
import json
import base64
import shutil
import subprocess
from urllib.parse import quote

import requests

# ---------- Config (env overrides) ----------
MH_HOST       = os.getenv("MH_HOST", "127.0.0.1")
MH_HTTP_PORT  = int(os.getenv("MH_HTTP_PORT", "8025"))
MH_BASE       = f"http://{MH_HOST}:{MH_HTTP_PORT}"

WP_BASE_URL   = os.getenv("WP_BASE_URL", "http://localhost").rstrip("/")
WP_USERNAME   = os.getenv("WP_USERNAME")
WP_APP_PASS   = os.getenv("WP_APP_PASSWORD")
MAIL_TO       = os.getenv("MAIL_TO", "inbox@example.test")
WAIT_SECS     = int(os.getenv("WAIT_SECS", "25"))
CLEAR_FIRST   = os.getenv("CLEAR_FIRST", "false").lower() == "true"

SUBJECT       = f"WP->MailHog test {int(time.time())}"  # only used in wp-cli path

if not WP_USERNAME or not WP_APP_PASS:
    print("ERROR: please export WP_USERNAME and WP_APP_PASSWORD", file=sys.stderr)
    sys.exit(1)

# ---------- MailHog helpers ----------
def mh_delete_all():
    try:
        requests.delete(f"{MH_BASE}/api/v1/messages", timeout=5)
    except requests.RequestException:
        pass

def mh_list_ids() -> set[str]:
    try:
        r = requests.get(f"{MH_BASE}/api/v2/messages", timeout=5)
        r.raise_for_status()
        data = r.json()
        return {it.get("ID") or it.get("Id") for it in (data or {}).get("items", [])}
    except requests.RequestException:
        return set()

def poll_for_subject_or_new(subject: str | None, old_ids: set[str], timeout_secs: int) -> str:
    """
    If subject is provided, wait for exact subject match and return its ID.
    Otherwise, return the first message ID that wasn't in old_ids.
    """
    t0 = time.time()
    msg = f"[*] Waiting for WP message (subject='{subject}')" if subject else "[*] Waiting for a NEW WP message"
    print(msg, file=sys.stderr)

    while True:
        try:
            r = requests.get(f"{MH_BASE}/api/v2/messages", timeout=5)
            r.raise_for_status()
            items = (r.json() or {}).get("items", []) or []
        except requests.RequestException:
            items = []

        if subject:
            for it in items:
                try:
                    subj = it["Content"]["Headers"]["Subject"][0]
                except Exception:
                    subj = None
                if subj == subject:
                    return it.get("ID") or it.get("Id") or ""
        else:
            for it in items:
                mid = it.get("ID") or it.get("Id") or ""
                if mid and mid not in old_ids:
                    return mid

        if time.time() - t0 > timeout_secs:
            raise TimeoutError("Timed out waiting for WordPress email in MailHog.")
        time.sleep(1)

def fetch_msg_json(msg_id: str) -> dict | None:
    eid = requests.utils.quote(msg_id, safe="")
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
    try:
        val = doc["Content"]["Headers"][name][0]
        if isinstance(val, str):
            return val
    except Exception:
        pass
    if name.lower() == "from":
        return doc.get("Raw", {}).get("From")
    if name.lower() == "to":
        raw_to = doc.get("Raw", {}).get("To")
        if isinstance(raw_to, list):
            return ", ".join(raw_to)
        return raw_to
    return None

def extract_plaintext(doc: dict) -> tuple[str, str]:
    parts = (doc.get("MIME", {}) or {}).get("Parts", []) or []
    for p in parts:
        try:
            ct = p["Headers"]["Content-Type"][0]
        except Exception:
            ct = ""
        if isinstance(ct, str) and "text/plain" in ct.lower():
            enc = (p.get("Headers", {}).get("Content-Transfer-Encoding", ["7bit"]) or ["7bit"])[0]
            body = p.get("Body") or p.get("Content") or ""
            return enc or "7bit", body or ""
    try:
        body = doc["Content"]["Body"]
        if isinstance(body, str):
            return "7bit", body
    except Exception:
        pass
    return "7bit", ""

def fetch_raw_eml(msg_id: str) -> str:
    try:
        eid = requests.utils.quote(msg_id, safe="")
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
                print(body)
        else:
            print(body)
        return
    raw = fetch_raw_eml(msg_id)
    if raw:
        saw_blank = False
        for line in raw.splitlines():
            if saw_blank:
                print(line)
            elif line.strip() == "":
                saw_blank = True

# ---------- WordPress helpers ----------
def wp_rest_auth_test() -> dict:
    url = f"{WP_BASE_URL}/wp-json/wp/v2/users/me"
    try:
        r = requests.get(url, auth=(WP_USERNAME, WP_APP_PASS), timeout=8)
        if r.status_code == 401:
            print(r.text)
            raise RuntimeError("401 unauthorized: check app password / plugins / proxy auth.")
        r.raise_for_status()
        return r.json()
    except requests.RequestException as e:
        raise RuntimeError(f"REST auth failed: {e}")

def have_wp_cli() -> bool:
    return shutil.which("wp") is not None

def trigger_wp_email_via_wpcli(subject: str, to_addr: str) -> None:
    # Escape single quotes by replacing with "\'"
    safe_to = to_addr.replace("'", "\\'")
    safe_subject = subject.replace("'", "\\'")
    body = f"Hello from WordPress via wp_mail! {int(time.time())}"
    safe_body = body.replace("'", "\\'")

    php = f"var_dump( wp_mail('{safe_to}', '{safe_subject}', '{safe_body}') );"
    cmd = ["wp", "eval", php]
    try:
        subprocess.run(cmd, check=False, capture_output=True, text=True)
    except Exception as e:
        print(f"[warn] wp-cli run failed: {e}", file=sys.stderr)


def trigger_wp_email_via_lostpassword(username: str) -> None:
    # This sends to the user's account email (not MAIL_TO).
    url = f"{WP_BASE_URL}/wp-login.php?action=lostpassword"
    data = {"user_login": username, "wp-submit": "Get New Password"}
    try:
        requests.post(url, data=data, timeout=8)
    except requests.RequestException:
        pass

# ---------- Main ----------
def main():
    if CLEAR_FIRST:
        print("[*] Clearing MailHog inbox...")
        mh_delete_all()

    print(f"[*] WordPress REST auth test -> {WP_BASE_URL}")
    me = wp_rest_auth_test()
    print(json.dumps({k: me.get(k) for k in ("id", "name", "slug", "roles")}, indent=2))

    pre_ids = mh_list_ids()
    if have_wp_cli():
        print("[*] Using wp-cli to send wp_mail()")
        trigger_wp_email_via_wpcli(SUBJECT, MAIL_TO)
        subject = SUBJECT
    else:
        print("[*] wp-cli not found; using Lost Password flow (subject may vary)")
        trigger_wp_email_via_lostpassword(WP_USERNAME)
        subject = None  # we don't know the exact subject

    try:
        msg_id = poll_for_subject_or_new(subject, pre_ids, WAIT_SECS)
    except TimeoutError as e:
        print("ERROR:", str(e), file=sys.stderr)
        sys.exit(1)

    print(f"[*] Got WP message ID: {msg_id}")
    doc = fetch_msg_json(msg_id)
    if not doc:
        print("ERROR: Could not fetch message JSON (v2 and v1 failed).", file=sys.stderr)
        sys.exit(1)

    print_summary(doc)
    print_plaintext(doc, msg_id)
    print("Done ✅")

if __name__ == "__main__":
    main()
