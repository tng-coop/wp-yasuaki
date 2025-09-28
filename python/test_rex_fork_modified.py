#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Tests for returning modified_gmt on forks and continuing edits with the returned token.

Assumptions:
- scripts/reset-wp.sh ran in the same shell (env vars exported)
- contributor role has caps:
    wp cap add contributor upload_files edit_others_posts delete_others_posts
- WP_APP_CONTRIBUTOR2 is set (second contributor session available)

Env used:
  WP_BASE_URL
  WP_USERNAME, WP_APP_PASSWORD
  WP_CONTRIB_USERNAME, WP_APP_CONTRIBUTOR
  WP_CONTRIB2_USERNAME, WP_APP_CONTRIBUTOR2
"""

import json
import os
import sys
import time
from typing import Any, Dict, Optional

import requests
from requests.auth import HTTPBasicAuth

# --- Config & sessions -------------------------------------------------------

BASE_URL = os.getenv("WP_BASE_URL") or "http://localhost:8000"
TIMEOUT = 30

def _u(path: str) -> str:
    if not path.startswith("/"):
        path = "/" + path
    return BASE_URL.rstrip("/") + path

def _make_session(user: Optional[str], app_pw: Optional[str]) -> Optional[requests.Session]:
    if not user or not app_pw:
        return None
    s = requests.Session()
    s.auth = HTTPBasicAuth(user, app_pw)
    s.headers.update({"Accept": "application/json"})
    return s

# Admin (required)
S_ADMIN = _make_session(os.getenv("WP_USERNAME"), os.getenv("WP_APP_PASSWORD"))
if S_ADMIN is None:
    print("ERROR: WP_USERNAME / WP_APP_PASSWORD must be set", file=sys.stderr)
    sys.exit(2)

# Contributor 1
S_C1 = _make_session(os.getenv("WP_CONTRIB_USERNAME", "contributor"),
                     os.getenv("WP_APP_CONTRIBUTOR"))

# Contributor 2 (required for this test per assumption)
S_C2 = _make_session(os.getenv("WP_CONTRIB2_USERNAME", "contributor2"),
                     os.getenv("WP_APP_CONTRIBUTOR2"))

if S_C1 is None or S_C2 is None:
    print("ERROR: configure both contributors (WP_APP_CONTRIBUTOR and WP_APP_CONTRIBUTOR2)", file=sys.stderr)
    sys.exit(2)

# --- Tiny helpers ------------------------------------------------------------

def _pp(label: str, payload: Any) -> None:
    print(label + ":")
    try:
        print(json.dumps(payload, indent=2, ensure_ascii=False))
    except Exception:
        print(str(payload))

def _json_or_text(r: requests.Response) -> Any:
    try:
        return r.json()
    except Exception:
        return r.text

def _require_ok(r: requests.Response) -> Dict[str, Any]:
    if 200 <= r.status_code < 300:
        try:
            return r.json()
        except Exception as e:
            raise AssertionError(f"Non-JSON OK response: {e} // {r.text}") from e
    body = _json_or_text(r)
    raise AssertionError(f"HTTP {r.status_code}: {body}")

def assert_true(cond: bool, msg: str = "assert_true failed") -> None:
    if not cond:
        raise AssertionError(msg)

def assert_equal(a: Any, b: Any, msg: str = "") -> None:
    if a != b:
        raise AssertionError(msg or f"assert_equal failed: {a} != {b}")

# --- Endpoint wrappers -------------------------------------------------------

def wp_post_create(session: requests.Session, payload: Dict[str, Any]) -> Dict[str, Any]:
    # For WP core create, keys are 'title', 'content', 'status'
    r = session.post(_u("/wp-json/wp/v2/posts"), json=payload, timeout=TIMEOUT)
    return _require_ok(r)

def wp_post_publish(post_id: int) -> Dict[str, Any]:
    r = S_ADMIN.post(_u(f"/wp-json/wp/v2/posts/{post_id}"),
                     json={"status": "publish"}, timeout=TIMEOUT)
    return _require_ok(r)

def wp_post_get(session: requests.Session, post_id: int, context: str = "edit") -> Dict[str, Any]:
    r = session.get(_u(f"/wp-json/wp/v2/posts/{post_id}"),
                    params={"context": context}, timeout=TIMEOUT)
    return _require_ok(r)

def rex_save(session: requests.Session, post_id: int, data: Dict[str, Any]) -> requests.Response:
    # rex/v1/save expects {"id": <int>, "data": {...}}
    return session.post(_u("/wp-json/rex/v1/save"),
                        json={"id": int(post_id), "data": data}, timeout=TIMEOUT)

def rex_fork(session: requests.Session, source_id: int, status: str = "draft") -> Dict[str, Any]:
    r = session.post(_u("/wp-json/rex/v1/fork"),
                     json={"source_id": int(source_id), "status": status}, timeout=TIMEOUT)
    return _require_ok(r)

# --- Tests -------------------------------------------------------------------

def test_two_contributors_conflict_fork_includes_modified_gmt() -> None:
    print("[1] Two-contributor conflict should fork and include modified_gmt…")

    # c1 creates a PENDING post (others with edit_others_posts can edit it)
    ts = int(time.time())
    src = wp_post_create(S_C1, {
        "title": f"REX conflict mgmt {ts}",
        "content": "v1",
        "status": "pending",
    })
    orig_id = int(src["id"])

    # c1 loads to capture the concurrency token
    p1 = wp_post_get(S_C1, orig_id, context="edit")
    token = str(p1.get("modified_gmt") or "").strip()
    assert_true(bool(token), "expected non-empty modified_gmt from GET")

    # c2 edits the same post (rotate modified_gmt)
    r_touch = rex_save(S_C2, orig_id, {
        "post_content": "v2 (c2 edit)",
        # No token on purpose; c2 is the first saver
    })
    payload_touch = _require_ok(r_touch)
    _pp("[1] c2 save (rotate token)", payload_touch)
    assert_true(payload_touch.get("forked") is not True, "first update should not fork")
    assert_equal(int(payload_touch.get("id") or 0), orig_id, "update should target same id")

    # c1 saves with stale token -> must fork
    r = rex_save(S_C1, orig_id, {
        "post_content": "v3 (c1 stale)",
        "expected_modified_gmt": token,
    })
    payload = _require_ok(r)
    _pp("[1] c1 save (expect fork)", payload)

    assert_true(bool(payload.get("forked")), f"Expected forked=true, got: {payload}")
    assert_equal(int(payload.get("original_post_id") or 0), orig_id, "original_post_id mismatch")

    mg = str(payload.get("modified_gmt") or "").strip()
    assert_true(bool(mg), "fork payload must include modified_gmt")

    # Immediately save the fork using the returned token -> should NOT fork again
    fork_id = int(payload.get("id") or 0)
    r_next = rex_save(S_C1, fork_id, {
        "post_content": "v4 (post-fork immediate edit)",
        "expected_modified_gmt": mg,
    })
    next_payload = _require_ok(r_next)
    _pp("[1] immediate post-fork save", next_payload)

    assert_true(next_payload.get("forked") is not True, f"unexpected second fork: {next_payload}")
    assert_equal(int(next_payload.get("id") or 0), fork_id, "post-fork save should update same id")
    assert_true(str(next_payload.get("modified_gmt") or "").strip() != "", "save response must include modified_gmt")

    print(f"[1] OK conflict yielded draft #{fork_id} with modified_gmt and the token worked onward")

def test_explicit_fork_includes_modified_gmt() -> None:
    print("[2] Explicit /rex/v1/fork should include modified_gmt…")

    # c1 creates pending; admin publishes; c2 forks the published post
    ts = int(time.time())
    source = wp_post_create(S_C1, {
        "title": f"REX explicit fork mgmt {ts}",
        "content": "live",
        "status": "pending",
    })
    source_id = int(source["id"])
    wp_post_publish(source_id)

    f = rex_fork(S_C2, source_id)
    _pp("[2] fork response", f)

    assert_equal(int(f.get("original_post_id") or 0), source_id, "fork original_post_id mismatch")
    assert_true(str(f.get("modified_gmt") or "").strip() != "", "fork payload must include modified_gmt")

    print(f"[2] OK explicit fork by contributor2 includes modified_gmt (draft #{f['id']})")

# --- Main --------------------------------------------------------------------

def main() -> None:
    try:
        test_two_contributors_conflict_fork_includes_modified_gmt()
        test_explicit_fork_includes_modified_gmt()
        print(" REX FORK modified_gmt TESTS PASSED ✔")
    except AssertionError as e:
        print(f" TEST FAILED: {e}", file=sys.stderr)
        sys.exit(1)
    except requests.RequestException as e:
        print(f" HTTP ERROR: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
