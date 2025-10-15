#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
data-seeding-nav.py
Build a minimal primary navigation for a block theme:
- Home
- Branches (links to /branches/ via page ID + url)
- (Optional) Blog (posts page) via page ID + url

Env:
  WP_BASE_URL       e.g. https://wp.lan      (no trailing slash OK)
  WP_USERNAME       e.g. admin
  WP_APP_PASSWORD   e.g. xxxx xxxx xxxx xxxx

Usage:
  python3 scripts/data-seeding-nav.py
    --no-header-touch   Do not modify Header template part
    --no-blog           Skip Blog link even if a posts page exists
"""
import os
import sys
import json
import re
import argparse
from typing import Optional, Dict, Any, Tuple

import requests

BASE = os.environ.get("WP_BASE_URL", "https://wp.lan").rstrip("/")
USER = os.environ.get("WP_USERNAME")
APP_PW = os.environ.get("WP_APP_PASSWORD")

API = f"{BASE}/wp-json/wp/v2"
TIMEOUT = 30


def die(msg: str, code: int = 1):
    print(f"[nav-seeder] ERROR: {msg}", file=sys.stderr)
    sys.exit(code)


def wp_req(method: str, path: str, **kwargs) -> requests.Response:
    if not USER or not APP_PW:
        die("WP_USERNAME/WP_APP_PASSWORD must be set in environment.")
    url = path if path.startswith("http") else f"{API}/{path.lstrip('/')}"
    headers = kwargs.pop("headers", {})
    headers.setdefault("Accept", "application/json")
    resp = requests.request(
        method,
        url,
        auth=(USER, APP_PW),
        headers=headers,
        timeout=TIMEOUT,
        **kwargs,
    )
    if not resp.ok:
        try:
            payload = resp.json()
        except Exception:
            payload = resp.text
        die(f"{method} {url} failed: {resp.status_code} {payload}")
    return resp


def wp_get_json(path: str, **params) -> Any:
    r = wp_req("GET", path, params=params)
    try:
        return r.json()
    except Exception:
        die(f"Failed to decode JSON for GET {path}")


def wp_post_json(path: str, payload: Dict[str, Any]) -> Any:
    r = wp_req("POST", path, json=payload)
    return r.json()


def wp_put_json(path: str, payload: Dict[str, Any]) -> Any:
    r = wp_req("PUT", path, json=payload)
    return r.json()


# -----------------------
# Content helpers
# -----------------------
def find_page_by_slug(slug: str) -> Optional[Dict[str, Any]]:
    pages = wp_get_json("pages", slug=slug, per_page=1, _fields="id,slug,title,link,status")
    return pages[0] if pages else None


def ensure_branches_page() -> int:
    page = find_page_by_slug("branches")
    if page:
        print(f"[nav-seeder] Found /branches page: ID {page['id']}")
        return page["id"]
    payload = {
        "title": "Branches",
        "slug": "branches",
        "status": "publish",
        "content": "<!-- wp:paragraph --><p>Branches directory</p><!-- /wp:paragraph -->",
    }
    created = wp_post_json("pages", payload)
    print(f"[nav-seeder] Created /branches page: ID {created['id']}")
    return created["id"]


def get_posts_page_id() -> Optional[int]:
    try:
        settings = wp_get_json("settings")
        pid = settings.get("page_for_posts", 0)
        if isinstance(pid, int) and pid > 0:
            print(f"[nav-seeder] Posts page ID: {pid}")
            return pid
    except SystemExit:
        raise
    except Exception:
        pass
    pages = wp_get_json("pages", search="Blog", per_page=1, _fields="id,title,slug")
    if pages:
        print(f"[nav-seeder] Using page as Blog: ID {pages[0]['id']}")
        return int(pages[0]["id"])
    print("[nav-seeder] No posts page configured.")
    return None


def get_page_link(page_id: int) -> str:
    """Resolve the canonical permalink for a page ID."""
    data = wp_get_json(f"pages/{page_id}", _fields="link")
    link = data.get("link") if isinstance(data, dict) else None
    if not link:
        link = f"{BASE}/?p={page_id}"
    return link


# -----------------------
# Navigation entity (wp_navigation) helpers
# -----------------------
def get_or_create_navigation(slug: str = "primary", title: str = "Primary") -> Dict[str, Any]:
    # context=edit helps ensure we get content as {"raw": "..."} on newer WP
    navs = wp_get_json("navigation", slug=slug, per_page=1, context="edit")
    if navs:
        print(f"[nav-seeder] Found navigation '{slug}': ID {navs[0]['id']}")
        return navs[0]
    payload = {"title": title, "slug": slug, "status": "publish", "content": ""}
    created = wp_post_json("navigation", payload)
    print(f"[nav-seeder] Created navigation '{slug}': ID {created['id']}")
    return created


def set_navigation_content(nav_id: int, blocks: str) -> Dict[str, Any]:
    # Use {"raw": "..."} when the REST shape expects it; fall back to plain str otherwise.
    payload_content: Any = blocks
    try:
        current = wp_get_json(f"navigation/{nav_id}", context="edit")
        if isinstance(current.get("content"), dict):
            payload_content = {"raw": blocks}
    except Exception:
        pass
    payload = {"content": payload_content, "status": "publish"}
    updated = wp_put_json(f"navigation/{nav_id}", payload)
    print(f"[nav-seeder] Updated navigation ID {nav_id} with {len(blocks)} chars of blocks.")
    return updated


# -----------------------
# Header template part wiring
# -----------------------
HEADER_SLUG = "header"


def get_header_template_part() -> Optional[Dict[str, Any]]:
    # Prefer context=edit so we get content.raw (WordPress 6.3+)
    parts = wp_get_json("template-parts", slug=HEADER_SLUG, per_page=1, context="edit")
    return parts[0] if parts else None


def _content_to_str_and_shape(content_field: Any) -> Tuple[str, bool]:
    """
    Returns (content_str, is_dict_shape).
    is_dict_shape=True means the original field was a dict like {"raw": "..."}.
    """
    if isinstance(content_field, dict):
        return (content_field.get("raw") or content_field.get("rendered") or ""), True
    if isinstance(content_field, str):
        return content_field, False
    return str(content_field or ""), False


def wire_header_to_navigation_ref(nav_id: int) -> None:
    part = get_header_template_part()
    if not part:
        print("[nav-seeder] No 'header' template part found; skipping header wiring.")
        return

    content_str, is_dict_shape = _content_to_str_and_shape(part.get("content", ""))
    original = content_str

    # 1) Replace the first opening navigation block's attrs to include/overwrite "ref"
    def repl_open(match: re.Match) -> str:
        attrs_str = match.group(1)
        try:
            attrs = json.loads(attrs_str)
            if not isinstance(attrs, dict):
                attrs = {}
        except Exception:
            attrs = {}
        attrs["ref"] = nav_id
        return f"<!-- wp:navigation {json.dumps(attrs, separators=(',',':'))} -->"

    content_str, n1 = re.subn(
        r"<!--\s*wp:navigation\s*({.*?})\s*-->",
        repl_open,
        content_str,
        count=1,
        flags=re.IGNORECASE | re.DOTALL,
    )

    # 2) If there was a self-closing navigation block, turn it into a ref’d one
    if n1 == 0:
        content_str, n2 = re.subn(
            r"<!--\s*wp:navigation\s*/\s*-->",
            f'<!-- wp:navigation {{"ref": {nav_id}}} /-->',
            content_str,
            count=1,
            flags=re.IGNORECASE | re.DOTALL,
        )
    else:
        n2 = 0

    # 3) If there is no navigation block at all, prepend one
    if n1 == 0 and n2 == 0:
        content_str = f'<!-- wp:navigation {{"ref": {nav_id}}} /-->\n{content_str}'

    if content_str != original:
        payload_content: Any = {"raw": content_str} if is_dict_shape else content_str
        wp_put_json(f"template-parts/{part['id']}", {"content": payload_content, "status": "publish"})
        print(f"[nav-seeder] Header wired to navigation ref {nav_id}.")
    else:
        print("[nav-seeder] Header already references the navigation; no change.")


# -----------------------
# Block content builders
# -----------------------
def build_nav_minimal(
    branches_page_id: int,
    branches_url: str,
    posts_page_id: Optional[int] = None,
    posts_url: Optional[str] = None,
) -> str:
    """
    Returns a string of block-serialized content for the wp_navigation entity.
    Keeps it super simple: Home, Branches, (optional) Blog.
    """
    blocks = []

    # Home (Home Link block)
    blocks.append('<!-- wp:home-link {"label":"Home"} /-->')

    # Branches -> page ID + url (required for clickable items in nav entity)
    branches_attrs = {
        "label": "Branches",
        "type": "page",
        "id": branches_page_id,
        "url": branches_url,
    }
    blocks.append(f'<!-- wp:navigation-link {json.dumps(branches_attrs, separators=(",",":"))} /-->')

    # Optional Blog link -> posts page ID + url
    if posts_page_id and posts_url:
        blog_attrs = {
            "label": "Blog",
            "type": "page",
            "id": posts_page_id,
            "url": posts_url,
        }
        blocks.append(f'<!-- wp:navigation-link {json.dumps(blog_attrs, separators=(",",":"))} /-->')

    return "\n".join(blocks)


# -----------------------
# Main
# -----------------------
def main():
    parser = argparse.ArgumentParser(description="Seed minimal primary navigation (no dropdown).")
    parser.add_argument("--no-header-touch", action="store_true", help="Do not modify Header template part.")
    parser.add_argument("--no-blog", action="store_true", help="Skip Blog link even if posts page exists.")
    args = parser.parse_args()

    print(f"[nav-seeder] Base: {BASE}")
    branches_page_id = ensure_branches_page()
    branches_url = get_page_link(branches_page_id)

    posts_page_id = None if args.no_blog else get_posts_page_id()
    posts_url = get_page_link(posts_page_id) if posts_page_id else None

    nav = get_or_create_navigation("primary", "Primary")
    blocks = build_nav_minimal(branches_page_id, branches_url, posts_page_id, posts_url)
    set_navigation_content(nav["id"], blocks)

    if not args.no_header_touch:
        wire_header_to_navigation_ref(nav["id"])
    else:
        print("[nav-seeder] Skipping header wiring per --no-header-touch")

    print("[nav-seeder] Done.")


if __name__ == "__main__":
    main()
