#!/usr/bin/env python3
"""
Seed a Formatting Test page and a Formatting Test (Post) with many core blocks.
Idempotent by slug. Uses WP REST via Application Passwords (same envs as other seeders).

ENV (required): WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD
Optional: WP_VERIFY_SSL=0 to skip TLS verify (dev only)

Usage:
  python3 scripts/data-seeding-formatting.py         # create if missing
  python3 scripts/data-seeding-formatting.py --update # force update
"""
from __future__ import annotations
import os, sys, json, argparse, html
from urllib.parse import urljoin
import requests
from string import Template

BASE = (os.getenv("WP_BASE_URL") or os.getenv("WP_URL") or "").rstrip("/")
USER = os.getenv("WP_USERNAME") or os.getenv("ADMIN_USER") or ""
PASS = (os.getenv("WP_APP_PASSWORD") or "").replace(" ", "")
VERIFY = os.getenv("WP_VERIFY_SSL", "1").lower() not in ("0","false","no")
HDRS = {"Accept":"application/json","Content-Type":"application/json; charset=utf-8"}

def die(msg: str, code: int = 1):
    print(msg, file=sys.stderr); sys.exit(code)

def req(method: str, path: str, *, params: dict | None = None, body: dict | None = None):
    if not (BASE and USER and PASS):
        die("Set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD")
    url = urljoin(BASE + "/", f"wp-json/wp/v2/{path}")
    r = requests.request(
        method,
        url,
        params=params,
        data=(json.dumps(body) if body else None),
        auth=(USER, PASS),
        headers=HDRS,
        timeout=30,
        verify=VERIFY,
    )
    if r.status_code == 404:
        return None
    r.raise_for_status()
    return r.json()

def get_by_slug(kind: str, slug: str):
    arr = req("GET", kind, params={"slug": slug})
    return arr[0] if isinstance(arr, list) and arr else None

def upsert(kind: str, slug: str, payload: dict, *, update: bool):
    ex = get_by_slug(kind, slug)
    if ex and not update:
        print(f"[skip] {kind} '{slug}' exists #{ex['id']}")
        return ex
    if ex:
        print(f"[update] {kind} #{ex['id']} ({slug})")
        return req("POST", f"{kind}/{ex['id']}", body=payload)
    print(f"[create] {kind} {slug}")
    return req("POST", kind, body=payload)

def pick_media():
    """Prefer the newest attachment; fall back to picsum."""
    try:
        arr = req(
            "GET",
            "media",
            params={
                "per_page": 1,
                "order": "desc",
                "orderby": "date",
                "_fields": "id,source_url,alt_text",
            },
        )
        if isinstance(arr, list) and arr:
            it = arr[0]
            return int(it.get("id")), it.get("source_url"), it.get("alt_text") or ""
    except Exception:
        pass
    return None, "https://picsum.photos/1200/600", "Placeholder"

def build_blocks(image_id=None, image_src: str = "", image_alt: str = "") -> str:
    """Return a big block of HTML comment + block markup without f-string braces issues.
    We use string.Template with $PLACEHOLDERs so Gutenberg's JSON braces stay literal.
    """
    # Build JSON attrs for the image block comment
    attrs = {"sizeSlug": "large", "linkDestination": "none"}
    if image_id is not None:
        attrs["id"] = int(image_id)
    json_attrs = json.dumps(attrs, separators=(",", ":"))

    # Escape image attributes
    image_src = html.escape(image_src or "", quote=True)
    image_alt = html.escape(image_alt or "", quote=True)

    tpl = Template(r"""
<!-- wp:group {"layout":{"type":"constrained"}} -->
<div class="wp-block-group">
<!-- wp:heading {"level":1} -->
<h1>Formatting Test</h1>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>This page exercises many block types so you can see Twenty Twenty‑Five’s styling and our debug outlines.</p>
<!-- /wp:paragraph -->

<!-- wp:columns -->
<div class="wp-block-columns"><!-- wp:column -->
<div class="wp-block-column"><!-- wp:heading {"level":2} -->
<h2>Typography</h2>
<!-- /wp:heading -->

<!-- wp:paragraph {"dropCap":true} -->
<p class="has-drop-cap">Drop cap. <strong>bold</strong>, <em>italic</em>, <code>code</code>, <a href="#">link</a>, <kbd>KBD</kbd>.</p>
<!-- /wp:paragraph -->

<!-- wp:list -->
<ul><li>Item one</li><li>Item two<ul><li>Nested item</li></ul></li></ul>
<!-- /wp:list --></div><!-- /wp:column -->

<!-- wp:column -->
<div class="wp-block-column"><!-- wp:quote -->
<blockquote class="wp-block-quote"><p>Blockquote — typography check.</p><cite>— Someone</cite></blockquote>
<!-- /wp:quote -->

<!-- wp:pullquote -->
<figure class="wp-block-pullquote"><blockquote><p>Pullquote to test spacing.</p></blockquote><cite>— Another</cite></figure>
<!-- /wp:pullquote -->

<!-- wp:code -->
<pre class="wp-block-code"><code>console.log('hello blocks')</code></pre>
<!-- /wp:code --></div><!-- /wp:column --></div>
<!-- /wp:columns -->

<!-- wp:separator {"className":"is-style-wide"} -->
<hr class="wp-block-separator is-style-wide"/>
<!-- /wp:separator -->

<!-- wp:image $JSON_ATTRS -->
<figure class="wp-block-image size-large"><img src="$IMAGE_SRC" alt="$IMAGE_ALT"/></figure>
<!-- /wp:image -->

<!-- wp:table -->
<figure class="wp-block-table"><table><thead><tr><th>Column A</th><th>Column B</th></tr></thead><tbody><tr><td>AA</td><td>BB</td></tr><tr><td>CC</td><td>DD</td></tr></tbody></table></figure>
<!-- /wp:table -->

<!-- wp:buttons -->
<div class="wp-block-buttons"><div class="wp-block-button"><a class="wp-block-button__link wp-element-button">Primary</a></div><div class="wp-block-button is-style-outline"><a class="wp-block-button__link wp-element-button">Outline</a></div></div>
<!-- /wp:buttons -->
</div>
<!-- /wp:group -->
""")
    return tpl.substitute(JSON_ATTRS=json_attrs, IMAGE_SRC=image_src, IMAGE_ALT=image_alt)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="Seed formatting test page and post (idempotent)")
    ap.add_argument("--update", action="store_true", help="Update if they exist")
    ns = ap.parse_args(argv)

    mid, msrc, malt = pick_media()
    blocks = build_blocks(mid, msrc, malt)

    page = {"status":"publish", "slug":"formatting-test", "title":"Formatting Test", "content": blocks}
    upsert("pages", "formatting-test", page, update=ns.update)

    post_blocks = blocks.replace("<h1>Formatting Test</h1>", "<h1>Formatting Test (Post)</h1>")
    post = {"status":"publish", "slug":"formatting-test-post", "title":"Formatting Test (Post)", "content": post_blocks}
    upsert("posts", "formatting-test-post", post, update=ns.update)

    print("[ok] formatting content seeded")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
