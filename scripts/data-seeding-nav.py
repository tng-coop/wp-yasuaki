#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
data-seeding-nav.py — self-checking, CONFIGURATION-ONLY

What it does (idempotent):
  • Preflight (fail-fast): verify we can
      - read runtime template & theme
      - find a header (same theme) that ALREADY has a Navigation block with {"ref": <nav id>}
      - read/update that nav entity
  • Ensure /branches exists; re-parent branch-* pages; make /branches list child pages (Query Loop).
  • Switch the runtime template to use that header slug (no header block injection).
  • Overwrite ONLY that nav entity to a single “Branches” link.
  • Verify with cache buster; exit non-zero if header still lacks nav/link.
"""

from __future__ import annotations
import os, sys, re, json, time, requests
from typing import Any, Dict, List, Tuple

BASE = os.environ.get("WP_BASE_URL","").rstrip("/")
USER = os.environ.get("WP_USERNAME","")
APP  = os.environ.get("WP_APP_PASSWORD","")
API  = f"{BASE}/wp-json/wp/v2"
TIMEOUT = 30

def die(msg: str, code: int = 1):
    print(msg, file=sys.stderr); sys.exit(code)

def ok(msg: str): print(f"[nav-seeder] {msg}")

if not (BASE and USER and APP):
    die("[nav-seeder] ERROR: set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD")

# ---------------- HTTP ----------------
def _req(method: str, path: str, **kwargs) -> requests.Response:
    url = path if path.startswith("http") else f"{API}/{path.lstrip('/')}"
    headers = {"Accept":"application/json", **kwargs.pop("headers", {})}
    try:
        r = requests.request(method, url, auth=(USER, APP), headers=headers, timeout=TIMEOUT, **kwargs)
    except Exception as e:
        die(f"[nav-seeder] {method} {url} failed: {e}")
    if not r.ok:
        try: payload = r.json()
        except Exception: payload = r.text
        die(f"[nav-seeder] {method} {url} -> {r.status_code} {payload}")
    return r

def wp_get(path: str, **params) -> Any: return _req("GET", path, params=params).json()
def wp_post(path: str, payload: Dict[str, Any]) -> Any: return _req("POST", path, json=payload).json()
def wp_put(path: str, payload: Dict[str, Any]) -> Any: return _req("PUT", path, json=payload).json()

def content_to_str(field: Any) -> str:
    if isinstance(field, dict): return (field.get("raw") or field.get("rendered") or "")
    if isinstance(field, str):  return field
    return ""

# ---------------- Utils ----------------
CANDIDATE_TPLS = ("front-page","home","page","index","archive")

def detect_template_and_theme() -> Tuple[str,str,str]:
    """Return (tpl_id, tpl_slug, theme) for runtime front template (best-effort)."""
    for slug in CANDIDATE_TPLS:
        arr = wp_get("templates", slug=slug, context="edit", _fields="id,slug,theme")
        if arr:
            return str(arr[0]["id"]), arr[0]["slug"], (arr[0].get("theme") or "")
    die("[nav-seeder] Preflight FAIL: no templates readable. Need edit access.")

def header_slugs_for_template_id(tpl_id: str) -> List[str]:
    tpl = wp_get(f"templates/{tpl_id}", context="edit", _fields="content,theme")
    raw = content_to_str(tpl.get("content"))
    slugs = re.findall(r'wp:template-part\s*\{[^}]*"slug"\s*:\s*"([^"]+)"', raw or "", flags=re.I)
    hdrs = sorted({s for s in slugs if s.startswith("header")})
    return hdrs or ["header"]

def list_header_parts_for_theme(theme: str) -> List[Dict[str,str]]:
    parts = wp_get("template-parts", per_page=100, context="edit", _fields="id,slug,theme,content")
    out = []
    for p in parts or []:
        if theme and p.get("theme") not in (theme,):  # same theme only
            continue
        slug = str(p.get("slug") or "")
        if not slug.startswith("header"): continue
        out.append({
            "id": str(p["id"]),
            "slug": slug,
            "theme": p.get("theme") or "",
            "raw": content_to_str(p.get("content")),
        })
    return out

def find_header_with_nav_ref(theme: str) -> Tuple[str,str]:
    """Return (header_slug, nav_id) for a header that already has Navigation with {"ref": …}."""
    for h in list_header_parts_for_theme(theme):
        m = re.search(r'"ref"\s*:\s*("[^"]+"|[0-9]+)', h["raw"] or "", re.I)
        if m:
            nav_id = m.group(1).strip('"')
            # ensure we can read that nav in edit context
            _ = wp_get(f"navigation/{nav_id}", context="edit", _fields="id")
            return h["slug"], nav_id
    die("[nav-seeder] Preflight FAIL: no header (same theme) with Navigation {\"ref\":…} found.\n"
        "Open Site Editor → choose any header → add a Navigation block → set it to use a Menu (wp_navigation).\n"
        "Then re-run this script.")

def switch_template_header_to_slug(tpl_id: str, new_header_slug: str) -> None:
    """Replace any 'header*' slug in the template with the chosen header slug (configuration only)."""
    tpl = wp_get(f"templates/{tpl_id}", context="edit", _fields="content")
    raw = content_to_str(tpl.get("content"))
    def _repl(match: re.Match) -> str:
        obj = match.group(0)
        return re.sub(r'("slug"\s*:\s*")([^"]+)(")', rf'\1{new_header_slug}\3', obj)
    new_raw = re.sub(
        r'<!--\s*wp:template-part\s*\{[^}]*"slug"\s*:\s*"header[^"]*"\s*[^}]*\}\s*-->',
        _repl, raw, flags=re.I
    )
    if new_raw != raw:
        wp_put(f"templates/{tpl_id}", {"content":{"raw":new_raw},"status":"publish"})
        ok(f"front-page template now uses header slug '{new_header_slug}'.")
    else:
        ok("Template already uses the chosen header slug.")

# ---------------- /branches helpers ----------------
def ensure_branches_page() -> Tuple[int,str]:
    pages = wp_get("pages", slug="branches", per_page=1, _fields="id,link,status")
    if pages: return int(pages[0]["id"]), pages[0]["link"]
    created = wp_post("pages", {"title":"Branches","slug":"branches","status":"publish"})
    ok(f"Created /branches page: id={created['id']}")
    return int(created["id"]), created["link"]

def reparent_branch_pages(branches_pid: int) -> None:
    page, updated = 1, 0
    while True:
        items = wp_get("pages", per_page=100, page=page, _fields="id,slug,parent,status")
        if not items: break
        for p in items:
            slug = str(p.get("slug") or "")
            if slug.startswith("branch-"):
                pid = int(p["id"])
                parent = int(p.get("parent") or 0)
                if parent != branches_pid or p.get("status") != "publish":
                    wp_put(f"pages/{pid}", {"parent": branches_pid, "status": "publish"})
                    updated += 1
        if len(items) < 100: break
        page += 1
    ok(f"Re-parented {updated} pages." if updated else "Branch page parenting OK.")

PAGES_LOOP = """<!-- wp:query {"query":{"perPage":100,"pages":0,"offset":0,"postType":"page","parents":[BR],"order":"asc","orderBy":"title","inherit":false},"displayLayout":{"type":"list"},"align":"wide"} -->
<div class="wp-block-query alignwide">
  <!-- wp:post-template -->
    <!-- wp:post-title {"isLink":true} /-->
  <!-- /wp:post-template -->
</div>
<!-- /wp:query -->
"""

def ensure_branches_lists_child_pages(branches_pid: int) -> None:
    data = wp_get(f"pages/{branches_pid}", context="edit", _fields="content")
    raw = content_to_str(data.get("content"))
    want = f'"postType":"page","parents":[{branches_pid}]'
    if want.replace(" ","") in (raw or "").replace(" ",""):
        ok("/branches already lists child pages. OK."); return
    # strip any posts loops
    raw = re.sub(
        r'<!--\s*wp:query\b(?:(?!<!--\s*/wp:query\s*-->).)*?"postType"\s*:\s*"post"(?:(?!<!--\s*/wp:query\s*-->).)*?<!--\s*/wp:query\s*-->',
        "", raw or "", flags=re.S|re.I
    )
    loop = PAGES_LOOP.replace("BR", str(branches_pid))
    new_raw = (raw.rstrip()+"\n\n"+loop) if raw else loop
    wp_put(f"pages/{branches_pid}", {"content":{"raw":new_raw},"status":"publish"})
    ok("Appended pages Query Loop to /branches.")

# ---------------- nav entity ----------------
def set_nav_to_single_branches(nav_id: str, branches_pid: int, branches_link: str) -> None:
    attrs = {"label":"Branches","type":"page","id":branches_pid,"url":branches_link}
    raw = ("<!-- wp:navigation -->\n"
           f"<!-- wp:navigation-link {json.dumps(attrs, separators=(',',':'))} /-->\n"
           "<!-- /wp:navigation -->")
    wp_put(f"navigation/{nav_id}", {"content":{"raw":raw},"status":"publish"})
    ok(f"Nav {nav_id} → single “Branches” link.")

# ---------------- verify ----------------
def verify_or_fail() -> None:
    ts = int(time.time())
    html = requests.get(f"{BASE}/?cb={ts}", timeout=TIMEOUT).text
    s, e = html.find("<header"), html.find("</header>", html.find("<header"))
    header = html[s:e] if s!=-1 and e!=-1 else html
    has_nav = ("wp-block-navigation" in header)
    branches_links = len(re.findall(r'href="[^"]*/branches/?', header))
    branch_detail  = len(re.findall(r'href="[^"]*/branch-[^"/]*/?', header))
    print(f"[nav-seeder] VERIFY: header has nav={has_nav}, /branches links={branches_links}, /branch-* links={branch_detail}")
    if not has_nav or branches_links < 1 or branch_detail > 0:
        print("----- header snippet (first 80 lines) -----")
        print("\n".join(header.splitlines()[:80]))
        die("[nav-seeder] ERROR: header still missing nav and/or /branches link.", 2)

# ---------------- main ----------------
def main() -> None:
    ok("Starting…")
    _ = wp_get("")  # REST sanity

    # 0) Preflight — detect template/theme and a header with nav ref (same theme)
    tpl_id, tpl_slug, theme = detect_template_and_theme()
    hdr_slugs = header_slugs_for_template_id(tpl_id)
    ok(f"runtime template={tpl_slug}, theme={theme or '<unknown>'}, current headers={hdr_slugs}")

    header_slug_with_ref, nav_id = find_header_with_nav_ref(theme)
    ok(f"found header with nav ref: slug='{header_slug_with_ref}', nav_id={nav_id}")

    # 1) /branches ensure + listing
    branches_pid, branches_link = ensure_branches_page()
    reparent_branch_pages(branches_pid)
    ensure_branches_lists_child_pages(branches_pid)

    # 2) Overwrite THAT nav entity to single “Branches”
    set_nav_to_single_branches(nav_id, branches_pid, branches_link)

    # 3) Switch front-page template to use that header slug (configuration-only)
    tpl = wp_get(f"templates/{tpl_id}", context="edit", _fields="content")
    switch_template_header_to_slug(tpl_id, content_to_str(tpl.get("content")), header_slug_with_ref)

    # 4) Verify, fail hard if missing
    verify_or_fail()
    ok("Done.")

if __name__ == "__main__":
    main()
