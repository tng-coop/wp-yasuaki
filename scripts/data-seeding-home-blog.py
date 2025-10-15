#!/usr/bin/env python3
"""
Ensure a static Front Page + separate Blog page in WordPress (TT25-friendly).

- Creates/ensures a 'Home' page (slug 'home')
- Creates/ensures a 'Blog' page (slug 'blog')
- Sets Reading options:
    show_on_front = 'page'
    page_on_front = (Home ID)
    page_for_posts = (Blog ID)

Auth: Basic (username + application password)
Env:
  WP_BASE_URL     (preferred) e.g. https://wp.lan
  WP_URL          (fallback if WP_BASE_URL is not set)
  WP_USERNAME     (preferred)
  ADMIN_USER      (fallback if WP_USERNAME not set)
  WP_APP_PASSWORD (required)
  WP_INSECURE=1   (optional; skip TLS verify for self-signed certs)
"""

import os
import sys
import json
from typing import Optional

try:
    import requests
except ImportError:
    sys.stderr.write("This script requires the 'requests' package.\n")
    sys.exit(1)

BASE = os.environ.get("WP_BASE_URL") or os.environ.get("WP_URL") or "https://wp.lan"
USER = os.environ.get("WP_USERNAME") or os.environ.get("ADMIN_USER") or "admin"
PASS = os.environ.get("WP_APP_PASSWORD")
VERIFY = not (os.environ.get("WP_INSECURE", "").lower() in ("1", "true", "yes"))
TIMEOUT = float(os.environ.get("WP_TIMEOUT_SEC", "30"))

if not PASS:
    sys.stderr.write("[skip] reading setup: missing WP_APP_PASSWORD\n")
    sys.exit(0)

API = BASE.rstrip("/") + "/wp-json/wp/v2"

s = requests.Session()
s.auth = (USER, PASS)
s.headers.update({"Accept": "application/json", "Content-Type": "application/json"})


def _req(method: str, path: str, *, params=None, data=None):
    url = f"{API}/{path.lstrip('/')}"
    r = s.request(method, url, params=params, json=data, timeout=TIMEOUT, verify=VERIFY)
    # Helpful error surface
    if not r.ok:
        try:
            err = r.json()
        except Exception:
            err = {"message": r.text}
        raise RuntimeError(f"{method} {path} -> {r.status_code}: {err}")
    return r.json()


def get_page_by_slug(slug: str) -> Optional[int]:
    # context=edit returns broader visibility when authenticated
    res = _req("GET", "pages", params={"slug": slug, "context": "edit", "_fields": "id,slug,status"})
    if isinstance(res, list) and res:
        return int(res[0]["id"])
    return None


def ensure_page(slug: str, title: str) -> int:
    page_id = get_page_by_slug(slug)
    if page_id:
        print(f"[INFO] {title!r} page exists (id={page_id})")
        return page_id
    print(f"[STEP] Creating {title!r} page…")
    created = _req(
        "POST",
        "pages",
        data={"title": title, "slug": slug, "status": "publish"},
    )
    pid = int(created["id"])
    print(f"[OK] Created {title!r} page (id={pid})")
    return pid


def get_settings() -> dict:
    return _req("GET", "settings")


def update_settings(payload: dict) -> dict:
    # WP uses POST to /settings for updates
    return _req("POST", "settings", data=payload)


def main():
    print("[STEP] Ensuring Home / Blog pages…")
    home_id = ensure_page("home", "Home")
    blog_id = ensure_page("blog", "Blog")

    print("[STEP] Reading current settings…")
    settings = get_settings()

    desired = {
        "show_on_front": "page",
        "page_on_front": home_id,
        "page_for_posts": blog_id,
    }

    needs_update = any(
        str(settings.get(k)) != str(v) for k, v in desired.items()
    )

    if needs_update:
        print("[STEP] Updating Reading settings (Front page + Posts page)…")
        updated = update_settings(desired)
        print(
            "[OK] Settings saved: show_on_front=%s, page_on_front=%s, page_for_posts=%s"
            % (
                updated.get("show_on_front"),
                updated.get("page_on_front"),
                updated.get("page_for_posts"),
            )
        )
    else:
        print("[INFO] Reading settings already correct; no changes")

    # Final confirm
    final = get_settings()
    ok = (
        final.get("show_on_front") == "page"
        and int(final.get("page_on_front") or 0) == home_id
        and int(final.get("page_for_posts") or 0) == blog_id
    )
    if ok:
        print("[DONE] Front page → #%s (/home), Posts page → #%s (/blog)" % (home_id, blog_id))
    else:
        print("[WARN] Settings not confirmed, got:", json.dumps(final, indent=2))
        sys.exit(2)

    # NOTE: If routes look odd on some setups, flush permalinks once via:
    #   wp rewrite flush --hard
    # (There is no core REST endpoint for flushing permalinks.)
    

if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        sys.stderr.write(f"[ERROR] {e}\n")
        sys.exit(1)

