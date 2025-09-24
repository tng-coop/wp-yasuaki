#!/usr/bin/env python3
"""
Minimal smoke tests for the custom REX WordPress endpoints.

Env vars required:
  WP_BASE_URL (e.g., https://wp.lan)
  WP_USERNAME (e.g., admin)
  WP_APP_PASSWORD (application password string)

Optional:
  WP_VERIFY_SSL (default: "false" for .lan) -> set to "true" to verify TLS

Usage:
  pip install -r requirements.txt
  WP_BASE_URL=https://wp.lan WP_USERNAME=admin WP_APP_PASSWORD=xxxx python test_rex_endpoints.py
"""
import os
import sys
import time
import json
from typing import Any, Dict, Optional, Tuple

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

def wp_post_create(title: str, content: str, status: str = "draft") -> Dict[str, Any]:
    r = S.post(
        _u("/wp-json/wp/v2/posts"),
        json={"title": title, "content": content, "status": status},
        timeout=30,
    )
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

def assert_equal(a: Any, b: Any, msg: str = "") -> None:
    if a != b:
        raise AssertionError(msg or f"Expected {a!r} == {b!r}")

def assert_true(expr: bool, msg: str = "") -> None:
    if not expr:
        raise AssertionError(msg or "Expected condition to be True")

def test_1_fork_from_published() -> Tuple[int, int]:
    ts = int(time.time())
    print("[1] Create original PUBLISHED post...")
    orig = wp_post_create(title=f"REX Orig {ts}", content="Original body", status="publish")
    orig_id = int(orig["id"])

    print("[1] Fork it via /rex/v1/fork ...")
    fork_res = rex_fork(source_id=orig_id)
    fork_id = int(fork_res["id"])

    assert_equal(int(fork_res.get("original_post_id") or 0), orig_id, "fork should point to original id when original is published")
    print(f"[1] OK forked draft {fork_id} with original_post_id={orig_id}")
    return orig_id, fork_id

def test_2_save_conflict_creates_fork(orig_id: int) -> int:
    print("[2] Trigger conflict fork by saving ORIGINAL with wrong expected_modified_gmt...")
    save_res = rex_save(orig_id, {
        "post_title": "REX Updated via conflict",
        "post_content": "Updated body (conflict path)",
        "expected_modified_gmt": "1970-01-01 00:00:00"
    })
    assert_true(bool(save_res.get("forked")), "save should fork on conflict")
    new_draft_id = int(save_res["id"])
    assert_equal(int(save_res.get("original_post_id") or 0), orig_id, "conflict-fork should retain original id")
    print(f"[2] OK conflict fork created draft {new_draft_id}")
    return new_draft_id

def test_3_publish_overwrites_original(staging_id: int, orig_id: int) -> None:
    print("[3] Publish staging to ORIGINAL via /rex/v1/publish ...")
    pub_res = rex_publish(staging_id)
    assert_true(pub_res.get("used_original") is True, "publish should overwrite original when original_post_id present")
    assert_equal(int(pub_res["published_id"]), orig_id, "published_id should equal original id")

    # Verify title propagated
    post = wp_post_get(orig_id, fields="id,title,content")
    title = post.get("title", {}).get("rendered", "")
    assert_true("REX Updated via conflict" in title or "REX Updated via conflict" == title, "original title should have been updated")
    print(f"[3] OK published to original {orig_id} and updated content/title.")

def test_4_fork_from_draft_has_no_original() -> int:
    ts = int(time.time())
    print("[4] Create a DRAFT source and fork it (should NOT carry original_post_id)...")
    src = wp_post_create(title=f"REX Draft Source {ts}", content="Source draft", status="draft")
    src_id = int(src["id"])
    fork_res = rex_fork(source_id=src_id)
    fork_id = int(fork_res["id"])
    # original_post_id should be null/absent/0
    opid = fork_res.get("original_post_id")
    assert_true(opid in (None, 0, "0", ""), "fork from draft should NOT have original_post_id")
    print(f"[4] OK forked draft {fork_id} without original_post_id")
    return fork_id

def test_5_publish_without_original_publishes_self(staging_id: int) -> None:
    print("[5] Publish a staging that has NO original (should publish itself)...")
    pub_res = rex_publish(staging_id)
    assert_true(pub_res.get("used_original") is False, "publish should not use original when none present")
    assert_equal(int(pub_res["published_id"]), staging_id, "published_id should equal staging id")
    print(f"[5] OK published {staging_id} as itself.")

def main() -> None:
    try:
        orig_id, fork_id = test_1_fork_from_published()
        conflict_draft_id = test_2_save_conflict_creates_fork(orig_id)
        test_3_publish_overwrites_original(conflict_draft_id, orig_id)
        staging_no_original = test_4_fork_from_draft_has_no_original()
        test_5_publish_without_original_publishes_self(staging_no_original)
        print("\nALL TESTS PASSED ✔")  # noqa: W605
    except AssertionError as e:
        print(f"\nTEST FAILED: {e}", file=sys.stderr)
        sys.exit(1)
    except requests.RequestException as e:
        print(f"\nHTTP ERROR: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
