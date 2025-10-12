#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Idempotent seeder: creates EXACTLY 1 page + 3 posts per *real* branch.

What counts as a "branch" here:
- Children of the Categories parent with slug "branch".
- Slugs for child terms follow your seeding pattern: "branch-<ID>" from scripts/offices.csv

Behavior (no arguments needed):
1) Ensures the parent "Branch" category (slug=branch) exists.
2) Loads existing branch children. If none exist, reads scripts/offices.csv and
   creates categories for each row (name from "Office", slug "branch-<ID>").
   If offices.csv is missing and there are no children, stops gracefully.
3) For each branch term:
   - Ensures there is exactly ONE page with slug:  <term.slug>-page
   - Ensures there are exactly THREE posts with slugs: <term.slug>-post-1..3
   - Posts are assigned to the branch category via the "categories" field.
   - Runs are idempotent: already-present slugs are skipped (not overwritten).

Required environment:
  WP_BASE_URL      e.g. "https://example.com" (no trailing slash needed)
  WP_USERNAME
  WP_APP_PASSWORD  (Application Password for WP_USERNAME)

Example:
  export WP_BASE_URL="https://wp.lan"
  export WP_USERNAME="admin"
  export WP_APP_PASSWORD="xxxx xxxx xxxx xxxx"
  ./scripts/data-seeding-posts-pages.py
"""

from __future__ import annotations

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
    {
        "Accept": "application/json",
        "Content-Type": "application/json; charset=utf-8",
    }
)

REQUEST_TIMEOUT = 20  # seconds


# --------------------------- Helpers ---------------------------

def _slugify(s: str) -> str:
    s = (s or "").strip()
    s = re.sub(r"[^a-zA-Z0-9_-]+", "-", s)
    s = re.sub(r"-{2,}", "-", s).strip("-").lower()
    return s


def _csv_path() -> Optional[str]:
    """Return scripts/offices.csv if it exists (relative to this file)."""
    here = os.path.dirname(os.path.abspath(__file__))
    p = os.path.join(here, "offices.csv")
    return p if os.path.exists(p) else None


# --------------------------- Categories (branch) ---------------------------

def ensure_parent_branch_category() -> int:
    """Ensure parent 'Branch' category (slug=branch) exists; return its ID."""
    r = SESSION.get(CATEGORIES_ENDPOINT, params={"slug": "branch", "_fields": "id,slug"}, timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    items = r.json() if isinstance(r.json(), list) else []
    if items:
        pid = int(items[0]["id"])
        print(f"[INFO] Using parent category 'Branch' (id={pid}).", flush=True)
        return pid

    r = SESSION.post(CATEGORIES_ENDPOINT, json={"name": "Branch", "slug": "branch", "parent": 0}, timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    pid = int(r.json()["id"])
    print(f"[OK] Created parent category 'Branch' (id={pid}).", flush=True)
    return pid


def fetch_branch_children(parent_id: int) -> List[Dict[str, Any]]:
    """Return all child categories under the 'Branch' parent."""
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
    """Idempotently ensure a child category exists under parent_id."""
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
    """
    If no child branches exist yet, create them from scripts/offices.csv.
    Uses columns: ID, Office   -> slug: branch-<ID>, name: Office
    """
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


# --------------------------- Content upsert ---------------------------

def get_single_item(endpoint: str, slug: str) -> Optional[int]:
    """Return page/post ID by slug, or None if absent."""
    r = SESSION.get(endpoint, params={"slug": slug, "_fields": "id,slug,status"}, timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    items = r.json()
    if not items:
        return None
    if isinstance(items, dict):
        return int(items.get("id")) if items.get("id") else None
    return int(items[0]["id"])


def build_page_payload(term: Dict[str, Any]) -> Dict[str, Any]:
    base = term.get("slug") or "branch"
    name = term.get("name") or "Branch"
    return {
        "status": "publish",
        "slug": f"{base}-page",
        "title": f"{name} Branch Overview",
        "content": (
            f"<p>Welcome to the {name} branch page.</p>\n"
            f"<p>This page provides an overview and sample content for the {name} branch.</p>"
        ),
    }


def build_post_payload(term: Dict[str, Any], index: int) -> Dict[str, Any]:
    base = term.get("slug") or "branch"
    name = term.get("name") or "Branch"
    term_id = term.get("id")
    return {
        "status": "publish",
        "slug": f"{base}-post-{index}",
        "title": f"{name} Branch Update #{index}",
        "content": (
            f"<p>This is sample post {index} for the {name} branch.</p>\n"
            f"<p>It demonstrates assigning content to the branch category.</p>"
        ),
        "categories": [term_id] if term_id is not None else [],
    }


def ensure_page(term: Dict[str, Any]) -> Tuple[str, str]:
    """Create the page if missing; never overwrite (idempotent)."""
    payload = build_page_payload(term)
    slug = payload["slug"]
    existing_id = get_single_item(PAGES_ENDPOINT, slug)
    if existing_id:
        return "skipped", f"exists (post_id={existing_id})"
    r = SESSION.post(PAGES_ENDPOINT, json=payload, timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    return "created", f"created (post_id={int(r.json()['id'])})"


def ensure_posts(term: Dict[str, Any]) -> Tuple[int, int, int]:
    """
    Ensure exactly 3 posts exist for this term (by slug).
    Idempotent: existing slugs are skipped; missing ones are created.
    """
    created = updated = skipped = 0
    for i in range(1, 4):
        payload = build_post_payload(term, i)
        slug = payload["slug"]
        existing_id = get_single_item(POSTS_ENDPOINT, slug)
        if existing_id:
            skipped += 1
            print(f"    [SKIP] {slug} exists (post_id={existing_id}).", flush=True)
            continue
        r = SESSION.post(POSTS_ENDPOINT, json=payload, timeout=REQUEST_TIMEOUT)
        r.raise_for_status()
        created += 1
        print(f"    [OK] {slug} created (post_id={int(r.json()['id'])}).", flush=True)
    return created, updated, skipped


# --------------------------- Main ---------------------------

def main() -> None:
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

    print(f"[INFO] Loaded {len(terms)} branch term(s).", flush=True)

    pages_created = pages_updated = pages_skipped = 0
    posts_created = posts_updated = posts_skipped = 0

    for term in terms:
        name = term.get("name") or term.get("slug") or "branch"
        print(f"[TERM] {name} (id={term.get('id')}, slug={term.get('slug')})", flush=True)

        status, msg = ensure_page(term)
        if status == "created":
            pages_created += 1
        elif status == "updated":
            pages_updated += 1
        else:
            pages_skipped += 1
        print(f"  [PAGE] {msg}", flush=True)

        c, u, s = ensure_posts(term)
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
        # Surface helpful API error details
        try:
            data = e.response.json()
        except Exception:
            data = e.response.text if e.response is not None else ""
        print(f"[HTTP ERROR] {e} :: {data}", file=sys.stderr, flush=True)
        sys.exit(1)
    except Exception as e:
        print(f"[ERROR] {type(e).__name__}: {e}", file=sys.stderr, flush=True)
        sys.exit(1)

