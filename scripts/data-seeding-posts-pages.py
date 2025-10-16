#!/usr/bin/env python3
"""
Seed WordPress pages and posts per branch and automatically embed a **no-API Google Map**
for each branch's address on **pages only** (posts will NOT include maps).

This version still shows the **branch name** and other fields
from `scripts/offices.csv` (Office, TEL, FAX, Email, URL, Work).

Auth uses the WordPress REST API (Application Passwords recommended).

ENV VARS (typical):
  WP_BASE_URL           e.g. https://example.com or https://example.com/wordpress
  WP_USERNAME           WP user with permission to publish posts/pages
  WP_APP_PASSWORD       Application password for that user

OPTIONAL ENV VARS:
  MAPS_ZOOM             e.g. 15 (adds &z=15 to the embed URL)
  MAPS_LANG             e.g. ja (adds &hl=ja to the embed URL)

INPUT DATA (CSV):
  scripts/offices.csv   headers: ID, ID2, Office, Address, TEL, FAX, Email, URL, Work

USAGE:
  python3 scripts/data-seeding-posts-pages.py --update
  # or tweak templates:
  python3 scripts/data-seeding-posts-pages.py \
      --page-title "{office} | アクセス" \
      --page-content "<h2>{office}</h2><p>{address}</p>\n{map_embed}\n{contact_html}"

Notes:
- **Pages:** If your page template omits {map_embed}, the script appends the iframe automatically when an address exists.
- **Posts:** Maps are NOT added (no auto-append, default template has no {map_embed}).
- No Google API key is used. The embed URL is: https://www.google.com/maps?q=...&output=embed
- **Display addresses keep the 〒 mark.** It is removed only inside the Google Maps iframe query.
"""
from __future__ import annotations

import argparse
import base64
import csv
import json
import os
import re
import sys
import typing as t
import urllib.parse
from dataclasses import dataclass

import urllib.request
import urllib.error

# ---------------------------------------------------------------------------
# Models
# ---------------------------------------------------------------------------

@dataclass
class Branch:
    slug: str
    office: str
    address: str
    id: str
    id2: str = ""
    tel: str = ""
    fax: str = ""
    email: str = ""
    site: str = ""
    work: str = ""
    category_id: int | None = None

# ---------------------------------------------------------------------------
# Utils
# ---------------------------------------------------------------------------

def _slugify(text: str) -> str:
    text = text.strip().lower()
    text = re.sub(r"[^a-z0-9]+", "-", text)
    text = re.sub(r"-+", "-", text).strip("-")
    return text or "x"

def _map_query_address(addr: str) -> str:
    """Prepare address for Google Maps query: remove '〒', flatten newlines, collapse spaces."""
    addr = (addr or '').replace('〒', '')
    addr = addr.replace('\r\n', ' ').replace('\r', ' ').replace('\n', ' ')
    addr = re.sub(r'\s+', ' ', addr).strip()
    return addr

def _here(*parts: str) -> str:
    return os.path.join(os.path.dirname(os.path.abspath(__file__)), *parts)

def _csv_path(cli_path: str | None) -> str | None:
    if cli_path:
        return cli_path if os.path.exists(cli_path) else None
    p = _here("offices.csv")
    return p if os.path.exists(p) else None

def load_address_book(csv_path: str | None) -> dict[str, Branch]:
    """Load branches from scripts/offices.csv. Headers: ID, ID2, Office, Address, TEL, FAX, Email, URL, Work"""
    book: dict[str, Branch] = {}
    if not csv_path:
        return book
    with open(csv_path, newline="", encoding="utf-8") as f:
        rdr = csv.DictReader(f)
        for row in rdr:
            code = (row.get("ID") or "").strip()
            if not code:
                continue
            slug = f"branch-{_slugify(code)}"
            office = (row.get("Office") or code).strip()
            # Keep original address for display (may include 〒)
            addr = (row.get("Address") or "").strip()
            b = Branch(
                slug=slug,
                office=office,
                address=addr,
                id=code,
                id2=(row.get("ID2") or "").strip(),
                tel=(row.get("TEL") or "").strip(),
                fax=(row.get("FAX") or "").strip(),
                email=(row.get("Email") or "").strip(),
                site=(row.get("URL") or "").strip(),
                work=(row.get("Work") or "").strip(),
            )
            book[slug] = b
    return book

# ---------------------------------------------------------------------------
# WP REST client (urllib)
# ---------------------------------------------------------------------------

class WP:
    def __init__(self, base_url: str, username: str, app_password: str):
        base = base_url.rstrip("/")
        self.base = base
        token = base64.b64encode(f"{username}:{app_password}".encode()).decode()
        self.headers = {
            "Authorization": f"Basic {token}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        }

    def _url(self, route: str) -> str:
        route = route.lstrip("/")
        return f"{self.base}/wp-json/wp/v2/{route}"

    def _req(self, method: str, route: str, params: dict | None = None, body: dict | None = None) -> dict | list:
        url = self._url(route)
        if params:
            url += ("?" + urllib.parse.urlencode(params))
        data: bytes | None = None
        if body is not None:
            data = json.dumps(body).encode("utf-8")
        req = urllib.request.Request(url, data=data, method=method.upper(), headers=self.headers)
        try:
            with urllib.request.urlopen(req) as resp:
                raw = resp.read()
                if not raw:
                    return {}
                return json.loads(raw.decode("utf-8"))
        except urllib.error.HTTPError as e:
            try:
                err = e.read().decode("utf-8")
            except Exception:
                err = str(e)
            raise RuntimeError(f"WP {method} {route} -> {e.code} {e.reason}: {err}")

    # ---- pagination ----
    def _paged(self, route: str, **params) -> list[dict]:
        out: list[dict] = []
        page = 1
        while True:
            p = dict(params)
            p.setdefault("per_page", 100)
            p["page"] = page
            try:
                data = self._req("GET", route, p)
            except RuntimeError as e:
                if "rest_post_invalid_page_number" in str(e):
                    break
                raise
            if not isinstance(data, list) or not data:
                break
            out.extend(t.cast(list[dict], data))
            if len(data) < p["per_page"]:
                break
            page += 1
        return out

    # ---- taxonomy ----
    def get_category_by_slug(self, slug: str) -> dict | None:
        arr = self._req("GET", "categories", {"slug": slug})
        return arr[0] if isinstance(arr, list) and arr else None

    def ensure_category(self, slug: str, name: str) -> dict:
        cat = self.get_category_by_slug(slug)
        if cat:
            return cat
        body = {"slug": slug, "name": name}
        return t.cast(dict, self._req("POST", "categories", body=body))

    def list_branch_categories(self) -> list[dict]:
        cats = self._paged("categories")
        return [c for c in cats if isinstance(c.get("slug"), str) and c["slug"].startswith("branch-")]

    # ---- content ----
    def get_by_slug(self, kind: str, slug: str) -> dict | None:
        arr = self._req("GET", kind, {"slug": slug})
        return arr[0] if isinstance(arr, list) and arr else None

    def create(self, kind: str, body: dict) -> dict:
        return t.cast(dict, self._req("POST", kind, body=body))

    def update(self, kind: str, post_id: int, body: dict) -> dict:
        return t.cast(dict, self._req("POST", f"{kind}/{post_id}", body=body))

# ---------------------------------------------------------------------------
# Google Maps (no API) + contact HTML
# ---------------------------------------------------------------------------

def maps_iframe_no_api(address: str) -> str:
    """Google Maps iframe without API key, using q= and output=embed.

    Emits exactly these attributes:
      width="600" height="450" style="border:0" loading="lazy"
      allowfullscreen referrerpolicy="no-referrer-when-downgrade"
    """
    if not address:
        return ""
    q = urllib.parse.quote_plus(_map_query_address(address))
    zoom = (os.environ.get("MAPS_ZOOM") or "").strip()
    lang = (os.environ.get("MAPS_LANG") or "").strip()
    params = ["output=embed"]
    if zoom.isdigit():
        params.append(f"z={zoom}")
    if lang:
        params.append("hl=" + urllib.parse.quote_plus(lang))
    src = f"https://www.google.com/maps?q={q}&" + "&".join(params)
    return (
        "<iframe\n"
        "  width=\"600\" height=\"450\" style=\"border:0\"\n"
        "  loading=\"lazy\" allowfullscreen\n"
        "  referrerpolicy=\"no-referrer-when-downgrade\"\n"
        f"  src=\"{src}\">\n"
        "</iframe>"
    )

def contact_block_html(b: Branch) -> str:
    items: list[str] = []
    if b.address:
        items.append(f'<div class="addr">{b.address}</div>')
    tf: list[str] = []
    if b.tel:
        tf.append(f'TEL: <a href="tel:{b.tel}">{b.tel}</a>')
    if b.fax:
        tf.append(f'FAX: {b.fax}')
    if tf:
        items.append('<div class="telfax">' + " / ".join(tf) + '</div>')
    if b.email:
        items.append(f'<div class="email">Email: <a href="mailto:{b.email}">{b.email}</a></div>')
    if b.site:
        items.append(f'<div class="site"><a href="{b.site}" target="_blank" rel="noopener">公式サイト</a></div>')
    if b.work:
        items.append(f'<div class="work">{b.work}</div>')
    if not items:
        return ""
    return '<div class="branch-contact">\n' + "\n".join(items) + '\n</div>'

# ---------------------------------------------------------------------------
# Template rendering
# ---------------------------------------------------------------------------

def render_template(tpl: str, **ctx) -> str:
    try:
        return tpl.format(**ctx)
    except KeyError as e:
        missing = e.args[0]
        raise SystemExit(f"Template missing key {{{missing}}}. Available: {sorted(ctx.keys())}")

# ---------------------------------------------------------------------------
# Payload builders
# ---------------------------------------------------------------------------

def build_page_payload(b: Branch, title_tpl: str, content_tpl: str) -> dict:
    id_display = b.id + (f" ({b.id2})" if b.id2 else "")
    ctx = {
        "office": b.office,
        "slug": b.slug,
        "id": id_display,
        "id2": b.id2,
        "address": b.address,
        "tel": b.tel,
        "fax": b.fax,
        "email": b.email,
        "site": b.site,
        "work": b.work,
        "map_embed": maps_iframe_no_api(b.address),
        "contact_html": contact_block_html(b),
    }
    title = render_template(title_tpl, **ctx)
    content = render_template(content_tpl, **ctx)
    # PAGES: auto-append map if the template omitted it and address exists
    if b.address and "{map_embed}" not in content_tpl:
        content = content + "\n" + ctx["map_embed"]
    if ctx["contact_html"] and "{contact_html}" not in content_tpl:
        content = content + "\n" + ctx["contact_html"]
    return {
        "status": "publish",
        "slug": f"{b.slug}-page",
        "title": title,
        "content": content,
    }

def build_post_payload(b: Branch, index: int, title_tpl: str, content_tpl: str) -> dict:
    id_display = b.id + (f" ({b.id2})" if b.id2 else "")
    ctx = {
        "office": b.office,
        "slug": b.slug,
        "id": id_display,
        "id2": b.id2,
        "address": b.address,
        "index": index,
        "map_embed": maps_iframe_no_api(b.address),  # available for templates, but NOT auto-appended
        "contact_html": contact_block_html(b),
    }
    title = render_template(title_tpl, **ctx)
    content = render_template(content_tpl, **ctx)
    # POSTS: do NOT auto-append map (keep blog/archive clean)
    if ctx["contact_html"] and "{contact_html}" not in content_tpl:
        content = content + "\n" + ctx["contact_html"]
    body = {
        "status": "publish",
        "slug": f"{b.slug}-post-{index}",
        "title": title,
        "content": content,
    }
    if b.category_id is not None:
        body["categories"] = [b.category_id]
    return body

# ---------------------------------------------------------------------------
# Upsert logic
# ---------------------------------------------------------------------------

def upsert_page(wp: WP, payload: dict, update: bool, dry_run: bool = False) -> int | None:
    slug = payload.get("slug")
    assert slug, "page payload requires slug"
    existing = wp.get_by_slug("pages", slug)
    if existing:
        if update:
            if dry_run:
                print(f"DRY-RUN: would UPDATE page {slug} (id={existing['id']})")
                return existing["id"]
            r = wp.update("pages", existing["id"], payload)
            print(f"Updated page {slug} (id={r['id']})")
            return r["id"]
        else:
            print(f"Skip existing page {slug} (id={existing['id']})")
            return existing["id"]
    else:
        if dry_run:
            print(f"DRY-RUN: would CREATE page {slug}")
            return None
        r = wp.create("pages", payload)
        print(f"Created page {slug} (id={r['id']})")
        return r["id"]

def upsert_post(wp: WP, payload: dict, update: bool, dry_run: bool = False) -> int | None:
    slug = payload.get("slug")
    assert slug, "post payload requires slug"
    existing = wp.get_by_slug("posts", slug)
    if existing:
        if update:
            if dry_run:
                print(f"DRY-RUN: would UPDATE post {slug} (id={existing['id']})")
                return existing["id"]
            r = wp.update("posts", existing["id"], payload)
            print(f"Updated post {slug} (id={r['id']})")
            return r["id"]
        else:
            print(f"Skip existing post {slug} (id={existing['id']})")
            return existing["id"]
    else:
        if dry_run:
            print(f"DRY-RUN: would CREATE post {slug}")
            return None
        r = wp.create("posts", payload)
        print(f"Created post {slug} (id={r['id']})")
        return r["id"]

# ---------------------------------------------------------------------------
# Discover branches (from CSV + existing categories)
# ---------------------------------------------------------------------------

def discover_branches(wp: WP, csv_path: str | None) -> list[Branch]:
    book = load_address_book(csv_path)
    # Index existing branch categories in WP
    existing = {c["slug"]: c for c in wp.list_branch_categories()}

    branches: list[Branch] = []

    # Ensure categories for branches from CSV
    for slug, b in book.items():
        cat = existing.get(slug)
        if not cat:
            try:
                cat = wp.ensure_category(slug, b.office)
                print(f"Created category {slug} (id={cat['id']})")
            except RuntimeError as e:
                print(f"WARNING: could not create category {slug}: {e}")
                cat = None
        cid = cat["id"] if cat else None
        b.category_id = cid
        branches.append(b)

    # Also include any branch categories present in WP that aren't in CSV
    for slug, cat in existing.items():
        if slug in book:
            continue
        nm = cat.get("name") or slug
        branches.append(Branch(slug=slug, office=str(nm), address="", id=slug.replace("branch-", ""), category_id=cat.get("id")))

    branches.sort(key=lambda x: x.slug)
    return branches

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def run(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description="Seed branch pages & posts (maps on pages only; never on posts).")
    ap.add_argument("--base", default=os.environ.get("WP_BASE_URL", ""), help="WP base URL (env: WP_BASE_URL)")
    ap.add_argument("--user", default=os.environ.get("WP_USERNAME", ""), help="WP username (env: WP_USERNAME)")
    ap.add_argument("--password", default=os.environ.get("WP_APP_PASSWORD", ""), help="WP application password (env: WP_APP_PASSWORD)")
    ap.add_argument("--csv", default=None, help="Path to offices.csv (default: scripts/offices.csv)")
    ap.add_argument("--posts-per-branch", type=int, default=1, help="How many posts to create per branch")
    ap.add_argument("--update", action="store_true", help="Update existing pages/posts if present")
    ap.add_argument("--dry-run", action="store_true", help="Print actions without modifying WP")

    ap.add_argument("--page-title", default="{office}", help="Page title template")
    ap.add_argument("--page-content", default=(
        "<h2>{office}</h2>\n"
        "<p><strong>ID:</strong> {id}</p>\n"
        "<p><strong>住所:</strong> {address}</p>\n"
        "{map_embed}\n"
        "{contact_html}"
    ), help="Page content HTML template")

    ap.add_argument("--post-title", default="{office} | お知らせ #{index}", help="Post title template")
    # IMPORTANT: default post content **no map**
    ap.add_argument("--post-content", default="<p>{office} の更新情報 #{index}</p>\n{contact_html}", help="Post content HTML template")

    args = ap.parse_args(argv)

    if not args.base or not args.user or not args.password:
        ap.error("--base, --user, and --password (or WP_* envs) are required")

    csv_path = _csv_path(args.csv)
    if not csv_path:
        print("WARNING: offices.csv not found; proceeding without addresses.")

    wp = WP(args.base, args.user, args.password)
    branches = discover_branches(wp, csv_path)

    print(f"Found {len(branches)} branches")
    for br in branches:
        page_payload = build_page_payload(br, args.page_title, args.page_content)
        upsert_page(wp, page_payload, update=args.update, dry_run=args.dry_run)
        for i in range(1, args.posts_per_branch + 1):
            post_payload = build_post_payload(br, i, args.post_title, args.post_content)
            upsert_post(wp, post_payload, update=args.update, dry_run=args.dry_run)

    print("✅ Done.")
    return 0

if __name__ == "__main__":
    try:
        raise SystemExit(run(sys.argv[1:]))
    except KeyboardInterrupt:
        raise SystemExit(130)
