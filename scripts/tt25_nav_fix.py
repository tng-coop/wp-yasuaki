#!/usr/bin/env python3
"""
TT25 nav + branches submenu (idempotent, no PHP).

What it does:
- Ensures /branches page exists.
- Builds/updates wp_navigation 'primary' to include:
    Home
    Branches (as a submenu that lists ALL branch Overview pages)
    + Blog (if you have a posts page set)
- Rewires the Header template part to reference that nav (keeps Page List removed).
- If /branches page content is blank, writes a simple directory list there.

Env:
  WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD
"""

import os, sys, json, re
import requests

BASE = (os.environ.get("WP_BASE_URL") or "").rstrip("/")
USER = os.environ.get("WP_USERNAME") or ""
PASS = (os.environ.get("WP_APP_PASSWORD") or "").replace(" ", "")

if not (BASE and USER and PASS):
    print("Set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD", file=sys.stderr)
    sys.exit(1)

API = f"{BASE}/wp-json/wp/v2"
S = requests.Session()
S.auth = (USER, PASS)
S.headers.update({"Accept":"application/json","Content-Type":"application/json; charset=utf-8"})
TIMEOUT = 25

def get(endpoint, **params):
    r = S.get(f"{API}/{endpoint}", params=params or None, timeout=TIMEOUT)
    if r.status_code == 404:
        return None
    r.raise_for_status()
    return r.json()

def post(endpoint, payload):
    r = S.post(f"{API}/{endpoint}", data=json.dumps(payload), timeout=TIMEOUT)
    r.raise_for_status()
    return r.json()

def content_to_text(content_obj):
    if isinstance(content_obj, str): return content_obj
    if isinstance(content_obj, dict):
        if isinstance(content_obj.get("raw"), str): return content_obj["raw"]
        if isinstance(content_obj.get("rendered"), str): return content_obj["rendered"]
    return ""

# ---------- site bits ----------

def ensure_branches_page():
    data = get("pages", slug="branches", _fields="id,slug,content")
    if isinstance(data, list) and data:
        pid = data[0]["id"]
        raw = content_to_text(data[0].get("content"))
        return pid, raw
    created = post("pages", {"status":"publish","slug":"branches","title":"Branches","content":" "})
    return created["id"], " "

def get_posts_page_id():
    s = get("settings", _fields="show_on_front,page_for_posts")
    if isinstance(s, dict) and s.get("show_on_front") == "page":
        return s.get("page_for_posts") or None
    return None

# ---------- taxonomy → branch terms & overview pages ----------

def get_branch_parent_id():
    data = get("categories", slug="branch", _fields="id")
    if isinstance(data, list) and data:
        return data[0]["id"]
    return None

def get_branch_terms(parent_id):
    out = []
    page = 1
    while True:
        r = get("categories", parent=parent_id, per_page=100, page=page, _fields="id,slug,name")
        if not r: break
        out.extend(r)
        if len(r) < 100: break
        page += 1
    return out

def get_overview_page_for_term(term):
    """
    Pages are created with slug '<term.slug>-page', e.g. 'branch-k03-page'.
    """
    slug = f"{term['slug']}-page"
    data = get("pages", slug=slug, _fields="id,slug,link,title")
    if isinstance(data, list) and data:
        p = data[0]
        return {"id": p["id"], "url": p.get("link") or f"{BASE}/{slug}/", "title": p.get("title",{}).get("rendered") or term["name"]}
    # fallback: link to the category archive if page missing
    term_full = get(f"categories/{term['id']}", _fields="link")
    return {"id": None, "url": (term_full or {}).get("link") or f"{BASE}/category/{term['slug']}/", "title": term["name"]}

# ---------- wp_navigation entity ----------

def ensure_navigation_entity():
    data = get("navigation", slug="primary", context="edit", _fields="id,slug,content")
    if isinstance(data, list) and data:
        nav = data[0]
        return nav["id"], content_to_text(nav.get("content"))
    created = post("navigation", {"status":"publish","title":"Primary","slug":"primary","content":""})
    return created["id"], ""

def build_nav_with_branches(branches_page_id, branch_links, posts_page_id=None):
    """
    Build block markup:
      Home
      Submenu 'Branches' (links inside)
      [Blog]
    """
    blocks = []
    blocks.append('<!-- wp:home-link {"label":"Home"} /-->')

    # Submenu under "Branches" that also keeps the parent clickable
    submenu_attrs = {"label":"Branches","url":f"{BASE}/branches/","type":"page","id":branches_page_id}
    blocks.append(f'<!-- wp:navigation-submenu {json.dumps(submenu_attrs, separators=(",",":"))} -->')
    for link in branch_links:
        # prefer page ids when available
        link_attrs = {"label": link["title"], "url": link["url"]}
        if link["id"]:
            link_attrs.update({"type":"page","id":link["id"]})
        blocks.append(f'<!-- wp:navigation-link {json.dumps(link_attrs, ensure_ascii=False)} /-->')
    blocks.append('<!-- /wp:navigation-submenu -->')

    if posts_page_id:
        blocks.append(f'<!-- wp:navigation-link {json.dumps({"label":"Blog","type":"page","id":posts_page_id,"url":f"{BASE}/?p={posts_page_id}"})} /-->')
    return "\n".join(blocks)

def update_navigation_content(nav_id, desired_blocks):
    data = get(f"navigation/{nav_id}", context="edit", _fields="id,content")
    current = content_to_text((data or {}).get("content"))
    if current.strip() == desired_blocks.strip():
        print("[INFO] Navigation content already up to date")
        return
    post(f"navigation/{nav_id}", {"content": desired_blocks})
    print("[OK] Updated Navigation entity content")

# ---------- header template part (set ref + no fallback) ----------

def get_header_template_part():
    data = get("template-parts", slug="header", context="edit", _fields="id,slug,area,content")
    if isinstance(data, list) and data:
        item = data[0]
        return item["id"], content_to_text(item.get("content"))
    data = get("template-parts", search="header", per_page=20, context="edit", _fields="id,slug,area,content")
    best = None
    if isinstance(data, list) and data:
        for it in data:
            if it.get("slug") == "header": best = it; break
        if not best:
            for it in data:
                if it.get("area") == "header": best = it; break
        if not best and data:
            best = data[0]
    if best:
        return best["id"], content_to_text(best.get("content"))
    return None, None

def set_nav_ref_and_strip_pagelist(header_content, nav_id):
    if not header_content: return header_content, False
    start_re = re.compile(r"<!--\s*wp:navigation(?P<attrs>\s+\{.*?\})?\s*-->", re.DOTALL|re.IGNORECASE)
    end_re   = re.compile(r"<!--\s*/wp:navigation\s*-->", re.IGNORECASE)
    m = start_re.search(header_content)
    if not m: return header_content, False
    end_m = end_re.search(header_content, m.end())
    if not end_m: return header_content, False

    attrs_text = m.group("attrs")
    attrs = {}
    if attrs_text:
        try: attrs = json.loads(attrs_text.strip())
        except Exception: attrs = {}
    if attrs.get("ref") != nav_id:
        attrs["ref"] = nav_id
    new_start = f"<!-- wp:navigation {json.dumps(attrs, separators=(',',':'))} -->"
    new_nav   = f"{new_start}\n<!-- /wp:navigation -->"
    new_content = header_content[:m.start()] + new_nav + header_content[end_m.end():]
    return new_content, (new_content != header_content)

def update_header_template_part(header_id, new_content):
    post(f"template-parts/{header_id}", {"content": new_content})
    print("[OK] Header updated: wired Navigation ref & removed fallback")

# ---------- optional: branches hub page directory if blank ----------

def update_branches_page_if_blank(page_id, current_raw, branch_links):
    if current_raw and current_raw.strip():
        print("[INFO] /branches page already has content; leaving it untouched")
        return
    # Simple list of links (keeps it lightweight)
    items = "\n".join([f'<li><a href="{l["url"]}">{l["title"]}</a></li>' for l in branch_links])
    page_blocks = f"""<!-- wp:group --><div class="wp-block-group">
<!-- wp:heading --><h2>Branches</h2><!-- /wp:heading -->
<!-- wp:list --><ul>
{items}
</ul><!-- /wp:list -->
</div><!-- /wp:group -->"""
    post(f"pages/{page_id}", {"content": page_blocks})
    print("[OK] Wrote a simple directory to /branches (only because it was blank)")

# ---------- main ----------

def main():
    print("[STEP] Finding branch taxonomy…")
    parent_id = get_branch_parent_id()
    if not parent_id:
        print("[ERROR] No parent 'branch' category found. Seed categories first.")
        sys.exit(1)

    print("[STEP] Loading branch terms…")
    terms = get_branch_terms(parent_id)
    print(f"[INFO] {len(terms)} branch terms")

    print("[STEP] Resolving Overview pages…")
    links = [get_overview_page_for_term(t) for t in terms]

    print("[STEP] Ensuring /branches page…")
    branches_page_id, branches_page_raw = ensure_branches_page()

    print("[STEP] Ensuring Navigation entity…")
    nav_id, _ = ensure_navigation_entity()

    print("[STEP] Writing Navigation (submenu under “Branches”)…")
    posts_page_id = get_posts_page_id()
    nav_blocks = build_nav_with_branches(branches_page_id, links, posts_page_id)
    update_navigation_content(nav_id, nav_blocks)

    print("[STEP] Wiring Header to this Navigation…")
    header_id, header_content = get_header_template_part()
    if header_id and header_content is not None:
        new_content, changed = set_nav_ref_and_strip_pagelist(header_content, nav_id)
        if changed:
            update_header_template_part(header_id, new_content)
        else:
            print("[INFO] Header already points at nav & has no fallback")
    else:
        print("[WARN] Could not load header template part via REST; skipping header write (front-end likely already fine)")

    print("[STEP] Filling /branches hub page if blank…")
    update_branches_page_if_blank(branches_page_id, branches_page_raw, links)

    print("[DONE] TT25 navigation + submenu ready.")

if __name__ == "__main__":
    try:
        main()
    except requests.HTTPError as e:
        try: detail = e.response.json()
        except Exception: detail = e.response.text if e.response is not None else ""
        print(f"[HTTP ERROR] {e} :: {detail}", file=sys.stderr); sys.exit(1)
    except Exception as e:
        print(f"[ERROR] {type(e).__name__}: {e}", file=sys.stderr); sys.exit(1)

