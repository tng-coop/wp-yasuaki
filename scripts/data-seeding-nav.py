#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Idempotent WordPress seeder for:
  - creating/publishing a "Branches" page (slug: branches)
  - creating/ensuring a wp_navigation menu with a Branches link
  - wiring the Header template part to reference that navigation

Environment variables (or use CLI flags):
  WP_BASE_URL        e.g. https://wp.lan
  WP_USERNAME        e.g. admin
  WP_APP_PASSWORD    24-char app password, with spaces allowed
  WP_VERIFY_SSL      "true" (default) or "false"

Usage:
  python3 scripts/data-seeding-nav.py \
    --base-url https://wp.lan \
    --username admin \
    --app-password "xxxx xxxx xxxx xxxx"

Options:
  --no-header-touch     Skip modifying Header template part
  --menu-title "Primary Menu"
  --branches-title "Branches"
  --branches-slug "branches"
"""

import argparse
import json
import os
import re
import sys
from typing import Any, Dict, Optional, Tuple
from urllib.parse import urljoin

import requests
from requests.auth import HTTPBasicAuth

LOG_PREFIX = "[nav-seeder]"

def log(msg: str) -> None:
    print(f"{LOG_PREFIX} {msg}", flush=True)

def warn(msg: str) -> None:
    print(f"{LOG_PREFIX} WARN: {msg}", flush=True)

def err(msg: str) -> None:
    print(f"{LOG_PREFIX} ERROR: {msg}", flush=True)


# -----------------------------
# WordPress REST client
# -----------------------------
class WPClient:
    def __init__(self, base_url: str, username: str, app_password: str, verify_ssl: bool = True):
        self.base_url = base_url.rstrip("/")
        self.api = f"{self.base_url}/wp-json/wp/v2"
        self.auth = HTTPBasicAuth(username, app_password)
        self.verify = verify_ssl
        self.session = requests.Session()
        self.session.auth = self.auth
        self.session.verify = self.verify
        self.session.headers.update({"Content-Type": "application/json"})

    # --- HTTP helpers ---
    def _req(self, method: str, path: str, params: Dict[str, Any] = None, data: Dict[str, Any] = None) -> Dict[str, Any]:
        url = f"{self.api}/{path.lstrip('/')}"
        try:
            resp = self.session.request(method, url, params=params, data=json.dumps(data) if data is not None else None, timeout=30)
        except requests.exceptions.SSLError:
            raise RuntimeError("SSL error. If using self-signed certs locally, set WP_VERIFY_SSL=false.")
        except requests.RequestException as e:
            raise RuntimeError(f"HTTP error calling {url}: {e}")

        if resp.status_code >= 400:
            try:
                payload = resp.json()
            except Exception:
                payload = {"message": resp.text}
            raise RuntimeError(f"{method} {url} failed: {resp.status_code} {payload.get('message')}")

        try:
            return resp.json()
        except Exception:
            return {}

    def get(self, path: str, params: Dict[str, Any] = None) -> Dict[str, Any]:
        return self._req("GET", path, params=params)

    def post(self, path: str, data: Dict[str, Any]) -> Dict[str, Any]:
        return self._req("POST", path, data=data)

    def patch(self, path: str, data: Dict[str, Any]) -> Dict[str, Any]:
        return self._req("POST", path, data=data)  # WP uses POST for updates to specific endpoints

    # --- Pages ---
    def find_page_by_slug(self, slug: str) -> Optional[Dict[str, Any]]:
        res = self.get("pages", params={"slug": slug, "per_page": 100})
        if isinstance(res, list) and res:
            return res[0]
        return None

    def create_page(self, title: str, slug: str, status: str = "publish") -> Dict[str, Any]:
        return self.post("pages", data={"title": title, "slug": slug, "status": status})

    def publish_page_if_needed(self, page: Dict[str, Any]) -> Dict[str, Any]:
        if page.get("status") != "publish":
            log(f"Publishing page id={page['id']} slug={page.get('slug')}")
            return self.patch(f"pages/{page['id']}", data={"status": "publish"})
        return page

    def ensure_page(self, title: str, slug: str) -> Tuple[int, str]:
        """
        Ensure a page exists and is published.
        Returns: (page_id, page_url)
        """
        existing = self.find_page_by_slug(slug)
        if existing:
            log(f"Found page '{slug}' id={existing['id']} (status={existing.get('status')})")
            existing = self.publish_page_if_needed(existing)
            return existing["id"], existing.get("link") or urljoin(self.base_url + "/", f"{slug}/")
        log(f"Creating page '{title}' (slug={slug})…")
        created = self.create_page(title=title, slug=slug, status="publish")
        return created["id"], created.get("link") or urljoin(self.base_url + "/", f"{slug}/")

    # --- Navigation (Block theme) ---
    def list_navigations(self) -> list:
        res = self.get("navigation", params={"per_page": 100})
        return res if isinstance(res, list) else []

    def create_navigation(self, title: str) -> Dict[str, Any]:
        return self.post("navigation", data={"title": title})

    def get_navigation_by_title(self, title: str) -> Optional[Dict[str, Any]]:
        navs = self.list_navigations()
        for n in navs:
            if (n.get("title") or {}).get("rendered", "").strip() == title:
                return n
        return None

    def ensure_navigation(self, title: str) -> int:
        nav = self.get_navigation_by_title(title)
        if nav:
            log(f"Found navigation '{title}' id={nav['id']}")
            return nav["id"]
        log(f"Creating navigation '{title}'…")
        nav = self.create_navigation(title)
        return nav["id"]

    def get_navigation_content_raw(self, nav_id: int) -> str:
        nav = self.get(f"navigation/{nav_id}", params={"context": "edit"})
        content = nav.get("content", {})
        return content.get("raw") or content.get("rendered") or ""

    def update_navigation_content(self, nav_id: int, new_content: str) -> None:
        self.patch(f"navigation/{nav_id}", data={"content": new_content})

    # --- Template Parts (Header) ---
    def get_header_template_part(self) -> Optional[Dict[str, Any]]:
        # Prefer exact slug=header
        items = self.get("template-parts", params={"slug": "header", "per_page": 100, "context": "edit"})
        if isinstance(items, list) and items:
            return items[0]
        # Fallback: any header area
        items = self.get("template-parts", params={"per_page": 100, "context": "edit"})
        for it in items or []:
            if it.get("area") == "header":
                return it
        return None

    def update_template_part_content(self, tp_id: int, new_content: str) -> None:
        self.patch(f"template-parts/{tp_id}", data={"content": new_content})


# -----------------------------
# Content helpers
# -----------------------------
def build_nav_ref_block(nav_id: int) -> str:
    # Self-closing nav block that references a wp_navigation entity by ID
    # WP will render the nav items from the referenced nav's post_content.
    payload = {"ref": nav_id}
    return f'<!-- wp:navigation {json.dumps(payload, separators=(",", ":"))} /-->'  # compact JSON

def build_nav_link_block(label: str, url: str, page_id: Optional[int] = None, link_type: str = "custom") -> str:
    """
    link_type:
      - "custom": for plain URLs
      - "page":   when you have a page ID
    """
    data: Dict[str, Any] = {"label": label, "type": link_type, "url": url}
    if page_id is not None:
        data["id"] = page_id
        data["type"] = "page"
    return f'<!-- wp:navigation-link {json.dumps(data, separators=(",", ":"))} /-->'

def header_has_nav_ref(content: str) -> bool:
    return bool(re.search(r'<!--\s*wp:navigation\b[^>]*"ref"\s*:\s*\d+', content or "", flags=re.IGNORECASE | re.DOTALL))

def replace_first_nav_or_pagelist_with_ref(content: str, nav_ref_block: str) -> str:
    """
    Tries, in order:
      1) Replace first wp:navigation (any form) with {ref} variant
      2) Replace first wp:page-list with nav {ref}
      3) If nothing matched, prepend nav {ref}
    """
    text = content or ""

    # Case 1: Replace wp:navigation (self-closing)
    m = re.search(r'<!--\s*wp:navigation\b[^>]*?/\s*-->', text, flags=re.IGNORECASE | re.DOTALL)
    if m:
        return text[:m.start()] + nav_ref_block + text[m.end():]

    # Case 1b: Replace wp:navigation (open/close)
    open_m = re.search(r'<!--\s*wp:navigation\b[^>]*-->', text, flags=re.IGNORECASE | re.DOTALL)
    if open_m:
        close_m = re.search(r'<!--\s*/wp:navigation\s*-->', text[open_m.end():], flags=re.IGNORECASE | re.DOTALL)
        if close_m:
            start = open_m.start()
            end = open_m.end() + close_m.end()
            return text[:start] + nav_ref_block + text[end:]

    # Case 2: Replace core/page-list block
    # It can be self-closing or paired (rare). Handle self-closing first.
    m2 = re.search(r'<!--\s*wp:page-list\b[^>]*?/\s*-->', text, flags=re.IGNORECASE | re.DOTALL)
    if m2:
        return text[:m2.start()] + nav_ref_block + text[m2.end():]

    # Paired <page-list> (fallback)
    open_m2 = re.search(r'<!--\s*wp:page-list\b[^>]*-->', text, flags=re.IGNORECASE | re.DOTALL)
    if open_m2:
        close_m2 = re.search(r'<!--\s*/wp:page-list\s*-->', text[open_m2.end():], flags=re.IGNORECASE | re.DOTALL)
        if close_m2:
            start = open_m2.start()
            end = open_m2.end() + close_m2.end()
            return text[:start] + nav_ref_block + text[end:]

    # Case 3: Prepend
    return nav_ref_block + ("\n" if text and not text.startswith("\n") else "") + text

def nav_content_has_branches(nav_content: str, branches_page_id: int, branches_url: str) -> bool:
    if not nav_content:
        return False
    if re.search(rf'"id"\s*:\s*{branches_page_id}\b', nav_content):
        return True
    if "/branches" in nav_content or branches_url.rstrip("/").endswith("/branches"):
        return True
    return False


# -----------------------------
# Orchestration
# -----------------------------
def main() -> int:
    parser = argparse.ArgumentParser(description="Seed pages + navigation and wire header.")
    parser.add_argument("--base-url", default=os.environ.get("WP_BASE_URL", "").strip(), help="Site base URL, e.g. https://wp.lan")
    parser.add_argument("--username", default=os.environ.get("WP_USERNAME", "").strip(), help="WP username")
    parser.add_argument("--app-password", default=os.environ.get("WP_APP_PASSWORD", "").strip(), help="WP application password")
    parser.add_argument("--verify-ssl", default=os.environ.get("WP_VERIFY_SSL", "true").lower() != "false", action="store_true", help="Verify SSL (default true)")
    parser.add_argument("--no-header-touch", action="store_true", help="Do not modify Header template part")
    parser.add_argument("--menu-title", default="Primary Menu")
    parser.add_argument("--branches-title", default="Branches")
    parser.add_argument("--branches-slug", default="branches")
    parser.add_argument("--add-home", action="store_true", help="Ensure a Home link is added to the menu as a custom link to site root")
    args = parser.parse_args()

    if not args.base_url or not args.username or not args.app_password:
        err("Missing required connection info. Provide --base-url, --username, --app-password (or WP_* envs).")
        return 2

    log("Starting…")
    log(f"runtime base_url={args.base_url}, menu_title={args.menu_title}, header_touch={'off' if args.no_header_touch else 'on'}")

    try:
        wp = WPClient(args.base_url, args.username, args.app_password, verify_ssl=args.verify_ssl)
    except Exception as e:
        err(str(e))
        return 2

    # 1) Ensure Branches page exists & is published
    try:
        branches_id, branches_url = wp.ensure_page(title=args.branches_title, slug=args.branches_slug)
        log(f"Branches page ready: id={branches_id} url={branches_url}")
    except Exception as e:
        err(f"Failed ensuring Branches page: {e}")
        return 2

    # 2) Ensure a wp_navigation exists
    try:
        nav_id = wp.ensure_navigation(args.menu_title)
        log(f"Navigation ready: id={nav_id}")
    except Exception as e:
        err(f"Failed ensuring navigation: {e}")
        return 2

    # 3) Ensure Menu contains Branches (and optional Home)
    try:
        current_content = wp.get_navigation_content_raw(nav_id)
        new_content = current_content

        # Optionally ensure Home link
        if args.add_home and "label\":\"Home\"" not in (current_content or ""):
            home_block = build_nav_link_block("Home", args.base_url.rstrip("/") + "/", link_type="custom")
            if not new_content.strip():
                new_content = home_block
            else:
                new_content = home_block + "\n" + new_content
            log(f"Added 'Home' link to navigation id={nav_id}")

        if not nav_content_has_branches(current_content, branches_id, branches_url):
            branch_block = build_nav_link_block(args.branches_title, branches_url, page_id=branches_id, link_type="page")
            new_content = (new_content + ("\n" if new_content and not new_content.endswith("\n") else "") + branch_block) if new_content else branch_block
            log(f"Added '{args.branches_title}' link to navigation id={nav_id} → page id={branches_id}")

        if new_content != current_content:
            wp.update_navigation_content(nav_id, new_content)
            log(f"Navigation content updated for id={nav_id}")
        else:
            log("Navigation already contains required links.")
    except Exception as e:
        err(f"Failed ensuring nav items: {e}")
        return 2

    # 4) Wire Header to use this nav (unless disabled)
    if not args.no_header_touch:
        try:
            header = wp.get_header_template_part()
            if not header:
                warn("No Header template part found. Cannot wire navigation. (You can add a Header in Site Editor.)")
            else:
                raw = (header.get("content") or {}).get("raw") or (header.get("content") or {}).get("rendered") or ""
                if header_has_nav_ref(raw):
                    log("Header already has a wp:navigation {\"ref\":…}. No change.")
                else:
                    nav_ref_block = build_nav_ref_block(nav_id)
                    new_header = replace_first_nav_or_pagelist_with_ref(raw, nav_ref_block)
                    if new_header != raw:
                        wp.update_template_part_content(header["id"], new_header)
                        log(f"Wired Header (template-part id={header['id']}) to navigation id={nav_id}")
                    else:
                        # Couldn’t find anything to replace; prepend as last resort
                        new_header = nav_ref_block + ("\n" if raw and not raw.startswith("\n") else "") + raw
                        wp.update_template_part_content(header["id"], new_header)
                        log(f"Prepended navigation ref into Header (template-part id={header['id']})")
        except Exception as e:
            err(f"Failed wiring Header to navigation: {e}")
            return 2
    else:
        log("Header wiring skipped (--no-header-touch)")

    log("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
