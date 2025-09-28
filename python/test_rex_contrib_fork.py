#!/usr/bin/env python3
# Path: python/test_rex_contributor_fork.py
"""
Repro/guard test for contributor → fork published post.

By default (current server behavior), a WordPress Contributor trying to fork a
PUBLISHED post created by someone else gets 403 Forbidden. This script confirms
that behavior. If/when you change server permissions to allow it, flip the
EXPECT_CONTRIB_FORK_OK flag to assert success instead.

Env vars required (admin creds are used to create the source post):
  WP_BASE_URL           (e.g., https://wp.lan)
  WP_USERNAME           (e.g., admin)
  WP_APP_PASSWORD       (application password for admin)
  WP_APP_CONTRIBUTOR (application password for contributor)

Optional:
  WP_CONTRIB_USERNAME   (default: "contributor")
  WP_VERIFY_SSL         (default: false)
  EXPECT_CONTRIB_FORK_OK (default: 0; set to 1 once policy allows forking)

Run:
  pip install -r requirements.txt
  WP_BASE_URL=https://wp.lan WP_USERNAME=admin WP_APP_PASSWORD=a \
  WP_CONTRIB_APP_PASSWORD=... \
  python python/test_rex_contributor_fork.py
"""
from __future__ import annotations

import json
import os
import sys
import time
from typing import Any, Dict

import requests
from requests.auth import HTTPBasicAuth

# --- Config ---
BASE_URL = os.getenv("WP_BASE_URL")
ADMIN_USER = os.getenv("WP_USERNAME")
ADMIN_PASS = os.getenv("WP_APP_PASSWORD")
VERIFY_SSL = os.getenv("WP_VERIFY_SSL", "false").strip().lower() in ("1", "true", "yes")

CONTRIB_USER = os.getenv("WP_CONTRIB_USERNAME", "contributor")
CONTRIB_PASS = os.getenv("WP_APP_CONTRIBUTOR")  # required, no default
EXPECT_OK = os.getenv("EXPECT_CONTRIB_FORK_OK", "0").strip().lower() in ("1", "true", "yes")

if not BASE_URL or not ADMIN_USER or not ADMIN_PASS or not CONTRIB_PASS:
    print(
        "Missing required env vars. Set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD, WP_CONTRIB_APP_PASSWORD.",
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


def _pp(label: str, data: Any) -> None:
    try:
        print(f"{label}: {json.dumps(data, indent=2, ensure_ascii=False)}")
    except Exception:
        print(f"{label}: {data}")


def _require_ok(r: requests.Response) -> Dict[str, Any]:
    try:
        data = r.json()
    except Exception:
        data = {"raw": r.text}
    if not r.ok:
        raise AssertionError(f"HTTP {r.status_code}: {data}")
    return data  # type: ignore[return-value]


def make_session(user: str, password: str) -> requests.Session:
    s = requests.Session()
    s.auth = HTTPBasicAuth(user, password)
    s.verify = VERIFY_SSL
    s.headers.update({"Accept": "application/json"})
    return s


S_ADMIN = make_session(ADMIN_USER, ADMIN_PASS)
S_CONTRIB = make_session(CONTRIB_USER, CONTRIB_PASS)


# --- WordPress helpers (admin session for setup) ---

def wp_post_create_published(title: str) -> int:
    r = S_ADMIN.post(
        _u("/wp-json/wp/v2/posts"),
        json={"title": title, "content": "seed body", "status": "publish"},
        timeout=30,
    )
    data = _require_ok(r)
    return int(data["id"])  # type: ignore[index]


# --- REX endpoint call (contributor session) ---

def rex_fork_as_contrib_raw(source_id: int, status: str = "draft") -> requests.Response:
    return S_CONTRIB.post(
        _u("/wp-json/rex/v1/fork"),
        json={"source_id": source_id, "status": status},
        timeout=30,
    )


# --- Test ---

def test_contributor_fork_published() -> None:
    ts = int(time.time())
    print("[Contrib] Create PUBLISHED post as admin, then try to fork as contributor…")
    orig_id = wp_post_create_published(f"REX ContribFork {ts}")

    r = rex_fork_as_contrib_raw(orig_id)
    try:
        payload = r.json()
    except Exception:
        payload = {"raw": r.text}

    _pp("[Contrib] Fork response", {"status": r.status_code, "json": payload})

    if EXPECT_OK:
        if not r.ok:
            raise AssertionError(f"Expected contributor to fork OK, got HTTP {r.status_code}: {payload}")
        new_id = payload.get("id")
        if not isinstance(new_id, int):
            raise AssertionError(f"Expected fork payload to contain 'id', got: {payload}")
        print(f"[Contrib] Fork succeeded, new post id={new_id}")
    else:
        if r.status_code != 403:
            raise AssertionError(f"Expected HTTP 403 for contributor fork, got {r.status_code}: {payload}")
        print("[Contrib] As expected, contributor cannot fork a published post (403).")


if __name__ == "__main__":
    test_contributor_fork_published()
