#!/usr/bin/env python3
import hashlib
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

def require_env(var: str) -> str:
    val = os.getenv(var)
    if val is None or val == "":
        print(f"Environment variable {var} must be set", file=sys.stderr)
        sys.exit(1)
    return val

def run(cmd, *, check=True, capture=False, cwd=None):
    if capture:
        result = subprocess.run(
            cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True
        )
        if check and result.returncode != 0:
            print(result.stdout, end="")
            sys.exit(result.returncode)
        return result
    else:
        result = subprocess.run(cmd, cwd=cwd)
        if check and result.returncode != 0:
            sys.exit(result.returncode)
        return result

def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()

def posix_rel(base: Path, p: Path) -> str:
    """base-relative path with forward slashes (what HTML uses)."""
    return p.relative_to(base).as_posix()

def collect_assets(wwwroot: Path) -> list[Path]:
    """All .css/.js under wwwroot, excluding _framework/**."""
    exts = {".css", ".js"}
    results: list[Path] = []
    for p in wwwroot.rglob("*"):
        if not p.is_file():
            continue
        if p.suffix.lower() not in exts:
            continue
        # skip framework-managed assets (already fingerprinted by Blazor)
        try:
            rel = p.relative_to(wwwroot)
        except ValueError:
            continue
        if rel.parts and rel.parts[0] == "_framework":
            continue
        results.append(p)
    return results

def collect_html(wwwroot: Path) -> list[Path]:
    return list(wwwroot.rglob("*.html"))

def version_assets_in_html(wwwroot: Path, assets: list[Path], html_files: list[Path]) -> None:
    """
    For each asset, compute sha256 and update all HTML files so that any
    matching href/src="asset[?v=...]" becomes href/src="asset?v=<hash>".
    """
    if not assets or not html_files:
        return

    # Precompute hashes and escaped patterns
    entries = []
    for a in assets:
        try:
            rel_posix = posix_rel(wwwroot, a)  # e.g. 'css/app.css'
        except Exception:
            continue
        digest = sha256_file(a)
        rel_escaped = re.escape(rel_posix)
        pattern = re.compile(
            rf'(href|src)\s*=\s*(["\']){rel_escaped}(?:\?v=[^"\']*)?\2',
            flags=re.IGNORECASE,
        )
        replacement = rf'\1=\2{rel_posix}?v={digest}\2'
        entries.append((pattern, replacement, rel_posix, digest))

    # Rewrite each HTML once, applying all asset rules
    for html in html_files:
        text = html.read_text(encoding="utf-8")
        original = text
        total_subs = 0
        for pattern, replacement, rel_posix, digest in entries:
            (text, nsubs) = pattern.subn(replacement, text)
            total_subs += nsubs
        if total_subs > 0 and text != original:
            html.write_text(text, encoding="utf-8")
            print(f"   → {html.relative_to(wwwroot)}: updated {total_subs} reference(s)")

def main():
    # === Ensure required environment variables are set ===
    REMOTE_USER = require_env("Server__User")
    REMOTE_HOST = require_env("Server__Host")
    REMOTE_WEBPATH = require_env("Server__RemoteBlazorDir")

    # === Paths (script is in ./scripts; project root is one level up) ===
    PROJECT_DIR = Path(__file__).resolve().parent          # .../scripts
    ROOT_DIR = PROJECT_DIR.parent                           # project root (has BlazorWP.csproj, libman.json)
    PROJECT_FILE = (ROOT_DIR / "BlazorWP.csproj").resolve()

    PUBLISH_DIR = (ROOT_DIR / "blazor-publish").resolve()
    WWWROOT_DIR = PUBLISH_DIR / "wwwroot"
    DESIRED_BASE = "/blazorapp/"

    # === 1) Clean previous publish ===
    print("→ Cleaning old publish…")
    shutil.rmtree(PUBLISH_DIR, ignore_errors=True)

    # === 2) Restore client-side libraries into wwwroot/libman (libman.json at ROOT_DIR) ===
    print("→ Restoring client-side libraries…")
    run(["libman", "restore"], cwd=ROOT_DIR)

    # === 3) Publish the project (implicit NuGet restore/build/pack) ===
    print(f"→ Publishing {PROJECT_FILE} to {PUBLISH_DIR}…")
    result = run(
        ["dotnet", "publish", str(PROJECT_FILE), "-c", "Release", "-o", str(PUBLISH_DIR)],
        check=False,
        capture=True,
        cwd=ROOT_DIR,
    )

    # Filter out specific lines (parity with the bash version)
    filtered = []
    pattern = re.compile(r"(WASM0001|WASM0060|WASM0062|Optimizing assemblies for size)")
    for line in result.stdout.splitlines():
        if not pattern.search(line):
            filtered.append(line)
    print("\n".join(filtered))
    if result.returncode != 0:
        sys.exit(result.returncode)

    # === 4) Patch <base> href in the generated index.html ===
    index_html = WWWROOT_DIR / "index.html"
    print(f"→ Patching <base> href in {index_html}…")
    if not index_html.is_file():
        print(f"❌ ERROR: {index_html} not found", file=sys.stderr)
        sys.exit(1)

    html = index_html.read_text(encoding="utf-8")
    new_html, nsubs = re.subn(
        r'<base\s+href="[^"]*"\s*/?>',
        f'<base href="{DESIRED_BASE}" />',
        html,
        flags=re.IGNORECASE,
    )
    if nsubs == 0:
        print('❌ ERROR: No <base href="…"> tag found to replace', file=sys.stderr)
        sys.exit(1)
    index_html.write_text(new_html, encoding="utf-8")

    # === 5) Cache-busting (fingerprint) for all CSS/JS in wwwroot (except _framework) ===
    print("→ Fingerprinting static assets and rewriting HTML links…")
    assets = collect_assets(WWWROOT_DIR)
    html_files = collect_html(WWWROOT_DIR)
    for a in sorted(assets):
        print(f"   • {a.relative_to(WWWROOT_DIR)}")
    version_assets_in_html(WWWROOT_DIR, assets, html_files)

    # === 6) Deploy via rsync ===
    print(f"→ Rsyncing to {REMOTE_HOST}:{REMOTE_WEBPATH}…")
    run([
        "rsync", "-azq", "--delete",
        str(WWWROOT_DIR) + "/",
        f"{REMOTE_USER}@{REMOTE_HOST}:{REMOTE_WEBPATH}/",
    ])

    print("✅ Deployment complete!")

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nAborted by user.", file=sys.stderr)
        sys.exit(130)
