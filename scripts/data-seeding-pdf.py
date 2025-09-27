#!/usr/bin/env python3
# scripts/data-seeding-pdf.py
"""
Seed WordPress media library with generated PDF files (idempotent).

Env / Args:
  PDF_COUNT            - how many PDFs (default: 6)
  PDF_PREFIX           - slug/title prefix (default: "pdf-sample")
  PDF_PAGES_MIN        - min pages per PDF (default: 1)
  PDF_PAGES_MAX        - max pages per PDF (default: 3)
  PDF_FORCE_NEW        - "1" to always upload even if slug exists (default: 0)
  PDF_UPDATE_EXISTING  - "1" to refresh title/caption when found (default: 1)

WordPress connection:
  WP_BASE_URL or WP_URL      - base URL (e.g., https://wp.lan)
  WP_USERNAME or ADMIN_USER  - username (default: admin)
  WP_APP_PASSWORD            - app password for that user (required)
  WP_VERIFY_SSL              - "0" to skip SSL verification (default: verify)

Usage:
  ./scripts/data-seeding-pdf.py --count 6
"""
from __future__ import annotations
import argparse
import io
import os
import random
import sys
from datetime import datetime
from typing import Any, Dict, Tuple
from urllib.parse import urljoin

import requests
from fpdf import FPDF  # pip install fpdf2

# Newer fpdf2 exports enums; older versions don’t. Support both.
try:
    from fpdf.enums import XPos, YPos
    HAVE_ENUMS = True
except Exception:
    HAVE_ENUMS = False


# ---------- env helpers ----------
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
    s.headers["User-Agent"] = "pdf-seeder/1.1 (+python-requests)"
    return s


# ---------- WordPress helpers ----------
def wp_check_me(s: requests.Session, base_url: str, auth: Tuple[str, str], verify_ssl: bool) -> None:
    endpoint = urljoin(base_url + "/", "wp-json/wp/v2/users/me")
    r = s.get(endpoint, auth=auth, timeout=20, verify=verify_ssl)
    r.raise_for_status()

def wp_find_media_by_slug(s: requests.Session, base_url: str, auth: Tuple[str, str], slug: str, verify_ssl: bool):
    endpoint = urljoin(base_url + "/", "wp-json/wp/v2/media")
    r = s.get(endpoint, auth=auth, params={"slug": slug, "per_page": 1}, timeout=20, verify=verify_ssl)
    r.raise_for_status()
    js = r.json()
    return js[0] if isinstance(js, list) and js else None

def wp_update_media_fields(
    s: requests.Session, base_url: str, auth: Tuple[str, str], media_id: int, data: Dict[str, Any], verify_ssl: bool
):
    endpoint = urljoin(base_url + "/", f"wp-json/wp/v2/media/{media_id}")
    r = s.post(endpoint, auth=auth, data=data, timeout=30, verify=verify_ssl)
    r.raise_for_status()
    return r.json()

def wp_upload_media_binary(
    s: requests.Session,
    base_url: str,
    auth: Tuple[str, str],
    filename: str,
    blob: bytes,
    content_type: str,
    meta: Dict[str, Any],
    verify_ssl: bool,
):
    """
    Create media by sending raw binary + headers, then update fields.
    More compatible than multipart on some WP stacks.
    """
    create_url = urljoin(base_url + "/", "wp-json/wp/v2/media")
    headers = {
        "Content-Disposition": f'attachment; filename="{filename}"',
        "Content-Type": content_type,
    }
    r = s.post(create_url, auth=auth, data=blob, headers=headers, timeout=90, verify=verify_ssl)
    try:
        r.raise_for_status()
    except requests.HTTPError as e:
        raise RuntimeError(f"media create failed: {e} — body: {r.text[:500]!r}") from e

    media = r.json()
    mid = media.get("id")
    if not mid:
        raise RuntimeError(f"media create returned no id: {media}")

    if meta:
        r2 = s.post(urljoin(base_url + "/", f"wp-json/wp/v2/media/{mid}"), auth=auth, data=meta, timeout=30, verify=verify_ssl)
        try:
            r2.raise_for_status()
        except requests.HTTPError as e:
            raise RuntimeError(f"media update failed for #{mid}: {e} — body: {r2.text[:500]!r}") from e
        media = r2.json()

    return media


# ---------- PDF generation ----------
_LOREM = (
    "Lorem ipsum dolor sit amet, consectetur adipiscing elit. "
    "Donec vitae dictum mauris. Integer et pretium tortor. "
    "Curabitur blandit, magna a egestas ultrices, dolor nulla "
    "vehicula neque, vitae fermentum elit nunc ut enim. "
    "Sed suscipit, nisl a dignissim finibus, lacus massa iaculis diam, "
    "sed cursus dui mauris nec leo."
)

def make_pdf_bytes(title: str, author: str, pages: int, seed: int) -> bytes:
    rng = random.Random(seed)
    pdf = FPDF(orientation="P", unit="pt", format="A4")  # 595x842 pt

    # Try Unicode fonts (bullets, em-dashes, etc). Fallback to core fonts.
    REG = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
    BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
    use_unicode = False
    if os.path.exists(REG):
        try:
            pdf.add_font("DejaVu", "", REG, uni=True)
            if os.path.exists(BOLD):
                pdf.add_font("DejaVu", "B", BOLD, uni=True)
            use_unicode = True
        except Exception:
            use_unicode = False  # fallback below

    # Metadata
    pdf.set_title(title)
    pdf.set_author(author)
    pdf.set_creator("data-seeding-pdf.py")
    pdf.set_subject("Seed PDF for testing uploads/preview")
    pdf.set_keywords("test, seed, pdf")

    for page in range(pages):
        pdf.add_page()

        # Title
        if use_unicode:
            try:
                pdf.set_font("DejaVu", "B", 18)
            except Exception:
                pdf.set_font("DejaVu", "", 18)
        else:
            pdf.set_font("Helvetica", "B", 18)

        if HAVE_ENUMS:
            pdf.cell(0, 24, title, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        else:
            pdf.cell(0, 24, title, ln=1)

        # Subtitle / meta
        if use_unicode:
            pdf.set_font("DejaVu", "", 10)
            bullet = "•"
        else:
            pdf.set_font("Helvetica", "", 10)
            bullet = "-"  # core-font safe

        meta = f"Author: {author} {bullet} Page {page+1}/{pages}"

        if HAVE_ENUMS:
            pdf.cell(0, 16, meta, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
            pdf.cell(0, 16, f"Generated: {datetime.utcnow().isoformat()}Z",
                     new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        else:
            pdf.cell(0, 16, meta, ln=1)
            pdf.cell(0, 16, f"Generated: {datetime.utcnow().isoformat()}Z", ln=1)
        pdf.ln(8)

        # Body text
        if use_unicode:
            pdf.set_font("DejaVu", "", 12)
        else:
            pdf.set_font("Helvetica", "", 12)

        paras = rng.randint(4, 8)
        for i in range(paras):
            pdf.multi_cell(0, 16, f"{_LOREM} #{i+1}")
            pdf.ln(4)

    out = pdf.output(dest="S")
    # fpdf2 returns bytes; older may return str
    return out if isinstance(out, (bytes, bytearray)) else str(out).encode("latin1", "ignore")


# ---------- main ----------
def main() -> int:
    parser = argparse.ArgumentParser(description="Seed WP with generated PDF files (idempotent)")
    parser.add_argument("--count", type=int, default=int(evar("PDF_COUNT", "6")))
    parser.add_argument("--prefix", default=evar("PDF_PREFIX", "pdf-sample"))
    parser.add_argument("--pages-min", type=int, default=int(evar("PDF_PAGES_MIN", "1")))
    parser.add_argument("--pages-max", type=int, default=int(evar("PDF_PAGES_MAX", "3")))
    parser.add_argument("--force-new", action="store_true", default=bool_env("PDF_FORCE_NEW", False))
    parser.add_argument("--update-existing", action="store_true", default=bool_env("PDF_UPDATE_EXISTING", True))
    args = parser.parse_args()

    if args.pages_min < 1 or args.pages_max < args.pages_min:
        sys.exit("ERROR: invalid pages range")

    base_url = wp_base_url()
    auth = wp_auth()
    verify_ssl = bool_env("WP_VERIFY_SSL", True)

    s = http()

    # Verify WordPress access
    try:
        wp_check_me(s, base_url, auth, verify_ssl)
        print(f"[pdf-seed] Authenticated to {base_url} as {auth[0]}", file=sys.stderr)
    except Exception as e:
        print(f"[pdf-seed] ERROR: WordPress auth failed: {e}", file=sys.stderr)
        return 2

    uploaded = skipped = updated = 0

    for i in range(1, args.count + 1):
        slug = f"{args.prefix}-{i:03d}"
        title = f"{args.prefix} #{i:03d}"
        caption = f"Seed PDF '{title}' generated for testing."
        filename = f"{slug}.pdf"

        existing = None
        if not args.force_new:
            try:
                existing = wp_find_media_by_slug(s, base_url, auth, slug, verify_ssl)
            except Exception as e:
                print(f"[pdf-seed] WARN: lookup failed for {slug}: {e}", file=sys.stderr)

        if existing and not args.force_new:
            mid = existing.get("id")
            print(f"[pdf-seed] exists media #{mid} (slug {slug}); skipping upload", file=sys.stderr)
            if args.update_existing:
                try:
                    wp_update_media_fields(s, base_url, auth, mid, {"title": title, "caption": caption}, verify_ssl)
                    updated += 1
                    print(f"[pdf-seed] updated media #{mid} text fields", file=sys.stderr)
                except Exception as e:
                    print(f"[pdf-seed] WARN: update failed for #{mid}: {e}", file=sys.stderr)
            skipped += 1
            continue

        # Create PDF bytes
        pages = random.randint(args.pages_min, args.pages_max)
        blob = make_pdf_bytes(title=title, author=auth[0], pages=pages, seed=i)

        try:
            res = wp_upload_media_binary(
                s,
                base_url,
                auth,
                filename=filename,
                blob=blob,
                content_type="application/pdf",
                meta={"slug": slug, "title": title, "caption": caption},
                verify_ssl=verify_ssl,
            )
            mid = res.get("id")
            url = res.get("source_url")
            print(f"[pdf-seed] uploaded media #{mid}: {url}", file=sys.stderr)
            uploaded += 1
        except Exception as e:
            print(f"[pdf-seed] ERROR: upload failed for {slug}: {e}", file=sys.stderr)
            continue

    print(f"[pdf-seed] Done. Uploaded {uploaded}, updated {updated}, skipped {skipped}.", file=sys.stderr)
    return 0 if (uploaded or skipped) else 4


if __name__ == "__main__":
    raise SystemExit(main())
