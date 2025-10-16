#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Seeds /branches and ensures it *lists branch pages* using a block list (no shortcodes).

What this script does (idempotent):
  1) Ensures a published page /branches exists.
  2) Ensures a wp_navigation exists (and wires Header to reference it unless --no-header-touch).
  3) Finds all pages whose slug matches a pattern (default: ^branch-[a-z0-9\-]+-page$).
  4) Writes/updates an auto-managed section inside /branches with a core/list block of links.

Run:
  WP_BASE_URL=https://wp.lan \
  WP_USERNAME=admin \
  WP_APP_PASSWORD='xxxx xxxx xxxx xxxx' \
  python3 scripts/data-seeding-nav.py

Flags:
  --no-header-touch           Skip wiring Header to the menu
  --menu-title "Primary Menu" Menu title to ensure
  --branches-title "Branches" Visible title of the /branches page
  --branches-slug "branches"  Slug for the branches index page
  --branch-page-pattern REGEX Regex for branch page slugs (default ^branch-[a-z0-9\-]+-page$)
  --verify-ssl                (default true) set WP_VERIFY_SSL=false to ignore self-signed certs
"""

import argparse
import html
import json
import os
import re
import sys
from typing import Any, Dict, List, Optional, Tuple

import requests
from requests.auth import HTTPBasicAuth

LOG = "[nav-seeder]"
SENTINEL_START = "<!-- branches-index:auto:start -->"
SENTINEL_END   = "<!-- branches-index:auto:end -->"

def log(m: str): print(f"{LOG} {m}", flush=True)
def warn(m: str): print(f"{LOG} WARN: {m}", flush=True)
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
            raise RuntimeError("SSL error; set WP_VERIFY_SSL=false (or --verify-ssl false) for self-signed certs.")
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
        # WP uses POST for create and update to resource paths, so use POST for patches too.
        return self._req("POST", path, data=data).json()

    # ---- PAGES ----
    def find_page_by_slug(self, slug: str) -> Optional[Dict[str, Any]]:
        res = self.get("pages", params={"slug": slug, "per_page": 100, "status": "any", "context": "edit"})
        return res[0] if isinstance(res, list) and res else None

    def ensure_page(self, title: str, slug: str) -> Tuple[int, str]:
        p = self.find_page_by_slug(slug)
        if p:
            if p.get("status") != "publish":
                p = self.post(f"pages/{p['id']}", data={"status": "publish"})
            url = p.get("link") or f"{self.base}/{slug}/"
            return p["id"], url
        log(f"Creating page '{title}' (slug={slug})…")
        created = self.post("pages", data={"title": title, "slug": slug, "status": "publish"})
        url = created.get("link") or f"{self.base}/{slug}/"
        return created["id"], url

    def list_all_pages(self) -> List[Dict[str, Any]]:
        out: List[Dict[str, Any]] = []
        page = 1
        while True:
            batch = self.get(
                "pages",
                params={"page": page, "per_page": 100, "status": "any", "context": "edit"},
            )
            if not isinstance(batch, list) or not batch:
                break
            out.extend(batch)
            if len(batch) < 100:
                break
            page += 1
        return out

    def read_page_raw_content(self, page_id: int) -> str:
        page = self.get(f"pages/{page_id}", params={"context": "edit"})
        c = page.get("content") or {}
        return c.get("raw") or c.get("rendered") or ""

    def update_page_content(self, page_id: int, content: str) -> Dict[str, Any]:
        return self.post(f"pages/{page_id}", data={"content": content})

    # ---- NAVIGATION ----
    def ensure_navigation(self, title: str) -> int:
        navs = self.get("navigation", params={"per_page": 100, "context": "edit"})
        for n in navs or []:
            if (n.get("title") or {}).get("rendered", "").strip() == title:
                return n["id"]
        nav = self.post("navigation", data={"title": title})
        return nav["id"]

    def nav_get_raw(self, nav_id: int) -> str:
        nav = self.get(f"navigation/{nav_id}", params={"context": "edit"})
        c = nav.get("content") or {}
        return c.get("raw") or c.get("rendered") or ""

    def nav_set_raw(self, nav_id: int, raw: str):
        self.post(f"navigation/{nav_id}", data={"content": raw})

    # ---- TEMPLATE PART (Header) ----
    def get_header_template_part(self) -> Optional[Dict[str, Any]]:
        items = self.get("template-parts", params={"slug": "header", "per_page": 100, "context": "edit"})
        if isinstance(items, list) and items:
            return items[0]
        items = self.get("template-parts", params={"per_page": 100, "context": "edit"})
        for it in items or []:
            if it.get("area") == "header":
                return it
        return None

    def header_update_content(self, tp_id: int, content: str):
        self.post(f"template-parts/{tp_id}", data={"content": content})

# -----------------------------
# Header helpers
# -----------------------------
def header_has_nav_ref(content: str) -> bool:
    return bool(re.search(r'<!--\s*wp:navigation\b[^>]*"ref"\s*:\s*\d+', content or "", flags=re.I | re.S))

def build_nav_ref(nav_id: int) -> str:
    return f'<!-- wp:navigation {json.dumps({"ref": nav_id}, separators=(",", ":"))} /-->'

def replace_nav_or_pagelist_with_ref(content: str, nav_ref: str) -> str:
    t = content or ""
    # Replace self-closing navigation
    m = re.search(r'<!--\s*wp:navigation\b[^>]*?/\s*-->', t, flags=re.I | re.S)
    if m:
        return t[:m.start()] + nav_ref + t[m.end():]
    # Replace open/close navigation
    open_m = re.search(r'<!--\s*wp:navigation\b[^>]*-->', t, flags=re.I | re.S)
    if open_m:
        close_m = re.search(r'<!--\s*/wp:navigation\s*-->', t[open_m.end():], flags=re.I | re.S)
        if close_m:
            start = open_m.start()
            end = open_m.end() + close_m.end()
            return t[:start] + nav_ref + t[end:]
    # Replace page-list
    m2 = re.search(r'<!--\s*wp:page-list\b[^>]*?/\s*-->', t, flags=re.I | re.S)
    if m2:
        return t[:m2.start()] + nav_ref + t[m2.end():]
    # Fallback: prepend
    return nav_ref + ("\n" if t and not t.startswith("\n") else "") + t

def ensure_nav_and_header(wp: WP, menu_title: str):
    nav_id = wp.ensure_navigation(menu_title)
    # keep existing nav content as-is
    header = wp.get_header_template_part()
    if not header:
        warn("No Header template part found; skipping header wiring.")
        return
    raw = (header.get("content") or {}).get("raw") or (header.get("content") or {}).get("rendered") or ""
    if header_has_nav_ref(raw):
        log("Header already references a wp_navigation (ref).")
        return
    new_header = replace_nav_or_pagelist_with_ref(raw, build_nav_ref(nav_id))
    wp.header_update_content(header["id"], new_header)
    log(f"Wired Header (template-part id={header['id']}) to navigation id={nav_id}")

# -----------------------------
# /branches listing helpers
# -----------------------------
def upsert_auto_section(existing: str, new_section: str) -> str:
    if SENTINEL_START in existing and SENTINEL_END in existing:
        return re.sub(
            re.escape(SENTINEL_START) + r".*?" + re.escape(SENTINEL_END),
            new_section,
            existing,
            flags=re.S,
        )
    stripped = (existing or "").strip()
    return (new_section if not stripped else existing.rstrip() + "\n\n" + new_section)

def title_of(page: Dict[str, Any]) -> str:
    return (page.get("title") or {}).get("rendered") or (page.get("slug") or "")

def link_of(page: Dict[str, Any], base_url: str) -> str:
    return page.get("link") or f"{base_url.rstrip('/')}/{page.get('slug','').strip('/')}/"

def make_list_block(pages: List[Dict[str, Any]], base_url: str) -> str:
    """
    Renders a core/list block of page links between sentinel markers.
    """
    items = []
    for p in pages:
        title_html = title_of(p)
        # Just in case: ensure we don't break HTML; rendered titles are already escaped by WP
        title_html = title_html if title_html else html.escape(p.get("slug") or "")
        link = link_of(p, base_url)
        items.append(f'<!-- wp:list-item --><li><a href="{link}">{title_html}</a></li><!-- /wp:list-item -->')
    ul = "\n".join(items)
    return f'{SENTINEL_START}\n<!-- wp:list -->\n<ul class="wp-block-list">\n{ul}\n</ul>\n<!-- /wp:list -->\n{SENTINEL_END}'

# -----------------------------
# Main
# -----------------------------
def main() -> int:
    ap = argparse.ArgumentParser(description="Ensure /branches exists; wire header; list branch pages as a block list.")
    ap.add_argument("--base-url",      default=os.environ.get("WP_BASE_URL", "").strip())
    ap.add_argument("--username",      default=os.environ.get("WP_USERNAME", "").strip())
    ap.add_argument("--app-password",  default=os.environ.get("WP_APP_PASSWORD", "").strip())
    ap.add_argument("--verify-ssl",    default=os.environ.get("WP_VERIFY_SSL", "true").lower() != "false", action="store_true")

    ap.add_argument("--no-header-touch", action="store_true", help="Skip wiring Header to the menu")
    ap.add_argument("--menu-title",      default="Primary Menu")

    ap.add_argument("--branches-title",  default="Branches")
    ap.add_argument("--branches-slug",   default="branches")

    ap.add_argument("--branch-page-pattern", default=r"^branch-[a-z0-9\-]+-page$",
                    help="Regex for branch page slugs")

    args = ap.parse_args()
    if not args.base_url or not args.username or not args.app_password:
        err("Missing --base-url/--username/--app-password (or WP_* envs).")
        return 2

    log("Starting…")
    log(f"runtime base_url={args.base_url}, header_touch={'off' if args.no_header_touch else 'on'}")

    try:
        wp = WP(args.base_url, args.username, args.app_password, verify_ssl=args.verify_ssl)
    except Exception as e:
        err(str(e))
        return 2

    # 1) Ensure /branches exists & is published
    try:
        branches_id, branches_url = wp.ensure_page(args.branches_title, args.branches_slug)
        log(f"/branches ready: id={branches_id} url={branches_url}")
    except Exception as e:
        err(f"Ensuring /branches failed: {e}")
        return 2

    # 2) Ensure nav + header wiring
    if not args.no_header_touch:
        try:
            ensure_nav_and_header(wp, args.menu_title)
        except Exception as e:
            err(f"Setting up navigation/header failed: {e}")
            return 2
    else:
        log("Header wiring skipped (--no-header-touch)")

    # 3) Collect branch pages by slug pattern (published only)
    try:
        pattern = re.compile(args.branch_page_pattern)
    except re.error as e:
        err(f"Invalid --branch-page-pattern regex: {e}")
        return 2

    try:
        all_pages = wp.list_all_pages()
        branch_pages = [
            p for p in all_pages
            if pattern.fullmatch(str(p.get("slug") or "")) and p.get("status") == "publish"
        ]
        branch_pages.sort(key=lambda p: (title_of(p) or "").lower())
        log(f"Found {len(branch_pages)} branch page(s) matching pattern.")
    except Exception as e:
        err(f"Listing pages failed: {e}")
        return 2

    # 4) Write/refresh the block list inside /branches
    try:
        current = wp.read_page_raw_content(branches_id)
        new_section = make_list_block(branch_pages, args.base_url)
        new_content = upsert_auto_section(current, new_section)
        if new_content != current:
            wp.update_page_content(branches_id, new_content)
            log(f"Updated /branches content with {len(branch_pages)} item(s).")
        else:
            log("Branches listing already up-to-date.")
    except Exception as e:
        err(f"Writing listing into /branches failed: {e}")
        return 2

    log("Done.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
