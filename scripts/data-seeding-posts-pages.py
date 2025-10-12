#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Idempotent seeder (only --update flag)

Zero-arg:
  - Ensures a parent Category with slug "branch" exists.
  - If no children, seeds branch categories from scripts/offices.csv (slug "branch-<ID>", name "Office").
  - For each branch (child category), ensures EXACTLY:
      • 1 Page   with slug: <term.slug>-page
      • 3 Posts  with slugs: <term.slug>-post-1..3
    Posts are attached to the branch via the "categories" field.
  - Each page includes an Addresses section when Address data is found.

--update:
  - Recomputes title/content for pages and posts and updates ONLY IF changed.
  - Also ensures post categories include the branch term.

Optional templates (no extra flags; place files under scripts/templates/):
  templates/branch_page.title.txt   # e.g. "{name} Branch Overview"
  templates/branch_post.title.txt   # e.g. "News #{index} – {name}"
  templates/branch_page.html        # may include "{addresses_html}"
  templates/branch_post.html

Placeholders:
  Pages: {name}, {slug}, {term_id}, {addresses_html}
  Posts: {name}, {slug}, {term_id}, {index}

Required env:
  WP_BASE_URL (no trailing slash), WP_USERNAME, WP_APP_PASSWORD
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
ADDRESS_ENDPOINT = f"{API_ROOT}/address"  # Address CPT REST base

SESSION = requests.Session()
SESSION.auth = (WP_USERNAME, WP_APP_PASSWORD)
SESSION.headers.update(
    {"Accept": "application/json", "Content-Type": "application/json; charset=utf-8"}
)

REQUEST_TIMEOUT = 20  # seconds

# --------------------------- Helpers ---------------------------

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
TPL_DIR = os.path.join(SCRIPT_DIR, "templates")

def _slugify(s: str) -> str:
    s = (s or "").strip()
    s = re.sub(r"[^a-zA-Z0-9_-]+", "-", s)
    s = re.sub(r"-{2,}", "-", s).strip("-").lower()
    return s

def _csv_path() -> Optional[str]:
    """Return scripts/offices.csv if it exists."""
    p = os.path.join(SCRIPT_DIR, "offices.csv")
    return p if os.path.exists(p) else None

def _read_file_or_blank(path: Optional[str]) -> str:
    if not path or not os.path.exists(path):
        return ""
    try:
        with open(path, "r", encoding="utf-8") as f:
            return f.read()
    except Exception as e:
        print(f"[WARN] Could not read template file '{path}': {e}", file=sys.stderr)
        return ""

def _tpl(path_in_templates: str, default_text: str) -> str:
    return _read_file_or_blank(os.path.join(TPL_DIR, path_in_templates)) or default_text

def _render(tpl: str, **ctx) -> str:
    try:
        return tpl.format(**ctx)
    except Exception:
        return tpl

def _normalize_html(s: Optional[str]) -> str:
    if not s:
        return ""
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
    """Return all child categories (include term meta; need context=edit for meta)."""
    out: List[Dict[str, Any]] = []
    page = 1
    while True:
        r = SESSION.get(
            CATEGORIES_ENDPOINT,
            params={
                "parent": parent_id,
                "per_page": 100,
                "page": page,
                "context": "edit",
                "_fields": "id,slug,name,meta",
            },
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

# --------------------------- Content fetch / upsert ---------------------------

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

def get_item_raw(endpoint: str, item_id: int) -> Dict[str, Any]:
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

# --------------------------- Addresses for a branch ---------------------------

def _fetch_address(aid: int) -> Optional[Dict[str, Any]]:
    try:
        r = SESSION.get(
            f"{ADDRESS_ENDPOINT}/{aid}",
            params={"context": "edit", "_fields": "id,link,title,meta"},
            timeout=REQUEST_TIMEOUT,
        )
        if r.status_code == 404:
            return None
        r.raise_for_status()
        a = r.json() or {}
        meta = a.get("meta") or {}
        return {
            "id": a.get("id"),
            "title": (a.get("title") or {}).get("rendered") or (a.get("title") or {}).get("raw") or "",
            "url": a.get("link") or "",
            "address": meta.get("address", ""),
            "tel": meta.get("tel", ""),
            "fax": meta.get("fax", ""),
            "email": meta.get("email", ""),
            "site": meta.get("url", ""),
            "work": meta.get("work", ""),
        }
    except requests.HTTPError:
        return None

def _fetch_address_by_slug(addr_slug: str) -> Optional[Dict[str, Any]]:
    """Fallback: resolve Address by slug (e.g., address-d01) if term meta is absent."""
    try:
        r = SESSION.get(
            ADDRESS_ENDPOINT,
            params={"slug": addr_slug, "context": "edit", "_fields": "id,link,title,meta"},
            timeout=REQUEST_TIMEOUT,
        )
        if r.status_code == 404:
            return None
        r.raise_for_status()
        items = r.json() if isinstance(r.json(), list) else []
        if not items:
            return None
        a = items[0]
        meta = a.get("meta") or {}
        return {
            "id": a.get("id"),
            "title": (a.get("title") or {}).get("rendered") or (a.get("title") or {}).get("raw") or "",
            "url": a.get("link") or "",
            "address": meta.get("address", ""),
            "tel": meta.get("tel", ""),
            "fax": meta.get("fax", ""),
            "email": meta.get("email", ""),
            "site": meta.get("url", ""),
            "work": meta.get("work", ""),
        }
    except requests.HTTPError:
        return None

def addresses_for_term(term: Dict[str, Any]) -> List[Dict[str, Any]]:
    meta = term.get("meta") or {}
    ids: List[int] = []
    # primary single ID
    apid = meta.get("address_post_id")
    if apid:
        try:
            ids.append(int(apid))
        except Exception:
            pass
    # optional multiple
    apids = meta.get("address_post_ids")
    if isinstance(apids, list):
        for x in apids:
            try:
                ids.append(int(x))
            except Exception:
                pass
    elif isinstance(apids, str):
        for x in re.split(r"[,\s]+", apids.strip()):
            if x.isdigit():
                ids.append(int(x))

    seen, out = set(), []
    for aid in ids:
        if aid in seen:
            continue
        a = _fetch_address(aid)
        if a:
            out.append(a)
            seen.add(aid)

    if out:
        return out

    # Fallback if meta missing: derive Address by slug convention
    slug = (term.get("slug") or "").strip().lower()
    if slug.startswith("branch-"):
        code = slug[len("branch-"):]
        guess = _fetch_address_by_slug(f"address-{code}")
        if guess:
            return [guess]
    return []

def build_addresses_html(term: Dict[str, Any], section_title: str = "所在地 / 連絡先") -> str:
    addrs = addresses_for_term(term)
    if not addrs:
        return ""
    li = []
    for a in addrs:
        pieces = []
        if a["address"]:
            pieces.append(f'<div class="addr">{a["address"]}</div>')
        telfax = []
        if a["tel"]:
            telfax.append(f'TEL: <a href="tel:{a["tel"]}">{a["tel"]}</a>')
        if a["fax"]:
            telfax.append(f'FAX: {a["fax"]}')
        if telfax:
            pieces.append(f'<div class="telfax">{" / ".join(telfax)}</div>')
        if a["email"]:
            pieces.append(f'<div class="email">Email: <a href="mailto:{a["email"]}">{a["email"]}</a></div>')
        if a["site"]:
            pieces.append(f'<div class="site"><a href="{a["site"]}" target="_blank" rel="noopener">公式サイト</a></div>')
        if a["work"]:
            pieces.append(f'<div class="work">{a["work"]}</div>')
        title = a["title"] or ""
        title_html = f"<strong>{title}</strong><br>" if title else ""
        li.append(f"<li>{title_html}{''.join(pieces)}</li>")
    return (
        '<div class="branch-addresses">\n'
        f'  <h3>{section_title}</h3>\n'
        '  <ul>\n    ' + "\n    ".join(li) + '\n  </ul>\n'
        '</div>\n'
    )

# --------------------------- Templates ---------------------------

DEFAULT_PAGE_TITLE_TPL = "{name}"
DEFAULT_POST_TITLE_TPL = "{name} Branch Update #{index}"
DEFAULT_PAGE_HTML = (
    "<p>Welcome to the {name} branch page.</p>\n"
    "<p>This page provides an overview and sample content for the {name} branch.</p>\n"
    "{addresses_html}"
)
DEFAULT_POST_HTML = (
    "<p>This is sample post {index} for the {name} branch.</p>\n"
    "<p>It demonstrates assigning content to the branch category.</p>"
)

def resolve_templates():
    page_title_tpl = _tpl("branch_page.title.txt", DEFAULT_PAGE_TITLE_TPL)
    post_title_tpl = _tpl("branch_post.title.txt", DEFAULT_POST_TITLE_TPL)
    page_content_tpl = _tpl("branch_page.html", DEFAULT_PAGE_HTML)
    post_content_tpl = _tpl("branch_post.html", DEFAULT_POST_HTML)
    return page_title_tpl, page_content_tpl, post_title_tpl, post_content_tpl

def build_page_payload(term: Dict[str, Any], page_title_tpl: str, page_content_tpl: str) -> Dict[str, Any]:
    slug = term.get("slug") or "branch"
    ctx = {"name": term.get("name") or "Branch", "slug": slug, "term_id": term.get("id")}
    addresses_html = build_addresses_html(term)  # default title inside
    content = _render(page_content_tpl, **ctx, addresses_html=addresses_html)
    if "{addresses_html}" not in page_content_tpl and addresses_html:
        content = content.rstrip() + "\n\n" + addresses_html
    return {
        "status": "publish",
        "slug": f"{slug}-page",
        "title": _render(page_title_tpl, **ctx),
        "content": content,
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

def ensure_page(term: Dict[str, Any], update: bool, page_title_tpl: str, page_content_tpl: str) -> Tuple[str, str]:
    payload = build_page_payload(term, page_title_tpl, page_content_tpl)
    slug = payload["slug"]
    existing_id = get_single_item(PAGES_ENDPOINT, slug)
    if not existing_id:
        r = SESSION.post(PAGES_ENDPOINT, json=payload, timeout=REQUEST_TIMEOUT)
        r.raise_for_status()
        return "created", f"created (post_id={int(r.json()['id'])})"

    if not update:
        return "skipped", f"exists (post_id={existing_id})"

    cur = get_item_raw(PAGES_ENDPOINT, existing_id)
    need_title   = _normalize_html(cur["title"])   != _normalize_html(payload["title"])
    need_content = _normalize_html(cur["content"]) != _normalize_html(payload["content"])
    if not (need_title or need_content):
        return "skipped", f"up-to-date (post_id={existing_id})"

    delta: Dict[str, Any] = {}
    if need_title:   delta["title"]   = payload["title"]
    if need_content: delta["content"] = payload["content"]
    r = SESSION.post(f"{PAGES_ENDPOINT}/{existing_id}", json=delta, timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    return "updated", f"updated (post_id={existing_id}, fields={','.join(delta.keys())})"

def ensure_posts(term: Dict[str, Any], update: bool, post_title_tpl: str, post_content_tpl: str) -> Tuple[int, int, int]:
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

        if not update:
            skipped += 1
            print(f"    [SKIP] {slug} exists (post_id={existing_id}).", flush=True)
            continue

        cur = get_item_raw(POSTS_ENDPOINT, existing_id)
        need_title   = _normalize_html(cur["title"])   != _normalize_html(payload["title"])
        need_content = _normalize_html(cur["content"]) != _normalize_html(payload["content"])
        want_cats = set(payload["categories"])
        have_cats = set(cur.get("categories") or [])
        need_cats = bool(want_cats - have_cats)

        if not (need_title or need_content or need_cats):
            skipped += 1
            print(f"    [OK] {slug} up-to-date (post_id={existing_id}).", flush=True)
            continue

        delta: Dict[str, Any] = {}
        if need_title:   delta["title"]   = payload["title"]
        if need_content: delta["content"] = payload["content"]
        if need_cats:    delta["categories"] = list(have_cats | want_cats)

        r = SESSION.post(f"{POSTS_ENDPOINT}/{existing_id}", json=delta, timeout=REQUEST_TIMEOUT)
        r.raise_for_status()
        updated += 1
        print(f"    [OK] {slug} updated (post_id={existing_id}, fields={','.join(delta.keys())}).", flush=True)
    return created, updated, skipped

# --------------------------- Main ---------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description="Seed branch pages and posts (idempotent; only --update flag).")
    parser.add_argument("--update", action="store_true", help="Update existing pages/posts when content/title differ.")
    args = parser.parse_args()

    update_mode = bool(args.update)

    page_title_tpl, page_content_tpl, post_title_tpl, post_content_tpl = resolve_templates()

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

    print(f"[INFO] Loaded {len(terms)} branch term(s). Update mode: {update_mode}", flush=True)

    pages_created = pages_updated = pages_skipped = 0
    posts_created = posts_updated = posts_skipped = 0

    for term in terms:
        name = term.get("name") or term.get("slug") or "branch"
        print(f"[TERM] {name} (id={term.get('id')}, slug={term.get('slug')})", flush=True)

        status, msg = ensure_page(term, update_mode, page_title_tpl, page_content_tpl)
        if status == "created":
            pages_created += 1
        elif status == "updated":
            pages_updated += 1
        else:
            pages_skipped += 1
        print(f"  [PAGE] {msg}", flush=True)

        c, u, s = ensure_posts(term, update_mode, post_title_tpl, post_content_tpl)
        posts_created += c
        posts_updated += u
        posts_skipped += s

    print("\nSummary:", flush=True)
    print(f"  Pages - created: {pages_created}, updated: {pages_updated}, skipped: {pages_skipped}", flush=True)
    print(f"  Posts  - created: {posts_created}, updated: {posts_updated}, skipped: {posts_skipped}", flush=True)

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

