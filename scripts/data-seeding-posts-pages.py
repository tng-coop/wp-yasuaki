#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Idempotent seeder with UPDATE mode:
- Zero-arg: creates EXACTLY 1 page + 3 posts per *real* branch (skips existing).
- --update: updates existing items using your templates (only when changed).

Branches = children of Categories parent with slug "branch".
Child slugs follow: "branch-<ID>" (from scripts/offices.csv).

CLI / Env:
  --update                     | SEED_UPDATE=1
  --update-scope {all,pages,posts} | SEED_UPDATE_SCOPE
  --page-title "{name}"        | SEED_PAGE_TITLE
  --post-title  "{name} #{index}" | SEED_POST_TITLE
  --page-content-file path.html| SEED_PAGE_CONTENT_FILE
  --post-content-file path.html| SEED_POST_CONTENT_FILE

Placeholders:
  Pages: {name}, {slug}, {term_id}
  Posts: {name}, {slug}, {term_id}, {index}

Required environment (same as before):
  WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD
"""

from __future__ import annotations
import argparse
import csv
import os
import re
import sys
from typing import Any, Dict, List, Optional, Tuple

import requests

# --------------------------- Config & session ---------------------------

WP_BASE_URL = (os.environ.get("WP_BASE_URL") or "").rstrip("/")
WP_USERNAME = os.environ.get("WP_USERNAME") or ""
WP_APP_PASSWORD = (os.environ.get("WP_APP_PASSWORD") or "").replace(" ", "")

if not (WP_BASE_URL and WP_USERNAME and WP_APP_PASSWORD):
    print("Set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD", file=sys.stderr, flush=True)
    sys.exit(1)

API_ROOT = f"{WP_BASE_URL}/wp-json/wp/v2"
PAGES_ENDPOINT = f"{API_ROOT}/pages"
POSTS_ENDPOINT = f"{API_ROOT}/posts"
CATEGORIES_ENDPOINT = f"{API_ROOT}/categories"

SESSION = requests.Session()
SESSION.auth = (WP_USERNAME, WP_APP_PASSWORD)
SESSION.headers.update(
    {"Accept": "application/json", "Content-Type": "application/json; charset=utf-8"}
)

REQUEST_TIMEOUT = 20  # seconds

# --------------------------- Helpers ---------------------------

def _slugify(s: str) -> str:
    s = (s or "").strip()
    s = re.sub(r"[^a-zA-Z0-9_-]+", "-", s)
    s = re.sub(r"-{2,}", "-", s).strip("-").lower()
    return s

def _csv_path() -> Optional[str]:
    here = os.path.dirname(os.path.abspath(__file__))
    p = os.path.join(here, "offices.csv")
    return p if os.path.exists(p) else None

def _read_file_or_blank(path: Optional[str]) -> str:
    if not path:
        return ""
    try:
        with open(path, "r", encoding="utf-8") as f:
            return f.read()
    except Exception as e:
        print(f"[WARN] Could not read template file '{path}': {e}", file=sys.stderr)
        return ""

def _render(tpl: str, **ctx) -> str:
    # Safe-ish str.format with our placeholders only.
    # If the template contains unmatched braces, leave it as-is.
    try:
        return tpl.format(**ctx)
    except Exception:
        return tpl

def _normalize_html(s: Optional[str]) -> str:
    if not s:
        return ""
    # light normalization to avoid false diffs
    return re.sub(r"\s+", " ", s).strip()

# --------------------------- Categories (branch) ---------------------------

def ensure_parent_branch_category() -> int:
    r = SESSION.get(
        CATEGORIES_ENDPOINT,
        params={"slug": "branch", "_fields": "id,slug"},
        timeout=REQUEST_TIMEOUT,
    )
    r.raise_for_status()
    items = r.json() if isinstance(r.json(), list) else []
    if items:
        pid = int(items[0]["id"])
        print(f"[INFO] Using parent category 'Branch' (id={pid}).", flush=True)
        return pid

    r = SESSION.post(
        CATEGORIES_ENDPOINT,
        json={"name": "Branch", "slug": "branch", "parent": 0},
        timeout=REQUEST_TIMEOUT,
    )
    r.raise_for_status()
    pid = int(r.json()["id"])
    print(f"[OK] Created parent category 'Branch' (id={pid}).", flush=True)
    return pid

def fetch_branch_children(parent_id: int) -> List[Dict[str, Any]]:
    out: List[Dict[str, Any]] = []
    page = 1
    while True:
        r = SESSION.get(
            CATEGORIES_ENDPOINT,
            params={"parent": parent_id, "per_page": 100, "page": page, "_fields": "id,slug,name"},
            timeout=REQUEST_TIMEOUT,
        )
        r.raise_for_status()
        batch = r.json() or []
        if not batch:
            break
        out.extend(batch)
        if len(batch) < 100:
            break
        page += 1
    return out

def ensure_child_category(name: str, slug: str, parent_id: int) -> int:
    r = SESSION.get(
        CATEGORIES_ENDPOINT,
        params={"slug": slug, "parent": parent_id, "_fields": "id,slug"},
        timeout=REQUEST_TIMEOUT,
    )
    r.raise_for_status()
    items = r.json() if isinstance(r.json(), list) else []
    if items:
        return int(items[0]["id"])

    r = SESSION.post(
        CATEGORIES_ENDPOINT,
        json={"name": name, "slug": slug, "parent": parent_id},
        timeout=REQUEST_TIMEOUT,
    )
    r.raise_for_status()
    tid = int(r.json()["id"])
    print(f"[OK] Created branch category '{name}' (slug={slug}, id={tid}).", flush=True)
    return tid

def ensure_children_from_offices_csv(parent_id: int) -> None:
    csv_path = _csv_path()
    if not csv_path:
        print("[INFO] No branch children found and scripts/offices.csv is missing; nothing to create.", flush=True)
        return

    with open(csv_path, newline="", encoding="utf-8") as f:
        rdr = csv.DictReader(f)
        required = {"ID", "Office"}
        if not required.issubset(set(rdr.fieldnames or [])):
            print(f"[WARN] offices.csv missing required headers {sorted(required)}; found {rdr.fieldnames or []}", flush=True)
            return

        created = 0
        for row in rdr:
            code = (row.get("ID") or "").strip()
            name = (row.get("Office") or "").strip()
            if not code or not name:
                continue
            slug = f"branch-{_slugify(code)}"
            ensure_child_category(name, slug, parent_id)
            created += 1

        if created:
            print(f"[INFO] Ensured {created} branch categories from offices.csv.", flush=True)

# --------------------------- Content fetch / upsert ---------------------------

def get_single_item(endpoint: str, slug: str) -> Optional[int]:
    r = SESSION.get(endpoint, params={"slug": slug, "_fields": "id,slug,status"}, timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    items = r.json()
    if not items:
        return None
    if isinstance(items, dict):
        return int(items.get("id")) if items.get("id") else None
    return int(items[0]["id"])

def get_item_raw(endpoint: str, item_id: int) -> Dict[str, str]:
    """Fetch raw title & content for comparison (context=edit)."""
    r = SESSION.get(
        f"{endpoint}/{item_id}",
        params={"context": "edit", "_fields": "title,content,categories"},
        timeout=REQUEST_TIMEOUT,
    )
    r.raise_for_status()
    data = r.json() or {}
    title_raw = (data.get("title") or {}).get("raw") or ""
    content_raw = (data.get("content") or {}).get("raw") or ""
    cats = data.get("categories") or []
    return {"title": title_raw, "content": content_raw, "categories": cats}

# --------------------------- Templates ---------------------------

def resolve_templates(args):
    # Defaults match your current wording
    page_title_tpl = (
        args.page_title
        or os.environ.get("SEED_PAGE_TITLE")
        or "{name}"
    )
    post_title_tpl = (
        args.post_title
        or os.environ.get("SEED_POST_TITLE")
        or "{name} Branch Update #{index}"
    )

    page_content_tpl = _read_file_or_blank(args.page_content_file or os.environ.get("SEED_PAGE_CONTENT_FILE"))
    post_content_tpl = _read_file_or_blank(args.post_content_file or os.environ.get("SEED_POST_CONTENT_FILE"))

    if not page_content_tpl:
        page_content_tpl = (
            "<p>Welcome to the {name} branch page.</p>\n"
            "<p>This page provides an overview and sample content for the {name} branch.</p>"
        )
    if not post_content_tpl:
        post_content_tpl = (
            "<p>This is sample post {index} for the {name} branch.</p>\n"
            "<p>It demonstrates assigning content to the branch category.</p>"
        )

    return page_title_tpl, page_content_tpl, post_title_tpl, post_content_tpl

def build_page_payload(term: Dict[str, Any], page_title_tpl: str, page_content_tpl: str) -> Dict[str, Any]:
    slug = term.get("slug") or "branch"
    ctx = {"name": term.get("name") or "Branch", "slug": slug, "term_id": term.get("id")}
    return {
        "status": "publish",
        "slug": f"{slug}-page",
        "title": _render(page_title_tpl, **ctx),
        "content": _render(page_content_tpl, **ctx),
    }

def build_post_payload(term: Dict[str, Any], index: int, post_title_tpl: str, post_content_tpl: str) -> Dict[str, Any]:
    slug = term.get("slug") or "branch"
    ctx = {"name": term.get("name") or "Branch", "slug": slug, "term_id": term.get("id"), "index": index}
    return {
        "status": "publish",
        "slug": f"{slug}-post-{index}",
        "title": _render(post_title_tpl, **ctx),
        "content": _render(post_content_tpl, **ctx),
        "categories": [term.get("id")] if term.get("id") is not None else [],
    }

# --------------------------- Ensure (create / update) ---------------------------

def ensure_page(term: Dict[str, Any], update: bool, page_title_tpl: str, page_content_tpl: str, update_scope: str) -> Tuple[str, str]:
    payload = build_page_payload(term, page_title_tpl, page_content_tpl)
    slug = payload["slug"]
    existing_id = get_single_item(PAGES_ENDPOINT, slug)
    if not existing_id:
        r = SESSION.post(PAGES_ENDPOINT, json=payload, timeout=REQUEST_TIMEOUT)
        r.raise_for_status()
        return "created", f"created (post_id={int(r.json()['id'])})"

    if not update or update_scope == "posts":
        return "skipped", f"exists (post_id={existing_id})"

    # Compare current vs desired
    cur = get_item_raw(PAGES_ENDPOINT, existing_id)
    need_title = _normalize_html(cur["title"]) != _normalize_html(payload["title"])
    need_content = _normalize_html(cur["content"]) != _normalize_html(payload["content"])
    if not (need_title or need_content):
        return "skipped", f"up-to-date (post_id={existing_id})"

    delta = {}
    if need_title: delta["title"] = payload["title"]
    if need_content: delta["content"] = payload["content"]
    r = SESSION.post(f"{PAGES_ENDPOINT}/{existing_id}", json=delta, timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    return "updated", f"updated (post_id={existing_id}, fields={','.join(delta.keys())})"

def ensure_posts(term: Dict[str, Any], update: bool, post_title_tpl: str, post_content_tpl: str, update_scope: str) -> Tuple[int, int, int]:
    created = updated = skipped = 0
    for i in range(1, 4):
        payload = build_post_payload(term, i, post_title_tpl, post_content_tpl)
        slug = payload["slug"]
        existing_id = get_single_item(POSTS_ENDPOINT, slug)
        if not existing_id:
            r = SESSION.post(POSTS_ENDPOINT, json=payload, timeout=REQUEST_TIMEOUT)
            r.raise_for_status()
            created += 1
            print(f"    [OK] {slug} created (post_id={int(r.json()['id'])}).", flush=True)
            continue

        if not update or update_scope == "pages":
            skipped += 1
            print(f"    [SKIP] {slug} exists (post_id={existing_id}).", flush=True)
            continue

        # Compare and update only when changed
        cur = get_item_raw(POSTS_ENDPOINT, existing_id)
        need_title = _normalize_html(cur["title"]) != _normalize_html(payload["title"])
        need_content = _normalize_html(cur["content"]) != _normalize_html(payload["content"])
        # also ensure branch category is present
        need_cats = payload["categories"] and set(payload["categories"]) - set(cur.get("categories") or [])
        if not (need_title or need_content or need_cats):
            skipped += 1
            print(f"    [OK] {slug} up-to-date (post_id={existing_id}).", flush=True)
            continue

        delta = {}
        if need_title: delta["title"] = payload["title"]
        if need_content: delta["content"] = payload["content"]
        if need_cats: delta["categories"] = list(set((cur.get("categories") or [])) | set(payload["categories"]))
        r = SESSION.post(f"{POSTS_ENDPOINT}/{existing_id}", json=delta, timeout=REQUEST_TIMEOUT)
        r.raise_for_status()
        updated += 1
        print(f"    [OK] {slug} updated (post_id={existing_id}, fields={','.join(delta.keys())}).", flush=True)
    return created, updated, skipped

# --------------------------- Main ---------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description="Seed branch pages and posts (with optional update mode).")
    parser.add_argument("--update", action="store_true", help="Update existing pages/posts using provided templates.")
    parser.add_argument("--update-scope", choices=["all", "pages", "posts"], default=os.environ.get("SEED_UPDATE_SCOPE", "all"),
                        help="Limit updates to pages or posts (default: all).")
    parser.add_argument("--page-title", help="Title template for pages. Placeholders: {name},{slug},{term_id}.")
    parser.add_argument("--post-title", help="Title template for posts. Placeholders: {name},{slug},{term_id},{index}.")
    parser.add_argument("--page-content-file", help="Path to HTML template file for page content.")
    parser.add_argument("--post-content-file", help="Path to HTML template file for post content.")
    args = parser.parse_args()

    # allow env var to flip update on without flags
    update_mode = args.update or os.environ.get("SEED_UPDATE", "").strip() in ("1", "true", "yes")
    update_scope = args.update_scope

    page_title_tpl, page_content_tpl, post_title_tpl, post_content_tpl = resolve_templates(args)

    print("[STEP] Ensuring parent 'Branch' category…", flush=True)
    parent_id = ensure_parent_branch_category()

    print("[STEP] Loading existing branch categories…", flush=True)
    terms = fetch_branch_children(parent_id)

    if not terms:
        print("[STEP] No branches found; creating from scripts/offices.csv…", flush=True)
        ensure_children_from_offices_csv(parent_id)
        terms = fetch_branch_children(parent_id)

    if not terms:
        print("[INFO] No branches available and no offices.csv to create from. Nothing to seed.", flush=True)
        return

    print(f"[INFO] Loaded {len(terms)} branch term(s). Update mode: {update_mode} (scope={update_scope})", flush=True)

    pages_created = pages_updated = pages_skipped = 0
    posts_created = posts_updated = posts_skipped = 0

    for term in terms:
        name = term.get("name") or term.get("slug") or "branch"
        print(f"[TERM] {name} (id={term.get('id')}, slug={term.get('slug')})", flush=True)

        status, msg = ensure_page(term, update_mode, page_title_tpl, page_content_tpl, update_scope)
        if status == "created":
            pages_created += 1
        elif status == "updated":
            pages_updated += 1
        else:
            pages_skipped += 1
        print(f"  [PAGE] {msg}", flush=True)

        c, u, s = ensure_posts(term, update_mode, post_title_tpl, post_content_tpl, update_scope)
        posts_created += c
        posts_updated += u
        posts_skipped += s

    print("\nSummary:", flush=True)
    print(f"  Pages - created: {pages_created}, updated: {pages_updated}, skipped: {pages_skipped}", flush=True)
    print(f"  Posts - created: {posts_created}, updated: {posts_updated}, skipped: {posts_skipped}", flush=True)

if __name__ == "__main__":
    try:
        main()
    except requests.HTTPError as e:
        try:
            data = e.response.json()
        except Exception:
            data = e.response.text if e.response is not None else ""
        print(f"[HTTP ERROR] {e} :: {data}", file=sys.stderr, flush=True)
        sys.exit(1)
    except Exception as e:
        print(f"[ERROR] {type(e).__name__}: {e}", file=sys.stderr, flush=True)
        sys.exit(1)

