#!/usr/bin/env python3
# scripts/data-seeding-media.py
"""
Seed WordPress media library with images from Pexels (idempotent).

Idempotence:
  - For each Pexels photo, we compute slug = "pexels-<id>".
  - If a media item already exists with that slug, we SKIP uploading.
    (Uses GET /wp/v2/media?slug=pexels-<id>)

Auth (required):
  PEXELS_API_KEY       - your Pexels API key
  WP_APP_PASSWORD      - WordPress application password for WP user

Behavior:
  MEDIA_MODE           - "popular" | "top" | "random"  (default: popular)
  MEDIA_COUNT          - how many images to upload     (default: 12)
  MEDIA_QUERY          - search query for "top/random" (default: "nature")
  MEDIA_ORIENTATION    - landscape|portrait|square     (optional; search only)
  MEDIA_SIZE           - small|medium|large            (optional; search only)
  PEXELS_SRC_KEY       - src key to upload from Pexels
                         [original|large2x|large|medium|small|portrait|landscape|tiny]
                         (default: original)
  MEDIA_FORCE_NEW      - "1" to bypass idempotence and always upload (default: 0)
  MEDIA_UPDATE_EXISTING- "1" to update title/alt/caption if item exists (default: 1)

WP connection:
  WP_BASE_URL or WP_URL  - base URL (e.g., https://wp.lan)
  WP_USERNAME or ADMIN_USER - username (default: admin)
  WP_VERIFY_SSL          - "0" to skip SSL verification (default: verify)
"""
from __future__ import annotations
import argparse
import math
import mimetypes
import os
import random
import sys
import time
from typing import Any, Dict, List, Tuple
from urllib.parse import urljoin

import requests


def evar(name: str, default: str | None = None) -> str | None:
    v = os.environ.get(name)
    return v if v not in (None, "") else default


def must(name: str) -> str:
    v = evar(name)
    if not v:
        sys.exit(f"ERROR: {name} is required")
    return v


def wp_base_url() -> str:
    url = evar("WP_BASE_URL") or evar("WP_URL") or "https://wp.lan"
    return url.rstrip("/")


def wp_auth() -> Tuple[str, str]:
    user = evar("WP_USERNAME") or evar("ADMIN_USER") or "admin"
    app_pw = must("WP_APP_PASSWORD")
    return user, app_pw


def bool_env(name: str, default: bool = True) -> bool:
    v = os.environ.get(name)
    if v is None:
        return default
    return v not in ("0", "false", "False", "no", "NO")


def http() -> requests.Session:
    s = requests.Session()
    s.headers["User-Agent"] = "media-seeder/1.2-idempotent (+python-requests)"
    return s


# ------------------- Pexels fetchers -------------------

def _pexels_headers(s: requests.Session, api_key: str) -> None:
    s.headers["Authorization"] = api_key


def pexels_random_photos(
    s: requests.Session,
    api_key: str,
    query: str,
    count: int,
    per_page: int = 80,
    orientation: str | None = None,
    size: str | None = None,
) -> List[Dict[str, Any]]:
    _pexels_headers(s, api_key)

    base_params: Dict[str, Any] = {"query": query, "per_page": 1}
    if orientation:
        base_params["orientation"] = orientation
    if size:
        base_params["size"] = size

    r = s.get("https://api.pexels.com/v1/search", params=base_params, timeout=20)
    r.raise_for_status()
    total = int(r.json().get("total_results", 0))
    pages = max(1, math.ceil(total / per_page))

    photos: List[Dict[str, Any]] = []
    while len(photos) < count:
        page = random.randint(1, max(1, pages))
        params = {**base_params, "per_page": per_page, "page": page}
        r = s.get("https://api.pexels.com/v1/search", params=params, timeout=30)
        if r.status_code == 429:
            time.sleep(int(r.headers.get("Retry-After", "2")))
            continue
        r.raise_for_status()
        batch = r.json().get("photos", []) or []
        if not batch:
            break
        random.shuffle(batch)
        for p in batch:
            photos.append(p)
            if len(photos) >= count:
                break
    return photos[:count]


def pexels_curated_photos(
    s: requests.Session,
    api_key: str,
    count: int,
    per_page: int = 80,
) -> List[Dict[str, Any]]:
    _pexels_headers(s, api_key)
    photos: List[Dict[str, Any]] = []
    page = 1
    while len(photos) < count:
        r = s.get(
            "https://api.pexels.com/v1/curated",
            params={"page": page, "per_page": min(per_page, count - len(photos))},
            timeout=30,
        )
        if r.status_code == 429:
            time.sleep(int(r.headers.get("Retry-After", "2")))
            continue
        r.raise_for_status()
        batch = r.json().get("photos", [])
        if not batch:
            break
        photos.extend(batch)
        page += 1
    return photos[:count]


def pexels_top_photos(
    s: requests.Session,
    api_key: str,
    query: str,
    count: int,
    per_page: int = 80,
    orientation: str | None = None,
    size: str | None = None,
) -> List[Dict[str, Any]]:
    _pexels_headers(s, api_key)
    params: Dict[str, Any] = {"query": query, "page": 1, "per_page": min(per_page, count)}
    if orientation:
        params["orientation"] = orientation
    if size:
        params["size"] = size
    photos: List[Dict[str, Any]] = []
    while len(photos) < count:
        r = s.get("https://api.pexels.com/v1/search", params=params, timeout=30)
        if r.status_code == 429:
            time.sleep(int(r.headers.get("Retry-After", "2")))
            continue
        r.raise_for_status()
        batch = r.json().get("photos", [])
        if not batch:
            break
        photos.extend(batch)
        params["page"] += 1
    return photos[:count]


# ------------------- WordPress helpers -------------------

def wp_check_me(s: requests.Session, base_url: str, auth: Tuple[str, str], verify_ssl: bool) -> None:
    endpoint = urljoin(base_url + "/", "wp-json/wp/v2/users/me")
    r = s.get(endpoint, auth=auth, timeout=20, verify=verify_ssl)
    r.raise_for_status()


def wp_find_media_by_slug(
    s: requests.Session,
    base_url: str,
    auth: Tuple[str, str],
    slug: str,
    verify_ssl: bool,
) -> Dict[str, Any] | None:
    """Return first media with exact slug, else None."""
    endpoint = urljoin(base_url + "/", "wp-json/wp/v2/media")
    r = s.get(
        endpoint,
        auth=auth,
        params={"slug": slug, "per_page": 1, "status": "inherit"},
        timeout=20,
        verify=verify_ssl,
    )
    r.raise_for_status()
    items = r.json() if isinstance(r.json(), list) else []
    return items[0] if items else None


def wp_search_media_fallback(
    s: requests.Session,
    base_url: str,
    auth: Tuple[str, str],
    query: str,
    verify_ssl: bool,
) -> Dict[str, Any] | None:
    """Fallback for legacy runs (before slugs were stable)."""
    endpoint = urljoin(base_url + "/", "wp-json/wp/v2/media")
    r = s.get(
        endpoint,
        auth=auth,
        params={"search": query, "media_type": "image", "per_page": 5},
        timeout=20,
        verify=verify_ssl,
    )
    r.raise_for_status()
    for item in (r.json() if isinstance(r.json(), list) else []):
        slug = (item.get("slug") or "").lower()
        title = (item.get("title", {}).get("rendered") or "").lower()
        if query.replace(" ", "-") in slug or query in title:
            return item
    return None


def wp_update_media_fields(
    s: requests.Session,
    base_url: str,
    auth: Tuple[str, str],
    media_id: int,
    data: Dict[str, Any],
    verify_ssl: bool,
) -> Dict[str, Any]:
    """Update title/alt/caption if needed."""
    endpoint = urljoin(base_url + "/", f"wp-json/wp/v2/media/{media_id}")
    r = s.post(endpoint, auth=auth, data=data, timeout=30, verify=verify_ssl)
    r.raise_for_status()
    return r.json()


def wp_upload_media(
    s: requests.Session,
    base_url: str,
    auth: tuple[str, str],
    filename: str,
    blob: bytes,
    content_type: str,
    slug: str,
    title: str,
    alt_text: str,
    caption: str,
    verify_ssl: bool,
) -> dict[str, any]:
    """
    Two-step upload:
      1) Create via raw binary body + Content-Disposition: attachment
      2) Update fields (slug/title/alt_text/caption)
    """
    # Step 1: create
    create_url = urljoin(base_url + "/", "wp-json/wp/v2/media")
    headers = {
        "Content-Disposition": f'attachment; filename="{filename}"',
        "Content-Type": content_type,
    }
    r = s.post(create_url, auth=auth, data=blob, headers=headers, timeout=90, verify=verify_ssl)
    try:
        r.raise_for_status()
    except requests.HTTPError as e:
        # Show WP’s error body to aid debugging
        msg = r.text.strip()
        raise RuntimeError(f"media create failed: {e} — body: {msg[:500]}") from e

    created = r.json()
    mid = created.get("id")
    if not mid:
        raise RuntimeError(f"media create returned no id: {created}")

    # Step 2: update meta/text fields
    update_url = urljoin(base_url + "/", f"wp-json/wp/v2/media/{mid}")
    data = {}
    if slug:
        data["slug"] = slug
    if title:
        data["title"] = title
    if alt_text:
        data["alt_text"] = alt_text
    if caption:
        data["caption"] = caption

    if data:
        r2 = s.post(update_url, auth=auth, data=data, timeout=30, verify=verify_ssl)
        try:
            r2.raise_for_status()
        except requests.HTTPError as e:
            msg = r2.text.strip()
            raise RuntimeError(f"media update failed for #{mid}: {e} — body: {msg[:500]}") from e
        return r2.json()

    return created


def download_image(s: requests.Session, url: str) -> tuple[bytes, str, str]:
    r = s.get(url, timeout=90)
    r.raise_for_status()
    ctype = (r.headers.get("Content-Type") or "image/jpeg").split(";")[0].strip()
    ext = mimetypes.guess_extension(ctype) or ".jpg"
    return r.content, ctype, ext

# ------------------- Main -------------------

def main() -> int:
    parser = argparse.ArgumentParser(description="Seed WP media from Pexels (idempotent)")
    parser.add_argument("--mode", choices=["popular", "top", "random"],
                        default=os.environ.get("MEDIA_MODE", "popular"))
    parser.add_argument("--count", type=int,
                        default=int(evar("MEDIA_COUNT", "12")))
    parser.add_argument("--query",
                        default=evar("MEDIA_QUERY", "nature"))
    parser.add_argument("--orientation",
                        default=evar("MEDIA_ORIENTATION"))
    parser.add_argument("--size",
                        default=evar("MEDIA_SIZE"))
    parser.add_argument("--src-key",
                        default=evar("PEXELS_SRC_KEY", "original"))
    parser.add_argument("--force-new", action="store_true",
                        default=bool_env("MEDIA_FORCE_NEW", False))
    parser.add_argument("--update-existing", action="store_true",
                        default=bool_env("MEDIA_UPDATE_EXISTING", True))
    args = parser.parse_args()

    api_key = must("PEXELS_API_KEY")
    base_url = wp_base_url()
    auth = wp_auth()
    verify_ssl = bool_env("WP_VERIFY_SSL", True)

    s = http()

    # Sanity-check WordPress credentials first
    try:
        wp_check_me(s, base_url, auth, verify_ssl)
        print(f"[seed] Authenticated to {base_url} as {auth[0]}", file=sys.stderr)
    except Exception as e:
        print(f"[seed] ERROR: WordPress auth failed: {e}", file=sys.stderr)
        return 2

    # Fetch photos according to mode
    try:
        if args.mode == "popular":
            photos = pexels_curated_photos(s, api_key, args.count, per_page=80)
        elif args.mode == "top":
            photos = pexels_top_photos(
                s, api_key, args.query, args.count, per_page=80,
                orientation=args.orientation, size=args.size
            )
        else:  # random
            photos = pexels_random_photos(
                s, api_key, args.query, args.count, per_page=80,
                orientation=args.orientation, size=args.size
            )
    except Exception as e:
        print(f"[seed] ERROR: Pexels fetch failed: {e}", file=sys.stderr)
        return 3

    uploaded = 0
    skipped = 0
    updated = 0

    for p in photos:
        pid = p.get("id")
        slug = f"pexels-{pid}"
        src = p.get("src", {}) or {}
        # choose the preferred URL, fall back sensibly
        url = (src.get(args.src_key)
               or src.get("original")
               or src.get("large2x")
               or src.get("large")
               or src.get("medium"))
        if not url:
            print(f"[seed] WARN: no usable URL for photo {pid}", file=sys.stderr)
            continue

        # Idempotence check (unless forced)
        existing = None
        if not args.force_new:
            try:
                existing = wp_find_media_by_slug(s, base_url, auth, slug, verify_ssl)
                if not existing:
                    # Fallback: search by "Pexels <id>" (for legacy runs before stable slugs)
                    existing = wp_search_media_fallback(s, base_url, auth, f"pexels {pid}".lower(), verify_ssl)
            except Exception as e:
                print(f"[seed] WARN: lookup failed for {slug}: {e}", file=sys.stderr)

        photographer = p.get("photographer") or "Unknown"
        pexels_page = p.get("url") or f"https://www.pexels.com/photo/{pid}/"
        title = f"Pexels {pid} — {photographer}"
        alt_text = f"Photo by {photographer} (Pexels)"
        caption = f'Photo by {photographer} on Pexels — <a href="{pexels_page}">{pexels_page}</a>'

        if existing and not args.force_new:
            mid = existing.get("id")
            print(f"[seed] exists media #{mid} (slug {slug}); skipping upload", file=sys.stderr)

            # Optionally update text fields to keep them tidy
            if args.update_existing:
                try:
                    wp_update_media_fields(
                        s, base_url, auth, mid,
                        {"title": title, "alt_text": alt_text, "caption": caption},
                        verify_ssl
                    )
                    updated += 1
                    print(f"[seed] updated media #{mid} meta/text", file=sys.stderr)
                except Exception as e:
                    print(f"[seed] WARN: update failed for #{mid}: {e}", file=sys.stderr)

            skipped += 1
            continue

        # Download & upload
        try:
            blob, ctype, ext = download_image(s, url)
        except Exception as e:
            print(f"[seed] WARN: download failed for {pid}: {e}", file=sys.stderr)
            continue

        filename = f"{slug}{ext}"
        try:
            res = wp_upload_media(
                s, base_url, auth, filename, blob, ctype, slug, title, alt_text, caption, verify_ssl
            )
            mid = res.get("id")
            src_url = res.get("source_url")
            print(f"[seed] uploaded media #{mid}: {src_url}", file=sys.stderr)
            uploaded += 1
        except Exception as e:
            print(f"[seed] ERROR: upload failed for {pid}: {e}", file=sys.stderr)
            continue

    print(f"[seed] Done. Uploaded {uploaded}, updated {updated}, skipped {skipped}.", file=sys.stderr)
    return 0 if (uploaded or skipped) else 4


if __name__ == "__main__":
    raise SystemExit(main())
