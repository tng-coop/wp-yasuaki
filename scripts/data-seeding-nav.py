#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Adopt branch detail pages as children of /branches, and list them inside /branches.

Why:
- Header uses a Page List (auto top-level). If branch pages are top-level, they flood the nav.
- Making branch pages CHILDREN of /branches keeps the header clean (only "Branches" shows).
- /branches itself gets a Query Loop listing its children.

Run:
  WP_BASE_URL=https://wp.lan \
  WP_USERNAME=admin \
  WP_APP_PASSWORD='xxxx xxxx xxxx xxxx' \
  python3 scripts/data-seeding-adopt-branches.py

Args:
  --branches-title "Branches"
  --branches-slug "branches"
  --branch-page-pattern '^branch-[a-z0-9\\-]+-page$'
  --insecure   (disable SSL verification, e.g., self-signed certs)
"""

import argparse
import json
import os
import re
import sys
from typing import Any, Dict, List, Optional, Tuple
import requests
from requests.auth import HTTPBasicAuth

LOG = "[branches-adopt]"
SENTINEL_START = "<!-- branches-index:auto:start -->"
SENTINEL_END   = "<!-- branches-index:auto:end -->"

def log(m: str): print(f"{LOG} {m}", flush=True)
def err(m: str): print(f"{LOG} ERROR: {m}", flush=True)

# -----------------------------
# WordPress REST client
# -----------------------------
class WP:
    def __init__(self, base_url: str, user: str, app_pass: str, verify_ssl: bool = True):
        self.base = base_url.rstrip("/")
        self.api = f"{self.base}/wp-json/wp/v2"
        self.s = requests.Session()
        self.s.auth = HTTPBasicAuth(user, app_pass)
        self.s.verify = verify_ssl
        self.s.headers.update({"Content-Type": "application/json"})

    def _req(self, method: str, path: str, *, params=None, data=None):
        url = f"{self.api}/{path.lstrip('/')}"
        try:
            r = self.s.request(method, url, params=params, data=json.dumps(data) if data is not None else None, timeout=30)
        except requests.exceptions.SSLError:
            raise RuntimeError("SSL error. Use --insecure or set WP_VERIFY_SSL=false for self-signed certs.")
        except requests.RequestException as e:
            raise RuntimeError(f"{method} {url} failed: {e}")
        if r.status_code >= 400:
            try:
                msg = r.json().get("message")
            except Exception:
                msg = r.text
            raise RuntimeError(f"{method} {url} → {r.status_code} {msg}")
        return r

    def get(self, path: str, **kw) -> Any:
        return self._req("GET", path, **kw).json()

    def post(self, path: str, data: Dict[str, Any]) -> Any:
        # WordPress uses POST for create and update to a specific resource path.
        return self._req("POST", path, data=data).json()

    # ---- Pages ----
    def find_page_by_slug(self, slug: str) -> Optional[Dict[str, Any]]:
        res = self.get("pages", params={"slug": slug, "per_page": 100, "status": "any", "context": "edit"})
        return res[0] if isinstance(res, list) and res else None

    def ensure_page(self, title: str, slug: str) -> Tuple[int, str]:
        p = self.find_page_by_slug(slug)
        if p:
            if p.get("status") != "publish":
                p = self.post(f"pages/{p['id']}", data={"status": "publish"})
            return int(p["id"]), p.get("link") or f"{self.base}/{slug}/"
        log(f"Creating page '{title}' (slug={slug})…")
        created = self.post("pages", data={"title": title, "slug": slug, "status": "publish"})
        return int(created["id"]), created.get("link") or f"{self.base}/{slug}/"

    def list_all_pages(self) -> List[Dict[str, Any]]:
        out: List[Dict[str, Any]] = []
        page = 1
        while True:
            batch = self.get("pages", params={"page": page, "per_page": 100, "status": "any", "context": "edit"})
            if not isinstance(batch, list) or not batch:
                break
            out.extend(batch)
            if len(batch) < 100:
                break
            page += 1
        return out

    def update_page_fields(self, page_id: int, **fields) -> Dict[str, Any]:
        return self.post(f"pages/{page_id}", data=fields)

    def read_page_raw(self, page_id: int) -> str:
        p = self.get(f"pages/{page_id}", params={"context": "edit"})
        c = p.get("content") or {}
        return c.get("raw") or c.get("rendered") or ""

# -----------------------------
# Helpers
# -----------------------------
def title_of(p: Dict[str, Any]) -> str:
    return (p.get("title") or {}).get("rendered") or (p.get("slug") or "")

def ensure_query_loop_children(block_parent_id: int) -> str:
    """
    Build a Query Loop block that lists child pages of `block_parent_id`, ASC by title.
    """
    query = {"perPage": -1, "postType": "page", "inherit": False, "parents": [block_parent_id], "order": "asc", "orderBy": "title"}
    return (
        f'<!-- wp:query {json.dumps({"query": query}, separators=(",", ":"))} -->\n'
        f'<div class="wp-block-query">\n'
        f'  <!-- wp:post-template -->\n'
        f'  <!-- wp:post-title {json.dumps({"isLink": True, "level": 3}, separators=(",", ":"))} /-->\n'
        f'  <!-- /wp:post-template -->\n'
        f'</div>\n'
        f'<!-- /wp:query -->'
    )

def upsert_auto_section(existing: str, new_section: str) -> str:
    """
    Insert or replace the managed section between SENTINEL markers.
    Leaves any manual content outside those markers untouched.
    """
    if SENTINEL_START in existing and SENTINEL_END in existing:
        return re.sub(
            re.escape(SENTINEL_START) + r".*?" + re.escape(SENTINEL_END),
            f"{SENTINEL_START}\n{new_section}\n{SENTINEL_END}",
            existing,
            flags=re.S,
        )
    return (f"{SENTINEL_START}\n{new_section}\n{SENTINEL_END}" if not existing.strip()
            else existing.rstrip() + "\n\n" + f"{SENTINEL_START}\n{new_section}\n{SENTINEL_END}")

def adopt_branch_pages(wp: WP, branches_id: int, pattern: re.Pattern) -> Tuple[int, int]:
    """
    Set parent=/branches for all pages whose slug matches the pattern.
    Returns (checked_count, adopted_count).
    """
    pages = wp.list_all_pages()
    checked = 0
    adopted = 0
    for p in pages:
        if p.get("status") != "publish":
            continue
        slug = p.get("slug") or ""
        if not pattern.fullmatch(slug):
            continue
        checked += 1
        current_parent = int(p.get("parent") or 0)
        if current_parent == branches_id:
            continue
        wp.update_page_fields(int(p["id"]), parent=branches_id, status="publish")
        adopted += 1
        log(f"Adopted page id={p['id']} slug={slug} under /branches")
    return checked, adopted

def ensure_branches_index_listing(wp: WP, branches_id: int) -> None:
    """
    Ensure /branches contains a managed Query Loop listing its child pages.
    """
    new_block = ensure_query_loop_children(branches_id)
    current = wp.read_page_raw(branches_id)
    merged = upsert_auto_section(current, new_block)
    if merged != current:
        wp.update_page_fields(branches_id, content=merged)
        log("Injected/updated Query Loop listing children inside /branches.")
    else:
        log("Branches content already up-to-date.")

# -----------------------------
# Main
# -----------------------------
def main() -> int:
    ap = argparse.ArgumentParser(description="Adopt branch pages under /branches and list them there.")
    ap.add_argument("--base-url", default=os.environ.get("WP_BASE_URL", "").strip())
    ap.add_argument("--username", default=os.environ.get("WP_USERNAME", "").strip())
    ap.add_argument("--app-password", default=os.environ.get("WP_APP_PASSWORD", "").strip())
    ap.add_argument("--insecure", action="store_true", help="Disable SSL verification (self-signed certs).")

    ap.add_argument("--branches-title", default="Branches")
    ap.add_argument("--branches-slug", default="branches")
    ap.add_argument("--branch-page-pattern", default=r"^branch-[a-z0-9\-]+-page$",
                    help="Regex for branch detail page slugs (full match).")

    args = ap.parse_args()
    if not args.base_url or not args.username or not args.app_password:
        err("Missing --base-url / --username / --app-password (or WP_* envs).")
        return 2

    verify_ssl = not args.insecure and (os.environ.get("WP_VERIFY_SSL", "true").lower() != "false")

    log("Starting…")

    try:
        wp = WP(args.base_url, args.username, args.app_password, verify_ssl=verify_ssl)
    except Exception as e:
        err(str(e)); return 2

    # 1) Ensure /branches
    try:
        branches_id, branches_url = wp.ensure_page(args.branches_title, args.branches_slug)
        log(f"/branches ready: id={branches_id} url={branches_url}")
    except Exception as e:
        err(f"Ensuring /branches failed: {e}"); return 2

    # 2) Adopt branch pages under /branches
    try:
        pat = re.compile(args.branch_page_pattern)
    except re.error as e:
        err(f"Invalid --branch-page-pattern: {e}"); return 2

    try:
        checked, adopted = adopt_branch_pages(wp, branches_id, pat)
        log(f"Checked {checked} branch page(s); adopted {adopted}.")
    except Exception as e:
        err(f"Adopting branch pages failed: {e}"); return 2

    # 3) Ensure /branches lists its child pages
    try:
        ensure_branches_index_listing(wp, branches_id)
    except Exception as e:
        err(f"Updating /branches content failed: {e}"); return 2

    log("Done.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
