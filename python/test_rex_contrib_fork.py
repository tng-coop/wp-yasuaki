#!/usr/bin/env python3
# Path: python/test_rex_contrib_fork.py
"""
Contributor fork tests for custom REX WordPress endpoint.

Covers (per revised permission model):
- Contributor CAN fork a PUBLISHED source they can read -> 200
- Contributor CANNOT fork an unreadable source (DRAFT by another user, PRIVATE) -> 403

Env vars required:
  WP_BASE_URL           (e.g., https://wp.lan)
  WP_USERNAME           (admin username, e.g., admin)
  WP_APP_PASSWORD       (admin app password)
  WP_APP_CONTRIBUTOR    (contributor app password)

Optional:
  WP_CONTRIB_USERNAME   (defaults to "contributor")
  WP_VERIFY_SSL         ("true"/"false" – default "false" for .lan)

Run:
  pip install -r requirements.txt
  WP_BASE_URL=https://wp.lan \
  WP_USERNAME=admin WP_APP_PASSWORD=xxxx \
  WP_CONTRIB_USERNAME=contributor WP_APP_CONTRIBUTOR=yyyy \
  ./python/test_rex_contrib_fork.py
"""
from __future__ import annotations

import os
import sys
import time
import json
from typing import Any, Dict, Optional

import requests
from requests.auth import HTTPBasicAuth


# --- Config ---
BASE_URL = os.getenv("WP_BASE_URL")
ADMIN_USER = os.getenv("WP_USERNAME")
ADMIN_APP_PASSWORD = os.getenv("WP_APP_PASSWORD")

CONTRIB_USER = os.getenv("WP_CONTRIB_USERNAME", "contributor")
CONTRIB_APP_PASSWORD = os.getenv("WP_APP_CONTRIBUTOR")

VERIFY_SSL = os.getenv("WP_VERIFY_SSL", "false").strip().lower() in ("1", "true", "yes")

if not BASE_URL or not ADMIN_USER or not ADMIN_APP_PASSWORD or not CONTRIB_APP_PASSWORD:
    print(
        "Missing required env vars. Please set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD, WP_APP_CONTRIBUTOR.",
        file=sys.stderr,
    )
    sys.exit(2)

# Default to not verifying certs for .lan unless explicitly enabled
if not VERIFY_SSL:
    try:
        from urllib3.exceptions import InsecureRequestWarning
        requests.packages.urllib3.disable_warnings(category=InsecureRequestWarning)  # type: ignore[attr-defined]
    except Exception:
        pass

def _u(path: str) -> str:
    return BASE_URL.rstrip("/") + path

def _make_session(user: str, app_password: str) -> requests.Session:
    s = requests.Session()
    s.auth = HTTPBasicAuth(user, app_password)
    s.verify = VERIFY_SSL
    s.headers.update({"Accept": "application/json"})
    return s

S_ADMIN = _make_session(ADMIN_USER,   ADMIN_APP_PASSWORD)
S_CONTR = _make_session(CONTRIB_USER, CONTRIB_APP_PASSWORD)


# --- Helpers ---
def _json_or_raw(r: requests.Response) -> Any:
    try:
        return r.json()
    except Exception:
        return {"raw": r.text}

def _require_ok(r: requests.Response) -> Dict[str, Any]:
    data: Any
    try:
        data = r.json()
    except Exception:
        data = {"raw": r.text}
    if not r.ok:
        raise AssertionError(f"HTTP {r.status_code}: {data}")
    return data  # type: ignore[return-value]

def _pp(label: str, data: Any) -> None:
    try:
        print(f"{label}: {json.dumps(data, indent=2, ensure_ascii=False)}")
    except Exception:
        print(f"{label}: {data}")


# --- Minimal API wrappers (admin creates, contributor forks) ---
def wp_post_create_as_admin(payload: Dict[str, Any]) -> Dict[str, Any]:
    r = S_ADMIN.post(_u("/wp-json/wp/v2/posts"), json=payload, timeout=30)
    return _require_ok(r)

def fork_as_contributor(source_id: int, status: str = "draft") -> requests.Response:
    return S_CONTR.post(_u("/wp-json/rex/v1/fork"), json={"source_id": source_id, "status": status}, timeout=30)


# --- Tests ---
def test_contributor_fork_published() -> None:
    ts = int(time.time())
    print("[Contrib] Create PUBLISHED post as admin, then try to fork as contributor…")

    src = wp_post_create_as_admin({"title": f"REX Contrib Pub {ts}", "content": "live", "status": "publish"})
    src_id = int(src["id"])

    r = fork_as_contributor(src_id)
    payload = _json_or_raw(r)
    _pp("[Contrib] Fork response", payload)

    # New permission model: contributor CAN fork a PUBLISHED source they can read.
    assert r.status_code == 200, f"Expected HTTP 200 for contributor fork, got {r.status_code}: {payload}"
    assert isinstance(payload.get("id"), int) and payload["id"] > 0, f"bad id: {payload}"
    assert payload.get("status") == "draft", f"fork should come back as draft: {payload}"
    assert int(payload.get("original_post_id") or 0) == src_id, f"original_post_id should equal source: {payload}"
    print("[Contrib] OK contributor forked published source")

def test_contributor_fork_unreadable_is_forbidden() -> None:
    ts = int(time.time())
    print("[Contrib] Create DRAFT + PRIVATE as admin, then ensure contributor gets 403 when forking…")

    # DRAFT (authored by admin → not readable by contributor)
    src_draft = wp_post_create_as_admin({"title": f"REX Contrib Draft {ts}", "content": "draft", "status": "draft"})
    r1 = fork_as_contributor(int(src_draft["id"]))
    p1 = _json_or_raw(r1)
    assert r1.status_code == 403, f"Expected 403 for draft source, got {r1.status_code}: {p1}"
    print("[Contrib] OK draft source returned 403")

    # PRIVATE (not readable by contributor)
    src_priv = wp_post_create_as_admin({"title": f"REX Contrib Private {ts}", "content": "hidden", "status": "private"})
    r2 = fork_as_contributor(int(src_priv["id"]))
    p2 = _json_or_raw(r2)
    assert r2.status_code == 403, f"Expected 403 for private source, got {r2.status_code}: {p2}"
    print("[Contrib] OK private source returned 403")


def main() -> None:
    try:
        test_contributor_fork_published()
        test_contributor_fork_unreadable_is_forbidden()
        print(" CONTRIBUTOR FORK TESTS PASSED ✔")
    except AssertionError as e:
        print(f" TEST FAILED: {e}", file=sys.stderr)
        sys.exit(1)
    except requests.RequestException as e:
        print(f" HTTP ERROR: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
