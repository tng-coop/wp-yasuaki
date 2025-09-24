#!/usr/bin/env python3
# Path: tests/test_rex_endpoints_essentials.py
"""
Additional essential tests for the custom REX WordPress endpoints.

Covers:
- Save success path (no fork, with and without optimistic concurrency)
- Fork-of-fork always points to the root original
- Publish untrashes and overwrites the original
- Publish with hard-deleted original falls back to publishing staging itself and clears original meta
- Negative: fork non-existent source -> 404
- Save ignores client-provided _rex_original_post_id meta
- Taxonomy copy on fork (categories)

Env vars required:
  WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD
Optional:
  WP_VERIFY_SSL=true|false

Run:
  pip install -r requirements.txt
  WP_BASE_URL=https://wp.lan WP_USERNAME=admin WP_APP_PASSWORD=xxxx python tests/test_rex_endpoints_essentials.py
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


def wp_post_delete(pid: int, force: bool = False) -> Dict[str, Any]:
    r = S.delete(_u(f"/wp-json/wp/v2/posts/{pid}"), params={"force": "true" if force else "false"}, timeout=30)
    try:
        data = r.json()
    except Exception:
        data = {"raw": r.text}
    if not r.ok:
        raise AssertionError(f"Delete failed HTTP {r.status_code}: {data}")
    return data


def wp_category_create(name: str) -> Dict[str, Any]:
    r = S.post(_u("/wp-json/wp/v2/categories"), json={"name": name}, timeout=30)
    return _require_ok(r)


def rex_fork(source_id: int, status: str = "draft") -> Dict[str, Any]:
    r = S.post(_u("/wp-json/rex/v1/fork"), json={"source_id": source_id, "status": status}, timeout=30)
    return _require_ok(r)


def rex_fork_raw(source_id: int, status: str = "draft") -> requests.Response:
    return S.post(_u("/wp-json/rex/v1/fork"), json={"source_id": source_id, "status": status}, timeout=30)


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


# --- Tests ---

def test_a_save_success_no_fork() -> int:
    ts = int(time.time())
    print("[A] Create DRAFT, save twice (second with optimistic concurrency, no fork)...")
    draft = wp_post_create({"title": f"REX SaveOK {ts}", "content": "v1", "status": "draft"})
    pid = int(draft["id"])
    _pp("[A] Created draft", {"id": pid})

    # First save without concurrency token
    res1 = rex_save(pid, {"post_title": "REX SaveOK A1"})
    _pp("[A] First save response", res1)

    # Expect the happy path: saved==True and same id
    assert_true(res1.get("forked") is not True, f"unexpected fork on first save: {res1}")
    assert_true(res1.get("saved") in (True, 1, "true"), f"save path expected saved=true, got: {res1}")
    assert_equal(int(res1.get("id", 0)), pid, f"expected same id on save, got: {res1}")

    # Derive concurrency token; fall back to fetching if absent
    token = str(res1.get("modified_gmt") or "").strip()
    if not token:
        fetched = wp_post_get(pid, fields="modified_gmt")
        token = str(fetched.get("modified_gmt") or "").strip()
    assert_true(bool(token), f"modified_gmt token should be present (got: {res1})")
    _pp("[A] Using modified_gmt token", token)

    # Second save with concurrency token
    res2 = rex_save(pid, {"post_content": "v2 - concurrency pass", "expected_modified_gmt": token})
    _pp("[A] Second save response", res2)

    assert_true(res2.get("forked") is not True, f"unexpected fork on second save: {res2}")
    assert_true(res2.get("saved") in (True, 1, "true"), f"second save expected saved=true, got: {res2}")
    assert_equal(int(res2.get("id", 0)), pid, f"expected same id on save2, got: {res2}")

    # Verify persisted content
    post = wp_post_get(pid)
    title = post.get("title", {}).get("rendered", "")
    content = post.get("content", {}).get("rendered", "")
    _pp("[A] Persisted post (subset)", {"id": pid, "title": title, "content": content[:80]})
    assert_true("REX SaveOK A1" in title, f"title not updated as expected: {title}")
    content_norm = html.unescape(content)
    assert_true("concurrency pass" in content_norm, f"content not updated as expected: {content}")

    print(f"[A] OK saved post {pid} without forking")
    return pid


def test_b_fork_of_fork_points_to_root() -> None:
    ts = int(time.time())
    print("[B] Fork-of-fork points to root original...")
    orig = wp_post_create({"title": f"REX Root {ts}", "content": "root", "status": "publish"})
    orig_id = int(orig["id"])

    f1 = rex_fork(orig_id)
    f1_id = int(f1["id"])
    assert_equal(int(f1.get("original_post_id") or 0), orig_id)

    f2 = rex_fork(f1_id)
    assert_equal(int(f2.get("original_post_id") or 0), orig_id, "fork-of-fork should keep root original id")
    print(f"[B] OK fork-of-fork retains root {orig_id}")


def test_c_publish_untrash_overwrite() -> None:
    ts = int(time.time())
    print("[C] Publish should untrash and overwrite original...")
    orig = wp_post_create({"title": f"REX Untrash {ts}", "content": "live", "status": "publish"})
    orig_id = int(orig["id"])

    stg = rex_fork(orig_id)
    stg_id = int(stg["id"])

    # Trash the original
    wp_post_delete(orig_id, force=False)

    # Update staging then publish
    rex_save(stg_id, {"post_title": f"REX Untrash New {ts}", "post_content": "updated before publish"})
    pub = rex_publish(stg_id)
    assert_true(pub.get("used_original") is True)
    assert_equal(int(pub["published_id"]), orig_id)

    post = wp_post_get(orig_id, fields="id,status,title")
    assert_equal(post.get("status"), "publish")
    assert_true("REX Untrash New" in post.get("title", {}).get("rendered", ""))
    # Verify staging got trashed after successful swap
    stg_after = wp_post_get(stg_id, fields="status")
    assert_equal(stg_after.get("status"), "trash", f"staging should be trashed, got: {stg_after}")
    print(f"[C] OK original {orig_id} untrashed & overwritten; staging trashed")


def test_d_publish_hard_deleted_original_fallback() -> None:
    ts = int(time.time())
    print("[D] Publish should fallback to self when original hard-deleted, and clear meta...")
    orig = wp_post_create({"title": f"REX HardDel {ts}", "content": "live", "status": "publish"})
    orig_id = int(orig["id"])

    stg = rex_fork(orig_id)
    stg_id = int(stg["id"])

    # Hard delete original
    wp_post_delete(orig_id, force=True)

    pub = rex_publish(stg_id)
    assert_true(pub.get("used_original") is False)
    assert_equal(int(pub["published_id"]), stg_id)

    # Ensure original meta was cleared on the staging post promoted to publish
    post = wp_post_get(stg_id, fields="id,status,title,meta")
    val = (post.get("meta") or {}).get("_rex_original_post_id")
    assert_true(val in (None, 0, "0", ""), "original meta should be cleared on fallback publish")
    print(f"[D] OK published staging {stg_id} as new and cleared meta")


def test_e_fork_nonexistent_404() -> None:
    print("[E] Forking non-existent source should 404...")
    huge_id = 2147480000  # extremely unlikely to exist
    r = rex_fork_raw(huge_id)
    assert_equal(r.status_code, 404, f"Expected 404 for non-existent source, got {r.status_code}")
    print("[E] OK got 404 for non-existent source")


def test_f_save_ignores_original_meta() -> None:
    ts = int(time.time())
    print("[F] Save should ignore client-provided _rex_original_post_id meta...")
    draft = wp_post_create({"title": f"REX IgnoreMeta {ts}", "content": "body", "status": "draft"})
    pid = int(draft["id"])

    res = rex_save(pid, {"post_title": "Ignore meta attempt", "meta": {"_rex_original_post_id": 12345}})
    assert_true(res.get("saved") in (True, 1, "true"), f"save failed unexpectedly: {res}")

    post = wp_post_get(pid, fields="id,meta,title")
    val = (post.get("meta") or {}).get("_rex_original_post_id")
    assert_true(val in (None, 0, "0", ""), "save should not write _rex_original_post_id from client")
    print("[F] OK meta write was ignored as expected")


def test_g_taxonomy_copy_on_fork() -> None:
    ts = int(time.time())
    print("[G] Taxonomy (category) terms should copy on fork...")
    cat = wp_category_create(f"rex-cat-{ts}")
    cat_id = int(cat["id"])

    orig = wp_post_create({
        "title": f"REX Cats {ts}",
        "content": "with cats",
        "status": "publish",
        "categories": [cat_id],
    })
    orig_id = int(orig["id"])

    f = rex_fork(orig_id)
    fid = int(f["id"])

    post = wp_post_get(fid, fields="id,categories")
    cats = post.get("categories", []) or []
    assert_true(cat_id in cats, "fork should inherit category term(s)")
    print(f"[G] OK fork {fid} inherited categories {cats}")


def main() -> None:
    try:
        test_a_save_success_no_fork()
        test_b_fork_of_fork_points_to_root()
        test_c_publish_untrash_overwrite()
        test_d_publish_hard_deleted_original_fallback()
        test_e_fork_nonexistent_404()
        test_f_save_ignores_original_meta()
        test_g_taxonomy_copy_on_fork()
        print(" ESSENTIAL TESTS PASSED ✔")
    except AssertionError as e:
        print(f" TEST FAILED: {e}", file=sys.stderr)
        sys.exit(1)
    except requests.RequestException as e:
        print(f" HTTP ERROR: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
