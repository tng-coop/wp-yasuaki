#!/usr/bin/env python3
# Path: python/test_rex_save_same_id.py
"""
Verify that plain (no-conflict) saves never change the post ID.

Env vars required (same as other tests):
  WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD
Optional:
  WP_VERIFY_SSL=true|false  (defaults to false for .lan)

Run:
  pip install -r requirements.txt
  WP_BASE_URL=https://wp.lan WP_USERNAME=admin WP_APP_PASSWORD=xxxx \
    python python/test_rex_save_same_id.py
"""

import os
import sys
import time
from typing import Any, Dict, Optional

import requests
from requests.auth import HTTPBasicAuth

# --- Config (mirrors other tests) ---
BASE_URL = os.getenv("WP_BASE_URL")
USERNAME = os.getenv("WP_USERNAME")
APP_PASSWORD = os.getenv("WP_APP_PASSWORD")
VERIFY_SSL = os.getenv("WP_VERIFY_SSL", "false").strip().lower() in ("1", "true", "yes")

if not BASE_URL or not USERNAME or not APP_PASSWORD:
    print("Missing required env vars. Please set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD.", file=sys.stderr)
    sys.exit(2)

# Default to not verifying certs for .lan unless explicitly enabled
if not VERIFY_SSL:
    try:
        from urllib3.exceptions import InsecureRequestWarning
        requests.packages.urllib3.disable_warnings(category=InsecureRequestWarning)  # type: ignore[attr-defined]
    except Exception:
        pass

S = requests.Session()
S.auth = HTTPBasicAuth(USERNAME, APP_PASSWORD)
S.verify = VERIFY_SSL
S.headers.update({"Accept": "application/json"})

# --- Helpers (same style as other files) ---
def _u(path: str) -> str:
    return BASE_URL.rstrip("/") + path

def _require_ok(r: requests.Response) -> Dict[str, Any]:
    try:
        data = r.json()
    except Exception:
        data = {"raw": r.text}
    if not r.ok:
        raise AssertionError(f"HTTP {r.status_code}: {data}")
    return data  # type: ignore[return-value]

def wp_post_create(payload: Dict[str, Any]) -> Dict[str, Any]:
    r = S.post(_u("/wp-json/wp/v2/posts"), json=payload, timeout=30)
    return _require_ok(r)

def wp_post_get(pid: int, fields: Optional[str] = None) -> Dict[str, Any]:
    params = {"context": "edit"}
    if fields:
        params["_fields"] = fields
    r = S.get(_u(f"/wp-json/wp/v2/posts/{pid}"), params=params, timeout=30)
    return _require_ok(r)

def rex_save(pid: int, data: Dict[str, Any]) -> Dict[str, Any]:
    r = S.post(_u("/wp-json/rex/v1/save"), json={"id": pid, "data": data}, timeout=30)
    return _require_ok(r)

def assert_equal(a: Any, b: Any, msg: str = "") -> None:
    if a != b:
        raise AssertionError(msg or f"Expected {a!r} == {b!r}")

def assert_true(expr: bool, msg: str = "") -> None:
    if not expr:
        raise AssertionError(msg or "Expected condition to be True")

# --- Test ---
def test_save_same_id_plain_path() -> int:
    ts = int(time.time())
    print("[SameID] Create a DRAFT post via core REST…")
    draft = wp_post_create({"title": f"REX SameID {ts}", "content": "v0", "status": "draft"})
    pid = int(draft["id"])
    print(f"[SameID] Draft created: id={pid}")

    # 1st save (no concurrency token) — should NOT fork and should return same id
    print("[SameID] Save #1 (no token)…")
    res1 = rex_save(pid, {"post_title": "SameID Save #1"})
    print("[SameID] Save #1:", res1)
    assert_true(res1.get("forked") is not True, f"unexpected fork on first save: {res1}")
    assert_true(res1.get("saved") in (True, 1, "true"), f"save path expected saved=true, got: {res1}")
    assert_equal(int(res1.get("id", 0)), pid, f"[Save #1] expected same id, got: {res1}")

    # Derive optimistic concurrency token
    token = str(res1.get("modified_gmt") or "").strip()
    if not token:
        fetched = wp_post_get(pid, fields="modified_gmt")
        token = str(fetched.get("modified_gmt") or "").strip()
    assert_true(bool(token), f"modified_gmt token should be present (got: {res1})")
    print("[SameID] Using token:", token)

    # 2nd save (with valid token) — still same id
    print("[SameID] Save #2 (with token)…")
    res2 = rex_save(pid, {"post_content": "v2 - ok", "expected_modified_gmt": token})
    print("[SameID] Save #2:", res2)
    assert_true(res2.get("forked") is not True, f"unexpected fork on second save: {res2}")
    assert_true(res2.get("saved") in (True, 1, "true"), f"second save expected saved=true, got: {res2}")
    assert_equal(int(res2.get("id", 0)), pid, f"[Save #2] expected same id, got: {res2}")

    # Refresh token (from response or fetch)
    token2 = str(res2.get("modified_gmt") or "").strip()
    if not token2:
        fetched2 = wp_post_get(pid, fields="modified_gmt")
        token2 = str(fetched2.get("modified_gmt") or "").strip()
    assert_true(bool(token2), "modified_gmt token should be present after save #2")

    # 3rd save (with latest token) — still same id
    print("[SameID] Save #3 (with latest token)…")
    res3 = rex_save(pid, {"post_title": "SameID Save #3", "expected_modified_gmt": token2})
    print("[SameID] Save #3:", res3)
    assert_true(res3.get("forked") is not True, f"unexpected fork on third save: {res3}")
    assert_true(res3.get("saved") in (True, 1, "true"), f"third save expected saved=true, got: {res3}")
    assert_equal(int(res3.get("id", 0)), pid, f"[Save #3] expected same id, got: {res3}")

    print(f"[SameID] OK: post {pid} preserved id across 3 saves on the plain path.")
    return pid

def main() -> None:
    try:
        test_save_same_id_plain_path()
        print(" SAME-ID TEST PASSED ✔")
    except AssertionError as e:
        print(f" TEST FAILED: {e}", file=sys.stderr)
        sys.exit(1)
    except requests.RequestException as e:
        print(f" HTTP ERROR: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
