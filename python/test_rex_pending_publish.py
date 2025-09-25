#!/usr/bin/env python3
"""
Test: staging post in PENDING state publishes over the original

Env vars required:
  WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD
Optional:
  WP_VERIFY_SSL=true|false (defaults to false for .lan)

Run:
  WP_BASE_URL=https://wp.lan WP_USERNAME=admin WP_APP_PASSWORD=xxxx \
  python3 test_rex_pending_publish.py
"""
import os
import sys
import time
import json
import html
from typing import Any, Dict, Optional

import requests
from requests.auth import HTTPBasicAuth

# --- Config ---
BASE_URL = os.getenv("WP_BASE_URL")
USERNAME = os.getenv("WP_USERNAME")
APP_PASSWORD = os.getenv("WP_APP_PASSWORD")
VERIFY_SSL = os.getenv("WP_VERIFY_SSL", "false").strip().lower() in ("1", "true", "yes")

if not BASE_URL or not USERNAME or not APP_PASSWORD:
    print("Missing required env vars. Please set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD.", file=sys.stderr)
    sys.exit(2)

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


def _pp(label: str, data: Any) -> None:
    try:
        print(f"{label}: {json.dumps(data, indent=2, ensure_ascii=False)}")
    except Exception:
        print(f"{label}: {data}")


def wp_post_create(payload: Dict[str, Any]) -> Dict[str, Any]:
    r = S.post(_u("/wp-json/wp/v2/posts"), json=payload, timeout=30)
    return _require_ok(r)


def wp_post_get(pid: int, fields: Optional[str] = None) -> Dict[str, Any]:
    params = {"context": "edit"}
    if fields:
        params["_fields"] = fields
    r = S.get(_u(f"/wp-json/wp/v2/posts/{pid}"), params=params, timeout=30)
    return _require_ok(r)


def rex_fork(source_id: int, status: str = "draft") -> Dict[str, Any]:
    r = S.post(_u("/wp-json/rex/v1/fork"), json={"source_id": source_id, "status": status}, timeout=30)
    return _require_ok(r)


def rex_save(pid: int, data: Dict[str, Any]) -> Dict[str, Any]:
    r = S.post(_u("/wp-json/rex/v1/save"), json={"id": pid, "data": data}, timeout=30)
    return _require_ok(r)


def rex_publish(staging_id: int) -> Dict[str, Any]:
    r = S.post(_u("/wp-json/rex/v1/publish"), json={"staging_id": staging_id}, timeout=30)
    return _require_ok(r)


def assert_true(expr: bool, msg: str = "") -> None:
    if not expr:
        raise AssertionError(msg or "Expected condition to be True")


def test_pending_staging_publishes_over_original() -> None:
    ts = int(time.time())
    print("[P] Create PUBLISHED original and fork…")
    orig = wp_post_create({"title": f"REX Pending Orig {ts}", "content": "live", "status": "publish"})
    orig_id = int(orig["id"]) 

    stg = rex_fork(orig_id)
    stg_id = int(stg["id"]) 

    print("[P] Mark staging as PENDING and change title…")
    rex_save(stg_id, {"post_status": "pending", "post_title": f"Pending Title {ts}"})

    stg_read = wp_post_get(stg_id, fields="id,status,title")
    assert_true(stg_read.get("status") == "pending", f"staging not pending: {stg_read}")

    print("[P] Publish staging → overwrite original (swap)")
    pub = rex_publish(stg_id)
    assert_true(pub.get("used_original") is True, f"expected used_original=true, got: {pub}")
    assert_true(int(pub.get("published_id") or 0) == orig_id, f"published_id mismatch: {pub}")

    print("[P] Verify original updated and published…")
    orig_read = wp_post_get(orig_id, fields="id,status,title")
    assert_true(orig_read.get("status") == "publish", f"original not publish: {orig_read}")
    title = (orig_read.get("title") or {}).get("rendered", "")
    assert_true(f"Pending Title {ts}" in title, f"original title not updated: {title}")

    print("[P] Verify staging is now in TRASH…")
    stg_after = wp_post_get(stg_id, fields="status")
    assert_true(stg_after.get("status") == "trash", f"staging status changed unexpectedly: {stg_after}")

    print("\nPENDING → PUBLISH SWAP TEST PASSED ✔")


if __name__ == "__main__":
    try:
        test_pending_staging_publishes_over_original()
    except AssertionError as e:
        print(f"\nTEST FAILED: {e}", file=sys.stderr)
        sys.exit(1)
    except requests.RequestException as e:
        print(f"\nHTTP ERROR: {e}", file=sys.stderr)
        sys.exit(1)
