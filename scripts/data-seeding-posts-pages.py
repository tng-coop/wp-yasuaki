#!/usr/bin/env python3
"""Seed deterministic branch pages and posts via the WP REST API.

Usage:
    export WP_BASE_URL="https://wp.lan"
    export WP_USERNAME="admin"
    export WP_APP_PASSWORD="xxxx"
    cd scripts && ./data-seeding-posts-pages.py [--force]

Environment variables:
    WP_BASE_URL       Base URL of the WordPress site (no trailing slash required).
    WP_USERNAME       Username with permissions to manage pages/posts.
    WP_APP_PASSWORD   Application password associated with WP_USERNAME.

Flags:
    --force           Recreate/update content even when a matching item already exists.

The script is idempotent. It ensures each branch term has exactly one published page
and three published posts with predictable slugs. Missing items are created. Existing
items are skipped unless --force is specified, in which case their content is refreshed.
"""

from __future__ import annotations

import argparse
import os
import sys
from typing import Any, Dict, List, Optional, Tuple

import requests

WP_BASE_URL = os.environ.get("WP_BASE_URL", "").rstrip("/")
WP_USERNAME = os.environ.get("WP_USERNAME", "")
WP_APP_PASSWORD = (os.environ.get("WP_APP_PASSWORD", "") or "").replace(" ", "")

if not (WP_BASE_URL and WP_USERNAME and WP_APP_PASSWORD):
    print("Set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD", file=sys.stderr)
    sys.exit(1)

API_ROOT = f"{WP_BASE_URL}/wp-json/wp/v2"
BRANCH_ENDPOINT = f"{API_ROOT}/branch"
PAGES_ENDPOINT = f"{API_ROOT}/pages"
POSTS_ENDPOINT = f"{API_ROOT}/posts"

SESSION = requests.Session()
SESSION.auth = (WP_USERNAME, WP_APP_PASSWORD)
SESSION.headers.update(
    {
        "Accept": "application/json",
        "Content-Type": "application/json; charset=utf-8",
    }
)


def fetch_branch_terms() -> List[Dict[str, Any]]:
    terms: List[Dict[str, Any]] = []
    page = 1
    while True:
        resp = SESSION.get(
            BRANCH_ENDPOINT,
            params={"per_page": 100, "page": page, "context": "view"},
        )
        if resp.status_code == 404:
            print(
                "[INFO] Branch taxonomy not found (is the taxonomy registered?).", 
                file=sys.stderr,
            )
            return []
        resp.raise_for_status()
        batch = resp.json()
        if not batch:
            break
        terms.extend(batch)
        if len(batch) < 100:
            break
        page += 1
    if not terms:
        print("[INFO] No branch terms available; nothing to seed.")
    else:
        print(f"[INFO] Loaded {len(terms)} branch terms.")
    return terms


def get_single_item(endpoint: str, slug: str) -> Tuple[Optional[int], int]:
    params = {"slug": slug, "status": "publish", "_fields": "id,slug"}
    resp = SESSION.get(endpoint, params=params)
    resp.raise_for_status()
    items = resp.json()
    if not items:
        return None, 0
    if isinstance(items, dict):  # unexpected shape
        return int(items.get("id")), 1
    if len(items) > 1:
        print(
            f"[WARN] Expected one item for slug '{slug}' at {endpoint}, got {len(items)}.",
            file=sys.stderr,
        )
    return int(items[0]["id"]), len(items)


def build_page_payload(term: Dict[str, Any]) -> Dict[str, Any]:
    name = term.get("name") or "Branch"
    slug = term.get("slug") or "branch"
    title = f"{name} Branch Overview"
    content = (
        f"<p>Welcome to the {name} branch page.</p>\n"
        f"<p>This page provides an overview and sample content for the {name} branch.</p>"
    )
    return {
        "status": "publish",
        "slug": f"branch-{slug}-page",
        "title": title,
        "content": content,
    }


def build_post_payload(term: Dict[str, Any], index: int) -> Dict[str, Any]:
    name = term.get("name") or "Branch"
    slug = term.get("slug") or "branch"
    branch_id = term.get("id")
    title = f"{name} Branch Update #{index}"
    content = (
        f"<p>This is sample post {index} for the {name} branch.</p>\n"
        f"<p>It demonstrates assigning content to the branch taxonomy.</p>"
    )
    return {
        "status": "publish",
        "slug": f"branch-{slug}-post-{index}",
        "title": title,
        "content": content,
        "branch": [branch_id] if branch_id is not None else [],
    }


def ensure_page(term: Dict[str, Any], force: bool) -> Tuple[str, str]:
    payload = build_page_payload(term)
    slug = payload["slug"]
    existing_id, count = get_single_item(PAGES_ENDPOINT, slug)
    if existing_id and not force:
        return "skipped", f"Page exists (post_id={existing_id})"
    if existing_id:
        resp = SESSION.post(f"{PAGES_ENDPOINT}/{existing_id}", json=payload)
        resp.raise_for_status()
        return "updated", f"Page updated (post_id={existing_id})"
    resp = SESSION.post(PAGES_ENDPOINT, json=payload)
    resp.raise_for_status()
    new_id = int(resp.json()["id"])
    return "created", f"Page created (post_id={new_id})"


def ensure_posts(term: Dict[str, Any], force: bool) -> Tuple[int, int, int]:
    created = updated = skipped = 0
    for index in range(1, 4):
        payload = build_post_payload(term, index)
        slug = payload["slug"]
        existing_id, _count = get_single_item(POSTS_ENDPOINT, slug)
        if existing_id and not force:
            skipped += 1
            print(
                f"    [SKIP] {slug} already exists (post_id={existing_id})."
            )
            continue
        if existing_id:
            resp = SESSION.post(f"{POSTS_ENDPOINT}/{existing_id}", json=payload)
            resp.raise_for_status()
            updated += 1
            print(f"    [OK] {slug} updated (post_id={existing_id}).")
        else:
            resp = SESSION.post(POSTS_ENDPOINT, json=payload)
            resp.raise_for_status()
            new_id = int(resp.json()["id"])
            created += 1
            print(f"    [OK] {slug} created (post_id={new_id}).")
    return created, updated, skipped


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed branch pages and posts.")
    parser.add_argument(
        "--force",
        action="store_true",
        help="Recreate/update content even when it already exists.",
    )
    args = parser.parse_args()

    terms = fetch_branch_terms()
    if not terms:
        sys.exit(0)

    pages_created = pages_updated = pages_skipped = 0
    posts_created = posts_updated = posts_skipped = 0

    for term in terms:
        name = term.get("name") or term.get("slug") or "branch"
        print(f"[TERM] {name} (id={term.get('id')}, slug={term.get('slug')})")
        status, message = ensure_page(term, args.force)
        if status == "created":
            pages_created += 1
        elif status == "updated":
            pages_updated += 1
        else:
            pages_skipped += 1
        print(f"  [PAGE] {message}")

        c, u, s = ensure_posts(term, args.force)
        posts_created += c
        posts_updated += u
        posts_skipped += s

    print("\nSummary:")
    print(
        f"  Pages - created: {pages_created}, updated: {pages_updated}, skipped: {pages_skipped}"
    )
    print(
        f"  Posts - created: {posts_created}, updated: {posts_updated}, skipped: {posts_skipped}"
    )


if __name__ == "__main__":
    main()
